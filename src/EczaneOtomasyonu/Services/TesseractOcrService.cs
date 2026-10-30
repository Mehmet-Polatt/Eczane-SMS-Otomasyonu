using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Tesseract;

namespace EczaneOtomasyonu.Services;

/// <summary>
/// Tesseract OCR motoru üzerinden görüntü okuma servisi.
///
/// ── Platform Stratejisi ────────────────────────────────────────────────────────
/// Tesseract NuGet (5.2.0) yalnızca Windows DLL içerir (tesseract50.dll).
/// macOS'ta Homebrew ile kurulan native kütüphaneler kullanılır:
///   ARM (Apple Silicon): /opt/homebrew/lib/
///   Intel Mac          : /usr/local/lib/
///
/// Bu yüzden Mac'te çalışmadan önce:
///   brew install tesseract
/// komutu çalıştırılmış olmalıdır.
///
/// ── tessdata Arama Sırası ─────────────────────────────────────────────────────
/// 1. Uygulama klasöründeki ./tessdata/          (deploy edilen traineddata)
/// 2. Homebrew paylaşım klasörü                  (geliştirme ortamında)
/// 3. Standart /usr/share/tessdata/              (Linux/WSL)
/// ─────────────────────────────────────────────────────────────────────────────
/// </summary>
public class TesseractOcrService : IOcrService
{
    // macOS ARM ve Intel için olası Homebrew kütüphane yolları
    private static readonly string[] MacLibraryPaths =
    {
        "/opt/homebrew/lib",   // Apple Silicon (M1/M2/M3)
        "/usr/local/lib",      // Intel Mac
    };

    private readonly string _tessdataKlasoru;

    public TesseractOcrService()
    {
        _tessdataKlasoru = TessdataBul();
        AyarlaLibraryPath();
    }

    // ─── IOcrService Implementasyonu ─────────────────────────────────────────

    public Task<IReadOnlyList<OcrSatirSonucu>> OkuAsync(string gorselYolu)
    {
        return Task.Run<IReadOnlyList<OcrSatirSonucu>>(() =>
        {
            if (!File.Exists(gorselYolu))
                throw new FileNotFoundException($"Görüntü dosyası bulunamadı: {gorselYolu}");

            var islenmisGorselYolu = GorseliOnIsle(gorselYolu);
            try
            {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return MacCliIleOku(islenmisGorselYolu);

            List<string> siraliSatirlar;

            try
            {
                using var engine = new TesseractEngine(
                    _tessdataKlasoru,
                    "tur+eng",                       // Türkçe + İngilizce dil modelleri
                    EngineMode.LstmOnly);            // Modern LSTM motoru (daha doğru)

                // Görüntü işleme iyileştirmeleri
                engine.DefaultPageSegMode = PageSegMode.SparseText;
                engine.SetVariable("user_defined_dpi", "300");
                engine.SetVariable("preserve_interword_spaces", "1");

                // Ön işlem PNG'sini diske kaydedip Leptonica'nın dosya okuyucusuyla aç.
                using var img = Pix.LoadFromFile(islenmisGorselYolu);
                using var page = engine.Process(img);

                var tsv = page.GetTsvText(0);
                siraliSatirlar = TsvSatirlariniOlustur(tsv);
            }
            catch (TesseractException tex)
            {
                throw new InvalidOperationException(
                    $"Tesseract motoru başlatılamadı.\n" +
                    $"tessdata klasörü: {_tessdataKlasoru}\n" +
                    $"'tur.traineddata' dosyasının bu klasörde bulunduğundan emin olun.\n" +
                    $"Detay: {tex.Message}", tex);
            }
            catch (DllNotFoundException dex)
            {
                var platform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" : "Windows";
                string ipucu = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? "macOS: `brew install tesseract` komutunu çalıştırın."
                    : "Windows: Proje klasöründe x64/tesseract50.dll dosyasının bulunduğundan emin olun.";

                throw new InvalidOperationException(
                    $"Tesseract native kütüphanesi ({platform}) yüklenemedi.\n{ipucu}\nDetay: {dex.Message}", dex);
            }

            // Ham metni yapısal satırlara ayrıştır
            return OcrMetinAyristirici.Ayristir(siraliSatirlar);
            }
            finally
            {
                try
                {
                    if (File.Exists(islenmisGorselYolu))
                        File.Delete(islenmisGorselYolu);
                }
                catch
                {
                }
            }
        });
    }

    private static string GorseliOnIsle(string gorselYolu)
    {
        var islenmisYol = Path.Combine(
            Path.GetTempPath(),
            $"eczane_ocr_{Guid.NewGuid():N}.png");

        using var image = Image.Load(gorselYolu);
        var kaynakGenislik = image.Width;
        var kaynakYukseklik = image.Height;
        var genislik = checked(kaynakGenislik * 2);
        var yukseklik = checked(kaynakYukseklik * 2);
        image.Mutate(x => x
            .BackgroundColor(Color.White)
            .Resize(genislik, yukseklik, KnownResamplers.Lanczos3)
            .Grayscale()
            .BinaryThreshold(0.5f));

        image.SaveAsPng(islenmisYol);
        if (!File.Exists(islenmisYol) || new FileInfo(islenmisYol).Length == 0)
            throw new IOException($"Ön işlem PNG dosyası oluşturulamadı: {islenmisYol}");

        return islenmisYol;
    }

    private IReadOnlyList<OcrSatirSonucu> MacCliIleOku(string gorselYolu)
    {
        const string tesseractYolu = "/opt/homebrew/bin/tesseract";
        if (!File.Exists(tesseractYolu))
            throw new InvalidOperationException(
                $"Tesseract CLI bulunamadı: {tesseractYolu}. Homebrew tesseract kurulumunu doğrulayın.");

        var startInfo = new ProcessStartInfo
        {
            FileName = tesseractYolu,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(gorselYolu);
        startInfo.ArgumentList.Add("stdout");
        startInfo.ArgumentList.Add("--psm");
        startInfo.ArgumentList.Add("11");
        startInfo.ArgumentList.Add("--dpi");
        startInfo.ArgumentList.Add("300");
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add("tur+eng");
        startInfo.ArgumentList.Add("--tessdata-dir");
        startInfo.ArgumentList.Add(_tessdataKlasoru);
        startInfo.ArgumentList.Add("tsv");

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WaitAll(stdoutTask, stderrTask);

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"macOS Tesseract CLI hata koduyla sonlandı ({process.ExitCode}). {stderrTask.Result}");

            var siraliSatirlar = TsvSatirlariniOlustur(stdoutTask.Result);
            return OcrMetinAyristirici.Ayristir(siraliSatirlar);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"macOS Tesseract CLI çalıştırılamadı ({tesseractYolu}). {ex}", ex);
        }
    }

    private static List<string> TsvSatirlariniOlustur(string tsv)
    {
        var kelimeler = new List<KonumluKelime>();
        using var reader = new StringReader(tsv);
        reader.ReadLine(); // TSV başlık satırı
        string? satir;
        while ((satir = reader.ReadLine()) != null)
        {
            var alanlar = satir.Split('\t');
            if (alanlar.Length < 12 || alanlar[0] != "5" || string.IsNullOrWhiteSpace(alanlar[11]))
                continue;

            if (int.TryParse(alanlar[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var left) &&
                int.TryParse(alanlar[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var top) &&
                int.TryParse(alanlar[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
            {
                kelimeler.Add(new KonumluKelime(alanlar[11].Trim(), left, top + height / 2));
            }
        }

        var satirGruplari = new List<KonumSatiri>();
        foreach (var kelime in kelimeler.OrderBy(k => k.MerkezY).ThenBy(k => k.Left))
        {
            var grup = satirGruplari
                .Where(s => Math.Abs(s.OrtalamaY - kelime.MerkezY) <= 10)
                .OrderBy(s => Math.Abs(s.OrtalamaY - kelime.MerkezY))
                .FirstOrDefault();

            if (grup == null)
            {
                grup = new KonumSatiri(kelime.MerkezY);
                satirGruplari.Add(grup);
            }

            grup.Kelimeler.Add(kelime);
            grup.OrtalamaY = grup.Kelimeler.Average(k => k.MerkezY);
        }

        return satirGruplari.OrderBy(s => s.OrtalamaY)
            .Select(s => string.Join(" | ", s.Kelimeler.OrderBy(k => k.Left).Select(k => k.Metin)))
            .ToList();
    }

    private sealed record KonumluKelime(string Metin, int Left, int MerkezY);

    private sealed class KonumSatiri
    {
        public KonumSatiri(double ortalamaY) => OrtalamaY = ortalamaY;
        public double OrtalamaY { get; set; }
        public List<KonumluKelime> Kelimeler { get; } = new();
    }

    // ─── Yardımcı Metotlar ────────────────────────────────────────────────────

    /// <summary>
    /// tessdata klasörünü sırasıyla arar ve bulunan ilk geçerli yolu döner.
    /// </summary>
    private static string TessdataBul()
    {
        // 1. Uygulama yürütme klasörü (dağıtım için öncelikli)
        var appBase = AppContext.BaseDirectory;
        var yereltessdataYolu = Path.Combine(appBase, "tessdata");
        if (Directory.Exists(yereltessdataYolu) &&
            File.Exists(Path.Combine(yereltessdataYolu, "tur.traineddata")))
        {
            return yereltessdataYolu;
        }

        // 2. macOS Homebrew paylaşım klasörü
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string[] homebrewTessdataPaths =
            {
                "/opt/homebrew/share/tessdata",   // ARM Mac
                "/usr/local/share/tessdata",      // Intel Mac
            };

            foreach (var yol in homebrewTessdataPaths)
            {
                if (Directory.Exists(yol) && File.Exists(Path.Combine(yol, "tur.traineddata")))
                {
                    return yol;
                }
            }

            // Homebrew'de tur.traineddata yoksa, uygulama tessdata'sına fallback
            // (tur.traineddata var ama Homebrew klasöründe değil)
            foreach (var yol in homebrewTessdataPaths)
            {
                if (Directory.Exists(yol))
                {
                    // Uygulama tessdata'sını döndür; tur.traineddata orada olmalı
                    return yereltessdataYolu;
                }
            }
        }

        // 3. Linux / WSL
        var linuxYolu = "/usr/share/tessdata";
        if (Directory.Exists(linuxYolu))
        {
            return linuxYolu;
        }

        // 4. Son çare: uygulama klasörü (tessdata eksik olsa bile motoru başlatmayı dene)
        return yereltessdataYolu;
    }

    /// <summary>
    /// macOS'ta Tesseract'ın Homebrew native kütüphanelerini bulabilmesi için
    /// DYLD_LIBRARY_PATH ortam değişkenini günceller.
    /// Windows'ta bu metot hiçbir şey yapmaz.
    /// </summary>
    private static void AyarlaLibraryPath()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        // Mevcut DYLD yolunu al
        var mevcutDyld = Environment.GetEnvironmentVariable("DYLD_LIBRARY_PATH") ?? string.Empty;

        // Homebrew yollarını ekle (zaten ekliyse tekrar ekleme)
        var yeniYollar = new List<string>();
        foreach (var libYolu in MacLibraryPaths)
        {
            if (Directory.Exists(libYolu) && !mevcutDyld.Contains(libYolu))
            {
                yeniYollar.Add(libYolu);
            }
        }

        if (yeniYollar.Count > 0)
        {
            var birlesik = string.Join(":", yeniYollar);
            var yeniDyld = string.IsNullOrEmpty(mevcutDyld)
                ? birlesik
                : $"{birlesik}:{mevcutDyld}";

            Environment.SetEnvironmentVariable("DYLD_LIBRARY_PATH", yeniDyld,
                EnvironmentVariableTarget.Process);
        }

        // Ek olarak: macOS'ta Tesseract NuGet'in beklediği dylib symlink'ini kontrol et
        // Tesseract .NET wrapper, macOS'ta "libtesseract.dylib" adıyla arar.
        EnsureTesseractSymlinkAcesible();
    }

    /// <summary>
    /// Tesseract .NET wrapper'ının macOS'ta yükleyebileceği kütüphaneyi doğrular.
    /// Gerekiyorsa geçici bir kopyalama yapar.
    /// </summary>
    private static void EnsureTesseractSymlinkAcesible()
    {
        // Tesseract NuGet, macOS'ta "libtesseract.dylib" adını arar.
        // Homebrew bunu /opt/homebrew/lib/libtesseract.dylib olarak kurar.
        // Buradaki kontrol sadece Debug loglamaya yarar; gerçek yükleme DllImport üzerinden olur.
        foreach (var libYolu in MacLibraryPaths)
        {
            var dylib = Path.Combine(libYolu, "libtesseract.dylib");
            if (File.Exists(dylib))
            {
                return;
            }
        }

    }
}
