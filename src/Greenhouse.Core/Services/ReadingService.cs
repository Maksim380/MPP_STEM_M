using Greenhouse.Core.Contracts;
using Greenhouse.Core.Data;
using Greenhouse.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Greenhouse.Core.Services;

/// <summary>Прийом і читання показів датчиків.</summary>
public sealed partial class ReadingService(
    GreenhouseDbContext db,
    TimeProvider clock,
    ITelemetryNotifier notifier,
    ILogger<ReadingService> logger)
{
    /// <summary>Найбільший дозволений пакет, щоб один запит не заблокував БД надовго.</summary>
    public const int MaxBatchSize = 1000;

    /// <summary>Наскільки час показу може випереджати годинник сервера (розсинхрон годинників).</summary>
    public static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Зберігає пакет показів атомарно: якщо хоч один некоректний, не зберігається жоден.
    /// Тривоги оновлюються в тій самій транзакції; після фіксації клієнти отримують сповіщення.
    /// </summary>
    public async Task<IReadOnlyList<ReadingOut>> AddAsync(IReadOnlyList<ReadingIn?>? batch, CancellationToken ct)
    {
        if (batch is null || batch.Count == 0)
        {
            throw new InvalidInputException("Порожній пакет вимірів");
        }

        if (batch.Count > MaxBatchSize)
        {
            throw new InvalidInputException($"Пакет більший за {MaxBatchSize} вимірів");
        }

        var names = batch.Select(r => r?.Sensor).OfType<string>().Distinct().ToList();
        var sensors = await db.Sensors.Include(s => s.Type)
            .Where(s => names.Contains(s.Name))
            .ToDictionaryAsync(s => s.Name, ct);
        var greenhouseIds = sensors.Values.Select(s => s.GreenhouseId).Distinct().ToList();
        var thresholds = await db.Thresholds
            .Where(t => greenhouseIds.Contains(t.GreenhouseId))
            .ToDictionaryAsync(t => (t.GreenhouseId, t.Kind), ct);

        var now = clock.GetUtcNow().UtcDateTime;
        var validated = batch.Select(r => Validate(r, sensors, thresholds, now)).ToList();

        var strategy = db.Database.CreateExecutionStrategy();
        var (saved, changedAlerts) = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var readings = validated
                .Select(v => new Reading { SensorId = v.Sensor.Id, Value = v.Value, RecordedAt = v.RecordedAt })
                .ToList();
            db.Readings.AddRange(readings);
            var alerts = await UpdateAlertsAsync(validated, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var output = readings
                .Select((r, i) => new ReadingOut(r.Id, validated[i].Sensor.Name, validated[i].Sensor.Kind,
                    validated[i].Sensor.GreenhouseId, r.Value, r.RecordedAt))
                .ToList();
            var alertOutput = alerts.Select(a => AlertService.ToOut(a, sensors.Values.First(s => s.Id == a.SensorId))).ToList();
            return (output, alertOutput);
        });

        LogSaved(logger, saved.Count);
        await notifier.ReadingsAddedAsync(saved, ct);
        if (changedAlerts.Count > 0)
        {
            await notifier.AlertsChangedAsync(changedAlerts, ct);
        }

        return saved;
    }

    /// <summary>Останні покази, новіші першими; фільтри за датчиком, теплицею і періодом.</summary>
    public async Task<IReadOnlyList<ReadingOut>> ListAsync(
        string? sensor, int? greenhouseId, DateTimeOffset? from, DateTimeOffset? to, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 1000)
        {
            throw new InvalidInputException("limit має бути від 1 до 1000");
        }

        if (from is not null && to is not null && from > to)
        {
            throw new InvalidInputException("from не може бути пізніше за to");
        }

        var query = db.Readings.AsNoTracking();
        if (sensor is not null)
        {
            if (!await db.Sensors.AnyAsync(s => s.Name == sensor, ct))
            {
                throw new UnknownSensorException(sensor);
            }

            query = query.Where(r => r.Sensor.Name == sensor);
        }

        if (greenhouseId is not null)
        {
            await GreenhouseService.EnsureExistsAsync(db, greenhouseId.Value, ct);
            query = query.Where(r => r.Sensor.GreenhouseId == greenhouseId);
        }

        if (from is not null)
        {
            var fromUtc = from.Value.UtcDateTime;
            query = query.Where(r => r.RecordedAt >= fromUtc);
        }

        if (to is not null)
        {
            var toUtc = to.Value.UtcDateTime;
            query = query.Where(r => r.RecordedAt <= toUtc);
        }

        return await query
            .OrderByDescending(r => r.RecordedAt).ThenByDescending(r => r.Id)
            .Take(limit)
            .Select(r => new ReadingOut(r.Id, r.Sensor.Name, r.Sensor.Kind, r.Sensor.GreenhouseId, r.Value, r.RecordedAt))
            .ToListAsync(ct);
    }

    /// <summary>Останній показ кожного датчика і його стан відносно норми теплиці.</summary>
    public async Task<IReadOnlyList<LatestReadingOut>> LatestAsync(int? greenhouseId, CancellationToken ct)
    {
        var sensors = db.Sensors.AsNoTracking();
        if (greenhouseId is not null)
        {
            await GreenhouseService.EnsureExistsAsync(db, greenhouseId.Value, ct);
            sensors = sensors.Where(s => s.GreenhouseId == greenhouseId);
        }

        var rows = await sensors
            .OrderBy(s => s.Id)
            .Select(s => new
            {
                s.Name,
                s.Kind,
                s.Type.Unit,
                s.GreenhouseId,
                Threshold = db.Thresholds.FirstOrDefault(t => t.GreenhouseId == s.GreenhouseId && t.Kind == s.Kind),
                Last = s.Readings
                    .OrderByDescending(r => r.RecordedAt).ThenByDescending(r => r.Id)
                    .Select(r => new { r.Value, r.RecordedAt })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return rows.Select(r =>
            {
                var (min, max) = r.Threshold is null
                    ? (SensorCatalog.Get(r.Kind).ComfortMin, SensorCatalog.Get(r.Kind).ComfortMax)
                    : (r.Threshold.ComfortMin, r.Threshold.ComfortMax);
                return new LatestReadingOut(r.Name, r.Kind, r.Unit, r.GreenhouseId, r.Last?.Value, r.Last?.RecordedAt,
                    ComfortClassifier.Classify(r.Last?.Value, min, max).ToApi());
            })
            .ToList();
    }

    private ValidatedReading Validate(
        ReadingIn? r,
        Dictionary<string, Sensor> sensors,
        Dictionary<(int, string), Threshold> thresholds,
        DateTime now)
    {
        if (r is null)
        {
            throw new InvalidInputException("Вимір не може бути null");
        }

        if (string.IsNullOrWhiteSpace(r.Sensor))
        {
            throw new InvalidInputException("Поле sensor обов'язкове");
        }

        if (r.Value is null)
        {
            throw new InvalidInputException($"Поле value обов'язкове ({r.Sensor})");
        }

        if (!sensors.TryGetValue(r.Sensor, out var sensor))
        {
            LogRejected(logger, r.Sensor, "невідомий датчик");
            throw new UnknownSensorException(r.Sensor);
        }

        var type = sensor.Type;
        var value = r.Value.Value;
        if (!double.IsFinite(value) || value < type.ValidMin || value > type.ValidMax)
        {
            LogRejected(logger, r.Sensor, $"значення {value} поза фізичним діапазоном");
            throw new ImplausibleValueException(
                $"Значення {value} {type.Unit} поза фізичним діапазоном [{type.ValidMin}; {type.ValidMax}] для {r.Sensor}");
        }

        var recordedAt = r.RecordedAt?.UtcDateTime ?? now;
        if (recordedAt > now + AllowedClockSkew)
        {
            LogRejected(logger, r.Sensor, "час показу в майбутньому");
            throw new ImplausibleValueException($"Час показу {recordedAt:O} у майбутньому ({r.Sensor})");
        }

        var defaults = SensorCatalog.Get(sensor.Kind);
        var (min, max) = thresholds.TryGetValue((sensor.GreenhouseId, sensor.Kind), out var t)
            ? (t.ComfortMin, t.ComfortMax)
            : (defaults.ComfortMin, defaults.ComfortMax);
        return new ValidatedReading(sensor, ComfortClassifier.Classify(value, min, max), value, recordedAt);
    }

    private async Task<List<Alert>> UpdateAlertsAsync(List<ValidatedReading> readings, CancellationToken ct)
    {
        var sensorIds = readings.Select(r => r.Sensor.Id).Distinct().ToList();
        var open = await db.Alerts
            .Where(a => sensorIds.Contains(a.SensorId) && a.ClosedAt == null)
            .ToDictionaryAsync(a => a.SensorId, ct);
        var changed = new List<Alert>();

        // Покази обробляються в порядку часу, щоб пакет зі старими даними дав правильну історію тривог.
        foreach (var r in readings.OrderBy(r => r.RecordedAt))
        {
            open.TryGetValue(r.Sensor.Id, out var current);
            var action = AlertPolicy.Decide(current is null ? null : ParseKind(current.Kind), r.Status);

            if (action is AlertAction.Close or AlertAction.Replace && current is not null)
            {
                current.ClosedAt = r.RecordedAt < current.OpenedAt ? current.OpenedAt : r.RecordedAt;
                open.Remove(r.Sensor.Id);
                changed.Add(current);
                LogAlertClosed(logger, r.Sensor.Name, current.Kind);
            }

            if (action is AlertAction.Open or AlertAction.Replace)
            {
                var alert = new Alert
                {
                    SensorId = r.Sensor.Id,
                    Kind = r.Status.ToApi(),
                    TriggerValue = r.Value,
                    OpenedAt = r.RecordedAt,
                };
                db.Alerts.Add(alert);
                open[r.Sensor.Id] = alert;
                changed.Add(alert);
                LogAlertOpened(logger, r.Sensor.Name, alert.Kind, r.Value);
            }
        }

        return changed;
    }

    private static ComfortStatus ParseKind(string kind) =>
        kind == "low" ? ComfortStatus.Low : ComfortStatus.High;

    private sealed record ValidatedReading(Sensor Sensor, ComfortStatus Status, double Value, DateTime RecordedAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Збережено {Count} вимірів")]
    private static partial void LogSaved(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Вимір від {Sensor} відхилено: {Reason}")]
    private static partial void LogRejected(ILogger logger, string sensor, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Тривога {Kind} для {Sensor}: {Value}")]
    private static partial void LogAlertOpened(ILogger logger, string sensor, string kind, double value);

    [LoggerMessage(Level = LogLevel.Information, Message = "Тривогу {Kind} для {Sensor} закрито")]
    private static partial void LogAlertClosed(ILogger logger, string sensor, string kind);
}
