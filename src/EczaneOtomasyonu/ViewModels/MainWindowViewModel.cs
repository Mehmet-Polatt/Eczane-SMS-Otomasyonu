using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EczaneOtomasyonu.Data;
using EczaneOtomasyonu.Models;
using EczaneOtomasyonu.Services;

namespace EczaneOtomasyonu.ViewModels;

/// <summary>Aktif sekmeyi belirten enum.</summary>
/// <summary>Aktif sekmeyi belirten enum.</summary>
public enum Sayfa { Tarama, Kayitlar, Ayarlar }

/// <summary>
/// MainWindow için MVVM ViewModel.
/// Tarama (OCR), hatalı kayıt düzenleme ve veritabanına kaydetme mantığını yönetir.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    // ─── Navigasyon ──────────────────────────────────────────────────────────

    /// <summary>Şu an gösterilen sayfa. MainWindow ContentControl buna bağlıdır.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TaramaSekmesiAktifMi))]
    [NotifyPropertyChangedFor(nameof(KayitlarSekmesiAktifMi))]
    [NotifyPropertyChangedFor(nameof(AyarlarSekmesiAktifMi))]
    private Sayfa _aktifSayfa = Sayfa.Tarama;

    /// <summary>Tarama sekmesi aktif mi? Sekme butonu stilini bağlamak için.</summary>
    public bool TaramaSekmesiAktifMi => AktifSayfa == Sayfa.Tarama;

    /// <summary>Kayıtlar sekmesi aktif mi?</summary>
    public bool KayitlarSekmesiAktifMi => AktifSayfa == Sayfa.Kayitlar;

    /// <summary>Ayarlar sekmesi aktif mi? Sekme butonu stilini bağlamak için.</summary>
    public bool AyarlarSekmesiAktifMi => AktifSayfa == Sayfa.Ayarlar;

    /// <summary>Kayıtlı Hastalar alt-ViewModel'i.</summary>
    private KayitliHastalarViewModel? _kayitliHastalarViewModel;
    public KayitliHastalarViewModel KayitliHastalarVm =>
        _kayitliHastalarViewModel ??= new KayitliHastalarViewModel();

    /// <summary>Ayarlar alt-ViewModel'i. Lazy: ilk kez ayarlara geçilince oluşturulur.</summary>
    private AyarlarViewModel? _ayarlarViewModel;
    public AyarlarViewModel AyarlarVm =>
        _ayarlarViewModel ??= new AyarlarViewModel();

    [RelayCommand]
    private void TaramayaGec() => AktifSayfa = Sayfa.Tarama;

    [RelayCommand]
    private async Task KayitlaraGec()
    {
        AktifSayfa = Sayfa.Kayitlar;
        await KayitliHastalarVm.KayitlariYukleAsync();
    }

    [RelayCommand]
    private void AyarlaraGec() => AktifSayfa = Sayfa.Ayarlar;

    // ─── Durum Listesi ────────────────────────────────────────────────────────

    /// <summary>
    /// Kusursuz doğrulanmış kayıtlar. Kaydedilmeyi bekler; kullanıcıya gösterilmez.
    /// </summary>
    private readonly ObservableCollection<OcrSatirSonucu> _gecerliKayitlar = new();

    /// <summary>
    /// OCR'ın hatalı/şüpheli okuduğu kayıtlar.
    /// DataGrid'e bağlıdır; kullanıcı hücreleri düzenleyebilir.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<HataliKayitSatiri> _hataliKayitlar = new();

    // ─── UI Durum Özellikleri ─────────────────────────────────────────────────

    /// <summary>Kullanıcıya gösterilen durum mesajı (işlem sonucu, hata vb.).</summary>
    [ObservableProperty]
    private string _durumMesaji = "Hazır. Taramayı başlatmak için 'Tara' butonuna basın.";

    /// <summary>İşlem devam ederken true; butonları devre dışı bırakır.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TaraCommand))]
    [NotifyCanExecuteChangedFor(nameof(KaydetCommand))]
    [NotifyCanExecuteChangedFor(nameof(HataliKaydiSilCommand))]
    private bool _islemYapiliyor = false;

    /// <summary>KaydetCommand'ın aktif olabilmesi için en az 1 kayıt gerekmektedir.</summary>
    private bool KaydetAktifMi =>
        !IslemYapiliyor && (_gecerliKayitlar.Count > 0 || HataliKayitlar.Count > 0);

    private bool TaraAktifMi => !IslemYapiliyor;

    [RelayCommand(CanExecute = nameof(TaraAktifMi))]
    private void HataliKaydiSil(HataliKayitSatiri? kayit)
    {
        if (kayit != null && HataliKayitlar.Remove(kayit))
        {
            KaydetCommand.NotifyCanExecuteChanged();
            DurumMesaji = "Kayıt listeden silindi.";
        }
    }

    // ─── Platform Servisleri ──────────────────────────────────────────────────

    /// <summary>
    /// Ekran görüntüsü servisi: Mac'te MacScreenshotService, Windows'ta WindowsScreenshotService.
    /// ServisFactory çalışma zamanında doğru implementasyonu seçer.
    /// </summary>
    private readonly IScreenshotService _ekranServisi = ServisFactory.EkranServisiOlustur();

    /// <summary>
    /// Tesseract OCR servisi. Dil: Türkçe (tur.traineddata).
    /// tessdata ve native kütüphane yönetimi TesseractOcrService içinde yapılır.
    /// </summary>
    private readonly IOcrService _ocrServisi = ServisFactory.OcrServisiOlustur();

    // ─── TARA KOMUTU ─────────────────────────────────────────────────────────

    /// <summary>
    /// Ekran görüntüsü alır, Tesseract OCR çalıştırır, sonuçları ayırır.
    /// Kusursuz kayıtlar _gecerliKayitlar'a, hatalılar HataliKayitlar'a gider.
    /// </summary>
    [RelayCommand(CanExecute = nameof(TaraAktifMi))]
    private async Task TaraAsync()
    {
        IslemYapiliyor = true;
        DurumMesaji = "Lütfen ekranda taramak istediğiniz alanı seçin...";

        _gecerliKayitlar.Clear();
        HataliKayitlar.Clear();

        string? geciciDosya = null;

        try
        {
            // ── ADIM 1: Ekran Görüntüsü Al ───────────────────────────────────
            // Mac: kullanıcıya interaktif alan seçtirir (screencapture -i -x)
            // Windows: tam ekranı yakalar
            geciciDosya = await _ekranServisi.EkranGoruntusiAlAsync();

            if (geciciDosya == null)
            {
                // Kullanıcı Esc ile iptal etti
                DurumMesaji = "Tarama iptal edildi. Alan seçilmedi.";
                Debug.WriteLine("[Tara] Kullanıcı ekran seçimini iptal etti.");
                return;
            }

            DurumMesaji = "Görüntü alındı, OCR işlemi yapılıyor...";

            // ── ADIM 2: Tesseract OCR ─────────────────────────────────────────
            // TesseractOcrService DYLD/tessdata yönetimini kendi içinde yapar.
            // Dönen liste zaten OcrMetinAyristirici tarafından ayrıştırılmıştır.
            var ocrSonuclari = await _ocrServisi.OkuAsync(geciciDosya);

            if (ocrSonuclari.Count == 0)
            {
                DurumMesaji = "OCR tamamlandı ancak seçilen alanda kayıt bulunamadı. " +
                              "Daha net bir alan seçmeyi deneyin.";
                return;
            }

            DurumMesaji = $"{ocrSonuclari.Count} satır tespit edildi, doğrulama yapılıyor...";

            // ── ADIM 3: Veritabanı mükerrer kontrolü + Validasyon ─────────────
            // Mükerrer eşleşme kriteri (3'lü Şart): İsim Soyisim + TC Kimlik + İlaç Adı
            // db.Hastalar.Any(h => h.IsimSoyisim == ocrKayit.IsimSoyisim && h.TcKimlik == ocrKayit.TcKimlik && h.IlacAdi == ocrKayit.IlacAdi)
            using var db = EczaneDbContextFactory.Create();

            var mevcutKayitlar = db.IlacKayitlari
                .Select(k => new
                {
                    IsimSoyisim = k.Hasta.IsimSoyisim ?? string.Empty,
                    TcKimlik = k.Hasta.TcKimlik ?? string.Empty,
                    IlacAdi = k.IlacAdi ?? string.Empty
                })
                .ToList();

            int gecerliSayac = 0;
            int hataliSayac = 0;
            int mukerrerSayac = 0;

            foreach (var satir in ocrSonuclari)
            {
                // ── 3'lü Şart Mükerrer Kontrolü (İsim Soyisim + TC Kimlik + İlaç Adı) ──
                // Daha önce düzeltilip kaydedilmiş bir kayıt OCR'dan tekrar hatalı
                // dönse bile (telefon okunamamış gibi), veritabanında varsa Hatalı
                // listesine DÜŞMEMELİ, mükerrer sayılmalıdır.
                var ocrIsim = (satir.IsimSoyisim ?? string.Empty).Trim();
                var ocrTc = (satir.TcKimlik ?? string.Empty).Trim();
                var ocrIlac = (satir.IlacAdi ?? string.Empty).Trim();

                // Null referans hatalarını önleyen güvenli string karşılaştırması
                bool dbdeMevcut = mevcutKayitlar.Any(k =>
                    string.Equals((k.IsimSoyisim ?? string.Empty).Trim(), ocrIsim, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((k.TcKimlik ?? string.Empty).Trim(), ocrTc, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((k.IlacAdi ?? string.Empty).Trim(), ocrIlac, StringComparison.OrdinalIgnoreCase));

                if (dbdeMevcut)
                {
                    // Veritabanında zaten mevcut → mükerrer, her iki listeden de dışla
                    mukerrerSayac++;
                    continue;
                }

                // ── Mükerrer değil: şimdi doğrulama yap ────────────────────────
                bool gecerli = ValidationService.KaydiDogrula(
                    satir.Telefon,
                    satir.IlacAdi,
                    satir.IlacBitisTarihiMetin,
                    satir.ReceteBitisTarihiMetin,
                    out string hataMesaji);

                if (!gecerli)
                {
                    // Geçersiz VE veritabanında yok → kullanıcı düzeltmeli
                    HataliKayitlar.Add(new HataliKayitSatiri
                    {
                        IsimSoyisim = satir.IsimSoyisim,
                        TcKimlik = satir.TcKimlik,
                        Telefon = satir.Telefon,
                        IlacAdi = satir.IlacAdi,
                        IlacBitisTarihiMetin = satir.IlacBitisTarihiMetin,
                        ReceteBitisTarihiMetin = satir.ReceteBitisTarihiMetin,
                        HataNedeni = hataMesaji
                    });
                    hataliSayac++;
                    continue;
                }

                // ── Geçerli + DB'de yok → Telefon+İlaç+Reçete ile mükerrer tekrar ──
                // (Geçerli kayıtlar için ek kontrol: aynı telefon+ilaç+reçete kombinasyonu)
                ValidationService.TarihGecerliMi(satir.ReceteBitisTarihiMetin, out var receteTarihi, out _);
                bool telefon3luMukerrer = db.IlacKayitlari.Any(k =>
                    k.Hasta.Telefon == satir.Telefon &&
                    k.IlacAdi == satir.IlacAdi &&
                    k.ReceteBitisTarihi == receteTarihi);

                if (telefon3luMukerrer)
                {
                    mukerrerSayac++;
                    continue;
                }

                // Geçerli, yeni ve eşsiz → kaydetme kuyruğuna al
                _gecerliKayitlar.Add(satir);
                gecerliSayac++;
            }

            // Buton durumlarını güncelle
            KaydetCommand.NotifyCanExecuteChanged();

            var ozet = $"Tarama tamamlandı: {gecerliSayac} geçerli, {hataliSayac} hatalı, {mukerrerSayac} mükerrer kayıt.";
            if (hataliSayac > 0)
                ozet += " Lütfen kırmızı kayıtları düzeltin.";
            DurumMesaji = ozet;
            Debug.WriteLine($"[Tara] {ozet}");
        }
        catch (Exception ex)
        {
            // TargetInvocationException dahil tüm sarmalayıcıların tam izini göster.
            var hataDetayi = ex.ToString();
            if (ex.InnerException != null)
                hataDetayi += $"\n\nInnerException:\n{ex.InnerException}";
            DurumMesaji = $"Tarama hatası:\n{hataDetayi}";
            Debug.WriteLine($"[Tara][HATA]\n{hataDetayi}");
        }
        finally
        {
            IslemYapiliyor = false;

            // Geçici PNG dosyasını temizle
            if (geciciDosya != null && File.Exists(geciciDosya))
            {
                try { File.Delete(geciciDosya); }
                catch { /* Temizleme başarısız olursa sessizce geç */ }
            }
        }
    }

    // ─── KAYDET KOMUTU ───────────────────────────────────────────────────────

    /// <summary>
    /// Kullanıcının düzelttiği hatalı kayıtları tekrar doğrular ve
    /// kusursuz olanları _gecerliKayitlar ile birlikte veritabanına kaydeder.
    /// </summary>
    [RelayCommand(CanExecute = nameof(KaydetAktifMi))]
    private async Task KaydetAsync()
    {
        IslemYapiliyor = true;
        DurumMesaji = "Kayıtlar veritabanına işleniyor...";
        var hataliKayitSnapshot = HataliKayitlar.ToList();

        try
        {
            await Task.Run(() =>
            {
                using var db = EczaneDbContextFactory.Create();

                int basariliKayitSayisi = 0;
                int halaHataliSayisi = 0;
                var duzeltilenKayitlar = new List<HataliKayitSatiri>();
                var guncellenenHatalar = new List<(HataliKayitSatiri Kayit, string Hata)>();

                // ── A: Kullanıcının düzelttiği hatalı kayıtları tekrar doğrula ──
                foreach (var satir in hataliKayitSnapshot)
                {
                    bool gecerli = ValidationService.KaydiDogrula(
                        satir.Telefon,
                        satir.IlacAdi,
                        satir.IlacBitisTarihiMetin,
                        satir.ReceteBitisTarihiMetin,
                        out string hataMesaji);

                    if (!gecerli)
                    {
                        // Hâlâ geçersiz → hata nedenini güncelle, atla
                        guncellenenHatalar.Add((satir, hataMesaji));
                        halaHataliSayisi++;
                        continue;
                    }

                    duzeltilenKayitlar.Add(satir);
                    // Düzeltilmiş kayıt geçerli → geçerli listeye ekle
                    ValidationService.TarihGecerliMi(satir.IlacBitisTarihiMetin, out var ilacBitis, out _);
                    ValidationService.TarihGecerliMi(satir.ReceteBitisTarihiMetin, out var receteBitis, out _);

                    _gecerliKayitlar.Add(new OcrSatirSonucu
                    {
                        IsimSoyisim = satir.IsimSoyisim,
                        TcKimlik = satir.TcKimlik,
                        Telefon = satir.Telefon,
                        IlacAdi = satir.IlacAdi,
                        IlacBitisTarihiMetin = satir.IlacBitisTarihiMetin,
                        ReceteBitisTarihiMetin = satir.ReceteBitisTarihiMetin
                    });
                }

                // ── B: Tüm geçerli kayıtları veritabanına yaz ─────────────────
                foreach (var kayit in _gecerliKayitlar)
                {
                    ValidationService.TarihGecerliMi(kayit.IlacBitisTarihiMetin, out var ilacBitis, out _);
                    ValidationService.TarihGecerliMi(kayit.ReceteBitisTarihiMetin, out var receteBitis, out _);

                    // Hasta bul veya oluştur (upsert mantığı: önce TC Kimlik varsa TC ile, yoksa telefon ile eşleştir)
                    var hasta = db.Hastalar.FirstOrDefault(h =>
                        (!string.IsNullOrEmpty(kayit.TcKimlik) && h.TcKimlik == kayit.TcKimlik) ||
                        h.Telefon == kayit.Telefon);

                    if (hasta == null)
                    {
                        hasta = new Hasta
                        {
                            Telefon = kayit.Telefon,
                            IsimSoyisim = kayit.IsimSoyisim,
                            TcKimlik = kayit.TcKimlik
                        };
                        db.Hastalar.Add(hasta);
                        db.SaveChanges(); // HastaId'yi almak için ara kayıt
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(kayit.IsimSoyisim))
                            hasta.IsimSoyisim = kayit.IsimSoyisim.Trim();
                        if (!string.IsNullOrWhiteSpace(kayit.TcKimlik))
                            hasta.TcKimlik = kayit.TcKimlik.Trim();
                    }

                    // Mükerrer kontrolü (DB'de zaten var mı?)
                    bool mukerrer = db.IlacKayitlari.Any(k =>
                        k.HastaId == hasta.Id &&
                        k.IlacAdi == kayit.IlacAdi &&
                        k.ReceteBitisTarihi == receteBitis);

                    if (mukerrer) continue;

                    var yeniKayit = new IlacKayit
                    {
                        HastaId = hasta.Id,
                        IlacAdi = kayit.IlacAdi,
                        IlacBitisTarihi = ilacBitis,
                        ReceteBitisTarihi = receteBitis,
                        KayitTarihi = DateTime.UtcNow
                    };

                    db.IlacKayitlari.Add(yeniKayit);
                    basariliKayitSayisi++;
                }

                db.SaveChanges();

                // ── C: UI'yı güncelle (ana thread'e gönder) ───────────────────
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _gecerliKayitlar.Clear();

                    foreach (var (kayit, hata) in guncellenenHatalar)
                        kayit.HataNedeni = $"⚠ {hata}";

                    // Yalnızca doğrulanıp DB işlemi tamamlanan satırları koleksiyondan çıkar.
                    foreach (var kayit in duzeltilenKayitlar)
                        HataliKayitlar.Remove(kayit);

                    KaydetCommand.NotifyCanExecuteChanged();

                    string mesaj = $"✓ {basariliKayitSayisi} adet yeni kayıt başarıyla veritabanına eklendi.";
                    if (halaHataliSayisi > 0)
                        mesaj += $" {halaHataliSayisi} kayıt hâlâ geçersiz, lütfen düzeltin.";

                    DurumMesaji = mesaj;
                    Debug.WriteLine($"[Kaydet] {mesaj}");
                });
            });
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Kayıt sırasında hata oluştu: {ex.Message}";
            Debug.WriteLine($"[Kaydet][HATA] {ex}");
        }
        finally
        {
            IslemYapiliyor = false;
        }
    }
}
