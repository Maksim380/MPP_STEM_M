using Greenhouse.Core.Contracts;
using Greenhouse.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Greenhouse.Core.Services;

/// <summary>Журнал тривог і їх підтвердження оператором.</summary>
public sealed class AlertService(GreenhouseDbContext db, TimeProvider clock, ITelemetryNotifier notifier)
{
    public async Task<IReadOnlyList<AlertOut>> ListAsync(bool activeOnly, int? greenhouseId, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 500)
        {
            throw new InvalidInputException("limit має бути від 1 до 500");
        }

        var query = db.Alerts.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(a => a.ClosedAt == null);
        }

        if (greenhouseId is not null)
        {
            await GreenhouseService.EnsureExistsAsync(db, greenhouseId.Value, ct);
            query = query.Where(a => a.Sensor.GreenhouseId == greenhouseId);
        }

        return await query
            .OrderByDescending(a => a.OpenedAt).ThenByDescending(a => a.Id)
            .Take(limit)
            .Select(a => new AlertOut(a.Id, a.Sensor.Name, a.Sensor.GreenhouseId, a.Kind, a.TriggerValue, a.OpenedAt,
                a.ClosedAt, a.AcknowledgedAt, a.AcknowledgedBy))
            .ToListAsync(ct);
    }

    /// <summary>Позначає тривогу як побачену. Повторне підтвердження нічого не змінює.</summary>
    public async Task<AlertOut> AcknowledgeAsync(long id, string user, CancellationToken ct)
    {
        var alert = await db.Alerts.Include(a => a.Sensor).FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Тривогу {id} не знайдено");

        if (alert.AcknowledgedAt is not null)
        {
            return ToOut(alert, alert.Sensor);
        }

        alert.AcknowledgedAt = clock.GetUtcNow().UtcDateTime;
        alert.AcknowledgedBy = user;
        await db.SaveChangesAsync(ct);
        var result = ToOut(alert, alert.Sensor);
        await notifier.AlertsChangedAsync([result], ct);
        return result;
    }

    internal static AlertOut ToOut(Alert a, Sensor sensor) =>
        new(a.Id, sensor.Name, sensor.GreenhouseId, a.Kind, a.TriggerValue, a.OpenedAt, a.ClosedAt, a.AcknowledgedAt, a.AcknowledgedBy);
}
