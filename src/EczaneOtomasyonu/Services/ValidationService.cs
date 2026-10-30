using System;
using System.Text.RegularExpressions;
using EczaneOtomasyonu.Models;

namespace EczaneOtomasyonu.Services;

/// <summary>
/// Mimari belgede tanımlanan doğrulama kurallarını uygular.
/// Tüm kurallar buraya toplanmıştır; ViewModel bunu kullanır.
/// </summary>
public static class ValidationService
{
    // Telefon: sadece rakam, 10 veya 11 hane
    private static readonly Regex TelefonRegex =
        new(@"^\d{10,11}$", RegexOptions.Compiled);

    // Tarih: GG.AA.YYYY veya GG/AA/YYYY veya GG-AA-YYYY
    private static readonly Regex TarihRegex =
        new(@"^(\d{2})[.\-/](\d{2})[.\-/](\d{4})$", RegexOptions.Compiled);

    /// <summary>Telefon numarasını doğrular. Geçerliyse true döner.</summary>
    public static bool TelefonGecerliMi(string telefon, out string hata)
    {
        hata = string.Empty;
        if (string.IsNullOrWhiteSpace(telefon))
        {
            hata = "Telefon boş olamaz.";
            return false;
        }

        if (!TelefonRegex.IsMatch(telefon))
        {
            hata = "Telefon sadece rakam içermeli ve 10-11 hane olmalı.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Tarih metnini doğrular ve başarılı ise DateOnly nesnesini döner.
    /// </summary>
    public static bool TarihGecerliMi(string tarihMetin, out DateOnly tarih, out string hata)
    {
        tarih = default;
        hata = string.Empty;

        if (string.IsNullOrWhiteSpace(tarihMetin))
        {
            hata = "Tarih boş olamaz.";
            return false;
        }

        var m = TarihRegex.Match(tarihMetin);
        if (!m.Success)
        {
            hata = $"'{tarihMetin}' geçerli bir tarih formatı değil (GG.AA.YYYY bekleniyor).";
            return false;
        }

        int gun = int.Parse(m.Groups[1].Value);
        int ay = int.Parse(m.Groups[2].Value);
        int yil = int.Parse(m.Groups[3].Value);

        try
        {
            tarih = new DateOnly(yil, ay, gun);
            return true;
        }
        catch
        {
            hata = $"'{tarihMetin}' geçersiz bir takvim tarihi (örn: 32.13.2024 kabul edilmez).";
            return false;
        }
    }

    /// <summary>İlaç adını doğrular. Geçerliyse true döner.</summary>
    public static bool IlacAdiGecerliMi(string ilacAdi, out string hata)
    {
        hata = string.Empty;
        if (string.IsNullOrWhiteSpace(ilacAdi) || ilacAdi.Trim().Length < 3)
        {
            hata = "İlaç adı en az 3 karakter olmalı.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Ham OCR verisini (metin) doğrulayıp geçerliyse IlacKayit nesnesi döner.
    /// </summary>
    public static bool KaydiDogrula(
        string telefon,
        string ilacAdi,
        string ilacBitisTarihiMetin,
        string receteBitisTarihiMetin,
        out string hataMesaji)
    {
        hataMesaji = string.Empty;

        if (!TelefonGecerliMi(telefon, out var telHata))
        {
            hataMesaji = telHata;
            return false;
        }

        if (!IlacAdiGecerliMi(ilacAdi, out var ilacHata))
        {
            hataMesaji = ilacHata;
            return false;
        }

        if (!TarihGecerliMi(ilacBitisTarihiMetin, out _, out var ilacTarihHata))
        {
            hataMesaji = ilacTarihHata;
            return false;
        }

        if (!TarihGecerliMi(receteBitisTarihiMetin, out _, out var receteTarihHata))
        {
            hataMesaji = receteTarihHata;
            return false;
        }

        return true;
    }
}
