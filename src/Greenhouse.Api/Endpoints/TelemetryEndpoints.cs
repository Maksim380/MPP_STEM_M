using System.Security.Claims;
using Greenhouse.Api.Auth;
using Greenhouse.Core.Contracts;
using Greenhouse.Core.Services;

namespace Greenhouse.Api.Endpoints;

/// <summary>/api/readings і /api/alerts: телеметрія і тривоги.</summary>
public static class TelemetryEndpoints
{
    public static RouteGroupBuilder MapTelemetryEndpoints(this RouteGroupBuilder api)
    {
        var readings = api.MapGroup("/readings").WithTags("Telemetry");

        readings.MapPost("/", async (ReadingIn reading, ReadingService s, CancellationToken ct) =>
            {
                var saved = await s.AddAsync([reading], ct);
                return TypedResults.Created($"/api/readings/{saved[0].Id}", saved[0]);
            })
            .RequireAuthorization(AuthNames.SensorPolicy)
            .WithSummary("Додати один вимір (заголовок X-Api-Key)");

        readings.MapPost("/batch", async (List<ReadingIn?> batch, ReadingService s, CancellationToken ct) =>
                TypedResults.Created("/api/readings", await s.AddAsync(batch, ct)))
            .RequireAuthorization(AuthNames.SensorPolicy)
            .WithSummary("Додати пакет вимірів: зберігаються всі або жоден");

        readings.MapGet("/", (ReadingService s, CancellationToken ct, string? sensor = null, int? greenhouse_id = null,
                DateTimeOffset? from = null, DateTimeOffset? to = null, int limit = 100) =>
                s.ListAsync(sensor, greenhouse_id, from, to, limit, ct))
            .WithSummary("Історія вимірів, новіші першими; фільтри sensor, greenhouse_id, from, to");

        readings.MapGet("/latest", (ReadingService s, CancellationToken ct, int? greenhouse_id = null) => s.LatestAsync(greenhouse_id, ct))
            .WithSummary("Останній вимір кожного датчика і його стан");

        var alerts = api.MapGroup("/alerts").WithTags("Alerts");

        alerts.MapGet("/", (AlertService s, CancellationToken ct, bool active = false, int? greenhouse_id = null, int limit = 50) =>
                s.ListAsync(active, greenhouse_id, limit, ct))
            .WithSummary("Журнал тривог; active=true — лише відкриті");

        alerts.MapPost("/{id:long}/ack", (long id, ClaimsPrincipal user, AlertService s, CancellationToken ct) =>
                s.AcknowledgeAsync(id, user.Identity?.Name ?? "operator", ct))
            .RequireAuthorization(AuthNames.OperatorPolicy)
            .WithSummary("Підтвердити тривогу (оператор або адміністратор)");

        return api;
    }
}
