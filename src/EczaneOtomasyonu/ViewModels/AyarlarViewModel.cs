using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EczaneOtomasyonu.Data;
using EczaneOtomasyonu.Models;

namespace EczaneOtomasyonu.ViewModels;

/// <summary>
/// Ayarlar ekranı için ViewModel.
/// Veritabanındaki Ayar tablosunu okur/yazar.
/// </summary>
public partial class AyarlarViewModel : ViewModelBase
{
    // ─── Varsayılan şablon sabitleri ──────────────────────────────────────────
    public const string VarsayilanIlacSablonu   = "Sayın {IsimSoyisim}, {IlacAdi} ilacınızın bitmesine 3 gün kalmıştır.";
    public const string VarsayilanReceteSablonu = "Sayın {IsimSoyisim}, {IlacAdi} ilacınızın reçete süresinin bitmesine 3 gün kalmıştır.";

    // ─── Bağlı Özellikler ─────────────────────────────────────────────────────

    /// <summary>
    /// GSM Modem port adı.
    /// macOS: /dev/tty.usbserial-XXXX  |  Windows: COM3
    /// </summary>
    [ObservableProperty]
    private string _gsmPort = string.Empty;

    /// <summary>
    /// İlaç bitiş SMS şablonu.
    /// Desteklenen değişkenler: {IsimSoyisim}, {IlacAdi}
    /// </summary>
    [ObservableProperty]
    private string _ilacSmsSablonu = string.Empty;

    /// <summary>
    /// Reçete bitiş SMS şablonu.
    /// Desteklenen değişkenler: {IsimSoyisim}, {IlacAdi}
    /// </summary>
    [ObservableProperty]
    private string _receteSmsSablonu = string.Empty;

    /// <summary>
    /// Karanlık mod aktif mi (true: Dark, false: Light / varsayılan: false).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemaButonMetni))]
    private bool _karanlikModAktif = false;

    /// <summary>
    /// Tema butonunun üzerindeki durum metni.
    /// </summary>
    public string TemaButonMetni => KaranlikModAktif ? "☀️  Açık Temaya Geç" : "🌙  Karanlık Temaya Geç";

    /// <summary>Kullanıcıya gösterilen durum mesajı (kayıt sonucu vb.).</summary>
    [ObservableProperty]
    private string _durumMesaji = string.Empty;

    /// <summary>Durum mesajının renk kodu (yeşil=başarı, kırmızı=hata).</summary>
    [ObservableProperty]
    private string _durumRengi = "#94A3B8";

    /// <summary>İşlem devam ederken true; buton ve inputları kilitler.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(KaydetCommand))]
    [NotifyCanExecuteChangedFor(nameof(TemaDegistirCommand))]
    private bool _islemYapiliyor = false;

    private bool KaydetAktifMi => !IslemYapiliyor;

    // ─── Başlatma ─────────────────────────────────────────────────────────────

    public AyarlarViewModel()
    {
        // ViewModel oluşturulduğunda DB'den mevcut değerleri yükle
        _ = YukleAsync();
    }

    private bool _yukleniyor = false;

    partial void OnKaranlikModAktifChanged(bool value)
    {
        TemayiUygula(value);

        // Kullanıcı ToggleSwitch'i değiştirdiğinde yeni tema ayarını otomatik kaydet
        if (!_yukleniyor)
        {
            Task.Run(async () =>
            {
                try
                {
                    using var db = EczaneDbContextFactory.Create();
                    AyarUpsert(db, "KaranlikModAktif", value ? "true" : "false");
                    await db.SaveChangesAsync();
                    Debug.WriteLine($"[Ayarlar] Tema tercihi kaydedildi: {(value ? "Karanlık" : "Açık")}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Ayarlar] Tema tercihi kaydedilirken hata: {ex.Message}");
                }
            });
        }
    }

    // ─── KOMUTLAR ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Mevcut ayarları veritabanından yükler ve UI alanlarını doldurur.
    /// </summary>
    [RelayCommand]
    private async Task YukleAsync()
    {
        IslemYapiliyor = true;
        try
        {
            await Task.Run(() =>
            {
                using var db = EczaneDbContextFactory.Create();

                var gsmPortAyar        = db.Ayarlar.FirstOrDefault(a => a.Key == "GsmPort");
                var ilacSablonuAyar    = db.Ayarlar.FirstOrDefault(a => a.Key == "IlacSmsSablonu");
                var recedeSablonuAyar  = db.Ayarlar.FirstOrDefault(a => a.Key == "ReceteSmsSablonu");
                var karanlikModAyar    = db.Ayarlar.FirstOrDefault(a => a.Key == "KaranlikModAktif");
                var karanlikMod        = karanlikModAyar?.Value == "true";

                // UI thread güncellemesi
                Dispatcher.UIThread.Post(() =>
                {
                    _yukleniyor = true;
                    try
                    {
                        GsmPort          = gsmPortAyar?.Value        ?? string.Empty;
                        IlacSmsSablonu   = ilacSablonuAyar?.Value    ?? VarsayilanIlacSablonu;
                        ReceteSmsSablonu = recedeSablonuAyar?.Value  ?? VarsayilanReceteSablonu;
                        KaranlikModAktif = karanlikMod;
                        DurumMesaji      = string.Empty;
                    }
                    finally
                    {
                        _yukleniyor = false;
                    }
                });
            });

            Debug.WriteLine("[Ayarlar] Ayarlar yüklendi.");
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Ayarlar yüklenirken hata: {ex.Message}";
            DurumRengi  = "#F87171";
            Debug.WriteLine($"[Ayarlar][HATA] {ex}");
        }
        finally
        {
            IslemYapiliyor = false;
        }
    }

    /// <summary>
    /// Ekrandaki değerleri veritabanına kaydeder.
    /// Kayıt yoksa ekler (upsert), varsa günceller.
    /// </summary>
    [RelayCommand(CanExecute = nameof(KaydetAktifMi))]
    private async Task KaydetAsync()
    {
        IslemYapiliyor = true;
        DurumMesaji    = string.Empty;

        try
        {
            var portDeger         = GsmPort.Trim();
            var ilacSablonDeger   = string.IsNullOrWhiteSpace(IlacSmsSablonu)
                                    ? VarsayilanIlacSablonu
                                    : IlacSmsSablonu.Trim();
            var recedeSablonDeger = string.IsNullOrWhiteSpace(ReceteSmsSablonu)
                                    ? VarsayilanReceteSablonu
                                    : ReceteSmsSablonu.Trim();

            await Task.Run(() =>
            {
                using var db = EczaneDbContextFactory.Create();

                // Upsert: bul veya oluştur
                AyarUpsert(db, "GsmPort",          portDeger);
                AyarUpsert(db, "IlacSmsSablonu",   ilacSablonDeger);
                AyarUpsert(db, "ReceteSmsSablonu",  recedeSablonDeger);
                AyarUpsert(db, "KaranlikModAktif", KaranlikModAktif ? "true" : "false");

                db.SaveChanges();
            });

            DurumMesaji = "✓ Ayarlar başarıyla kaydedildi.";
            DurumRengi  = "#34D399";
            Debug.WriteLine("[Ayarlar] Ayarlar kaydedildi.");
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Kayıt sırasında hata: {ex.Message}";
            DurumRengi  = "#F87171";
            Debug.WriteLine($"[Ayarlar][HATA] {ex}");
        }
        finally
        {
            IslemYapiliyor = false;
        }
    }

    /// <summary>
    /// Tema değiştirme butonuna basıldığında karanlık ve açık mod arasında geçiş yapar,
    /// temayı anlık değiştirir ve yeni ayarı veritabanına kaydeder.
    /// </summary>
    [RelayCommand(CanExecute = nameof(KaydetAktifMi))]
    private async Task TemaDegistirAsync()
    {
        KaranlikModAktif = !KaranlikModAktif;
        TemayiUygula(KaranlikModAktif);
        await KaydetAsync();
    }

    /// <summary>
    /// Application.Current.RequestedThemeVariant özelliğini kullanarak
    /// uygulamanın temasını anlık olarak ThemeVariant.Dark veya ThemeVariant.Light olarak günceller.
    /// </summary>
    public static void TemayiUygula(bool karanlik)
    {
        if (Application.Current != null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Application.Current.RequestedThemeVariant =
                    karanlik ? ThemeVariant.Dark : ThemeVariant.Light;
            });
        }
    }

    // ─── Yardımcı ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Verilen anahtara sahip kayıt varsa günceller, yoksa ekler.
    /// </summary>
    private static void AyarUpsert(EczaneDbContext db, string key, string value)
    {
        var kayit = db.Ayarlar.Find(key);
        if (kayit == null)
        {
            db.Ayarlar.Add(new Ayar { Key = key, Value = value });
        }
        else
        {
            kayit.Value = value;
        }
    }
}
