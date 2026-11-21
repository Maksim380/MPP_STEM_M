namespace Greenhouse.Core.Domain;

/// <summary>
/// Властивості типу датчика: одиниця, фізично можливий діапазон (усе поза ним
/// вважаємо збоєм датчика) і комфортний діапазон для рослин (поза ним — тривога).
/// </summary>
public sealed record SensorTypeDefinition(
    string Kind,
    string Unit,
    double ValidMin,
    double ValidMax,
    double ComfortMin,
    double ComfortMax)
{
    /// <summary>Чи може датчик фізично видати таке значення.</summary>
    public bool IsPhysicallyValid(double value) =>
        double.IsFinite(value) && value >= ValidMin && value <= ValidMax;

    /// <summary>Порівнює значення з комфортним діапазоном.</summary>
    public ComfortStatus Classify(double? value) =>
        ComfortClassifier.Classify(value, ComfortMin, ComfortMax);
}
