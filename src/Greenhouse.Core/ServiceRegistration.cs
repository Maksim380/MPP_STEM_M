using Greenhouse.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Greenhouse.Core;

public static class ServiceRegistration
{
    /// <summary>Реєструє сервіси ядра. Веб-шар може підмінити <see cref="ITelemetryNotifier"/> на SignalR.</summary>
    public static IServiceCollection AddGreenhouseCore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ITelemetryNotifier, NullTelemetryNotifier>();
        services.AddScoped<ReadingService>();
        services.AddScoped<AlertService>();
        services.AddScoped<GreenhouseService>();
        services.AddScoped<UserService>();
        return services;
    }
}
