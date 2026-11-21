using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Greenhouse.Core.Data;

/// <summary>Вибір СУБД через конфігурацію: Database:Provider = Sqlite | Postgres.</summary>
public static class DatabaseRegistration
{
    public const string Sqlite = "Sqlite";
    public const string Postgres = "Postgres";

    public static IServiceCollection AddGreenhouseDatabase(this IServiceCollection services, string? provider, string connectionString)
    {
        switch (provider ?? Sqlite)
        {
            case var p when p.Equals(Sqlite, StringComparison.OrdinalIgnoreCase):
                services.AddDbContext<GreenhouseDbContext, SqliteGreenhouseDbContext>(o => o.UseSqlite(connectionString));
                break;
            case var p when p.Equals(Postgres, StringComparison.OrdinalIgnoreCase):
                services.AddDbContext<GreenhouseDbContext, PostgresGreenhouseDbContext>(o =>
                    o.UseNpgsql(connectionString, n => n.EnableRetryOnFailure()));
                break;
            default:
                throw new InvalidOperationException($"Невідомий Database:Provider «{provider}». Допустимо: {Sqlite}, {Postgres}.");
        }

        return services;
    }
}
