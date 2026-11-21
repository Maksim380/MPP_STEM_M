using Greenhouse.Api.Background;
using Greenhouse.Api.Realtime;
using Greenhouse.Core.Contracts;
using Greenhouse.Core.Data;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Greenhouse.Tests;

public class RealtimeTests : IDisposable
{
    private readonly GreenhouseFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private HubConnection Connect() =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, TelemetryHub.Path), o =>
            {
                o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                o.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions = GreenhouseFactory.Json)
            .Build();

    [Fact]
    public async Task Clients_receive_new_readings_and_alerts()
    {
        _factory.CreateClient().Dispose();
        await using var hub = Connect();
        var readings = new TaskCompletionSource<List<ReadingOut>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var alerts = new TaskCompletionSource<List<AlertOut>>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<List<ReadingOut>>(SignalRTelemetryNotifier.ReadingsEvent, r => readings.TrySetResult(r));
        hub.On<List<AlertOut>>(SignalRTelemetryNotifier.AlertsEvent, a => alerts.TrySetResult(a));
        await hub.StartAsync();

        using var device = _factory.CreateSensorClient();
        await device.PostReadingAsync(new { sensor = "temp-1", value = 35 });

        var r = await readings.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("temp-1", Assert.Single(r).Sensor);
        var a = await alerts.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("high", Assert.Single(a).Kind);
    }

    [Fact]
    public async Task Subscriber_of_other_greenhouse_gets_nothing()
    {
        _factory.CreateClient().Dispose();
        await using var hub = Connect();
        var received = 0;
        hub.On<List<ReadingOut>>(SignalRTelemetryNotifier.ReadingsEvent, _ => Interlocked.Increment(ref received));
        await hub.StartAsync();
        await hub.InvokeAsync("Subscribe", 2);

        using var device = _factory.CreateSensorClient();
        await device.PostReadingAsync(new { sensor = "temp-1", value = 22 });
        await Task.Delay(500);

        Assert.Equal(0, received);
    }
}

public class SimulatorWorkerTests : IDisposable
{
    private readonly GreenhouseFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Tick_writes_reading_for_every_sensor()
    {
        _factory.CreateClient().Dispose();
        var worker = new SimulatorWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SimulatorOptions()),
            TimeProvider.System,
            NullLogger<SimulatorWorker>.Instance);

        await worker.TickAsync(hour: 12, anomalyRate: 0, CancellationToken.None);

        using var client = _factory.CreateClient();
        var readings = await client.ReadingsAsync();
        Assert.Equal(["hum-1", "light-1", "temp-1"], readings.Select(r => r.Sensor).Order());
    }

    [Fact]
    public async Task Enabled_worker_runs_in_background()
    {
        using var factory = _factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Simulator:Enabled", "true");
            b.UseSetting("Simulator:IntervalSeconds", "0.1");
        });
        using var client = factory.CreateClient();
        Assert.Contains(factory.Services.GetServices<IHostedService>(), s => s is SimulatorWorker);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while ((await client.ReadingsAsync()).Count < 6 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.True((await client.ReadingsAsync()).Count >= 6);
    }

    [Fact]
    public async Task Tick_without_sensors_does_nothing()
    {
        _factory.CreateClient().Dispose();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GreenhouseDbContext>();
            db.Sensors.RemoveRange(db.Sensors);
            await db.SaveChangesAsync();
        }

        var worker = new SimulatorWorker(_factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SimulatorOptions()), TimeProvider.System, NullLogger<SimulatorWorker>.Instance);
        await worker.TickAsync(12, 0, CancellationToken.None);

        using var client = _factory.CreateClient();
        Assert.Empty(await client.ReadingsAsync());
    }
}

public class DatabaseRegistrationTests
{
    [Theory]
    [InlineData("Sqlite", typeof(SqliteGreenhouseDbContext))]
    [InlineData("postgres", typeof(PostgresGreenhouseDbContext))]
    [InlineData(null, typeof(SqliteGreenhouseDbContext))]
    public void Provider_selects_context(string? provider, Type expected)
    {
        using var sp = new ServiceCollection()
            .AddGreenhouseDatabase(provider, provider?.StartsWith('p') == true ? "Host=localhost" : "Data Source=:memory:")
            .BuildServiceProvider();
        using var scope = sp.CreateScope();
        Assert.IsType(expected, scope.ServiceProvider.GetRequiredService<GreenhouseDbContext>());
    }

    [Fact]
    public void Unknown_provider_throws() =>
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddGreenhouseDatabase("Oracle", "x"));

    [Fact]
    public void Design_time_factories_create_contexts()
    {
        using var sqlite = new SqliteDesignTimeFactory().CreateDbContext([]);
        using var pg = new PostgresDesignTimeFactory().CreateDbContext([]);
        Assert.NotNull(sqlite.Model.FindEntityType(typeof(Reading)));
        Assert.NotNull(pg.Model.FindEntityType(typeof(Reading)));
    }
}
