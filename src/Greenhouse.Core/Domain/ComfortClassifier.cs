namespace Greenhouse.Core.Domain;

/// <summary>Оцінка показу: норма, низько чи високо.</summary>
public static class ComfortClassifier
{
    /// <summary>Межі діапазону включні: значення, що дорівнює межі, — норма.</summary>
    public static ComfortStatus Classify(double? value, double comfortMin, double comfortMax)
    {
        if (value is null)
        {
            return ComfortStatus.NoData;
        }

        if (value < comfortMin)
        {
            return ComfortStatus.Low;
        }

        return value > comfortMax ? ComfortStatus.High : ComfortStatus.Ok;
    }
}
