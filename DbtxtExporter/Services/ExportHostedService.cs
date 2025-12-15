using DbtxtExporter.Data;
using DbtxtExporter.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Numerics;

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
        _logger.LogInformation("Первый запуск отчёта...");
await GenerateAllReports(stoppingToken);

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
        var today = DateTime.Today;

        // === Определяем начало текущей смены (твоя идеальная логика) ===
        DateTime currentShiftStart;
        if (now.Hour >= 8 && now.Hour < 20)
        {
            currentShiftStart = today.AddHours(8);                    // день: началась сегодня в 08:00
        }
        else if (now.Hour < 8)
        {
            currentShiftStart = today.AddDays(-1).AddHours(20);       // ночь: началась вчера в 20:00
        }
        else // 20:00 – 23:59
        {
            currentShiftStart = today.AddHours(20);                   // ночь: началась сегодня в 20:00
        }

        var currentShiftEnd = currentShiftStart.AddHours(12);

        // Предыдущая смена
        var previousShiftStart = currentShiftStart.AddHours(-12);
        var previousShiftEnd = currentShiftStart;

        // Определяем, какая смена дневная, какая ночная
        bool isCurrentDayShift = currentShiftStart.Hour == 8;

        DateTime dayShiftStart, dayShiftEnd;
        DateTime nightShiftStart, nightShiftEnd;

        if (isCurrentDayShift)
        {
            // Сейчас день → слева: текущий день (обновляется), справа: предыдущая ночь (завершена)
            dayShiftStart = currentShiftStart;
            dayShiftEnd = currentShiftEnd;
            nightShiftStart = previousShiftStart;
            nightShiftEnd = previousShiftEnd;
        }
        else
        {
            // Сейчас ночь → слева: предыдущий день (завершён), справа: текущая ночь (обновляется)
            dayShiftStart = previousShiftStart;
            dayShiftEnd = previousShiftEnd;
            nightShiftStart = currentShiftStart;
            nightShiftEnd = currentShiftEnd;
        }

        // Производственный месяц (для плана АБВГ)
        var prodMonthStart = new DateTime(now.Year, now.Month, 1).AddHours(8);
        var prodMonthEnd = prodMonthStart.AddMonths(1);

        try
        {
            await GenerateReportNkt(dbNkt.Nkt12Reps, "nkt", ct, prodMonthStart, prodMonthEnd,
                dayShiftStart, dayShiftEnd, nightShiftStart, nightShiftEnd, previousShiftStart, currentShiftEnd);

            await GenerateReportNot(dbNot.NotPakMp6Reps, "not", ct, prodMonthStart, prodMonthEnd,
                dayShiftStart, dayShiftEnd, nightShiftStart, nightShiftEnd, previousShiftStart, currentShiftEnd) ;

            await GenerateReportTpa(dbTpa.Tpa140Nc9Reps, "tpa140", ct, prodMonthStart, prodMonthEnd,
                dayShiftStart, dayShiftEnd, nightShiftStart, nightShiftEnd, previousShiftStart, currentShiftEnd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при генерации отчётов");
        }
    }

    private async Task GenerateReportNkt(IQueryable<Nkt12Rep> source, string prefix, CancellationToken ct,
        DateTime prodMonthStart, DateTime prodMonthEnd,
        DateTime dayShiftStart, DateTime dayShiftEnd, DateTime nightShiftStart, DateTime nightShiftEnd, DateTime previousShiftStart, DateTime currentShiftEnd)
    {
        var records = await source
            .Where(r => r.DateTime >= prodMonthStart.AddDays(-1) && r.DateTime < prodMonthEnd.AddHours(1))
            .OrderBy(r => r.DateTime)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

        await GenerateReportCore(records, r => r.DateTime, prefix, ct,
            prodMonthStart, prodMonthEnd, dayShiftStart, dayShiftEnd, nightShiftStart, nightShiftEnd, previousShiftStart, currentShiftEnd);
    }

    private async Task GenerateReportNot(IQueryable<NotPakRep> source, string prefix, CancellationToken ct,
        DateTime prodMonthStart, DateTime prodMonthEnd,
        DateTime dayShiftStart, DateTime dayShiftEnd, DateTime nightShiftStart, DateTime nightShiftEnd, DateTime previousShiftStart, DateTime currentShiftEnd)
    {
        var records = await source
            .Where(r => r.DateTime >= prodMonthStart.AddDays(-1) && r.DateTime < prodMonthEnd.AddHours(1))
            .OrderBy(r => r.DateTime)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

        await GenerateReportCore(records, r => r.DateTime, prefix, ct,
            prodMonthStart, prodMonthEnd, dayShiftStart, dayShiftEnd, nightShiftStart, nightShiftEnd, previousShiftStart, currentShiftEnd);
    }

    private async Task GenerateReportTpa(IQueryable<Tpa140Nc9Rep> source, string prefix, CancellationToken ct,
        DateTime prodMonthStart, DateTime prodMonthEnd,
        DateTime dayShiftStart, DateTime dayShiftEnd, DateTime nightShiftStart, DateTime nightShiftEnd, DateTime previousShiftStart, DateTime currentShiftEnd)
    {
        var records = await source
            .Where(r => r.DateTime >= prodMonthStart.AddDays(-1) && r.DateTime < prodMonthEnd.AddHours(1))
            .OrderBy(r => r.DateTime)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

        await GenerateReportCore(records, r => r.DateTime, prefix, ct,
            prodMonthStart, prodMonthEnd, dayShiftStart, dayShiftEnd, nightShiftStart, nightShiftEnd, previousShiftStart, currentShiftEnd);
    }

    private async Task GenerateReportCore<T>(
    List<T> records,
    Func<T, DateTime> timeSelector,
    string prefix,
    CancellationToken ct,
    DateTime prodMonthStart, DateTime prodMonthEnd,
    DateTime dayShiftStart, DateTime dayShiftEnd, DateTime nightShiftStart, DateTime nightShiftEnd, DateTime loadFrom,      // ← новая
    DateTime loadTo) where T : class
    {
        if (!records.Any())
        {
            _logger.LogInformation($"[{prefix.ToUpper()}] Нет данных");
            return;
        }

        // Последняя запись — берём Plan
        var last = records.MaxBy(r => (timeSelector(r), GetId(r)));
        int planPerHour = GetIntProperty(last, "Plan") ?? 20;
        var shiftPlan12h = (int)Math.Round(planPerHour * 10.54m);

        // === НОВЫЙ РАСЧЁТ ПРОСТОЯ ИЗ БД N0T ===
        int dayDowntime = 0;
        int nightDowntime = 0;
        int dayBad = 0;
        int nightBad = 0;
        using var scope = _sp.CreateScope();
        var delayDb = scope.ServiceProvider.GetRequiredService<NotDelayDbContext>();


        List<object> delayData = prefix.ToUpper() switch
        {
            "NKT" => await delayDb.NewNkt12Delay
                .Where(d => d.DateFrom >= loadFrom && d.DateTo <= loadTo)
                .ToListAsync<object>(ct),
            "NOT" => await delayDb.NewNkt3Delay
                .Where(d => d.DateFrom >= loadFrom && d.DateTo <= loadTo)
                .ToListAsync<object>(ct),
            "TPA140" => await delayDb.NewNc9Delay
                .Where(d => d.DateFrom >= loadFrom && d.DateTo <= loadTo)
                .ToListAsync<object>(ct),
            _ => throw new NotSupportedException($"Неизвестный префикс: {prefix}")
        };

        // Вспомогательные методы для рефлексии
        T GetProperty<T>(object obj, string name, T defaultValue = default)
        {
            var prop = obj.GetType().GetProperty(name);
            return prop != null ? (T)prop.GetValue(obj)! : defaultValue;
        }

        // День
        var dayHours = delayData.Where(d => GetProperty<DateTime>(d, "DateFrom") >= dayShiftStart &&
                                            GetProperty<DateTime>(d, "DateFrom") < dayShiftEnd).ToList();

        foreach (var h in dayHours)
        {
            int delayTime = GetProperty<int>(h, "DelayTime", 0);
            delayTime = delayTime / 60;
            dayDowntime += delayTime;
        }
        
        // Ночь
        var nightHours = delayData.Where(d => GetProperty<DateTime>(d, "DateFrom") >= nightShiftStart &&
                                             GetProperty<DateTime>(d, "DateFrom") < nightShiftEnd).ToList();

        foreach (var h in nightHours)
        {
            int delayTime = GetProperty<int>(h, "DelayTime", 0);
            delayTime = delayTime / 60;
            nightDowntime += delayTime;
        }
        dayBad = dayHours.Sum(h => GetProperty<int>(h, "Bad", 0));
        nightBad = nightHours.Sum(h => GetProperty<int>(h, "Bad", 0));
        // === Факт и план АБВГ — как было ===
        var dayFact = records.Count(r => timeSelector(r) >= dayShiftStart && timeSelector(r) < dayShiftEnd);
        var nightFact = records.Count(r => timeSelector(r) >= nightShiftStart && timeSelector(r) < nightShiftEnd);

        // Кумулятивный план А,Б,В,Г — оставляем как было
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

        // === Запись ===
        var line = $"{shiftPlan12h};{dayFact};{dayDowntime};{dayBad};{shiftPlan12h};{nightFact};{nightDowntime};{nightBad};{planA};{factA};{planB};{factB};{planV};{factV};{planG};{factG}";

        var folderPath = Path.Combine(_settings.OutputPath, prefix.ToUpper());
        Directory.CreateDirectory(folderPath);
        var path = Path.Combine(folderPath, $"report_{prefix}.txt");

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