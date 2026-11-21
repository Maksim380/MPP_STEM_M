using System.Net;
using Greenhouse.Core.Domain;
using Greenhouse.Simulator;

namespace Greenhouse.Tests;

public class SignalGeneratorTests
{
    [Fact]
    public void Values_stay_within_physical_ranges_for_whole_day()
    {
        var gen = new SignalGenerator(new Random(1));
        for (var hour = 0.0; hour < 24; hour += 0.25)
        {
            foreach (var t in SensorCatalog.Types)
            {
                Assert.True(t.IsPhysicallyValid(gen.Generate(t.Kind, hour)), $"{t.Kind} о {hour}");
            }
        }
    }

    [Fact]
    public void Day_is_warmer_and_brighter_than_night()
    {
        var gen = new SignalGenerator(new Random(2));
        Assert.True(gen.Generate(SensorCatalog.Temperature, 15) > gen.Generate(SensorCatalog.Temperature, 3));
        Assert.Equal(0, gen.Generate(SensorCatalog.Light, 2));
        Assert.True(gen.Generate(SensorCatalog.Light, 13) > 20_000);
    }

    [Fact]
    public void Same_seed_gives_same_batch()
    {
        var a = new SignalGenerator(new Random(42)).MakeBatch(SensorCatalog.DefaultSensors, 10, 0.5);
        var b = new SignalGenerator(new Random(42)).MakeBatch(SensorCatalog.DefaultSensors, 10, 0.5);
        Assert.Equal(a, b);
        Assert.Equal(3, a.Count);
    }

    [Fact]
    public void Anomalies_keep_humidity_physical()
    {
        var gen = new SignalGenerator(new Random(3));
        for (var i = 0; i < 200; i++)
        {
            var batch = gen.MakeBatch(SensorCatalog.DefaultSensors, 4, anomalyRate: 1);
            Assert.InRange(batch.Single(r => r.Sensor == "hum-1").Value, 0, 100);
        }
    }

    [Fact]
    public void Unknown_kind_throws() =>
        Assert.Throws<ArgumentException>(() => new SignalGenerator(new Random()).Generate("co2", 12));
}

public class SimulatorOptionsTests
{
    [Fact]
    public void Defaults_when_no_args()
    {
        var o = SimulatorOptions.Parse([]);
        Assert.Equal(new Uri("http://localhost:5000"), o.Api);
        Assert.Null(o.Count);
    }

    [Fact]
    public void Parses_all_options()
    {
        var o = SimulatorOptions.Parse(["--api", "https://example.org", "--api-key", "k", "--interval", "0.5",
            "--speed", "60", "--anomaly-rate", "0.1", "--seed", "7", "--count", "3"]);
        Assert.Equal("https://example.org/", o.Api.ToString());
        Assert.Equal("k", o.ApiKey);
        Assert.Equal(0.5, o.IntervalSeconds);
        Assert.Equal(60, o.Speed);
        Assert.Equal(0.1, o.AnomalyRate);
        Assert.Equal(7, o.Seed);
        Assert.Equal(3, o.Count);
    }

    [Fact]
    public void Help_flag() => Assert.True(SimulatorOptions.Parse(["--help"]).ShowHelp);

    [Theory]
    [InlineData("--api", "ftp://x")]
    [InlineData("--api", "not a url")]
    [InlineData("--interval", "-1")]
    [InlineData("--anomaly-rate", "2")]
    [InlineData("--speed", "abc")]
    [InlineData("--count", "0")]
    [InlineData("--unknown", "1")]
    public void Rejects_invalid_values(string name, string value) =>
        Assert.Throws<ArgumentException>(() => SimulatorOptions.Parse([name, value]));

    [Fact]
    public void Rejects_missing_value() =>
        Assert.Throws<ArgumentException>(() => SimulatorOptions.Parse(["--count"]));
}

public class SimulatorRunnerTests
{
    private static SimulatorOptions Options(int count) =>
        new() { IntervalSeconds = 0, Count = count, Seed = 1, ApiKey = "k" };

    [Fact]
    public async Task Sends_requested_number_of_batches_with_api_key()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Created));
        using var http = new HttpClient(handler);
        using var output = new StringWriter();

        var accepted = await new SimulatorRunner(http, Options(3), output, TimeProvider.System).RunAsync(CancellationToken.None);

        Assert.Equal(3, accepted);
        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, r =>
        {
            Assert.Equal("/api/readings/batch", r.RequestUri!.AbsolutePath);
            Assert.Equal("k", r.Headers.GetValues("X-Api-Key").Single());
        });
        Assert.Contains("201", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_rejected_batch_and_continues()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.UnprocessableEntity) { Content = new StringContent("bad") }));
        using var output = new StringWriter();

        var accepted = await new SimulatorRunner(http, Options(2), output, TimeProvider.System).RunAsync(CancellationToken.None);

        Assert.Equal(0, accepted);
        Assert.Contains("відхилив пакет: 422 bad", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Survives_unavailable_backend()
    {
        using var http = new HttpClient(new StubHandler(_ => throw new HttpRequestException("connection refused")));
        using var output = new StringWriter();

        var accepted = await new SimulatorRunner(http, Options(2), output, TimeProvider.System).RunAsync(CancellationToken.None);

        Assert.Equal(0, accepted);
        Assert.Contains("Бекенд недоступний", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Survives_timeout()
    {
        using var http = new HttpClient(new StubHandler(_ => throw new TaskCanceledException("timeout")));
        using var output = new StringWriter();

        await new SimulatorRunner(http, Options(1), output, TimeProvider.System).RunAsync(CancellationToken.None);

        Assert.Contains("не відповів вчасно", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stops_on_cancellation()
    {
        using var cts = new CancellationTokenSource();
        var handler = new StubHandler(_ =>
        {
            cts.Cancel();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using var http = new HttpClient(handler);
        var options = new SimulatorOptions { IntervalSeconds = 60, Seed = 1 };

        // Без Count цикл безкінечний; скасування має завершити його без винятку і без 60 с очікування.
        await new SimulatorRunner(http, options, TextWriter.Null, TimeProvider.System).RunAsync(cts.Token);

        Assert.Single(handler.Requests);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
