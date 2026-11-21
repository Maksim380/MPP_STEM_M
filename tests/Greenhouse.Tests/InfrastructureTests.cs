using Greenhouse.Api.Data;
using Greenhouse.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Greenhouse.Tests;

public class DatabaseSchemaTests : IDisposable
{
    private readonly GreenhouseFactory _factory = new();

    public DatabaseSchemaTests() => _factory.CreateClient().Dispose(); // запуск застосунку застосовує міграції

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_factory.DbPath};Pooling=False");
        conn.Open();
        using var fk = conn.CreateCommand();
        fk.CommandText = "PRAGMA foreign_keys = ON";
        fk.ExecuteNonQuery();
        return conn;
    }

    [Fact]
    public void Indexes_exist_on_search_and_join_columns()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' AND name NOT LIKE 'sqlite_%'";
        var names = new List<string>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                names.Add(reader.GetString(0));
            }
        }

        Assert.Contains("IX_readings_sensor_id_recorded_at", names);
        Assert.Contains("IX_sensors_name", names);
        Assert.Contains("IX_alerts_sensor_id_closed_at", names);
    }

    [Theory]
    [InlineData("INSERT INTO readings (sensor_id, value, recorded_at) VALUES (999, 1, '2026-01-01')")] // FK
    [InlineData("INSERT INTO sensors (name, kind) VALUES ('temp-1', 'temperature')")] // UNIQUE
    [InlineData("INSERT INTO sensors (name, kind) VALUES ('co2-1', 'co2')")] // FK на тип
    [InlineData("INSERT INTO alerts (sensor_id, kind, trigger_value, opened_at) VALUES (1, 'medium', 1, '2026-01-01')")] // CHECK
    [InlineData("INSERT INTO sensor_types (kind, unit, valid_min, valid_max, comfort_min, comfort_max) VALUES ('x', 'u', 5, 1, 0, 0)")] // CHECK
    public void Integrity_constraints_reject_bad_rows(string sql)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
    }

    [Fact]
    public void No_pending_migrations()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GreenhouseDbContext>();
        Assert.Empty(db.Database.GetPendingMigrations());
    }
}

public class StartupTests
{
    [Fact]
    public void Missing_signing_key_stops_startup_with_clear_message()
    {
        using var factory = new GreenhouseFactory().WithWebHostBuilder(b => b.UseSetting("Auth:JwtSigningKey", "short"));
        var e = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("JwtSigningKey", e.ToString(), StringComparison.Ordinal);
    }
}

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task Unexpected_exception_becomes_500_without_details()
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        var logger = new CollectingLogger();
        var handler = new GlobalExceptionHandler(services.GetRequiredService<IProblemDetailsService>(), logger);

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("секретна деталь"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("секретна деталь", body, StringComparison.Ordinal);
        Assert.Contains(logger.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Error && e.Exception is not null);
    }

    private sealed class CollectingLogger : Microsoft.Extensions.Logging.ILogger<GlobalExceptionHandler>
    {
        public List<(Microsoft.Extensions.Logging.LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullLogger.Instance.BeginScope(state);

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception));
    }
}
