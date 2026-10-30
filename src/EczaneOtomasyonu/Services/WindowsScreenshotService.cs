using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace EczaneOtomasyonu.Services;

/// <summary>
/// Windows ekran görüntüsü servisi.
///
/// Strateji: <c>System.Drawing.Common</c> üzerinden <c>Graphics.CopyFromScreen</c>.
/// Tüm birincil ekranı yakalar (1920×1080 varsayılan).
///
/// NOT: Windows'a özgü API kullanır. Mac'te <see cref="MacScreenshotService"/> kullanılır.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsScreenshotService : IScreenshotService
{
    /// <summary>Varsayılan ekran genişliği (Windows'ta runtime'da güncellenir).</summary>
    private const int VarsayilanGenislik = 1920;

    /// <summary>Varsayılan ekran yüksekliği.</summary>
    private const int VarsayilanYukseklik = 1080;

    public Task<string?> EkranGoruntusiAlAsync()
    {
        var geciciDosya = Path.Combine(
            Path.GetTempPath(),
            $"eczane_tarama_{Guid.NewGuid():N}.png");

        try
        {
            // Ekran boyutlarını al
            // Not: System.Windows.Forms bağımlılığı eklemeden basit yaklaşım kullanılıyor.
            // Gelecekte Avalonia'nın Screens API'si ile değiştirilebilir.
            int genislik = VarsayilanGenislik;
            int yukseklik = VarsayilanYukseklik;

            // System.Drawing ile tam ekranı yakala
            using var bitmap = new Bitmap(genislik, yukseklik, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);

            graphics.CopyFromScreen(
                sourceX: 0,
                sourceY: 0,
                destinationX: 0,
                destinationY: 0,
                blockRegionSize: new System.Drawing.Size(genislik, yukseklik),
                copyPixelOperation: CopyPixelOperation.SourceCopy);

            bitmap.Save(geciciDosya, ImageFormat.Png);

            Debug.WriteLine($"[Win Ekran] Tam ekran görüntüsü alındı ({genislik}x{yukseklik}): {geciciDosya}");
            return Task.FromResult<string?>(geciciDosya);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Win Ekran][HATA] {ex.Message}");
            throw new InvalidOperationException(
                $"Windows ekran görüntüsü alınamadı: {ex.Message}", ex);
        }
    }
}
