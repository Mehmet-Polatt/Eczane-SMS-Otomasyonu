using CommunityToolkit.Mvvm.ComponentModel;

namespace EczaneOtomasyonu.ViewModels;

/// <summary>
/// OCR'ın hatalı/şüpheli okuduğu kayıtlar için DataGrid satır modeli.
/// Kullanıcı hücreleri düzenleyebilir; her değişiklik UI'ya anında yansır.
/// </summary>
public partial class HataliKayitSatiri : ObservableObject
{
    /// <summary>OCR çıktısından çıkarılan hasta adı ve soyadı.</summary>
    [ObservableProperty]
    private string _isimSoyisim = string.Empty;

    /// <summary>OCR çıktısından çıkarılan TC Kimlik numarası.</summary>
    [ObservableProperty]
    private string _tcKimlik = string.Empty;

    /// <summary>Ham OCR çıktısından gelen telefon (henüz doğrulanmamış).</summary>
    [ObservableProperty]
    private string _telefon = string.Empty;

    /// <summary>Ham OCR çıktısından gelen ilaç adı (henüz doğrulanmamış).</summary>
    [ObservableProperty]
    private string _ilacAdi = string.Empty;

    /// <summary>Ham OCR çıktısından gelen ilaç bitiş tarihi metni (henüz doğrulanmamış).</summary>
    [ObservableProperty]
    private string _ilacBitisTarihiMetin = string.Empty;

    /// <summary>Ham OCR çıktısından gelen reçete bitiş tarihi metni (henüz doğrulanmamış).</summary>
    [ObservableProperty]
    private string _receteBitisTarihiMetin = string.Empty;

    /// <summary>Satırın neden hatalı olduğunu açıklayan kısa not (kullanıcıya bilgi amaçlı).</summary>
    [ObservableProperty]
    private string _hataNedeni = string.Empty;
}
