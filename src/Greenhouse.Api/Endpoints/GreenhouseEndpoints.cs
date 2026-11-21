using Greenhouse.Api.Auth;
using Greenhouse.Core.Contracts;
using Greenhouse.Core.Services;

namespace Greenhouse.Api.Endpoints;

/// <summary>/api/greenhouses і /api/sensors: теплиці, датчики, пороги норми.</summary>
public static class GreenhouseEndpoints
{
    public static RouteGroupBuilder MapGreenhouseEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/greenhouses").WithTags("Greenhouses");

        g.MapGet("/", (GreenhouseService s, CancellationToken ct) => s.ListAsync(ct))
            .WithSummary("Теплиці з датчиками і порогами");

        g.MapGet("/{id:int}", (int id, GreenhouseService s, CancellationToken ct) => s.GetAsync(id, ct))
            .WithSummary("Одна теплиця");

        g.MapPost("/", async (GreenhouseIn input, GreenhouseService s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(input, ct);
                return TypedResults.Created($"/api/greenhouses/{created.Id}", created);
            })
            .RequireAuthorization(AuthNames.AdminPolicy)
            .WithSummary("Створити теплицю (адміністратор)");

        g.MapPut("/{id:int}", (int id, GreenhouseIn input, GreenhouseService s, CancellationToken ct) => s.UpdateAsync(id, input, ct))
            .RequireAuthorization(AuthNames.AdminPolicy)
            .WithSummary("Змінити назву чи розташування (адміністратор)");

        g.MapDelete("/{id:int}", async (int id, GreenhouseService s, CancellationToken ct) =>
            {
                await s.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(AuthNames.AdminPolicy)
            .WithSummary("Видалити теплицю без датчиків (адміністратор)");

        g.MapPost("/{id:int}/sensors", async (int id, NewSensorIn input, GreenhouseService s, CancellationToken ct) =>
            {
                var sensor = await s.AddSensorAsync(id, input, ct);
                return TypedResults.Created($"/api/sensors?greenhouse_id={id}", sensor);
            })
            .RequireAuthorization(AuthNames.AdminPolicy)
            .WithSummary("Додати датчик у теплицю (адміністратор)");

        g.MapPut("/{id:int}/thresholds/{kind}", (int id, string kind, ThresholdIn input, GreenhouseService s, CancellationToken ct) =>
                s.SetThresholdAsync(id, kind, input, ct))
            .RequireAuthorization(AuthNames.OperatorPolicy)
            .WithSummary("Змінити норму для типу датчика (оператор або адміністратор)");

        api.MapGet("/sensors", (GreenhouseService s, CancellationToken ct, int? greenhouse_id = null) =>
                s.ListSensorsAsync(greenhouse_id, ct))
            .WithTags("Greenhouses")
            .WithSummary("Датчики з одиницями та нормою; фільтр greenhouse_id");

        return api;
    }
}
