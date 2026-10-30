using System.Threading.Tasks;

namespace EczaneOtomasyonu.Services;

/// <summary>
/// SMS gönderim sözleşmesi.
/// GsmModemSmsService (gerçek donanım) ve geliştirme ortamı için
/// DebugConsoleSmsService bu arayüzü uygular.
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Belirtilen numaraya SMS gönderir.
    /// </summary>
    /// <param name="telefon">Alıcı numarası (uluslararası formatta: +905XXXXXXXXX veya 05XXXXXXXXX).</param>
    /// <param name="mesaj">Gönderilecek metin (160 karakter sınırı GSM modem için önerilir).</param>
    /// <returns>Gönderim başarılıysa true, başarısız veya bağlantı kurulamazsa false.</returns>
    Task<bool> SmsGonderAsync(string telefon, string mesaj);

    /// <summary>
    /// Servisin aktif ve bağlantı kurabilir durumda olup olmadığını döner.
    /// Quartz job'ı çalışmadan önce bunu kontrol edebilir.
    /// </summary>
    bool BaglantiHazirMi { get; }
}
