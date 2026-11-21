using Greenhouse.Api.Contracts;
using Greenhouse.Api.Data;
using Greenhouse.Domain;
using Microsoft.EntityFrameworkCore;

namespace Greenhouse.Api.Services;

/// <summary>Прийом і читання показів датчиків.</summary>
public sealed partial class ReadingService(
    GreenhouseDbContext db,
    TimeProvider clock,
    ILogger<ReadingService> logger)
{
    /// <summary>Найбільший дозволений пакет, щоб один запит не заблокував БД надовго.</summary>
    public const int MaxBatchSize = 1000;

    /// <summary>Наскільки час показу може випереджати годинник сервера (розсинхрон годинників).</summary>
    public static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Зберігає пакет показів атомарно: якщо хоч один некоректний, не зберігається жоден.
    /// Тривоги оновлюються в тій самій транзакції.
    /// </summary>
    public async Task<IReadOnlyList<ReadingOut>> AddAsync(
        IReadOnlyList<ReadingIn>? batch, CancellationToken ct)
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

        var now = clock.GetUtcNow().UtcDateTime;
        var validated = batch.Select(r => Validate(r, sensors, now)).ToList();

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var readings = validated
                .Select(v => new Reading { SensorId = v.Sensor.Id, Value = v.Value, RecordedAt = v.RecordedAt })
                .ToList();
            db.Readings.AddRange(readings);
            await UpdateAlertsAsync(validated, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            LogSaved(logger, readings.Count);
            return (IReadOnlyList<ReadingOut>)readings
                .Select((r, i) => new ReadingOut(r.Id, validated[i].Sensor.Name, validated[i].Sensor.Kind, r.Value, r.RecordedAt))
                .ToList();
        });
    }

    /// <summary>Останні покази, новіші першими; за потреби — одного датчика і за період.</summary>
    public async Task<IReadOnlyList<ReadingOut>> ListAsync(
        string? sensor, DateTimeOffset? from, DateTimeOffset? to, int limit, CancellationToken ct)
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
            var exists = await db.Sensors.AnyAsync(s => s.Name == sensor, ct);
            if (!exists)
            {
                throw new UnknownSensorException(sensor);
            }

            query = query.Where(r => r.Sensor.Name == sensor);
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
            .Select(r => new ReadingOut(r.Id, r.Sensor.Name, r.Sensor.Kind, r.Value, r.RecordedAt))
            .ToListAsync(ct);
    }

    /// <summary>Останній показ кожного датчика і його стан відносно норми.</summary>
    public async Task<IReadOnlyList<LatestReadingOut>> LatestAsync(CancellationToken ct)
    {
        var rows = await db.Sensors.AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => new
            {
                s.Name,
                s.Kind,
                s.Type.Unit,
                s.Type.ComfortMin,
                s.Type.ComfortMax,
                Last = s.Readings
                    .OrderByDescending(r => r.RecordedAt).ThenByDescending(r => r.Id)
                    .Select(r => new { r.Value, r.RecordedAt })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return rows.Select(r => new LatestReadingOut(
                r.Name,
                r.Kind,
                r.Unit,
                r.Last?.Value,
                r.Last?.RecordedAt,
                ComfortClassifier.Classify(r.Last?.Value, r.ComfortMin, r.ComfortMax).ToApi()))
            .ToList();
    }

    private ValidatedReading Validate(ReadingIn? r, Dictionary<string, Sensor> sensors, DateTime now)
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
        var definition = new SensorTypeDefinition(type.Kind, type.Unit, type.ValidMin, type.ValidMax, type.ComfortMin, type.ComfortMax);
        if (!definition.IsPhysicallyValid(value))
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

        return new ValidatedReading(sensor, definition, value, recordedAt);
    }

    private async Task UpdateAlertsAsync(List<ValidatedReading> readings, CancellationToken ct)
    {
        var sensorIds = readings.Select(r => r.Sensor.Id).Distinct().ToList();
        var open = await db.Alerts
            .Where(a => sensorIds.Contains(a.SensorId) && a.ClosedAt == null)
            .ToDictionaryAsync(a => a.SensorId, ct);

        // Покази обробляються в порядку часу, щоб пакет зі старими даними дав правильну історію тривог.
        foreach (var r in readings.OrderBy(r => r.RecordedAt))
        {
            open.TryGetValue(r.Sensor.Id, out var current);
            var status = r.Definition.Classify(r.Value);
            var action = AlertPolicy.Decide(current is null ? null : ParseKind(current.Kind), status);

            if (action is AlertAction.Close or AlertAction.Replace && current is not null)
            {
                current.ClosedAt = r.RecordedAt < current.OpenedAt ? current.OpenedAt : r.RecordedAt;
                open.Remove(r.Sensor.Id);
                LogAlertClosed(logger, r.Sensor.Name, current.Kind);
            }

            if (action is AlertAction.Open or AlertAction.Replace)
            {
                var alert = new Alert
                {
                    SensorId = r.Sensor.Id,
                    Kind = status.ToApi(),
                    TriggerValue = r.Value,
                    OpenedAt = r.RecordedAt,
                };
                db.Alerts.Add(alert);
                open[r.Sensor.Id] = alert;
                LogAlertOpened(logger, r.Sensor.Name, alert.Kind, r.Value);
            }
        }
    }

    private static ComfortStatus ParseKind(string kind) =>
        kind == "low" ? ComfortStatus.Low : ComfortStatus.High;

    private sealed record ValidatedReading(Sensor Sensor, SensorTypeDefinition Definition, double Value, DateTime RecordedAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Збережено {Count} вимірів")]
    private static partial void LogSaved(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Вимір від {Sensor} відхилено: {Reason}")]
    private static partial void LogRejected(ILogger logger, string sensor, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Тривога {Kind} для {Sensor}: {Value}")]
    private static partial void LogAlertOpened(ILogger logger, string sensor, string kind, double value);

    [LoggerMessage(Level = LogLevel.Information, Message = "Тривогу {Kind} для {Sensor} закрито")]
    private static partial void LogAlertClosed(ILogger logger, string sensor, string kind);
}
