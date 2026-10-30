using System.Threading.Tasks;

namespace EczaneOtomasyonu.Services;

/// <summary>
/// Ekran görüntüsü alma sözleşmesi.
/// Mac ve Windows implementasyonları bu arayüzü uygular.
/// </summary>
public interface IScreenshotService
{
    /// <summary>
    /// Kullanıcının seçtiği ya da tanımlanan bölgenin ekran görüntüsünü alır
    /// ve geçici bir dosya yolu döner. Kullanıcı iptal ederse null döner.
    /// </summary>
    Task<string?> EkranGoruntusiAlAsync();
}
