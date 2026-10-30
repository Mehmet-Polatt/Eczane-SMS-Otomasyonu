using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace EczaneOtomasyonu.Services;

/// <summary>
/// macOS ekran görüntüsü servisi.
/// 
/// Strateji: `screencapture -i -x &lt;dosya&gt;`
///   -i → Interaktif: Kullanıcı ekranda bir dikdörtgen seçer (veya Esc ile iptal eder).
///   -x → Sistem shutter sesini kapatır.
/// 
/// Eğer kullanıcı Esc'ye basarsa dosya oluşturulmaz → null döner.
/// </summary>
public class MacScreenshotService : IScreenshotService
{
    public async Task<string?> EkranGoruntusiAlAsync()
    {
        // Geçici PNG dosyası oluştur (benzersiz isim ile)
        var geciciDosya = Path.Combine(
            Path.GetTempPath(),
            $"eczane_tarama_{Guid.NewGuid():N}.png");

        var psi = new ProcessStartInfo
        {
            FileName = "screencapture",
            // -i: interaktif seçim, -x: sessiz, -t png: format
            Arguments = $"-i -x -t png \"{geciciDosya}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();

            // screencapture kullanıcı Esc'ye basana ya da alanı seçene kadar bloklar.
            // Asenkron bekleme: arka plana at, UI donmasın.
            await Task.Run(() => process.WaitForExit());

            var hataCikisi = await process.StandardError.ReadToEndAsync();
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Ekran kaydı izni yok veya işlem iptal edildi. screencapture çıkış kodu: {process.ExitCode}. {hataCikisi}".Trim());

            // Kullanıcı alanı seçtiyse dosya oluşturulmalıdır.
            if (File.Exists(geciciDosya))
            {
                Debug.WriteLine($"[Mac Ekran] Görüntü alındı: {geciciDosya}");
                return geciciDosya;
            }

            throw new InvalidOperationException(
                "Ekran kaydı izni yok veya işlem iptal edildi; screencapture görüntü dosyası oluşturmadı.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Mac Ekran][HATA] screencapture başarısız: {ex}");
            throw new InvalidOperationException(
                $"Ekran kaydı izni yok veya işlem iptal edildi. 'screencapture' başarısız oldu.\n{ex}", ex);
        }
    }
}
