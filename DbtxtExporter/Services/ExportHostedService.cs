using DbtxtExporter.Data;
using DbtxtExporter.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DbtxtExporter.Services;

public class ExportHostedService : BackgroundService
{
    private readonly PeriodicTimer _timer = new(TimeSpan.FromMinutes(1));
    private readonly IServiceProvider _sp;
    private readonly ExportSettings _settings;
    private readonly ILogger<ExportHostedService> _logger;

    public ExportHostedService(IServiceProvider sp, IOptions<ExportSettings> settings, ILogger<ExportHostedService> logger)
    {
        _sp = sp;
        _settings = settings.Value;
        _logger = logger;
        Directory.CreateDirectory(_settings.OutputPath);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Экспортер запущен → {Path}", _settings.OutputPath);

        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            await GenerateAllReports(stoppingToken);
        }
    }

    private async Task GenerateAllReports(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();

        var dbNkt = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbNot = scope.ServiceProvider.GetRequiredService<NotPakDbContext>();
        var dbTpa = scope.ServiceProvider.GetRequiredService<Tpa140DbContext>();

        var now = DateTime.Now;

        var productionDayStart = now.Hour >= 8
            ? now.Date.AddHours(8)
            : now.Date.AddDays(-1).AddHours(8);

        var dayShiftStart = productionDayStart;
        var dayShiftEnd = productionDayStart.AddHours(12);
        var nightShiftStart = productionDayStart.AddHours(12);
        var nightShiftEnd = productionDayStart.AddDays(1);

        var prodMonthStart = new DateTime(now.Year, now.Month, 1).AddHours(8);
        var prodMonthEnd = prodMonthStart.AddMonths(1);

        try
        {
            await GenerateReportNkt(dbNkt.Nkt12Reps, "nkt", ct, prodMonthStart, prodMonthEnd, dayShiftStart, dayShiftEnd, nightShiftStart, nightShiftEnd);
            await GenerateReportNot(dbNot.NotPakMp6Reps, "not", ct, prodMonthStart, prodMonthEnd, dayShiftStart, dayShiftEnd, nightShiftStart, nightShiftEnd);
            await GenerateReportTpa(dbTpa.Tpa140Nc9Reps, "tpa140", ct, prodMonthStart, prodMonthEnd, dayShiftStart, dayShiftEnd, nightShiftStart, nightShiftEnd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при генерации отчётов");
        }
    }

    private async Task GenerateReportNkt(IQueryable<Nkt12Rep> source, string prefix, CancellationToken ct,
        DateTime prodMonthStart, DateTime prodMonthEnd,
        DateTime dayShiftStart, DateTime dayShiftEnd, DateTime nightShiftStart, DateTime nightShiftEnd)
    {
        var records = await source
            .Where(r => r.DateTime >= prodMonthStart.AddDays(-1) && r.DateTime < prodMonthEnd.AddHours(1))
            .OrderBy(r => r.DateTime)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

        await GenerateReportCore(records, r => r.DateTime, prefix, ct,
            prodMonthStart, prodMonthEnd, dayShiftStart, dayShiftEnd, nightShiftStart, nightShiftEnd);
    }

    private async Task GenerateReportNot(IQueryable<NotPakMp6Rep> source, string prefix, CancellationToken ct,
        DateTime prodMonthStart, DateTime prodMonthEnd,
        DateTime dayShiftStart, DateTime dayShiftEnd, DateTime nightShiftStart, DateTime nightShiftEnd)
    {
        var records = await source
            .Where(r => r.DateTimeUpdate >= prodMonthStart.AddDays(-1) && r.DateTimeUpdate < prodMonthEnd.AddHours(1))
            .OrderBy(r => r.DateTimeUpdate)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

        await GenerateReportCore(records, r => r.DateTimeUpdate, prefix, ct,
            prodMonthStart, prodMonthEnd, dayShiftStart, dayShiftEnd, nightShiftStart, nightShiftEnd);
    }

    private async Task GenerateReportTpa(IQueryable<Tpa140Nc9Rep> source, string prefix, CancellationToken ct,
        DateTime prodMonthStart, DateTime prodMonthEnd,
        DateTime dayShiftStart, DateTime dayShiftEnd, DateTime nightShiftStart, DateTime nightShiftEnd)
    {
        var records = await source
            .Where(r => r.DateTime >= prodMonthStart.AddDays(-1) && r.DateTime < prodMonthEnd.AddHours(1))
            .OrderBy(r => r.DateTime)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

        await GenerateReportCore(records, r => r.DateTime, prefix, ct,
            prodMonthStart, prodMonthEnd, dayShiftStart, dayShiftEnd, nightShiftStart, nightShiftEnd);
    }

    private async Task GenerateReportCore<T>(
        List<T> records,
        Func<T, DateTime> timeSelector,
        string prefix,
        CancellationToken ct,
        DateTime prodMonthStart, DateTime prodMonthEnd,
        DateTime dayShiftStart, DateTime dayShiftEnd, DateTime nightShiftStart, DateTime nightShiftEnd) where T : class
    {
        if (!records.Any())
        {
            _logger.LogInformation($"[{prefix.ToUpper()}] Нет данных");
            return;
        }

        // Последняя запись
        var last = records.MaxBy(r => (timeSelector(r), GetId(r)));
        int planPerHour = GetIntProperty(last, "Plan") ?? 20;
        var shiftPlan12h = (int)Math.Round(planPerHour * 10.54m);

        // Простой
        decimal CalcDowntime(DateTime start, DateTime end, DateTime nextStart)
        {
            var shift = records.Where(r => timeSelector(r) >= start && timeSelector(r) < end).ToList();
            if (!shift.Any()) return 0;

            var first = shift.MinBy(r => (timeSelector(r), GetId(r)))!;
            var firstNext = records.FirstOrDefault(r => timeSelector(r) >= nextStart);

            decimal totalSec = shift.Sum(r => GetIntProperty(r, "DelayTimeThisHour") ?? 0);
            totalSec += shift.Where(r => !ReferenceEquals(r, first))
                             .Sum(r => GetIntProperty(r, "DelayTimePrevHour") ?? 0);

            int firstPrev = GetIntProperty(first, "DelayTimePrevHour") ?? 0;
            if (firstPrev > 0)
            {
                var lastBefore = records.Where(r => timeSelector(r) < start)
                                        .MaxBy(r => (timeSelector(r), GetId(r)));

                decimal secFromLast = lastBefore != null
                    ? (decimal)(start - timeSelector(lastBefore)).TotalSeconds
                    : 3600m;

                var expected = secFromLast - (3600m / planPerHour);
                var corrected = firstPrev;
                if (expected > 0) corrected -= (int)Math.Floor(expected);
                if (corrected > 0) totalSec += corrected;
            }

            if (firstNext != null)
            {
                int nextPrev = GetIntProperty(firstNext, "DelayTimePrevHour") ?? 0;
                if (nextPrev > 0) totalSec += nextPrev;
            }

            return Math.Round(totalSec / 60m);
        }

        var dayDowntime = CalcDowntime(dayShiftStart, dayShiftEnd, nightShiftStart);
        var nightDowntime = CalcDowntime(nightShiftStart, nightShiftEnd, nightShiftEnd);

        var dayFact = records.Count(r => timeSelector(r) >= dayShiftStart && timeSelector(r) < dayShiftEnd);
        var nightFact = records.Count(r => timeSelector(r) >= nightShiftStart && timeSelector(r) < nightShiftEnd);

        // Кумулятивный план А,Б,В,Г
        var planBySmena = new Dictionary<int, decimal> { { 1, 0m }, { 2, 0m }, { 3, 0m }, { 4, 0m } };

        var monthly = records
            .Where(r =>
            {
                var smena = GetIntProperty(r, "Smena");
                return timeSelector(r) >= prodMonthStart && timeSelector(r) < prodMonthEnd &&
                       smena.HasValue && smena.Value >= 1 && smena.Value <= 4;
            })
            .ToList();

        var groups = monthly
            .GroupBy(r => new
            {
                ProductionDay = timeSelector(r).AddHours(-8).Date,
                Smena = GetIntProperty(r, "Smena")!.Value
            });

        foreach (var g in groups)
        {
            var sorted = g.OrderBy(timeSelector).ThenBy(r => GetId(r)).ToList();
            var firstTime = timeSelector(sorted.First());
            var shiftEnd = firstTime.Hour >= 8 && firstTime.Hour < 20
                ? firstTime.Date.AddHours(20)
                : firstTime.Date.AddDays(1).AddHours(8);

            decimal running = 0m;
            for (int i = 0; i < sorted.Count; i++)
            {
                var curr = sorted[i];
                var next = i < sorted.Count - 1 ? timeSelector(sorted[i + 1]) : shiftEnd;
                var duration = (decimal)(next - timeSelector(curr)).TotalHours;
                var prev = running;
                running += duration;

                decimal eff = prev >= 10.54m ? 0m : running <= 10.54m ? duration : 10.54m - prev;
                int planVal = GetIntProperty(curr, "Plan") ?? 20;
                planBySmena[g.Key.Smena] += planVal * eff;
            }
        }

        int planA = (int)Math.Round(planBySmena[1]);
        int planB = (int)Math.Round(planBySmena[2]);
        int planV = (int)Math.Round(planBySmena[3]);
        int planG = (int)Math.Round(planBySmena[4]);

        var factDict = monthly
            .Where(r => GetIntProperty(r, "Smena").HasValue)
            .GroupBy(r => GetIntProperty(r, "Smena")!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        int factA = factDict.GetValueOrDefault(1, 0);
        int factB = factDict.GetValueOrDefault(2, 0);
        int factV = factDict.GetValueOrDefault(3, 0);
        int factG = factDict.GetValueOrDefault(4, 0);

        var line = $"{shiftPlan12h};{dayFact};{dayDowntime};;{shiftPlan12h};{nightFact};{nightDowntime};;{planA};{factA};{planB};{factB};{planV};{factV};{planG};{factG}";

        var path = Path.Combine(_settings.OutputPath, $"report_{prefix}.txt");
        await File.WriteAllTextAsync(path, line + Environment.NewLine, ct);

        _logger.LogInformation($"[OK] {prefix.ToUpper()} → {line}");
    }

    private static int GetId<T>(T obj) where T : class
    {
        var prop = obj.GetType().GetProperty("Id");
        return prop != null ? (int)(prop.GetValue(obj) ?? 0) : 0;
    }

    private static int? GetIntProperty<T>(T obj, string propertyName) where T : class
    {
        var prop = obj.GetType().GetProperty(propertyName);
        return prop != null ? (int?)(prop.GetValue(obj)!) : null;
    }
}