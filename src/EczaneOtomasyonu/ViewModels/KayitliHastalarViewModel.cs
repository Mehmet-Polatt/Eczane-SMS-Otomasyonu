using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EczaneOtomasyonu.Data;
using EczaneOtomasyonu.Jobs;
using Microsoft.EntityFrameworkCore;

namespace EczaneOtomasyonu.ViewModels;

public partial class KayitliHastalarViewModel : ViewModelBase
{
    private List<KayitliHastaSatiri> _tumKayitlar = new();

    public KayitliHastalarViewModel()
    {
        // SMS job'ı arka planda başarıyla SMS gönderip DB'yi güncelleyince
        // bu ekranı otomatik yenile — böylece SMS Durumu rozeti anında güncellenir.
        SmsKontrolJob.SmsGonderimTamamlandi += OnSmsGonderimTamamlandi;
    }

    /// <summary>
    /// SmsKontrolJob'ın SmsGonderimTamamlandi eventini UI thread'de karşılar
    /// ve kayıtlar listesini otomatik yeniler.
    /// </summary>
    private void OnSmsGonderimTamamlandi(object? sender, EventArgs e)
    {
        Debug.WriteLine("[KayitliHastalar] SMS job tamamlandı → tablo otomatik yenileniyor.");
        // Avalonia UI thread'ine geç ve listeyi yenile
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await KayitlariYukleAsync();
            BilgiMesaji = "SMS gönderimi tamamlandı. SMS Durumu güncellendi.";
        });
    }

    [ObservableProperty]
    private ObservableCollection<KayitliHastaSatiri> _filtrelenmisKayitlar = new();

    [ObservableProperty]
    private string _aramaMetni = string.Empty;

    [ObservableProperty]
    private bool _yukleniyor = false;

    [ObservableProperty]
    private string _bilgiMesaji = string.Empty;

    [ObservableProperty]
    private int _toplamKayitSayisi = 0;

    partial void OnAramaMetniChanged(string value)
    {
        Filtrele();
    }

    [RelayCommand]
    public async Task KayitlariYukleAsync()
    {
        Yukleniyor = true;
        BilgiMesaji = "Kayıtlar yükleniyor...";

        try
        {
            await Task.Run(() =>
            {
                using var db = EczaneDbContextFactory.Create();

                var liste = db.IlacKayitlari
                    .Include(i => i.Hasta)
                    .OrderByDescending(i => i.KayitTarihi)
                    .Select(i => new KayitliHastaSatiri
                    {
                        IlacKayitId = i.Id,
                        HastaId = i.HastaId,
                        IsimSoyisim = i.Hasta != null ? i.Hasta.IsimSoyisim : string.Empty,
                        TcKimlik = i.Hasta != null ? (i.Hasta.TcKimlik ?? string.Empty) : string.Empty,
                        Telefon = i.Hasta != null ? i.Hasta.Telefon : string.Empty,
                        IlacAdi = i.IlacAdi,
                        IlacBitisTarihi = i.IlacBitisTarihi.ToString("dd.MM.yyyy"),
                        ReceteBitisTarihi = i.ReceteBitisTarihi.ToString("dd.MM.yyyy"),
                        KayitTarihi = i.KayitTarihi.ToLocalTime().ToString("dd.MM.yyyy"),
                        IlacSmsGonderildi = i.IlacBitisSmsGonderildi,
                        ReceteSmsGonderildi = i.ReceteBitisSmsGonderildi
                    })
                    .ToList();

                _tumKayitlar = liste;
            });

            Filtrele();
            BilgiMesaji = $"{ToplamKayitSayisi} kayıt listelendi.";
        }
        catch (Exception ex)
        {
            BilgiMesaji = $"Kayıtlar yüklenirken hata oluştu: {ex.Message}";
            Debug.WriteLine($"[KayitliHastalar][HATA] {ex}");
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    [RelayCommand]
    private void Filtrele()
    {
        var arama = AramaMetni.Trim().ToLowerInvariant();

        var sonuc = string.IsNullOrWhiteSpace(arama)
            ? _tumKayitlar
            : _tumKayitlar.Where(k =>
                (k.IsimSoyisim?.ToLowerInvariant().Contains(arama) ?? false) ||
                (k.TcKimlik?.ToLowerInvariant().Contains(arama) ?? false) ||
                (k.Telefon?.ToLowerInvariant().Contains(arama) ?? false) ||
                (k.IlacAdi?.ToLowerInvariant().Contains(arama) ?? false)
            ).ToList();

        FiltrelenmisKayitlar = new ObservableCollection<KayitliHastaSatiri>(sonuc);
        ToplamKayitSayisi = _tumKayitlar.Count;
    }

    [RelayCommand]
    public async Task KayitSilAsync(KayitliHastaSatiri? satir)
    {
        if (satir == null) return;

        try
        {
            await Task.Run(() =>
            {
                using var db = EczaneDbContextFactory.Create();

                var ilac = db.IlacKayitlari.FirstOrDefault(x => x.Id == satir.IlacKayitId);
                if (ilac != null)
                {
                    db.IlacKayitlari.Remove(ilac);
                    db.SaveChanges();

                    // Hastanın başka ilacı kalmadıysa hastayı da temizleyelim
                    var baskaIlacVarMi = db.IlacKayitlari.Any(x => x.HastaId == satir.HastaId);
                    if (!baskaIlacVarMi)
                    {
                        var hasta = db.Hastalar.FirstOrDefault(x => x.Id == satir.HastaId);
                        if (hasta != null)
                        {
                            db.Hastalar.Remove(hasta);
                            db.SaveChanges();
                        }
                    }
                }
            });

            _tumKayitlar.Remove(satir);
            FiltrelenmisKayitlar.Remove(satir);
            ToplamKayitSayisi = _tumKayitlar.Count;
            BilgiMesaji = "Kayıt veritabanından silindi.";
        }
        catch (Exception ex)
        {
            BilgiMesaji = $"Silme işleminde hata: {ex.Message}";
        }
    }

    /// <summary>
    /// Kullanıcının tablodaki hücrelerde yaptığı düzenlemeleri toplu olarak veritabanına kaydeder.
    /// </summary>
    [RelayCommand]
    public async Task DegisiklikleriKaydetAsync()
    {
        // ── Ön Kontrol: Değişiklik var mı? ─────────────────────────────────
        // Mevcut UI listesini veritabanındaki orijinal snapshot ile karşılaştır.
        // Bu kontrol, kullanıcı hiçbir hücreyi değiştirmeden butona basmasını engeller
        // ve gereksiz db.SaveChangesAsync() çağrısını önler.
        bool degisiklikVar = await Task.Run(() =>
        {
            using var db = EczaneDbContextFactory.Create();
            var ilacIdler = _tumKayitlar.Select(s => s.IlacKayitId).ToList();
            var dbKayitlar = db.IlacKayitlari
                .Include(i => i.Hasta)
                .Where(i => ilacIdler.Contains(i.Id))
                .ToList();

            foreach (var satir in _tumKayitlar)
            {
                var ilac = dbKayitlar.FirstOrDefault(i => i.Id == satir.IlacKayitId);
                if (ilac == null) return true; // Tutarsızlık → güvenli tarafta kal

                // İlaç alanları kontrolü
                if (!string.IsNullOrWhiteSpace(satir.IlacAdi) && ilac.IlacAdi != satir.IlacAdi.Trim())
                    return true;

                if (Services.ValidationService.TarihGecerliMi(satir.IlacBitisTarihi, out var ilacBitis, out _)
                    && ilac.IlacBitisTarihi != ilacBitis)
                    return true;

                if (Services.ValidationService.TarihGecerliMi(satir.ReceteBitisTarihi, out var receteBitis, out _)
                    && ilac.ReceteBitisTarihi != receteBitis)
                    return true;

                // Hasta alanları kontrolü
                if (ilac.Hasta != null)
                {
                    if (!string.IsNullOrWhiteSpace(satir.IsimSoyisim) && ilac.Hasta.IsimSoyisim != satir.IsimSoyisim.Trim())
                        return true;

                    if (ilac.Hasta.TcKimlik != (satir.TcKimlik ?? string.Empty).Trim())
                        return true;

                    var temizTel = (satir.Telefon ?? string.Empty).Replace(" ", "").Trim();
                    if (!string.IsNullOrWhiteSpace(temizTel) && ilac.Hasta.Telefon != temizTel)
                        return true;
                }
            }
            return false;
        });

        if (!degisiklikVar)
        {
            BilgiMesaji = "Herhangi bir değişiklik yapılmadı.";
            return;
        }

        Yukleniyor = true;
        BilgiMesaji = "Değişiklikler veritabanına kaydediliyor...";

        try
        {
            var satirlar = _tumKayitlar.ToList();

            // ── 1. Doğrulama (Validation) Kontrolü ───────────────────────────
            foreach (var satir in satirlar)
            {
                string hastaAdi = string.IsNullOrWhiteSpace(satir.IsimSoyisim)
                    ? "İsimsiz Hasta"
                    : satir.IsimSoyisim.Trim();

                // TC Kimlik Kontrolü: Doluysa kesinlikle 11 haneli ve sadece rakam olmalı
                var tc = (satir.TcKimlik ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(tc))
                {
                    if (tc.Length != 11 || !tc.All(char.IsDigit))
                    {
                        var hataMesaji = $"İşlem İptal Edildi: {hastaAdi} adlı hastanın TC Kimlik numarası 11 haneli ve sadece rakamlardan oluşmalıdır!";
                        BilgiMesaji = hataMesaji;
                        Yukleniyor = false;

                        // Kullanıcıya anında belirgin modal uyarı penceresi göster
                        await Services.DialogService.UyariGosterAsync("Geçersiz TC Kimlik", hataMesaji);

                        // Tabloyu veritabanındaki orijinal haline geri al (revert)
                        await KayitlariYukleAsync();
                        return;
                    }
                }

                // Telefon Kontrolü: Doluysa boşluklar silindikten sonra 10 veya 11 haneli ve sadece rakam olmalı
                var tel = (satir.Telefon ?? string.Empty).Replace(" ", "").Trim();
                if (!string.IsNullOrEmpty(tel))
                {
                    if ((tel.Length != 10 && tel.Length != 11) || !tel.All(char.IsDigit))
                    {
                        var hataMesaji = $"İşlem İptal Edildi: {hastaAdi} adlı hastanın telefon numarası geçersiz! (10 veya 11 haneli rakam olmalıdır)";
                        BilgiMesaji = hataMesaji;
                        Yukleniyor = false;

                        // Kullanıcıya anında belirgin modal uyarı penceresi göster
                        await Services.DialogService.UyariGosterAsync("Geçersiz Telefon Numarası", hataMesaji);

                        // Tabloyu veritabanındaki orijinal haline geri al (revert)
                        await KayitlariYukleAsync();
                        return;
                    }
                }
            }

            // ── 2. Veritabanı Güncelleme ─────────────────────────────────────
            await Task.Run(async () =>
            {
                using var db = EczaneDbContextFactory.Create();

                var ilacIdler = satirlar.Select(s => s.IlacKayitId).ToList();
                var dbIlaclar = await db.IlacKayitlari
                    .Include(i => i.Hasta)
                    .Where(i => ilacIdler.Contains(i.Id))
                    .ToListAsync();

                foreach (var satir in satirlar)
                {
                    var ilac = dbIlaclar.FirstOrDefault(i => i.Id == satir.IlacKayitId);
                    if (ilac == null) continue;

                    // İlaç Adı güncelleme
                    if (!string.IsNullOrWhiteSpace(satir.IlacAdi))
                    {
                        ilac.IlacAdi = satir.IlacAdi.Trim();
                    }

                    // Tarih alanlarını güvenli güncelleme
                    // ── SMS Sıfırlama Mantığı ──────────────────────────────────────────
                    // Eğer kullanıcı bitiş tarihini değiştirdiyse, o tarih için daha önce
                    // gönderilmiş olan SMS durumunu "Bekliyor" (false) olarak sıfırla.
                    // Bu sayede yeni tarih için SMS job yeniden SMS gönderebilir.
                    if (Services.ValidationService.TarihGecerliMi(satir.IlacBitisTarihi, out var ilacBitis, out _))
                    {
                        if (ilac.IlacBitisTarihi != ilacBitis)
                        {
                            Debug.WriteLine(
                                $"[KayitliHastalar][SMS_SIFIRLA] Kayıt #{ilac.Id} ({ilac.IlacAdi}): " +
                                $"İlaç bitiş tarihi {ilac.IlacBitisTarihi:dd.MM.yyyy} → {ilacBitis:dd.MM.yyyy} değişti. " +
                                $"IlacBitisSmsGonderildi=false yapılıyor.");
                            ilac.IlacBitisTarihi = ilacBitis;
                            ilac.IlacBitisSmsGonderildi = false; // SMS durumunu sıfırla
                        }
                    }

                    if (Services.ValidationService.TarihGecerliMi(satir.ReceteBitisTarihi, out var receteBitis, out _))
                    {
                        if (ilac.ReceteBitisTarihi != receteBitis)
                        {
                            Debug.WriteLine(
                                $"[KayitliHastalar][SMS_SIFIRLA] Kayıt #{ilac.Id} ({ilac.IlacAdi}): " +
                                $"Reçete bitiş tarihi {ilac.ReceteBitisTarihi:dd.MM.yyyy} → {receteBitis:dd.MM.yyyy} değişti. " +
                                $"ReceteBitisSmsGonderildi=false yapılıyor.");
                            ilac.ReceteBitisTarihi = receteBitis;
                            ilac.ReceteBitisSmsGonderildi = false; // SMS durumunu sıfırla
                        }
                    }

                    // Hasta bilgilerini güncelleme
                    if (ilac.Hasta != null)
                    {
                        if (!string.IsNullOrWhiteSpace(satir.IsimSoyisim))
                            ilac.Hasta.IsimSoyisim = satir.IsimSoyisim.Trim();

                        ilac.Hasta.TcKimlik = (satir.TcKimlik ?? string.Empty).Trim();

                        var temizTel = (satir.Telefon ?? string.Empty).Replace(" ", "").Trim();
                        if (!string.IsNullOrWhiteSpace(temizTel))
                            ilac.Hasta.Telefon = temizTel;
                    }
                }

                await db.SaveChangesAsync();
            });

            // Listeyi yeniden yükleyerek verilerin başarıyla kalıcı hale geldiğini doğrula
            await KayitlariYukleAsync();
            BilgiMesaji = "Tüm değişiklikler başarıyla veritabanına kaydedildi.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[KayitliHastalar][KAYDET_HATA] {ex}");
            BilgiMesaji = $"Kayıt sırasında hata oluştu: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }
}
