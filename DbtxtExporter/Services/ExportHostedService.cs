using DbtxtExporter.Data;
using DbtxtExporter.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

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
        _logger.LogInformation("Экспортер отчёта запущен → {Path}", _settings.OutputPath);

        await DoExportAsync(stoppingToken);

        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            await DoExportAsync(stoppingToken);
        }
    }

    private async Task DoExportAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.Now;
            var today = DateTime.Today;

            // === 1. Производственные сутки: с 08:00 до 08:00 ===
            var productionDayStart = now.Hour >= 8
                ? now.Date.AddHours(8)
                : now.Date.AddDays(-1).AddHours(8);

            var dayShiftStart = productionDayStart;                    // Смена 1: 08:00
            var dayShiftEnd = productionDayStart.AddHours(12);       //          20:00
            var nightShiftStart = productionDayStart.AddHours(12);       // Смена 2: 20:00
            var nightShiftEnd = productionDayStart.AddDays(1);         //          08:00 +1 день
            var nextShiftStart = nightShiftEnd;                         // для переходящего простоя

            // === 2. Определяем производственный месяц ===
            var currentMonthStart = now.Date.AddDays(1 - now.Day); // 1-е число текущего месяца
            var prodMonthStart = currentMonthStart.AddHours(8);
            var nextMonthStart = currentMonthStart.AddMonths(1);
            var prodMonthEnd = nextMonthStart.AddHours(8);

            // === 3. Загружаем все записи за производственный месяц + буфер ===
            var records = await db.Nkt12Reps
                .Where(x => x.Deleted == 0 &&
                            x.DateTime >= prodMonthStart.AddDays(-1) &&
                            x.DateTime < prodMonthEnd.AddHours(1))
                .OrderBy(x => x.DateTime)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);

            if (!records.Any())
            {
                _logger.LogWarning("Нет данных в NKT_12_REP");
                return;
            }

            var lastRecord = records.MaxBy(x => (x.DateTime, x.Id));
            var planPerHour = lastRecord?.Plan ?? 20;
            var planSecondsPerPipe = 3600m / planPerHour;
            var shiftPlan12h = Decimal.Round(planPerHour * 10.54m);

            // === 4. Функция расчёта простоя за 12-часовую смену ===
            decimal CalcDowntime12h(DateTime start, DateTime end, DateTime nextStart)
            {
                var shiftRecs = records.Where(x => x.DateTime >= start && x.DateTime < end).ToList();
                if (!shiftRecs.Any()) return 0;

                var first = shiftRecs.MinBy(x => (x.DateTime, x.Id))!;
                var firstNext = records.FirstOrDefault(x => x.DateTime >= nextStart);

                decimal totalSec = shiftRecs.Sum(x => x.DelayTimeThisHour ?? 0);

                totalSec += shiftRecs
                    .Where(x => !ReferenceEquals(x, first))
                    .Sum(x => x.DelayTimePrevHour ?? 0);

                if (first.DelayTimePrevHour > 0)
                {
                    var lastBefore = records.Where(x => x.DateTime < start).MaxBy(x => (x.DateTime, x.Id));
                    decimal secFromLast = lastBefore != null
                        ? (decimal)(start - lastBefore.DateTime).TotalSeconds
                        : 3600m;

                    var expected = secFromLast - planSecondsPerPipe;
                    var corrected = first.DelayTimePrevHour.Value;
                    if (expected > 0) corrected -= (int)Math.Floor(expected);
                    if (corrected > 0) totalSec += corrected;
                }

                if (firstNext?.DelayTimePrevHour > 0)
                    totalSec += firstNext.DelayTimePrevHour.Value;

                return Math.Round(totalSec / 60m);
            }

            var prevDowntime = CalcDowntime12h(dayShiftStart, dayShiftEnd, nightShiftStart);  // Смена 1 (день)
            var currDowntime = CalcDowntime12h(nightShiftStart, nightShiftEnd, nextShiftStart);   // Смена 2 (ночь)

            var prevFact = records.Count(x => x.DateTime >= dayShiftStart && x.DateTime < dayShiftEnd);
            var currFact = records.Count(x => x.DateTime >= nightShiftStart && x.DateTime < nightShiftEnd);

            // === 5. Точный кумулятивный план для смен А,Б,В,Г (как в твоём детальном SQL) ===
            var planBySmena = new Dictionary<int, decimal> { { 1, 0m }, { 2, 0m }, { 3, 0m }, { 4, 0m } };

            // Фильтруем записи за производственный месяц, Smena 1-4
            var monthlyRecords = records
                .Where(x => x.DateTime >= prodMonthStart && x.DateTime < prodMonthEnd &&
                            x.Smena >= 1 && x.Smena <= 4)
                .ToList();

            // Для каждой записи рассчитываем ProductionDay, ShiftStart, ShiftEnd
            var shiftIntervals = monthlyRecords.Select(r =>
            {
                var dt = r.DateTime;
                var productionDay = dt.AddHours(-8).Date;

                DateTime shiftStart;
                if (dt.Hour >= 8 && dt.Hour < 20)
                    shiftStart = dt.Date.AddHours(8);
                else if (dt.Hour >= 20)
                    shiftStart = dt.Date.AddHours(20);
                else // < 8
                    shiftStart = dt.Date.AddDays(-1).AddHours(20);

                var shiftEnd = shiftStart.AddHours(12);

                return new { r, ProductionDay = productionDay, ShiftStart = shiftStart, ShiftEnd = shiftEnd };
            })
            .Where(x => x.r.DateTime >= x.ShiftStart && x.r.DateTime < x.ShiftEnd)
            .ToList();

            // Группируем по ProductionDay и Smena
            var groups = shiftIntervals
            .Where(x => x.r.Smena.HasValue)  
            .GroupBy(x => new {x.ProductionDay, Smena = x.r.Smena.Value});

            foreach (var group in groups)
            {
                var sorted = group.OrderBy(x => x.r.DateTime).ThenBy(x => x.r.Id).ToList();
                if (!sorted.Any()) continue;

                // ShiftEnd одинаковый для группы
                var shiftEnd = sorted.First().ShiftEnd;

                // Рассчитываем NextDateTime
                var nextTimes = new DateTime[sorted.Count];
                for (int i = 0; i < sorted.Count - 1; i++)
                    nextTimes[i] = sorted[i + 1].r.DateTime;
                nextTimes[sorted.Count - 1] = shiftEnd;

                decimal runningTotal = 0m;

                for (int i = 0; i < sorted.Count; i++)
                {
                    var current = sorted[i].r;
                    var durationHours = (decimal)(nextTimes[i] - current.DateTime).TotalHours;

                    runningTotal += durationHours;

                    decimal prevRunning = runningTotal - durationHours;
                    decimal effectiveDuration = prevRunning >= 10.54m ? 0m :
                        (runningTotal <= 10.54m ? durationHours : 10.54m - prevRunning);

                    decimal planContribution = (current.Plan ?? 20) * effectiveDuration;

                    planBySmena[group.Key.Smena] += planContribution;
                }
            }

            // Округляем планы как в SQL (ROUND(..., 3)), но поскольку план целочисленный, используем Math.Round
            int planA = (int)Math.Round(planBySmena[1]);
            int planB = (int)Math.Round(planBySmena[2]);
            int planV = (int)Math.Round(planBySmena[3]);
            int planG = (int)Math.Round(planBySmena[4]);

            // Факт по сменам А,Б,В,Г (по процедуре: COUNT за месяц, но с фильтром EndDateTime >= start, StartDateTime < end)
            // Предполагаем, что в модели StartDateTime и EndDateTime нет, используем DateTime как индикатор
            // Если есть StartDateTime/EndDateTime, скорректируй
            var factBySmena = monthlyRecords
                .GroupBy(x => x.Smena)
                .ToDictionary(g => g.Key, g => g.Count());

            int factA = factBySmena.GetValueOrDefault(1, 0);
            int factB = factBySmena.GetValueOrDefault(2, 0);
            int factV = factBySmena.GetValueOrDefault(3, 0);
            int factG = factBySmena.GetValueOrDefault(4, 0);

            // === 6. Формируем строку ===
            var line = string.Join("",
                $"{shiftPlan12h};{prevFact};{prevDowntime};;",
                $"{shiftPlan12h};{currFact};{currDowntime};;",
                $"{planA};{factA};",
                $"{planB};{factB};",
                $"{planV};{factV};",
                $"{planG};{factG}"
            );

            var filePath = Path.Combine(_settings.OutputPath, "report.txt");
            await File.WriteAllTextAsync(filePath, line + Environment.NewLine, Encoding.UTF8, ct);

            _logger.LogInformation("Отчёт обновлён: {Line}", line);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при формировании отчёта");
        }
    }
}