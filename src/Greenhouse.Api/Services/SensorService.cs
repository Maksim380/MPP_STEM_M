using Greenhouse.Api.Contracts;
using Greenhouse.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Greenhouse.Api.Services;

/// <summary>Довідник датчиків.</summary>
public sealed class SensorService(GreenhouseDbContext db)
{
    public async Task<IReadOnlyList<SensorOut>> ListAsync(CancellationToken ct) =>
        await db.Sensors.AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => new SensorOut(s.Name, s.Kind, s.Type.Unit, s.Type.ComfortMin, s.Type.ComfortMax))
            .ToListAsync(ct);
}
