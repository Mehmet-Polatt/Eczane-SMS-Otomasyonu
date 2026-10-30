using EczaneOtomasyonu.Models;
using Microsoft.EntityFrameworkCore;

namespace EczaneOtomasyonu.Data;

public class EczaneDbContext : DbContext
{
    public EczaneDbContext(DbContextOptions<EczaneDbContext> options)
        : base(options)
    {
    }

    public DbSet<Hasta> Hastalar => Set<Hasta>();

    public DbSet<IlacKayit> IlacKayitlari => Set<IlacKayit>();

    /// <summary>
    /// Uygulama ayarları (GsmPort, SmsSablonu vb.).
    /// Key sütunu birincil anahtar olduğu için her değer benzersizdir.
    /// </summary>
    public DbSet<Ayar> Ayarlar => Set<Ayar>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Hasta>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.IsimSoyisim)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.TcKimlik)
                .IsRequired(false)
                .HasMaxLength(11);

            entity.Property(x => x.Telefon)
                .IsRequired()
                .HasMaxLength(11);

            entity.HasIndex(x => x.Telefon)
                .IsUnique();

            entity.HasMany(x => x.IlacKayitlari)
                .WithOne(x => x.Hasta)
                .HasForeignKey(x => x.HastaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IlacKayit>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.IlacAdi)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.IlacBitisTarihi)
                .IsRequired();

            entity.Property(x => x.ReceteBitisTarihi)
                .IsRequired();

            entity.Property(x => x.KayitTarihi)
                .IsRequired();

            // Mimari kural: Telefon + İlaç Adı + Reçete Bitiş Tarihi tekrarı yoksayılır.
            entity.HasIndex(x => new { x.HastaId, x.IlacAdi, x.ReceteBitisTarihi })
                .IsUnique();
        });

        // ── Ayarlar tablosu ────────────────────────────────────────────────
        modelBuilder.Entity<Ayar>(entity =>
        {
            // Key string primary key — EF bunu otomatik tanımaz, açıkça belirtiyoruz.
            entity.HasKey(x => x.Key);

            entity.Property(x => x.Key)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Value)
                .IsRequired(false)     // Boş bırakılabilir (örn: GsmPort henüz girilmedi)
                .HasMaxLength(500);

            // Varsayılan tohumlar: EnsureCreated ile oluşturulan DB'ye
            // ilk açılışta eklenir. Kullanıcı sonra üzerine yazabilir.
            entity.HasData(
                new Ayar
                {
                    Key   = "GsmPort",
                    Value = ""       // Kullanıcı ayarlar ekranından doldurur
                },
                new Ayar
                {
                    Key   = "IlacSmsSablonu",
                    Value = "Sayın {IsimSoyisim}, {IlacAdi} ilacınızın bitmesine 3 gün kalmıştır."
                },
                new Ayar
                {
                    Key   = "ReceteSmsSablonu",
                    Value = "Sayın {IsimSoyisim}, {IlacAdi} ilacınızın reçete süresinin bitmesine 3 gün kalmıştır."
                },
                new Ayar
                {
                    Key   = "KaranlikModAktif",
                    Value = "false"  // Varsayılan: false (açık mod)
                }
            );
        });
    }
}
