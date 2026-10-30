using System.Collections.Generic;

namespace EczaneOtomasyonu.Services;

/// <summary>
/// Tesseract'ın döndürdüğü ham metin satırını temsil eder.
/// Parse edilmeden önce doğrulama servisi bu veriyi işler.
/// </summary>
public class OcrSatirSonucu
{
    public string IsimSoyisim { get; set; } = string.Empty;
    public string TcKimlik { get; set; } = string.Empty;
    public string Telefon { get; set; } = string.Empty;
    public string IlacAdi { get; set; } = string.Empty;
    public string IlacBitisTarihiMetin { get; set; } = string.Empty;
    public string ReceteBitisTarihiMetin { get; set; } = string.Empty;
}

/// <summary>
/// Bir tarama oturumunun toplam çıktısını tutar.
/// GecerliKayitlar direkt DB'ye, HataliKayitlar kullanıcıya gösterilir.
/// </summary>
public class OcrTaramaSonucu
{
    public List<OcrSatirSonucu> GecerliKayitlar { get; set; } = new();
    public List<OcrSatirSonucu> HataliKayitlar { get; set; } = new();
    public List<string> HataNedenler { get; set; } = new();
}
