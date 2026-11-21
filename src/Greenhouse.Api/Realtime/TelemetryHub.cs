using Greenhouse.Core.Contracts;
using Greenhouse.Core.Services;
using Microsoft.AspNetCore.SignalR;

namespace Greenhouse.Api.Realtime;

/// <summary>
/// SignalR-хаб /hubs/telemetry. Клієнт може підписатися на одну теплицю (група greenhouse-{id})
/// або отримувати всі події (група all).
/// </summary>
public sealed class TelemetryHub : Hub
{
    public const string Path = "/hubs/telemetry";
    public const string AllGroup = "all";

    public static string GroupFor(int greenhouseId) => $"greenhouse-{greenhouseId}";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AllGroup);
        await base.OnConnectedAsync();
    }

    /// <summary>Отримувати лише події однієї теплиці.</summary>
    public async Task Subscribe(int greenhouseId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AllGroup);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(greenhouseId));
    }
}

/// <summary>Надсилає нові покази і зміни тривог підключеним клієнтам.</summary>
public sealed class SignalRTelemetryNotifier(IHubContext<TelemetryHub> hub) : ITelemetryNotifier
{
    public const string ReadingsEvent = "readingsAdded";
    public const string AlertsEvent = "alertsChanged";

    public Task ReadingsAddedAsync(IReadOnlyList<ReadingOut> readings, CancellationToken ct) =>
        BroadcastAsync(ReadingsEvent, readings, r => r.GreenhouseId, ct);

    public Task AlertsChangedAsync(IReadOnlyList<AlertOut> alerts, CancellationToken ct) =>
        BroadcastAsync(AlertsEvent, alerts, a => a.GreenhouseId, ct);

    private async Task BroadcastAsync<T>(string method, IReadOnlyList<T> items, Func<T, int> greenhouse, CancellationToken ct)
    {
        await hub.Clients.Group(TelemetryHub.AllGroup).SendAsync(method, items, ct);
        foreach (var group in items.GroupBy(greenhouse))
        {
            await hub.Clients.Group(TelemetryHub.GroupFor(group.Key)).SendAsync(method, group.ToList(), ct);
        }
    }
}
