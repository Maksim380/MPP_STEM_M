using Greenhouse.Core.Contracts;
using Greenhouse.Core.Data;
using Greenhouse.Core.Domain;
using Greenhouse.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Greenhouse.Api.Background;

public sealed class SimulatorOptions
{
    public const string Section = "Simulator";

    /// <summary>Вбудований симулятор вимкнено за замовчуванням: справжні дані надходять від пристроїв.</summary>
    public bool Enabled { get; set; }

    public double IntervalSeconds { get; set; } = 2;
    public double Speed { get; set; } = 600;
    public double AnomalyRate { get; set; } = 0.02;
}

/// <summary>
/// Фоновий сервіс для демонстрації: раз на інтервал генерує покази для всіх датчиків усіх теплиць
/// і записує їх через той самий <see cref="ReadingService"/>, що й REST API.
/// </summary>
public sealed partial class SimulatorWorker(
    IServiceScopeFactory scopes,
    IOptions<SimulatorOptions> options,
    TimeProvider clock,
    ILogger<SimulatorWorker> logger) : BackgroundService
{
    private readonly SignalGenerator _generator = new(new Random());

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        var start = clock.GetUtcNow();
        var startHour = start.Hour + start.Minute / 60.0;
        LogStarted(logger, o.IntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(o.IntervalSeconds), clock);
        do
        {
            var hour = (startHour + (clock.GetUtcNow() - start).TotalHours * o.Speed) % 24;
            await TickAsync(hour, o.AnomalyRate, stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Один крок симуляції. Помилка кроку логується і не зупиняє сервіс.</summary>
    internal async Task TickAsync(double hour, double anomalyRate, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<GreenhouseDbContext>();
            var sensors = await db.Sensors.AsNoTracking().Select(s => new { s.Name, s.Kind }).ToListAsync(ct);
            if (sensors.Count == 0)
            {
                return;
            }

            var batch = _generator.MakeBatch(sensors.Select(s => (s.Name, s.Kind)), hour, anomalyRate)
                .Select(r => (ReadingIn?)new ReadingIn(r.Sensor, r.Value))
                .ToList();
            await scope.ServiceProvider.GetRequiredService<ReadingService>().AddAsync(batch, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (e is ClientDataException or DbUpdateException or InvalidOperationException)
        {
            LogTickFailed(logger, e);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Вбудований симулятор запущено, інтервал {Interval} с")]
    private static partial void LogStarted(ILogger logger, double interval);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Крок симулятора не вдався")]
    private static partial void LogTickFailed(ILogger logger, Exception exception);
}
