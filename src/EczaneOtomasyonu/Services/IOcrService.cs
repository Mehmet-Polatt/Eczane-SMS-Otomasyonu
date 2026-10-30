using System.Threading.Tasks;
using System.Collections.Generic;

namespace EczaneOtomasyonu.Services;

/// <summary>
/// OCR motoru sözleşmesi.
/// TesseractOcrService bu arayüzü uygular; test için mock implemente edilebilir.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Verilen görüntü dosyasını OCR ile okur.
    /// Her bir öğe bir tablo satırını temsil eden ham metin içerir.
    /// </summary>
    /// <param name="gorselYolu">Okunacak PNG/BMP dosyasının tam yolu.</param>
    /// <returns>Ayrıştırılmış satır listesi; motor başlatamazsa boş liste.</returns>
    Task<IReadOnlyList<OcrSatirSonucu>> OkuAsync(string gorselYolu);
}
