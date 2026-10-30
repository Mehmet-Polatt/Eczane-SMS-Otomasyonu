using System.Collections.Generic;

namespace EczaneOtomasyonu.Models;

/// <summary>
/// SMS hatırlatması yapılacak hasta. Telefon numarası benzersiz kimliktir.
/// Mükerrer kontrol: Telefon + İlaç Adı + Reçete Bitiş Tarihi.
/// </summary>
public class Hasta
{
    public int Id { get; set; }

    /// <summary>Hastanın adı ve soyadı; SMS mesajlarında kullanılır.</summary>
    public string IsimSoyisim { get; set; } = string.Empty;

    /// <summary>11 haneli TC Kimlik numarası.</summary>
    public string TcKimlik { get; set; } = string.Empty;

    /// <summary>
    /// Sadece rakam; doğrulanmış kayıtlar 10 veya 11 hanelidir (5XX... veya 05XX...).
    /// </summary>
    public string Telefon { get; set; } = string.Empty;

    public ICollection<IlacKayit> IlacKayitlari { get; set; } = new List<IlacKayit>();
}
