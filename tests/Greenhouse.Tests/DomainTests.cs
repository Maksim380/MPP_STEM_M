using Greenhouse.Core.Domain;

namespace Greenhouse.Tests;

public class ComfortClassifierTests
{
    [Theory]
    [InlineData(null, ComfortStatus.NoData)]
    [InlineData(17.9, ComfortStatus.Low)]
    [InlineData(18.0, ComfortStatus.Ok)] // межа включна
    [InlineData(23.0, ComfortStatus.Ok)]
    [InlineData(28.0, ComfortStatus.Ok)] // межа включна
    [InlineData(28.1, ComfortStatus.High)]
    public void Classify_uses_inclusive_bounds(double? value, ComfortStatus expected) =>
        Assert.Equal(expected, ComfortClassifier.Classify(value, 18, 28));
}

public class SensorTypeDefinitionTests
{
    private static readonly SensorTypeDefinition Humidity = SensorCatalog.Get(SensorCatalog.Humidity);

    [Theory]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(-0.1, false)]
    [InlineData(100.1, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    public void IsPhysicallyValid_checks_range_and_finiteness(double value, bool expected) =>
        Assert.Equal(expected, Humidity.IsPhysicallyValid(value));

    [Fact]
    public void Classify_delegates_to_comfort_range()
    {
        Assert.Equal(ComfortStatus.Low, Humidity.Classify(40));
        Assert.Equal(ComfortStatus.High, Humidity.Classify(90));
    }

    [Fact]
    public void Catalog_rejects_unknown_kind() =>
        Assert.Throws<ArgumentException>(() => SensorCatalog.Get("co2"));

    [Fact]
    public void Catalog_ranges_are_consistent()
    {
        foreach (var t in SensorCatalog.Types)
        {
            Assert.True(t.ValidMin < t.ValidMax, t.Kind);
            Assert.True(t.ComfortMin <= t.ComfortMax, t.Kind);
            Assert.True(t.IsPhysicallyValid(t.ComfortMin) && t.IsPhysicallyValid(t.ComfortMax), t.Kind);
        }
    }
}

public class AlertPolicyTests
{
    [Theory]
    [InlineData(null, ComfortStatus.Ok, AlertAction.None)]
    [InlineData(null, ComfortStatus.NoData, AlertAction.None)]
    [InlineData(null, ComfortStatus.High, AlertAction.Open)]
    [InlineData(null, ComfortStatus.Low, AlertAction.Open)]
    [InlineData(ComfortStatus.High, ComfortStatus.High, AlertAction.None)] // без дублікатів
    [InlineData(ComfortStatus.High, ComfortStatus.Ok, AlertAction.Close)]
    [InlineData(ComfortStatus.High, ComfortStatus.Low, AlertAction.Replace)]
    [InlineData(ComfortStatus.Low, ComfortStatus.NoData, AlertAction.None)]
    public void Decide(ComfortStatus? open, ComfortStatus status, AlertAction expected) =>
        Assert.Equal(expected, AlertPolicy.Decide(open, status));
}
