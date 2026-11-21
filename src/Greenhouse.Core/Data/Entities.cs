namespace Greenhouse.Core.Data;

/// <summary>Користувач системи. Пароль зберігається лише як хеш PBKDF2.</summary>
public sealed class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<RefreshToken> RefreshTokens { get; } = [];
}

/// <summary>Refresh-токен. Зберігається хеш, а не сам токен; при оновленні старий відкликається.</summary>
public sealed class RefreshToken
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public User User { get; set; } = null!;
}

/// <summary>Теплиця: має свої датчики й свої пороги норми.</summary>
public sealed class GreenhouseEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Location { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<Sensor> Sensors { get; } = [];
    public List<Threshold> Thresholds { get; } = [];
}

/// <summary>Тип датчика: одиниця і фізично можливий діапазон (усе поза ним — збій датчика).</summary>
public sealed class SensorType
{
    public required string Kind { get; set; }
    public required string Unit { get; set; }
    public double ValidMin { get; set; }
    public double ValidMax { get; set; }

    public List<Sensor> Sensors { get; } = [];
}

/// <summary>Комфортний діапазон для типу датчика в конкретній теплиці.</summary>
public sealed class Threshold
{
    public int GreenhouseId { get; set; }
    public required string Kind { get; set; }
    public double ComfortMin { get; set; }
    public double ComfortMax { get; set; }

    public GreenhouseEntity Greenhouse { get; set; } = null!;
    public SensorType Type { get; set; } = null!;
}

/// <summary>Конкретний датчик у теплиці.</summary>
public sealed class Sensor
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Kind { get; set; }
    public int GreenhouseId { get; set; }

    public SensorType Type { get; set; } = null!;
    public GreenhouseEntity Greenhouse { get; set; } = null!;
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

    public double TriggerValue { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }

    public Sensor Sensor { get; set; } = null!;
}
