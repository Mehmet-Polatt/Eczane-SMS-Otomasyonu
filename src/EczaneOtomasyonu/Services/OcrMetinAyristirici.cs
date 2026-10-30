using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EczaneOtomasyonu.Services;

/// <summary>
/// Koordinatlara göre sıralanmış OCR satırlarını ayraçlı sütunlara dönüştürür.
/// Beklenen sıra: kimlik/diğer alanlar, telefon, ilaç adı, tarih alanları.
/// </summary>
public static class OcrMetinAyristirici
{
    private static readonly Regex TarihBul =
        new(@"\d{2}[./-]\d{2}[./-]\d{4}", RegexOptions.Compiled);
    private static readonly Regex TelefonBul =
        new(@"(?<!\d)(?:0?5\d{9})(?!\d)", RegexOptions.Compiled);
    private static readonly Regex TcBul =
        new(@"(?<!\d)\d{11}(?!\d)", RegexOptions.Compiled);

    public static List<OcrSatirSonucu> Ayristir(IEnumerable<string> duzenlenmisSatirlar)
    {
        var sonuclar = new List<OcrSatirSonucu>();
        if (duzenlenmisSatirlar == null)
            return sonuclar;

        foreach (var satir in duzenlenmisSatirlar)
        {
            if (string.IsNullOrWhiteSpace(satir))
                continue;

            var hucreler = satir.Split('|').Select(x => x.Trim()).ToArray();
            var telefonEslesmesi = hucreler
                .Select((hucre, indis) => new { Hucre = hucre, Indis = indis })
                .Select(x => new { x.Hucre, x.Indis, Match = TelefonBul.Match(TelefonAdaylariniDuzelt(x.Hucre)) })
                .FirstOrDefault(x => x.Match.Success);
            var tarihler = hucreler
                .SelectMany(hucre => TarihBul.Matches(hucre).Select(m => m.Value))
                .ToList();
            bool tcVar = hucreler.Any(hucre => TcBul.IsMatch(hucre));
            var tcEslesmesi = hucreler
                .Select(hucre => TcBul.Match(hucre))
                .FirstOrDefault(m => m.Success && (telefonEslesmesi == null || m.Value != telefonEslesmesi.Match.Value))
                ?? TcBul.Matches(satir).Cast<Match>().FirstOrDefault(m => telefonEslesmesi == null || m.Value != telefonEslesmesi.Match.Value);
            string tcKimlik = tcEslesmesi?.Success == true ? tcEslesmesi.Value : string.Empty;

            int isimBitisIndisi = tcVar
                ? Array.FindIndex(hucreler, TcBul.IsMatch)
                : telefonEslesmesi?.Indis ?? 0;
            string isimSoyisim = isimBitisIndisi > 0
                ? string.Join(" ", hucreler
                    .Take(isimBitisIndisi)
                    .Where(hucre =>
                        !string.IsNullOrWhiteSpace(hucre) &&
                        !TcBul.IsMatch(hucre) &&
                        !TelefonBul.IsMatch(TelefonAdaylariniDuzelt(hucre)) &&
                        !TarihBul.IsMatch(hucre) &&
                        !Regex.IsMatch(hucre, @"^\d+$")))
                    .Trim()
                : string.Empty;

            // Başlıkları ve metin satırlarını kayda çevirmeyin; kimlik, telefon
            // veya tarih bulunan her görsel satır ise eksik alanlarla da tutulur.
            if (telefonEslesmesi == null && !tcVar && string.IsNullOrEmpty(tcKimlik) && tarihler.Count == 0)
                continue;

            string ilacAdi = string.Empty;
            if (telefonEslesmesi != null)
            {
                int tarihHucreIndisi = Array.FindIndex(hucreler, hucre => TarihBul.IsMatch(hucre));
                int ilacBaslangici = telefonEslesmesi.Indis + 1;
                int ilacBitisi = tarihHucreIndisi >= 0 ? tarihHucreIndisi : hucreler.Length;
                if (ilacBitisi > ilacBaslangici)
                {
                    ilacAdi = string.Join(" ", hucreler
                        .Skip(ilacBaslangici)
                        .Take(ilacBitisi - ilacBaslangici)
                        .Where(hucre => !TcBul.IsMatch(hucre) && !TarihBul.IsMatch(hucre)))
                        .Trim();
                }
            }

            sonuclar.Add(new OcrSatirSonucu
            {
                IsimSoyisim = isimSoyisim,
                TcKimlik = tcKimlik,
                Telefon = telefonEslesmesi?.Match.Value ?? string.Empty,
                IlacAdi = ilacAdi,
                IlacBitisTarihiMetin = tarihler.Count >= 2 ? tarihler[tarihler.Count - 2] : string.Empty,
                ReceteBitisTarihiMetin = tarihler.Count >= 1 ? tarihler[tarihler.Count - 1] : string.Empty
            });
        }

        return sonuclar;
    }

    private static string TelefonAdaylariniDuzelt(string satir) =>
        satir.Replace('O', '0')
             .Replace('l', '1')
             .Replace('I', '1');
}
