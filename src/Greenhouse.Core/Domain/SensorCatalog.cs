namespace Greenhouse.Core.Domain;

/// <summary>
/// Типи датчиків, стандартні норми для нової теплиці і датчики, які створюються під час першого запуску.
/// Норми орієнтовні (типові для овочевих культур), див. docs/decisions.md.
/// </summary>
public static class SensorCatalog
{
    public const string Temperature = "temperature";
    public const string Humidity = "humidity";
    public const string Light = "light";

    public static IReadOnlyList<SensorTypeDefinition> Types { get; } =
    [
        new(Temperature, "°C", ValidMin: -40, ValidMax: 80, ComfortMin: 18, ComfortMax: 28),
        new(Humidity, "%", ValidMin: 0, ValidMax: 100, ComfortMin: 50, ComfortMax: 80),
        new(Light, "lx", ValidMin: 0, ValidMax: 150_000, ComfortMin: 1_000, ComfortMax: 60_000),
    ];

    /// <summary>Датчики першої теплиці: назва → тип.</summary>
    public static IReadOnlyList<(string Name, string Kind)> DefaultSensors { get; } =
    [
        ("temp-1", Temperature),
        ("hum-1", Humidity),
        ("light-1", Light),
    ];

    public static SensorTypeDefinition Get(string kind) =>
        Types.FirstOrDefault(t => t.Kind == kind)
        ?? throw new ArgumentException($"Невідомий тип датчика: {kind}", nameof(kind));
}
