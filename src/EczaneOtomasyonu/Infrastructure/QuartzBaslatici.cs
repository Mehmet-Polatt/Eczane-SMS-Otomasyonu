using System;
using System.Diagnostics;
using System.Threading.Tasks;
using EczaneOtomasyonu.Data;
using EczaneOtomasyonu.Jobs;
using Quartz;
using Quartz.Impl;

namespace EczaneOtomasyonu.Infrastructure;

/// <summary>
/// Quartz.NET zamanlayıcısını kurar ve başlatır.
///
/// ── Aşama 6 Değişikliği ────────────────────────────────────────────────────────
/// QuartzBaslatici artık başlangıçta port almaz.
/// SmsKontrolJob her çalışmasında kendi DB okumasını yapar (dinamik port/şablon).
/// Bu sayede kullanıcı ayarlar ekranından port değiştirdiğinde scheduler yeniden
/// başlatılmadan bir sonraki job çalışmasında yeni port geçerli olur.
/// ─────────────────────────────────────────────────────────────────────────────
/// </summary>
public static class QuartzBaslatici
{
    private static IScheduler? _scheduler;

    /// <summary>
    /// Quartz scheduler'ı oluşturur, konfigüre eder ve başlatır.
    /// App.axaml.cs OnFrameworkInitializationCompleted içinden çağrılır.
    /// Port/şablon artık burada alınmaz; SmsKontrolJob her çalışmada DB'den okur.
    /// </summary>
    public static async Task BaslatAsync()
    {
        var factory  = new StdSchedulerFactory();
        _scheduler   = await factory.GetScheduler();

        // Job tanımı — DataMap artık SMS servisini taşımaz (DB'den okunacak)
        IJobDetail job = JobBuilder.Create<SmsKontrolJob>()
            .WithIdentity("smsKontrolJob", "eczaneGrup")
            .WithDescription("Bitiş tarihi 3 gün kalan hastalara SMS gönderir.")
            .Build();

        // ═══════════════════════════════════════════════════════════════════
        // TEST MODU: Her 1 dakikada bir çalışır (geliştirme ve doğrulama için)
        // ═══════════════════════════════════════════════════════════════════
        ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity("smsKontrolTetikleyici", "eczaneGrup")
            .WithDescription("Test: Her 1 dakikada bir | Üretim: Günlük 09:00")
            .StartNow()
            .WithSimpleSchedule(x => x
                .WithIntervalInMinutes(1)
                .RepeatForever())
            .Build();

        // ═══════════════════════════════════════════════════════════════════
        // ÜRETİM MODU: Her gün sabah 09:00'da çalışır.
        // Aktif etmek için yukarıdaki trigger bloğunu silin ve aşağıdakini açın:
        //
        // ITrigger trigger = TriggerBuilder.Create()
        //     .WithIdentity("smsKontrolTetikleyici", "eczaneGrup")
        //     .WithDescription("Her gün sabah 09:00 — Üretim zamanlayıcısı")
        //     .WithCronSchedule(
        //         "0 0 9 * * ?",
        //         x => x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time")))
        //     .Build();
        //
        // Cron: "0 0 9 * * ?" → Her gün 09:00 (Türkiye saatiyle)
        // ═══════════════════════════════════════════════════════════════════

        await _scheduler.ScheduleJob(job, trigger);
        await _scheduler.Start();

        Debug.WriteLine($"[Quartz] ✓ Scheduler başlatıldı. " +
                        $"Sonraki çalışma: {trigger.GetNextFireTimeUtc()?.LocalDateTime:dd.MM.yyyy HH:mm:ss}");

        // Başlangıç DB kontrolü — Ayarlar tablosu mevcut mu?
        try
        {
            using var db = EczaneDbContextFactory.Create();
            var port = db.Ayarlar.Find("GsmPort")?.Value ?? "(boş)";
            Debug.WriteLine($"[Quartz] Başlangıç GsmPort: '{port}'");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Quartz][UYARI] Ayarlar okunamadı: {ex.Message}");
        }
    }

    /// <summary>
    /// Uygulama kapanırken Quartz scheduler'ı güvenli şekilde durdurur.
    /// </summary>
    public static async Task DurdurAsync()
    {
        if (_scheduler != null && _scheduler.IsStarted)
        {
            await _scheduler.Shutdown(waitForJobsToComplete: true);
            Debug.WriteLine("[Quartz] Scheduler durduruldu.");
        }
    }
}
