using System.Runtime.InteropServices;

namespace EczaneOtomasyonu.Services;

/// <summary>
/// Platform bağımlı servislerin doğru implementasyonunu üretir.
/// ViewModel bu sınıftan servislerini alır; platform detaylarını bilmek zorunda kalmaz.
/// </summary>
public static class ServisFactory
{
    /// <summary>
    /// Mevcut işletim sistemine göre uygun ekran görüntüsü servisini döner.
    /// </summary>
    public static IScreenshotService EkranServisiOlustur()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacScreenshotService();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsScreenshotService();

        // Linux / diğer: Mac benzeri davranış (screencapture yerine scrot vb. eklenebilir)
        return new MacScreenshotService();
    }

    /// <summary>
    /// Tesseract OCR servisini döner. Tüm platformlarda aynı sınıf kullanılır;
    /// platform farkı sınıfın içinde yönetilir.
    /// </summary>
    public static IOcrService OcrServisiOlustur()
    {
        return new TesseractOcrService();
    }
}
