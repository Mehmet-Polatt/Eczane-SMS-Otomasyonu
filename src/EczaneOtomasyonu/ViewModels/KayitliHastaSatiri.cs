using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EczaneOtomasyonu.ViewModels;

/// <summary>
/// Veritabanındaki hasta ve ilaç kayıtlarını DataGrid üzerinde göstermek için kullanılan model.
/// </summary>
public partial class KayitliHastaSatiri : ObservableObject
{
    public int IlacKayitId { get; set; }
    public int HastaId { get; set; }

    [ObservableProperty]
    private string _isimSoyisim = string.Empty;

    [ObservableProperty]
    private string _tcKimlik = string.Empty;

    [ObservableProperty]
    private string _telefon = string.Empty;

    [ObservableProperty]
    private string _ilacAdi = string.Empty;

    [ObservableProperty]
    private string _ilacBitisTarihi = string.Empty;

    [ObservableProperty]
    private string _receteBitisTarihi = string.Empty;

    [ObservableProperty]
    private string _kayitTarihi = string.Empty;

    [ObservableProperty]
    private bool _ilacSmsGonderildi;

    [ObservableProperty]
    private bool _receteSmsGonderildi;
}
