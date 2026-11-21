using System.Net;
using Greenhouse.Core.Contracts;

namespace Greenhouse.Tests;

/// <summary>Приймальні сценарії REST API (див. docs/test-cases.md).</summary>
public class ApiTests : IDisposable
{
    private readonly GreenhouseFactory _factory = new();
    private readonly HttpClient _sensor;
    private readonly HttpClient _anon;

    public ApiTests()
    {
        _sensor = _factory.CreateSensorClient();
        _anon = _factory.CreateClient();
    }

    public void Dispose()
    {
        _sensor.Dispose();
        _anon.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Health_is_ok()
    {
        var response = await _anon.GetAsync(new Uri("/api/health", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Default_sensors_are_seeded_by_migration()
    {
        var sensors = await _anon.GetAsync<List<SensorOut>>("/api/sensors");
        Assert.Equal(["temp-1", "hum-1", "light-1"], sensors.Select(s => s.Name));
        Assert.Equal("°C", sensors[0].Unit);
    }

    [Fact]
    public async Task Dashboard_and_openapi_are_served()
    {
        Assert.Equal(HttpStatusCode.OK, (await _anon.GetAsync(new Uri("/", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _anon.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Add_and_list_reading()
    {
        var response = await _sensor.PostReadingAsync(new { sensor = "temp-1", value = 23.4 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var listed = await _anon.ReadingsAsync("?sensor=temp-1");
        Assert.Equal([23.4], listed.Select(r => r.Value));
        Assert.Equal(DateTimeKind.Utc, listed[0].RecordedAt.Kind);
    }

    [Fact]
    public async Task Latest_classifies_status_and_handles_sensor_without_data()
    {
        await _sensor.PostReadingAsync(new { sensor = "temp-1", value = 35 });
        await _sensor.PostReadingAsync(new { sensor = "hum-1", value = 30 });

        var latest = await _anon.LatestAsync();

        Assert.Equal("high", latest["temp-1"].Status);
        Assert.Equal("low", latest["hum-1"].Status);
        Assert.Equal("no_data", latest["light-1"].Status);
        Assert.Null(latest["light-1"].Value);
    }

    [Fact]
    public async Task Latest_picks_newest_by_time_not_by_insert_order()
    {
        await _sensor.PostReadingAsync(new { sensor = "temp-1", value = 20, recorded_at = "2026-01-01T12:00:00Z" });
        await _sensor.PostReadingAsync(new { sensor = "temp-1", value = 25, recorded_at = "2026-01-01T10:00:00Z" });

        Assert.Equal(20, (await _anon.LatestAsync())["temp-1"].Value);
    }

    [Fact]
    public async Task Timezone_is_normalized_to_utc()
    {
        await _sensor.PostReadingAsync(new { sensor = "temp-1", value = 20, recorded_at = "2026-01-01T14:00:00+02:00" });

        var r = (await _anon.ReadingsAsync()).Single();
        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), r.RecordedAt);
    }

    [Fact]
    public async Task History_can_be_filtered_by_period()
    {
        await _sensor.PostBatchAsync(new[]
        {
            new { sensor = "temp-1", value = 20.0, recorded_at = "2026-01-01T10:00:00Z" },
            new { sensor = "temp-1", value = 21.0, recorded_at = "2026-01-02T10:00:00Z" },
            new { sensor = "temp-1", value = 22.0, recorded_at = "2026-01-03T10:00:00Z" },
        });

        var mid = await _anon.ReadingsAsync("?from=2026-01-02T00:00:00Z&to=2026-01-02T23:59:59Z");
        Assert.Equal([21.0], mid.Select(r => r.Value));

        var limited = await _anon.ReadingsAsync("?limit=2");
        Assert.Equal([22.0, 21.0], limited.Select(r => r.Value));
    }

    [Fact]
    public async Task Batch_is_saved_atomically()
    {
        var response = await _sensor.PostBatchAsync(new object[]
        {
            new { sensor = "temp-1", value = 22 },
            new { sensor = "hum-1", value = 101 }, // некоректний
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(await _anon.ReadingsAsync());
    }

    [Fact]
    public async Task Valid_batch_is_saved()
    {
        var response = await _sensor.PostBatchAsync(new object[]
        {
            new { sensor = "temp-1", value = 22 },
            new { sensor = "hum-1", value = 60 },
            new { sensor = "light-1", value = 30000 },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(3, (await _anon.ReadingsAsync()).Count);
    }

    // --- Крайні випадки --------------------------------------------------------

    [Fact]
    public async Task Unknown_sensor_is_404_and_nothing_saved()
    {
        var response = await _sensor.PostReadingAsync(new { sensor = "nope", value = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await _anon.ReadingsAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await _anon.GetAsync(new Uri("/api/readings?sensor=nope", UriKind.Relative))).StatusCode);
    }

    [Theory]
    [InlineData("hum-1", 101)]
    [InlineData("hum-1", -0.1)]
    [InlineData("temp-1", -41)]
    [InlineData("temp-1", 81)]
    [InlineData("light-1", -1)]
    public async Task Value_outside_physical_range_is_422(string sensor, double value)
    {
        var response = await _sensor.PostReadingAsync(new { sensor, value });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Theory]
    [InlineData("hum-1", 0)]
    [InlineData("hum-1", 100)]
    [InlineData("temp-1", -40)]
    [InlineData("temp-1", 80)]
    [InlineData("light-1", 150000)]
    public async Task Boundary_values_are_accepted(string sensor, double value)
    {
        var response = await _sensor.PostReadingAsync(new { sensor, value });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("""{"sensor":"temp-1","value":"abc"}""")]
    [InlineData("""{"sensor":"temp-1","value":NaN}""")]
    [InlineData("{\"sensor\":\"temp-1\",")]
    [InlineData("""not json""")]
    [InlineData("""{"sensor":"temp-1","value":20,"recorded_at":"вчора"}""")]
    public async Task Malformed_body_is_400(string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _sensor.PostAsync(new Uri("/api/readings", UriKind.Relative), content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Infinite_value_is_422()
    {
        using var content = new StringContent("""{"sensor":"temp-1","value":1e999}""", System.Text.Encoding.UTF8, "application/json");
        var response = await _sensor.PostAsync(new Uri("/api/readings", UriKind.Relative), content);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Missing_fields_are_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _sensor.PostReadingAsync(new { sensor = "temp-1" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _sensor.PostReadingAsync(new { value = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _sensor.PostReadingAsync(new { sensor = "  ", value = 1 })).StatusCode);
    }

    [Fact]
    public async Task Empty_and_oversized_batches_are_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _sensor.PostBatchAsync(Array.Empty<object>())).StatusCode);
        var huge = Enumerable.Range(0, 1001).Select(_ => new { sensor = "temp-1", value = 20 }).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await _sensor.PostBatchAsync(huge)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _sensor.PostBatchAsync(new object?[] { null })).StatusCode);
    }

    [Fact]
    public async Task Future_timestamp_is_422()
    {
        var future = DateTimeOffset.UtcNow.AddHours(1);
        var response = await _sensor.PostReadingAsync(new { sensor = "temp-1", value = 20, recorded_at = future });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=1001")]
    [InlineData("?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    public async Task Invalid_query_is_400(string query)
    {
        var response = await _anon.GetAsync(new Uri("/api/readings" + query, UriKind.Relative));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sql_injection_in_sensor_name_is_harmless()
    {
        var response = await _sensor.PostReadingAsync(new { sensor = "temp-1'; DROP TABLE readings; --", value = 20 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var query = await _anon.GetAsync(new Uri("/api/readings?sensor=" + Uri.EscapeDataString("x' OR '1'='1"), UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, query.StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await _sensor.PostReadingAsync(new { sensor = "temp-1", value = 20 })).StatusCode);
    }

    [Fact]
    public async Task Errors_are_problem_details_and_logged_to_file()
    {
        var response = await _sensor.PostReadingAsync(new { sensor = "hum-1", value = 101 });
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("поза фізичним діапазоном", body, StringComparison.Ordinal);

        var log = await ReadLogAsync("відхилено");
        Assert.Contains("відхилено", log, StringComparison.Ordinal);
        Assert.DoesNotContain("[ERR]", log, StringComparison.Ordinal);
    }

    /// <summary>Читає лог-файл; чекає до 5 с, поки в ньому з'явиться очікуваний рядок (запис на диск може відставати).</summary>
    private async Task<string> ReadLogAsync(string expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            var text = new System.Text.StringBuilder();
            foreach (var file in Directory.GetFiles(_factory.LogDir, "*.log"))
            {
                await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                text.Append(await reader.ReadToEndAsync());
            }

            if (text.ToString().Contains(expected, StringComparison.Ordinal) || DateTime.UtcNow > deadline)
            {
                return text.ToString();
            }

            await Task.Delay(100);
        }
    }
}
