using Greenhouse.Api.Contracts;
using Greenhouse.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Greenhouse.Api.Services;

/// <summary>Журнал тривог і їх підтвердження оператором.</summary>
public sealed class AlertService(GreenhouseDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<AlertOut>> ListAsync(bool activeOnly, int limit, CancellationToken ct)
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

        return await query
            .OrderByDescending(a => a.OpenedAt).ThenByDescending(a => a.Id)
            .Take(limit)
            .Select(a => new AlertOut(a.Id, a.Sensor.Name, a.Kind, a.TriggerValue, a.OpenedAt,
                a.ClosedAt, a.AcknowledgedAt, a.AcknowledgedBy))
            .ToListAsync(ct);
    }

    /// <summary>Позначає тривогу як побачену. Повторне підтвердження нічого не змінює.</summary>
    public async Task<AlertOut> AcknowledgeAsync(long id, string user, CancellationToken ct)
    {
        var alert = await db.Alerts.Include(a => a.Sensor).FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Тривогу {id} не знайдено");

        if (alert.AcknowledgedAt is null)
        {
            alert.AcknowledgedAt = clock.GetUtcNow().UtcDateTime;
            alert.AcknowledgedBy = user;
            await db.SaveChangesAsync(ct);
        }

        return new AlertOut(alert.Id, alert.Sensor.Name, alert.Kind, alert.TriggerValue, alert.OpenedAt,
            alert.ClosedAt, alert.AcknowledgedAt, alert.AcknowledgedBy);
    }
}
