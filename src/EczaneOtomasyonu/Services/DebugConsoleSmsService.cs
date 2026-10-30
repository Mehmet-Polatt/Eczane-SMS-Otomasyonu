using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace EczaneOtomasyonu.Services;

/// <summary>
/// Geliştirme ve test ortamı için SMS servis implementasyonu.
/// Gerçek modem olmadan SMS akışını Debug konsoluna yazar.
/// Üretimde <see cref="GsmModemSmsService"/> ile değiştirin.
/// </summary>
public class DebugConsoleSmsService : ISmsService
{
    public bool BaglantiHazirMi => true; // Her zaman hazır (simülasyon)

    public Task<bool> SmsGonderAsync(string telefon, string mesaj)
    {
        var simdi = DateTime.Now;
        var zaman = simdi.ToString("HH:mm:ss");
        var logSatiri = $"[{simdi:dd.MM.yyyy HH:mm}] SMS GÖNDERİLDİ: {telefon} | {mesaj}{Environment.NewLine}";
        Debug.WriteLine($"[SMS-DEBUG] [{zaman}] → {telefon}");
        Debug.WriteLine($"[SMS-DEBUG]   Mesaj: {mesaj}");
        Debug.WriteLine($"[SMS-DEBUG]   ✓ (Simülasyon — gerçek SMS gönderilmedi)");

        Console.WriteLine($"[SMS] {zaman} | {telefon} | {mesaj}");

        try
        {
            var masaustu = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var logYolu = Path.Combine(masaustu, "sms_log.txt");
            File.AppendAllText(logYolu, logSatiri);
            Debug.WriteLine($"[SMS-DEBUG] Log dosyasına eklendi: {logYolu}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SMS-DEBUG][LOG HATA] Masaüstüne sms_log.txt yazılamadı: {ex}");
        }

        return Task.FromResult(true);
    }
}
