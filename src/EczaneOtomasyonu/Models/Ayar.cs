namespace EczaneOtomasyonu.Models;

/// <summary>
/// Anahtar-değer çifti olarak uygulama ayarlarını saklar.
/// Tablo adı: Ayarlar. EnsureCreated ile otomatik oluşturulur.
/// Varsayılan tohumlar App.axaml.cs içinde eklenir.
/// </summary>
public class Ayar
{
    public const string KaranlikModKey = "KaranlikModAktif";

    /// <summary>
    /// Ayar anahtarı — birincil anahtar.
    /// Örn: "GsmPort", "IlacSmsSablonu", "ReceteSmsSablonu", "KaranlikModAktif"
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Ayar değeri.
    /// Örn: "COM3", "/dev/tty.usbserial-XXXX", şablon metni, "true"/"false".
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Karanlık modun aktif olup olmadığını belirtir (varsayılan: false / açık mod).
    /// Key-Value tablosundaki "KaranlikModAktif" ayarını modeller.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool KaranlikModAktif
    {
        get => Value == "true";
        set => Value = value ? "true" : "false";
    }
}

