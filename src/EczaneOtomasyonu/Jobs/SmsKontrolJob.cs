using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EczaneOtomasyonu.Data;
using EczaneOtomasyonu.Services;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace EczaneOtomasyonu.Jobs;

/// <summary>
/// Quartz.NET arka plan görevi: Bitiş tarihi 3 gün kalan hastalara SMS gönderir.
///
/// ── Dinamik Konfigürasyon (Aşama 6) ──────────────────────────────────────────
/// Her çalışmada veritabanından "GsmPort" ve "SmsSablonu" okunur.
///   • GsmPort boşsa → DebugConsoleSmsService kullanılır.
///   • SmsSablonu içindeki {IsimSoyisim} ve {IlacAdi} değişkenleri doldurulur.
/// Bu sayede kullanıcı ayarlar ekranından değişiklik yaptığında bir sonraki
/// job çalışmasında yeni ayarlar otomatik devreye girer.
/// ─────────────────────────────────────────────────────────────────────────────
/// </summary>
[DisallowConcurrentExecution]
public class SmsKontrolJob : IJob
{
    // JobDataMap için sabit anahtar (geriye dönük uyumluluk için korunuyor)
    public const string SmsServisAnahtari = "ISmsService";

    /// <summary>
    /// SMS gönderim job'ı başarıyla tamamlandığında tetiklenir.
    /// KayitliHastalarViewModel bu eventi dinleyerek tabloyu otomatik yeniler.
    /// </summary>
    public static event EventHandler? SmsGonderimTamamlandi;

    public async Task Execute(IJobExecutionContext context)
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "sms_log.txt");
        AppendLog(logPath, $"\n[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] --- SMS KONTROL JOB TETİKLENDİ ---\n");
        Debug.WriteLine($"[SmsJob] ▶ Çalışıyor — {DateTime.Now:dd.MM.yyyy HH:mm:ss}");

        // Model alanları DateOnly olduğu için gün karşılaştırması saat bileşeni içermez.
        var bugun = DateOnly.FromDateTime(DateTime.Today);
        var hedefTarih = bugun.AddDays(3);

        Debug.WriteLine($"[SmsJob] Hedef tarih: {hedefTarih:dd.MM.yyyy} (Bugün + 3 gün)");

        try
        {
            using var db = EczaneDbContextFactory.Create();

            // ── ADIM 1: Ayarları her çalışmada DB'den taze oku ───────────────
            var gsmPort          = db.Ayarlar.Find("GsmPort")?.Value ?? string.Empty;
            var ilacSmsSablonu   = db.Ayarlar.Find("IlacSmsSablonu")?.Value
                                   ?? EczaneOtomasyonu.ViewModels.AyarlarViewModel.VarsayilanIlacSablonu;
            var receteSmsSablonu = db.Ayarlar.Find("ReceteSmsSablonu")?.Value
                                   ?? EczaneOtomasyonu.ViewModels.AyarlarViewModel.VarsayilanReceteSablonu;

            Debug.WriteLine($"[SmsJob] GsmPort='{gsmPort}'");
            Debug.WriteLine($"[SmsJob] İlaç şablonu : '{ilacSmsSablonu[..Math.Min(60, ilacSmsSablonu.Length)]}'");
            Debug.WriteLine($"[SmsJob] Reçete şablonu: '{receteSmsSablonu[..Math.Min(60, receteSmsSablonu.Length)]}'");

            // SQLite tarih dönüştürme/eşitlik davranışından bağımsız olarak tarihleri
            // DateOnly ile C# tarafında karşılaştır.
            var tumKayitlar = await db.IlacKayitlari
                .Include(k => k.Hasta)
                .ToListAsync();
            var tariheUyanlar = tumKayitlar
                .Where(k => AyniTakvimGunu(k.IlacBitisTarihi, hedefTarih) ||
                            AyniTakvimGunu(k.ReceteBitisTarihi, hedefTarih))
                .ToList();
            AppendLog(logPath,
                $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] Veritabanında {tumKayitlar.Count} kayıt incelendi, " +
                $"{tariheUyanlar.Count} kaydın ilaç veya reçete bitiş tarihi 3 gün sonrası ({hedefTarih:dd.MM.yyyy}).");
            foreach (var kayit in tumKayitlar)
            {
                AppendLog(logPath,
                    $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] Kayıt #{kayit.Id} ({kayit.IlacAdi}): " +
                    $"ilaç bitiş={kayit.IlacBitisTarihi:dd.MM.yyyy}, " +
                    $"reçete bitiş={kayit.ReceteBitisTarihi:dd.MM.yyyy}");
            }

            // ── ADIM 2: GsmPort'a göre SMS servisini seç ─────────────────────
            ISmsService smsServis = await SmsServisiSec(gsmPort);

            if (!smsServis.BaglantiHazirMi)
            {
                Debug.WriteLine("[SmsJob][UYARI] SMS servisi hazır değil. Bu çalışma atlandı.");
                return;
            }

            // ── ADIM 3: İlaç Bitiş SMS Kontrolü ─────────────────────────────
            // Tarihe uyan ama SMS zaten gönderilmiş kayıtları logla (atlıyoruz)
            var ilacAtlananlar = tumKayitlar
                .Where(k => AyniTakvimGunu(k.IlacBitisTarihi, hedefTarih) && k.IlacBitisSmsGonderildi)
                .ToList();
            foreach (var atlanan in ilacAtlananlar)
            {
                var logMesaji = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] Kayıt #{atlanan.Id} ({atlanan.IlacAdi}) " +
                                $"için İlaç SMS zaten gönderilmiş, atlanıyor. (Bitiş: {atlanan.IlacBitisTarihi:dd.MM.yyyy})";
                AppendLog(logPath, logMesaji);
                Debug.WriteLine($"[SmsJob][ATLANDI] {logMesaji}");
            }

            var ilacBitisKayitlari = tumKayitlar
                .Where(k => AyniTakvimGunu(k.IlacBitisTarihi, hedefTarih) && !k.IlacBitisSmsGonderildi)
                .Select(k => new { Kayit = k, Telefon = k.Hasta.Telefon, IsimSoyisim = k.Hasta.IsimSoyisim, k.IlacAdi, k.IlacBitisTarihi })
                .ToList();

            Debug.WriteLine($"[SmsJob] İlaç bitiş SMS bekleyen: {ilacBitisKayitlari.Count} kayıt");

            int basariliIlacSayisi = 0;
            foreach (var item in ilacBitisKayitlari)
            {
                // İlaç bitişine özgü SMS metni: DB'deki IlacSmsSablonu kullanılır
                var mesaj = MesajOlustur(ilacSmsSablonu, item.IsimSoyisim, item.IlacAdi);

                bool basarili = await smsServis.SmsGonderAsync(item.Telefon, mesaj);
                if (basarili)
                {
                    item.Kayit.IlacBitisSmsGonderildi = true;
                    basariliIlacSayisi++;
                    var logMesaji = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] ✓ İlaç SMS gönderildi: " +
                                    $"{item.Telefon} | {item.IsimSoyisim} | {item.IlacAdi} " +
                                    $"(Bitiş: {item.IlacBitisTarihi:dd.MM.yyyy})";
                    AppendLog(logPath, logMesaji);
                    Debug.WriteLine($"[SmsJob] ✓ İlaç SMS: {item.Telefon} ({item.IlacAdi})");
                }
                else
                {
                    var logMesaji = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] ✗ İlaç SMS GÖNDERİLEMEDİ: " +
                                    $"{item.Telefon} | {item.IsimSoyisim} | {item.IlacAdi}";
                    AppendLog(logPath, logMesaji);
                    Debug.WriteLine($"[SmsJob] ✗ İlaç SMS GÖNDERİLEMEDİ: {item.Telefon}");
                }
            }

            // ── ADIM 4: Reçete Bitiş SMS Kontrolü ────────────────────────────
            // Tarihe uyan ama SMS zaten gönderilmiş kayıtları logla (atlıyoruz)
            var receteAtlananlar = tumKayitlar
                .Where(k => AyniTakvimGunu(k.ReceteBitisTarihi, hedefTarih) && k.ReceteBitisSmsGonderildi)
                .ToList();
            foreach (var atlanan in receteAtlananlar)
            {
                var logMesaji = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] Kayıt #{atlanan.Id} ({atlanan.IlacAdi}) " +
                                $"için Reçete SMS zaten gönderilmiş, atlanıyor. (Bitiş: {atlanan.ReceteBitisTarihi:dd.MM.yyyy})";
                AppendLog(logPath, logMesaji);
                Debug.WriteLine($"[SmsJob][ATLANDI] {logMesaji}");
            }

            var receteBitisKayitlari = tumKayitlar
                .Where(k => AyniTakvimGunu(k.ReceteBitisTarihi, hedefTarih) && !k.ReceteBitisSmsGonderildi)
                .Select(k => new { Kayit = k, Telefon = k.Hasta.Telefon, IsimSoyisim = k.Hasta.IsimSoyisim, k.IlacAdi, k.ReceteBitisTarihi })
                .ToList();

            Debug.WriteLine($"[SmsJob] Reçete bitiş SMS bekleyen: {receteBitisKayitlari.Count} kayıt");

            int basariliReceteSayisi = 0;
            foreach (var item in receteBitisKayitlari)
            {
                // Reçete bitişine özgü SMS metni: DB'deki ReceteSmsSablonu kullanılır
                var mesaj = MesajOlustur(receteSmsSablonu, item.IsimSoyisim, item.IlacAdi);

                bool basarili = await smsServis.SmsGonderAsync(item.Telefon, mesaj);
                if (basarili)
                {
                    item.Kayit.ReceteBitisSmsGonderildi = true;
                    basariliReceteSayisi++;
                    var logMesaji = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] ✓ Reçete SMS gönderildi: " +
                                    $"{item.Telefon} | {item.IsimSoyisim} | {item.IlacAdi} " +
                                    $"(Bitiş: {item.ReceteBitisTarihi:dd.MM.yyyy})";
                    AppendLog(logPath, logMesaji);
                    Debug.WriteLine($"[SmsJob] ✓ Reçete SMS: {item.Telefon} ({item.IlacAdi})");
                }
                else
                {
                    var logMesaji = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] ✗ Reçete SMS GÖNDERİLEMEDİ: " +
                                    $"{item.Telefon} | {item.IsimSoyisim} | {item.IlacAdi}";
                    AppendLog(logPath, logMesaji);
                    Debug.WriteLine($"[SmsJob] ✗ Reçete SMS GÖNDERİLEMEDİ: {item.Telefon}");
                }
            }

            // ── ADIM 5: Değişiklikleri kaydet ────────────────────────────────
            int degisiklik = await db.SaveChangesAsync();
            int toplamBasarili = basariliIlacSayisi + basariliReceteSayisi;
            Debug.WriteLine($"[SmsJob] ■ Tamamlandı. DB: {degisiklik} satır güncellendi, " +
                            $"SMS: {toplamBasarili} gönderildi (İlaç: {basariliIlacSayisi}, Reçete: {basariliReceteSayisi}).");
            AppendLog(logPath,
                $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] Job tamamlandı; DB {degisiklik} satır güncellendi. " +
                $"Başarılı SMS: {toplamBasarili} (ilaç: {basariliIlacSayisi}, reçete: {basariliReceteSayisi}). " +
                $"Atlanan (zaten gönderilmiş): ilaç {ilacAtlananlar.Count}, reçete {receteAtlananlar.Count}.");

            // GsmModemSmsService ise bağlantıyı temizle
            if (smsServis is IDisposable disposable)
                disposable.Dispose();

            // ── ADIM 6: Kayıtlı Hastalar ekranını uyar ──────────────────────
            // En az bir SMS başarıyla gönderilip DB'ye kaydedildiyse UI'ı haberdar et.
            // KayitliHastalarViewModel bu eventi alınca tabloyu otomatik yeniler ve
            // SMS Durumu rozetleri "Bekliyor" → "Gönderildi" olarak güncellenir.
            if (toplamBasarili > 0)
            {
                Debug.WriteLine($"[SmsJob] UI yenileme eventi tetikleniyor ({toplamBasarili} başarılı SMS)...");
                SmsGonderimTamamlandi?.Invoke(null, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SmsJob][HATA] {ex.Message}\n{ex.StackTrace}");
            AppendLog(logPath, $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] SMS JOB HATA: {ex}");
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }

    private static void AppendLog(string logPath, string message)
    {
        try
        {
            File.AppendAllText(logPath, message + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SmsJob][LOG HATA] Masaüstü loguna yazılamadı ({logPath}): {ex}");
        }
    }

    private static bool AyniTakvimGunu(DateOnly tarih, DateOnly hedef) =>
        tarih.Day == hedef.Day &&
        tarih.Month == hedef.Month &&
        tarih.Year == hedef.Year;

    /// <summary>
    /// Verilen şablondaki {IsimSoyisim} ve {IlacAdi} değişkenlerini gerçek değerlerle
    /// Replace ederek hazır SMS metnini döner.
    /// Şablon doğrudan veritabanından okunur; bu metot hiçbir fallback veya
    /// yedek metin üretmez — sistem sadece kullanıcının ayarladığı metni kullanır.
    /// </summary>
    private static string MesajOlustur(string sablon, string isimSoyisim, string ilacAdi)
    {
        var isim = string.IsNullOrWhiteSpace(isimSoyisim) ? "Değerli Hasta" : isimSoyisim.Trim();
        var ilac = string.IsNullOrWhiteSpace(ilacAdi)     ? string.Empty    : ilacAdi.Trim();

        return sablon
            .Replace("{IsimSoyisim}", isim, StringComparison.OrdinalIgnoreCase)
            .Replace("{IlacAdi}",     ilac, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Yardımcı: Port → Servis Seçimi ──────────────────────────────────────

    /// <summary>
    /// GsmPort değerine göre uygun SMS servisini döner.
    /// Boşsa DebugConsoleSmsService, doluysa GsmModemSmsService.
    /// Modem bağlanamazsa yedek olarak Debug servisi devreye girer.
    /// </summary>
    private static async Task<ISmsService> SmsServisiSec(string portAdi)
    {
        if (string.IsNullOrWhiteSpace(portAdi))
        {
            Debug.WriteLine("[SmsJob] GsmPort boş → DebugConsoleSmsService");
            return new DebugConsoleSmsService();
        }

        var modem = new GsmModemSmsService(portAdi);
        bool baglandi = await modem.BaglanAsync();
        if (!baglandi)
        {
            Debug.WriteLine($"[SmsJob][UYARI] Modem bağlanamadı ({portAdi}) → DebugConsoleSmsService");
            modem.Dispose();
            return new DebugConsoleSmsService();
        }

        Debug.WriteLine($"[SmsJob] GSM Modem bağlandı: {portAdi}");
        return modem;
    }
}
