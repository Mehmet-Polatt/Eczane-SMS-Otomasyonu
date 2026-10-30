using System;

namespace EczaneOtomasyonu.Models;

/// <summary>
/// Hastaya ait ilaç/reçete kaydı. Quartz görevi bitiş tarihine tam 3 gün kala SMS gönderir.
/// </summary>
public class IlacKayit
{
    public int Id { get; set; }

    public int HastaId { get; set; }

    public Hasta Hasta { get; set; } = null!;

    public string IlacAdi { get; set; } = string.Empty;

    public DateOnly IlacBitisTarihi { get; set; }

    public DateOnly ReceteBitisTarihi { get; set; }

    public DateTime KayitTarihi { get; set; } = DateTime.UtcNow;

    public bool IlacBitisSmsGonderildi { get; set; }

    public bool ReceteBitisSmsGonderildi { get; set; }
}
