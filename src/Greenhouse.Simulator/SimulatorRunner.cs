using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Greenhouse.Domain;

namespace Greenhouse.Simulator;

/// <summary>Цикл симулятора: генерує пакет, надсилає в API, чекає інтервал.</summary>
public sealed class SimulatorRunner(HttpClient http, SimulatorOptions options, TextWriter output, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>Повертає кількість пакетів, які API прийняв.</summary>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var rng = options.Seed is { } seed ? new Random(seed) : new Random();
        var generator = new SignalGenerator(rng);
        var start = clock.GetUtcNow();
        var startHour = start.Hour + start.Minute / 60.0;
        var sent = 0;
        var accepted = 0;

        while (!ct.IsCancellationRequested && (options.Count is null || sent < options.Count))
        {
            var simHours = (clock.GetUtcNow() - start).TotalHours * options.Speed;
            var hour = (startHour + simHours) % 24;
            var batch = generator.MakeBatch(SensorCatalog.DefaultSensors, hour, options.AnomalyRate);

            if (await SendAsync(batch, hour, ct))
            {
                accepted++;
            }

            sent++;
            if (options.Count is null || sent < options.Count)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(options.IntervalSeconds), clock, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        return accepted;
    }

    private async Task<bool> SendAsync(IReadOnlyList<SimulatedReading> batch, double hour, CancellationToken ct)
    {
        var values = string.Join(", ", batch.Select(b => string.Create(CultureInfo.InvariantCulture, $"{b.Sensor}={b.Value}")));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(options.Api, "/api/readings/batch"))
            {
                Content = JsonContent.Create(batch, options: Json),
            };
            request.Headers.Add("X-Api-Key", options.ApiKey);
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"[{hour:00.00} год] {(int)response.StatusCode}: {values}"));
                return true;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            await output.WriteLineAsync($"Бекенд відхилив пакет: {(int)response.StatusCode} {body}");
        }
        catch (HttpRequestException e)
        {
            await output.WriteLineAsync($"Бекенд недоступний ({e.Message}), повтор через {options.IntervalSeconds} с");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            await output.WriteLineAsync("Бекенд не відповів вчасно, повтор");
        }

        return false;
    }
}
