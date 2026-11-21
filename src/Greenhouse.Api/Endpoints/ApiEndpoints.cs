using System.Security.Claims;
using Greenhouse.Api.Auth;
using Greenhouse.Api.Contracts;
using Greenhouse.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Greenhouse.Api.Endpoints;

/// <summary>Маршрути REST API (Minimal API).</summary>
public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapGreenhouseApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }))
            .WithSummary("Перевірка, що сервер працює");

        api.MapGet("/sensors", (SensorService s, CancellationToken ct) => s.ListAsync(ct))
            .WithSummary("Список датчиків з одиницями та нормою");

        var readings = api.MapGroup("/readings").WithTags("Readings");

        readings.MapPost("/", async (ReadingIn reading, ReadingService s, CancellationToken ct) =>
            {
                var saved = await s.AddAsync([reading], ct);
                return TypedResults.Created($"/api/readings/{saved[0].Id}", saved[0]);
            })
            .RequireAuthorization(AuthNames.SensorPolicy)
            .WithSummary("Додати один вимір (потрібен заголовок X-Api-Key)");

        readings.MapPost("/batch", async (List<ReadingIn> batch, ReadingService s, CancellationToken ct) =>
                TypedResults.Created("/api/readings", await s.AddAsync(batch, ct)))
            .RequireAuthorization(AuthNames.SensorPolicy)
            .WithSummary("Додати пакет вимірів: зберігаються всі або жоден");

        readings.MapGet("/", (ReadingService s, CancellationToken ct, string? sensor = null,
                DateTimeOffset? from = null, DateTimeOffset? to = null, int limit = 100) =>
                s.ListAsync(sensor, from, to, limit, ct))
            .WithSummary("Останні виміри, новіші першими; фільтри sensor, from, to");

        readings.MapGet("/latest", (ReadingService s, CancellationToken ct) => s.LatestAsync(ct))
            .WithSummary("Останній вимір кожного датчика і його стан");

        var alerts = api.MapGroup("/alerts").WithTags("Alerts");

        alerts.MapGet("/", (AlertService s, CancellationToken ct, bool active = false, int limit = 50) =>
                s.ListAsync(active, limit, ct))
            .WithSummary("Журнал тривог; active=true — лише відкриті");

        alerts.MapPost("/{id:long}/ack", (long id, ClaimsPrincipal user, AlertService s, CancellationToken ct) =>
                s.AcknowledgeAsync(id, user.Identity?.Name ?? "operator", ct))
            .RequireAuthorization(AuthNames.OperatorPolicy)
            .WithSummary("Підтвердити тривогу (потрібен JWT оператора)");

        api.MapPost("/auth/token", Results<Ok<TokenResponse>, UnauthorizedHttpResult> (TokenRequest request, TokenService tokens) =>
                tokens.TryIssue(request) is { } token ? TypedResults.Ok(token) : TypedResults.Unauthorized())
            .WithTags("Auth")
            .WithSummary("Отримати JWT оператора за логіном і паролем");

        return app;
    }
}
