using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using EczaneOtomasyonu.Data;
using EczaneOtomasyonu.Infrastructure;
using EczaneOtomasyonu.Models;
using EczaneOtomasyonu.ViewModels;
using EczaneOtomasyonu.Views;

namespace EczaneOtomasyonu;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // ── 1. Veritabanını Oluştur ──────────────────────────────────────────
        using (var db = EczaneDbContextFactory.Create())
        {
            db.Database.EnsureCreated();
            EczaneDbContextFactory.EnsureNameColumn(db);
            EczaneDbContextFactory.EnsureTcKimlikColumn(db);
            Debug.WriteLine($"[App] Veritabanı hazır: {EczaneDbContextFactory.GetDatabasePath()}");

            // ── 2. Varsayılan Ayar Tohumlarını Güvence Altına Al ────────────
            // HasData ile oluşturulan tohumlar EnsureCreated'da işlenmez (Migrations gerektirir).
            // Bu yüzden uygulama başlangıcında manuel kontrol yapılır.
            if (!db.Ayarlar.Any(a => a.Key == "GsmPort"))
            {
                db.Ayarlar.Add(new Ayar { Key = "GsmPort", Value = "" });
                Debug.WriteLine("[App] Varsayılan ayar eklendi: GsmPort");
            }

            // ── İlaç SMS Şablonu seed ────────────────────────────────────────
            if (!db.Ayarlar.Any(a => a.Key == "IlacSmsSablonu"))
            {
                db.Ayarlar.Add(new Ayar
                {
                    Key   = "IlacSmsSablonu",
                    Value = ViewModels.AyarlarViewModel.VarsayilanIlacSablonu
                });
                Debug.WriteLine("[App] Varsayılan ayar eklendi: IlacSmsSablonu");
            }

            // ── Reçete SMS Şablonu seed ──────────────────────────────────────
            if (!db.Ayarlar.Any(a => a.Key == "ReceteSmsSablonu"))
            {
                db.Ayarlar.Add(new Ayar
                {
                    Key   = "ReceteSmsSablonu",
                    Value = ViewModels.AyarlarViewModel.VarsayilanReceteSablonu
                });
                Debug.WriteLine("[App] Varsayılan ayar eklendi: ReceteSmsSablonu");
            }

            // ── Karanlık Mod Ayarı seed ──────────────────────────────────────
            if (!db.Ayarlar.Any(a => a.Key == "KaranlikModAktif"))
            {
                db.Ayarlar.Add(new Ayar
                {
                    Key   = "KaranlikModAktif",
                    Value = "false"
                });
                Debug.WriteLine("[App] Varsayılan ayar eklendi: KaranlikModAktif");
            }

            db.SaveChanges();

            // ── 3. Başlangıç Temasını Veritabanından Oku ve Ayarla ────────────
            var karanlikAyar = db.Ayarlar.FirstOrDefault(a => a.Key == "KaranlikModAktif");
            var karanlikModAktif = karanlikAyar?.Value == "true";
            RequestedThemeVariant = karanlikModAktif ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
            Debug.WriteLine($"[App] Başlangıç teması ayarlandı: {(karanlikModAktif ? "Karanlık" : "Açık")}");
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };

            // ── 3. Quartz Zamanlayıcısını Başlat ────────────────────────────
            // Port ve şablon artık burada değil; SmsKontrolJob her çalışmada DB'den okur.
            QuartzBaslatici.BaslatAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Debug.WriteLine($"[App][HATA] Quartz başlatılamadı: {t.Exception?.InnerException?.Message}");
            });

            // ── 4. Uygulama Kapanınca Quartz'ı Durdur ───────────────────────
            desktop.ShutdownRequested += async (_, _) =>
            {
                Debug.WriteLine("[App] Uygulama kapanıyor, Quartz durduruluyor...");
                await QuartzBaslatici.DurdurAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();;
    }
}
