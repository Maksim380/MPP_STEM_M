using System.Globalization;

namespace Greenhouse.Simulator;

/// <summary>Параметри командного рядка симулятора.</summary>
public sealed record SimulatorOptions
{
    public Uri Api { get; init; } = new("http://localhost:5000");
    public string ApiKey { get; init; } = Environment.GetEnvironmentVariable("GREENHOUSE_API_KEY") ?? "dev-sensor-key";
    public double IntervalSeconds { get; init; } = 2;
    public double Speed { get; init; } = 600;
    public double AnomalyRate { get; init; } = 0.02;
    public int? Seed { get; init; }
    public int? Count { get; init; }
    public bool ShowHelp { get; init; }

    public const string Usage = """
        Симулятор датчиків теплиці
          --api <url>            адреса бекенда (http://localhost:5000)
          --api-key <ключ>       ключ датчика, або змінна GREENHOUSE_API_KEY
          --interval <с>         секунд між пакетами (2)
          --speed <k>            прискорення часу, 600 = доба за 2,4 хв (600)
          --anomaly-rate <0..1>  імовірність аномального показу (0.02)
          --seed <n>             зерно генератора для відтворюваності
          --count <n>            скільки пакетів надіслати (безкінечно)
        """;

    /// <summary>Розбирає аргументи. Некоректне значення дає ArgumentException з поясненням.</summary>
    public static SimulatorOptions Parse(IReadOnlyList<string> args)
    {
        var o = new SimulatorOptions();
        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i];
            if (name is "-h" or "--help")
            {
                return o with { ShowHelp = true };
            }

            if (i + 1 >= args.Count)
            {
                throw new ArgumentException($"Для {name} не вказано значення");
            }

            var value = args[++i];
            o = name switch
            {
                "--api" => o with { Api = ParseUrl(value) },
                "--api-key" => o with { ApiKey = value },
                "--interval" => o with { IntervalSeconds = ParseDouble(name, value, 0, 3600) },
                "--speed" => o with { Speed = ParseDouble(name, value, 0, 1_000_000) },
                "--anomaly-rate" => o with { AnomalyRate = ParseDouble(name, value, 0, 1) },
                "--seed" => o with { Seed = ParseInt(name, value, int.MinValue) },
                "--count" => o with { Count = ParseInt(name, value, 1) },
                _ => throw new ArgumentException($"Невідомий параметр: {name}"),
            };
        }

        return o;
    }

    private static Uri ParseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"Адреса бекенда має починатися з http:// або https://: {value}");
        }

        return uri;
    }

    private static double ParseDouble(string name, string value, double min, double max)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || d < min || d > max)
        {
            throw new ArgumentException($"{name}: очікується число від {min} до {max}, отримано «{value}»");
        }

        return d;
    }

    private static int ParseInt(string name, string value, int min)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < min)
        {
            throw new ArgumentException($"{name}: очікується ціле число ≥ {min}, отримано «{value}»");
        }

        return n;
    }
}
