using Greenhouse.Core.Contracts;

namespace Greenhouse.Core.Services;

/// <summary>
/// Повідомлення клієнтів у реальному часі. Ядро не залежить від SignalR:
/// реалізацію (через IHubContext) надає веб-шар.
/// </summary>
public interface ITelemetryNotifier
{
    Task ReadingsAddedAsync(IReadOnlyList<ReadingOut> readings, CancellationToken ct);

    Task AlertsChangedAsync(IReadOnlyList<AlertOut> alerts, CancellationToken ct);
}

/// <summary>Порожня реалізація для консольних сценаріїв і тестів ядра.</summary>
public sealed class NullTelemetryNotifier : ITelemetryNotifier
{
    public Task ReadingsAddedAsync(IReadOnlyList<ReadingOut> readings, CancellationToken ct) => Task.CompletedTask;

    public Task AlertsChangedAsync(IReadOnlyList<AlertOut> alerts, CancellationToken ct) => Task.CompletedTask;
}
