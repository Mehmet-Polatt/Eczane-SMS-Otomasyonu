using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EczaneOtomasyonu.Services;

/// <summary>
/// GSM Modem üzerinden AT komutları ile SMS gönderen gerçek implementasyon.
///
/// ── AT Komut Akışı ────────────────────────────────────────────────────────────
///  1. AT            → Modem cevap veriyor mu? (OK beklenir)
///  2. AT+CMGF=1     → Text moduna geç (PDU yerine okunabilir metin)
///  3. AT+CSCS="GSM" → Karakter seti (Türkçe için GSM veya UCS2)
///  4. AT+CMGS="05X" → Alıcı numarası; modem ">" ile yanıt verir
///  5. [mesaj metni] → Metin gönderilir
///  6. CTRL+Z (0x1A) → Gönderimi tamamla
/// ─────────────────────────────────────────────────────────────────────────────
///
/// ── Platform Port Adları ──────────────────────────────────────────────────────
///  macOS  : /dev/tty.usbserial-XXXX  veya  /dev/tty.usbmodem-XXXX
///  Windows: COM3, COM4, COM5 vb.
///
/// Port adı yapılandırma dosyasından veya uygulama ayarlarından okunmalıdır.
/// ─────────────────────────────────────────────────────────────────────────────
/// </summary>
public sealed class GsmModemSmsService : ISmsService, IDisposable
{
    private readonly SerialPort _port;
    private readonly SemaphoreSlim _kilit = new(1, 1); // Aynı anda tek AT komutu
    private bool _disposed;

    // AT komut yanıt bekleme süresi (ms)
    private const int KomutZamanAsimi = 5000;
    // SMS gönderim bekleme süresi (daha uzun)
    private const int SmsZamanAsimi = 15000;

    /// <param name="portAdi">
    ///   macOS: /dev/tty.usbserial-XXXX
    ///   Windows: COM3
    /// </param>
    /// <param name="baudRate">Varsayılan: 115200 (modern modemler için)</param>
    public GsmModemSmsService(string portAdi, int baudRate = 115200)
    {
        _port = new SerialPort(portAdi, baudRate)
        {
            Parity     = Parity.None,
            DataBits   = 8,
            StopBits   = StopBits.One,
            Handshake  = Handshake.RequestToSend,
            Encoding   = Encoding.ASCII,
            // Yanıt okuma için zaman aşımı
            ReadTimeout  = KomutZamanAsimi,
            WriteTimeout = KomutZamanAsimi,
            NewLine      = "\r\n",
        };
    }

    /// <inheritdoc/>
    public bool BaglantiHazirMi => _port.IsOpen;

    // ─── Bağlantı Yönetimi ───────────────────────────────────────────────────

    /// <summary>
    /// Seri portu açar ve modem ile iletişim kurulabildiğini doğrular.
    /// Zamanlayıcı job'u çalışmadan önce bu metodu çağırmalıdır.
    /// </summary>
    public async Task<bool> BaglanAsync()
    {
        if (_port.IsOpen) return true;

        try
        {
            _port.Open();
            Debug.WriteLine($"[GSM] Port açıldı: {_port.PortName}");

            // Temel AT testi: modem yanıt veriyor mu?
            string yanit = await AtKomutuGonderAsync("AT", KomutZamanAsimi);
            if (!yanit.Contains("OK"))
            {
                Debug.WriteLine($"[GSM] Modem yanıt vermedi. Yanıt: {yanit}");
                _port.Close();
                return false;
            }

            // Echo'yu kapat (temiz yanıtlar için)
            await AtKomutuGonderAsync("ATE0", KomutZamanAsimi);

            Debug.WriteLine("[GSM] Modem bağlantısı doğrulandı.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GSM][HATA] Bağlanamadı ({_port.PortName}): {ex.Message}");
            if (_port.IsOpen) _port.Close();
            return false;
        }
    }

    /// <summary>Seri portu kapatır.</summary>
    public void BaglantiKes()
    {
        if (_port.IsOpen)
        {
            _port.Close();
            Debug.WriteLine($"[GSM] Port kapatıldı: {_port.PortName}");
        }
    }

    // ─── ISmsService Implementasyonu ─────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<bool> SmsGonderAsync(string telefon, string mesaj)
    {
        if (!_port.IsOpen)
        {
            Debug.WriteLine("[GSM][UYARI] Port kapalı, SMS gönderilemiyor.");
            return false;
        }

        // Eşzamanlı AT komutu karışıklığını önle
        await _kilit.WaitAsync();
        try
        {
            return await SmsGonderIcAsync(telefon, mesaj);
        }
        finally
        {
            _kilit.Release();
        }
    }

    // ─── Özel Yardımcı Metotlar ──────────────────────────────────────────────

    private async Task<bool> SmsGonderIcAsync(string telefon, string mesaj)
    {
        try
        {
            // Adım 1: Text moduna geç
            string cmgfYanit = await AtKomutuGonderAsync("AT+CMGF=1", KomutZamanAsimi);
            if (!cmgfYanit.Contains("OK"))
            {
                Debug.WriteLine($"[GSM][HATA] AT+CMGF=1 başarısız: {cmgfYanit}");
                return false;
            }

            // Adım 2: Karakter seti (Türkçe karakterleri için UCS2; ASCII için GSM)
            // Not: UCS2 mesaj limitini 70 karaktere düşürür. İçerik ASCII ise GSM daha verimlidir.
            bool turkceIceriyor = TurkceKarakterVarMi(mesaj);
            string cscs = turkceIceriyor ? "UCS2" : "GSM";
            await AtKomutuGonderAsync($"AT+CSCS=\"{cscs}\"", KomutZamanAsimi);

            // Adım 3: Alıcı numarasını belirt
            // Modem '>' karakteriyle giriş bekler; timeout burada SmsZamanAsimi kullanır
            string cmgsYanit = await AtKomutuGonderAsync(
                $"AT+CMGS=\"{NormalizeTelefon(telefon)}\"",
                SmsZamanAsimi,
                beklenenSuffix: ">");

            if (!cmgsYanit.Contains(">"))
            {
                Debug.WriteLine($"[GSM][HATA] AT+CMGS '>' yanıtı gelmedi: {cmgsYanit}");
                return false;
            }

            // Adım 4: Mesaj metnini yaz ve CTRL+Z (0x1A) ile bitir
            string gonderilecekMetin = turkceIceriyor
                ? UnicodeToUcs2Hex(mesaj)  // UCS2 modunda hex encode
                : mesaj;

            _port.Write(gonderilecekMetin);
            _port.Write(new[] { (char)26 }, 0, 1); // CTRL+Z

            // Adım 5: Gönderim onayını bekle (+CMGS: <msgref> veya OK)
            string sonucYanit = await BekleAsync(SmsZamanAsimi, beklenenIcerik: "+CMGS:");

            if (sonucYanit.Contains("+CMGS:") || sonucYanit.Contains("OK"))
            {
                Debug.WriteLine($"[GSM] ✓ SMS gönderildi → {telefon}");
                return true;
            }

            Debug.WriteLine($"[GSM][HATA] SMS gönderilemedi. Yanıt: {sonucYanit}");
            return false;
        }
        catch (TimeoutException)
        {
            Debug.WriteLine($"[GSM][HATA] SMS zaman aşımı → {telefon}");
            // Gönderimi iptal et (ESC komutu)
            try { _port.Write("\x1B"); } catch { /* ignored */ }
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GSM][HATA] SMS gönderilemedi: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Seri porta AT komutu gönderir ve yanıtı bekler.
    /// </summary>
    private async Task<string> AtKomutuGonderAsync(
        string komut,
        int zamanAsimi,
        string beklenenSuffix = "OK")
    {
        _port.DiscardInBuffer();
        _port.DiscardOutBuffer();

        _port.WriteLine(komut);
        Debug.WriteLine($"[GSM] → {komut}");

        return await BekleAsync(zamanAsimi, beklenenIcerik: beklenenSuffix);
    }

    /// <summary>
    /// Seri porttan gelen yanıtı toplar, beklenen içerik veya zaman aşımı gerçekleşene kadar okur.
    /// </summary>
    private Task<string> BekleAsync(int zamanAsimiMs, string beklenenIcerik)
    {
        return Task.Run(() =>
        {
            var sb = new StringBuilder();
            var bitis = DateTime.UtcNow.AddMilliseconds(zamanAsimiMs);

            while (DateTime.UtcNow < bitis)
            {
                if (_port.BytesToRead > 0)
                {
                    try
                    {
                        string parca = _port.ReadExisting();
                        sb.Append(parca);
                        Debug.WriteLine($"[GSM] ← {parca.Replace("\r", "\\r").Replace("\n", "\\n")}");

                        if (sb.ToString().Contains(beklenenIcerik) ||
                            sb.ToString().Contains("ERROR"))
                            break;
                    }
                    catch (TimeoutException) { break; }
                }
                Thread.Sleep(50);
            }

            return sb.ToString();
        });
    }

    // ─── Yardımcı Dönüşüm Metotları ──────────────────────────────────────────

    /// <summary>
    /// Türkçe özel karakterleri tespit eder (ş, ğ, ü, ö, ı, ç ve büyük halleri).
    /// </summary>
    private static bool TurkceKarakterVarMi(string metin) =>
        metin.IndexOfAny(new[] { 'ş','Ş','ğ','Ğ','ü','Ü','ö','Ö','ı','İ','ç','Ç' }) >= 0;

    /// <summary>
    /// AT+CMGS için telefon numarasını uluslararası formata çevirir.
    /// 0'la başlıyorsa +90 ön ekiyle değiştirir.
    /// </summary>
    private static string NormalizeTelefon(string telefon)
    {
        var temiz = telefon.Trim();
        if (temiz.StartsWith("0") && temiz.Length == 11)
            return $"+90{temiz.Substring(1)}"; // 05XXXXXXXXX → +905XXXXXXXXX
        if (temiz.StartsWith("5") && temiz.Length == 10)
            return $"+90{temiz}";              // 5XXXXXXXXX → +905XXXXXXXXX
        return temiz; // Zaten uluslararası formatta veya bilinmiyor
    }

    /// <summary>
    /// Metni UCS2 hex'e çevirir (Türkçe karakterli SMS için gerekli).
    /// </summary>
    private static string UnicodeToUcs2Hex(string metin)
    {
        var sb = new StringBuilder();
        foreach (char c in metin)
            sb.Append(((int)c).ToString("X4"));
        return sb.ToString();
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        BaglantiKes();
        _port.Dispose();
        _kilit.Dispose();
        _disposed = true;
    }
}
