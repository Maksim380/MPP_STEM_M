using System.Net;
using System.Net.Http.Json;
using Greenhouse.Core.Contracts;
using Greenhouse.Core.Domain;

namespace Greenhouse.Tests;

/// <summary>Теплиці, датчики і пороги норми.</summary>
public class GreenhouseTests : IDisposable
{
    private readonly GreenhouseFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Task<HttpResponseMessage> Post(HttpClient c, string url, object body) =>
        c.PostAsJsonAsync(url, body, GreenhouseFactory.Json);

    private static Task<HttpResponseMessage> Put(HttpClient c, string url, object body) =>
        c.PutAsJsonAsync(url, body, GreenhouseFactory.Json);

    [Fact]
    public async Task Seeded_greenhouse_has_sensors_and_default_thresholds()
    {
        using var anon = _factory.CreateClient();
        var g = Assert.Single(await anon.GetAsync<List<GreenhouseOut>>("/api/greenhouses"));
        Assert.Equal(["temp-1", "hum-1", "light-1"], g.Sensors);
        var temp = g.Thresholds.Single(t => t.Kind == SensorCatalog.Temperature);
        Assert.Equal((18, 28), (temp.ComfortMin, temp.ComfortMax));
        var single = await anon.GetAsync<GreenhouseOut>("/api/greenhouses/1");
        Assert.Equal((g.Name, g.Sensors.Count, g.Thresholds.Count), (single.Name, single.Sensors.Count, single.Thresholds.Count));
    }

    [Fact]
    public async Task Admin_creates_greenhouse_with_sensor_and_it_receives_data()
    {
        using var admin = await _factory.CreateAdminClientAsync();
        var created = await Post(admin, "/api/greenhouses", new { name = "  Теплиця №2 ", location = "Схід" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var g = (await created.Content.ReadFromJsonAsync<GreenhouseOut>(GreenhouseFactory.Json))!;
        Assert.Equal("Теплиця №2", g.Name);
        Assert.Equal(3, g.Thresholds.Count);

        var sensor = await Post(admin, $"/api/greenhouses/{g.Id}/sensors", new { name = "temp-2", kind = "temperature" });
        Assert.Equal(HttpStatusCode.Created, sensor.StatusCode);

        using var device = _factory.CreateSensorClient();
        await device.PostReadingAsync(new { sensor = "temp-2", value = 40 });

        var latest = await device.GetAsync<List<LatestReadingOut>>($"/api/readings/latest?greenhouse_id={g.Id}");
        Assert.Equal("high", Assert.Single(latest).Status);
        Assert.Single(await device.ReadingsAsync($"?greenhouse_id={g.Id}"));
        Assert.Empty(await device.ReadingsAsync("?greenhouse_id=1"));
        Assert.Single(await device.AlertsAsync($"?greenhouse_id={g.Id}"));
        Assert.Single(await device.GetAsync<List<SensorOut>>($"/api/sensors?greenhouse_id={g.Id}"));
    }

    [Fact]
    public async Task Greenhouse_validation_and_conflicts()
    {
        using var admin = await _factory.CreateAdminClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(admin, "/api/greenhouses", new { name = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(admin, "/api/greenhouses", new { name = "Теплиця №1" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Put(admin, "/api/greenhouses/99", new { name = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync(new Uri("/api/greenhouses/1", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync(new Uri("/api/greenhouses/99", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(new Uri("/api/greenhouses/99", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(new Uri("/api/readings?greenhouse_id=99", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(new Uri("/api/readings/latest?greenhouse_id=99", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(new Uri("/api/alerts?greenhouse_id=99", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(new Uri("/api/sensors?greenhouse_id=99", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Admin_renames_and_deletes_empty_greenhouse()
    {
        using var admin = await _factory.CreateAdminClientAsync();
        await Post(admin, "/api/greenhouses", new { name = "Тимчасова" });
        var id = (await admin.GetAsync<List<GreenhouseOut>>("/api/greenhouses")).Single(g => g.Name == "Тимчасова").Id;

        Assert.Equal(HttpStatusCode.Conflict, (await Put(admin, $"/api/greenhouses/{id}", new { name = "Теплиця №1" })).StatusCode);
        var renamed = await (await Put(admin, $"/api/greenhouses/{id}", new { name = "Нова", location = " " }))
            .Content.ReadFromJsonAsync<GreenhouseOut>(GreenhouseFactory.Json);
        Assert.Equal("Нова", renamed!.Name);
        Assert.Null(renamed.Location);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(new Uri($"/api/greenhouses/{id}", UriKind.Relative))).StatusCode);
        Assert.Single(await admin.GetAsync<List<GreenhouseOut>>("/api/greenhouses"));
    }

    [Theory]
    [InlineData("", "temperature", HttpStatusCode.BadRequest)]
    [InlineData("co2-1", "co2", HttpStatusCode.BadRequest)]
    [InlineData("temp-1", "temperature", HttpStatusCode.Conflict)]
    public async Task Invalid_new_sensor_is_rejected(string name, string kind, HttpStatusCode expected)
    {
        using var admin = await _factory.CreateAdminClientAsync();
        Assert.Equal(expected, (await Post(admin, "/api/greenhouses/1/sensors", new { name, kind })).StatusCode);
    }

    [Fact]
    public async Task Operator_changes_threshold_and_it_affects_status_and_alerts()
    {
        using var op = await _factory.CreateUserClientAsync(Roles.Operator);
        var response = await Put(op, "/api/greenhouses/1/thresholds/temperature", new { comfort_min = 10, comfort_max = 15 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var device = _factory.CreateSensorClient();
        await device.PostReadingAsync(new { sensor = "temp-1", value = 20 });

        Assert.Equal("high", (await device.LatestAsync())["temp-1"].Status);
        Assert.Equal("high", Assert.Single(await device.AlertsAsync()).Kind);
        Assert.Equal(15, (await device.GetAsync<List<SensorOut>>("/api/sensors")).Single(s => s.Name == "temp-1").ComfortMax);
    }

    [Theory]
    [InlineData(20, 10)] // min > max
    [InlineData(-50, 10)] // нижче фізичного мінімуму
    [InlineData(10, 90)] // вище фізичного максимуму
    public async Task Invalid_threshold_is_rejected(double min, double max)
    {
        using var op = await _factory.CreateUserClientAsync(Roles.Operator);
        var response = await Put(op, "/api/greenhouses/1/thresholds/temperature", new { comfort_min = min, comfort_max = max });
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Threshold_requires_both_bounds_and_known_kind()
    {
        using var op = await _factory.CreateUserClientAsync(Roles.Operator);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(op, "/api/greenhouses/1/thresholds/temperature", new { comfort_min = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Put(op, "/api/greenhouses/1/thresholds/co2", new { comfort_min = 1, comfort_max = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Put(op, "/api/greenhouses/9/thresholds/light", new { comfort_min = 1, comfort_max = 2 })).StatusCode);
    }
}
