using System;
using System.Data.Common;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace EczaneOtomasyonu.Data;

public static class EczaneDbContextFactory
{
    public static string GetDatabasePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EczaneOtomasyonu");

        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "eczane.db");
    }

    public static DbContextOptions<EczaneDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<EczaneDbContext>()
            .UseSqlite($"Data Source={GetDatabasePath()}")
            .Options;
    }

    public static EczaneDbContext Create()
    {
        return new EczaneDbContext(CreateOptions());
    }

    /// <summary>
    /// EnsureCreated mevcut SQLite dosyalarında tablo değişikliklerini uygulamaz.
    /// Bu küçük uyumluluk adımı eski Hastalar tablolarına isim sütununu ekler.
    /// </summary>
    public static void EnsureNameColumn(EczaneDbContext db)
    {
        db.Database.OpenConnection();
        try
        {
            var connection = db.Database.GetDbConnection();
            using DbCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info('Hastalar');";

            bool columnExists = false;
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), "IsimSoyisim", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
            {
                command.CommandText =
                    "ALTER TABLE \"Hastalar\" ADD COLUMN \"IsimSoyisim\" TEXT NOT NULL DEFAULT '';";
                command.ExecuteNonQuery();
            }
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }

    /// <summary>
    /// Eski veritabanlarında TcKimlik sütunu yoksa dinamik olarak ekler.
    /// </summary>
    public static void EnsureTcKimlikColumn(EczaneDbContext db)
    {
        db.Database.OpenConnection();
        try
        {
            var connection = db.Database.GetDbConnection();
            using DbCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info('Hastalar');";

            bool columnExists = false;
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), "TcKimlik", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
            {
                command.CommandText =
                    "ALTER TABLE \"Hastalar\" ADD COLUMN \"TcKimlik\" TEXT NOT NULL DEFAULT '';";
                command.ExecuteNonQuery();
            }
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }
}
