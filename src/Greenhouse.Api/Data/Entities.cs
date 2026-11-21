namespace Greenhouse.Api.Data;

/// <summary>Тип датчика (температура, вологість, освітленість) і його діапазони.</summary>
public sealed class SensorType
{
    public required string Kind { get; set; }
    public required string Unit { get; set; }
    public double ValidMin { get; set; }
    public double ValidMax { get; set; }
    public double ComfortMin { get; set; }
    public double ComfortMax { get; set; }

    public List<Sensor> Sensors { get; } = [];
}

/// <summary>Конкретний датчик у теплиці.</summary>
public sealed class Sensor
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Kind { get; set; }

    public SensorType Type { get; set; } = null!;
    public List<Reading> Readings { get; } = [];
    public List<Alert> Alerts { get; } = [];
}

/// <summary>Один вимір датчика. Час завжди в UTC.</summary>
public sealed class Reading
{
    public long Id { get; set; }
    public int SensorId { get; set; }
    public double Value { get; set; }
    public DateTime RecordedAt { get; set; }

    public Sensor Sensor { get; set; } = null!;
}

/// <summary>Тривога: період, коли показ датчика був поза нормою.</summary>
public sealed class Alert
{
    public long Id { get; set; }
    public int SensorId { get; set; }

    /// <summary>"low" або "high".</summary>
    public required string Kind { get; set; }

    /// <summary>Значення, з якого почалася тривога.</summary>
    public double TriggerValue { get; set; }

    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }

    public Sensor Sensor { get; set; } = null!;
}
