using Greenhouse.Core.Domain;

namespace Greenhouse.Core.Domain;

/// <summary>Один показ для надсилання в API.</summary>
public sealed record SimulatedReading(string Sensor, double Value);

/// <summary>
/// Генерує правдоподібні покази з добовим циклом: вдень тепліше, світліше і сухіше.
/// </summary>
public sealed class SignalGenerator(Random rng)
{
    /// <summary>Від -1 (найхолодніша пора, ~03:00) до 1 (найтепліша, ~15:00).</summary>
    public static double DayPhase(double hour) => Math.Sin((hour - 9.0) / 24.0 * 2 * Math.PI);

    /// <summary>Значення датчика заданого типу для години доби (0–24).</summary>
    public double Generate(string kind, double hour)
    {
        var phase = DayPhase(hour);
        return kind switch
        {
            SensorCatalog.Temperature => Math.Round(22.0 + 5.0 * phase + Gauss(0.4), 1),
            SensorCatalog.Humidity => Math.Round(Math.Clamp(65.0 - 12.0 * phase + Gauss(1.5), 0, 100), 1),
            SensorCatalog.Light => Light(hour),
            _ => throw new ArgumentException($"Невідомий тип датчика: {kind}", nameof(kind)),
        };
    }

    /// <summary>
    /// Пакет показів для всіх датчиків. З імовірністю anomalyRate показ «стрибає»
    /// (імітація збою або аварії вентиляції).
    /// </summary>
    public IReadOnlyList<SimulatedReading> MakeBatch(
        IEnumerable<(string Name, string Kind)> sensors, double hour, double anomalyRate)
    {
        var batch = new List<SimulatedReading>();
        foreach (var (name, kind) in sensors)
        {
            var value = Generate(kind, hour);
            if (anomalyRate > 0 && rng.NextDouble() < anomalyRate)
            {
                value = Math.Round(value * (rng.Next(2) == 0 ? 0.3 : 1.6), 1);
                if (kind == SensorCatalog.Humidity)
                {
                    value = Math.Min(100, value);
                }
            }

            batch.Add(new SimulatedReading(name, value));
        }

        return batch;
    }

    private double Light(double hour)
    {
        // Світло є лише між 06:00 і 20:00, з максимумом опівдні.
        if (hour is < 6.0 or > 20.0)
        {
            return 0;
        }

        var sun = Math.Sin((hour - 6.0) / 14.0 * Math.PI);
        return Math.Round(Math.Max(0, 40_000 * sun + Gauss(800)));
    }

    /// <summary>Нормальний шум методом Бокса — Мюллера.</summary>
    private double Gauss(double sigma)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = rng.NextDouble();
        return sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
