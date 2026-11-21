using Greenhouse.Core.Contracts;
using Greenhouse.Core.Data;
using Greenhouse.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Greenhouse.Core.Services;

/// <summary>Теплиці, їхні датчики і пороги норми.</summary>
public sealed class GreenhouseService(GreenhouseDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<GreenhouseOut>> ListAsync(CancellationToken ct)
    {
        var items = await Query().OrderBy(g => g.Id).ToListAsync(ct);
        return items.Select(ToOut).ToList();
    }

    public async Task<GreenhouseOut> GetAsync(int id, CancellationToken ct) =>
        ToOut(await Query().FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException($"Теплицю {id} не знайдено"));

    /// <summary>Нова теплиця отримує стандартні пороги для всіх типів датчиків.</summary>
    public async Task<GreenhouseOut> CreateAsync(GreenhouseIn input, CancellationToken ct)
    {
        var name = ValidateName(input.Name);
        if (await db.Greenhouses.AnyAsync(g => g.Name == name, ct))
        {
            throw new ConflictException($"Теплиця «{name}» вже існує");
        }

        var greenhouse = new GreenhouseEntity { Name = name, Location = Trim(input.Location), CreatedAt = clock.GetUtcNow().UtcDateTime };
        foreach (var t in SensorCatalog.Types)
        {
            greenhouse.Thresholds.Add(new Threshold { Kind = t.Kind, ComfortMin = t.ComfortMin, ComfortMax = t.ComfortMax });
        }

        db.Greenhouses.Add(greenhouse);
        await db.SaveChangesAsync(ct);
        return await GetAsync(greenhouse.Id, ct);
    }

    public async Task<GreenhouseOut> UpdateAsync(int id, GreenhouseIn input, CancellationToken ct)
    {
        var greenhouse = await db.Greenhouses.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException($"Теплицю {id} не знайдено");
        var name = ValidateName(input.Name);
        if (await db.Greenhouses.AnyAsync(g => g.Name == name && g.Id != id, ct))
        {
            throw new ConflictException($"Теплиця «{name}» вже існує");
        }

        greenhouse.Name = name;
        greenhouse.Location = Trim(input.Location);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Видалити можна лише теплицю без датчиків, щоб випадково не втратити історію вимірів.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var greenhouse = await db.Greenhouses.Include(g => g.Sensors).FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException($"Теплицю {id} не знайдено");
        if (greenhouse.Sensors.Count > 0)
        {
            throw new ConflictException("Спершу видаліть або перенесіть датчики теплиці");
        }

        db.Greenhouses.Remove(greenhouse);
        await db.SaveChangesAsync(ct);
    }

    public async Task<SensorOut> AddSensorAsync(int greenhouseId, NewSensorIn input, CancellationToken ct)
    {
        await EnsureExistsAsync(db, greenhouseId, ct);
        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 64)
        {
            throw new InvalidInputException("Назва датчика обов'язкова, до 64 символів");
        }

        var type = await db.SensorTypes.FirstOrDefaultAsync(t => t.Kind == input.Kind, ct)
            ?? throw new InvalidInputException($"Невідомий тип датчика: {input.Kind}. Допустимо: {string.Join(", ", SensorCatalog.Types.Select(t => t.Kind))}");
        if (await db.Sensors.AnyAsync(s => s.Name == name, ct))
        {
            throw new ConflictException($"Датчик «{name}» вже існує");
        }

        db.Sensors.Add(new Sensor { Name = name, Kind = type.Kind, GreenhouseId = greenhouseId });
        await db.SaveChangesAsync(ct);
        var threshold = await db.Thresholds.FirstAsync(t => t.GreenhouseId == greenhouseId && t.Kind == type.Kind, ct);
        return new SensorOut(name, type.Kind, type.Unit, greenhouseId, threshold.ComfortMin, threshold.ComfortMax);
    }

    public async Task<IReadOnlyList<SensorOut>> ListSensorsAsync(int? greenhouseId, CancellationToken ct)
    {
        var query = db.Sensors.AsNoTracking();
        if (greenhouseId is not null)
        {
            await EnsureExistsAsync(db, greenhouseId.Value, ct);
            query = query.Where(s => s.GreenhouseId == greenhouseId);
        }

        return await query.OrderBy(s => s.Id)
            .Select(s => new
            {
                s.Name,
                s.Kind,
                s.Type.Unit,
                s.GreenhouseId,
                Threshold = db.Thresholds.First(t => t.GreenhouseId == s.GreenhouseId && t.Kind == s.Kind),
            })
            .Select(s => new SensorOut(s.Name, s.Kind, s.Unit, s.GreenhouseId, s.Threshold.ComfortMin, s.Threshold.ComfortMax))
            .ToListAsync(ct);
    }

    /// <summary>Нова норма має бути в межах фізичного діапазону датчика і min ≤ max.</summary>
    public async Task<ThresholdOut> SetThresholdAsync(int greenhouseId, string kind, ThresholdIn input, CancellationToken ct)
    {
        await EnsureExistsAsync(db, greenhouseId, ct);
        var threshold = await db.Thresholds.Include(t => t.Type)
            .FirstOrDefaultAsync(t => t.GreenhouseId == greenhouseId && t.Kind == kind, ct)
            ?? throw new NotFoundException($"Тип датчика «{kind}» не знайдено");
        if (input.ComfortMin is not { } min || input.ComfortMax is not { } max)
        {
            throw new InvalidInputException("Поля comfort_min і comfort_max обов'язкові");
        }

        if (min > max)
        {
            throw new InvalidInputException("comfort_min не може бути більшим за comfort_max");
        }

        if (!double.IsFinite(min) || !double.IsFinite(max) || min < threshold.Type.ValidMin || max > threshold.Type.ValidMax)
        {
            throw new ImplausibleValueException(
                $"Норма має бути в межах [{threshold.Type.ValidMin}; {threshold.Type.ValidMax}] {threshold.Type.Unit}");
        }

        threshold.ComfortMin = min;
        threshold.ComfortMax = max;
        await db.SaveChangesAsync(ct);
        return new ThresholdOut(threshold.Kind, threshold.Type.Unit, min, max);
    }

    internal static async Task EnsureExistsAsync(GreenhouseDbContext db, int id, CancellationToken ct)
    {
        if (!await db.Greenhouses.AnyAsync(g => g.Id == id, ct))
        {
            throw new NotFoundException($"Теплицю {id} не знайдено");
        }
    }

    private IQueryable<GreenhouseEntity> Query() =>
        db.Greenhouses.AsNoTracking()
            .Include(g => g.Sensors)
            .Include(g => g.Thresholds).ThenInclude(t => t.Type)
            .AsSplitQuery();

    private static GreenhouseOut ToOut(GreenhouseEntity g) => new(
        g.Id,
        g.Name,
        g.Location,
        g.Sensors.OrderBy(s => s.Id).Select(s => s.Name).ToList(),
        g.Thresholds.OrderBy(t => t.Kind, StringComparer.Ordinal)
            .Select(t => new ThresholdOut(t.Kind, t.Type.Unit, t.ComfortMin, t.ComfortMax)).ToList());

    private static string ValidateName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 100)
        {
            throw new InvalidInputException("Назва теплиці обов'язкова, до 100 символів");
        }

        return trimmed;
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
