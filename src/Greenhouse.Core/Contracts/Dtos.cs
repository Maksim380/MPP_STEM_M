using Greenhouse.Core.Domain;

namespace Greenhouse.Core.Contracts;

/// <summary>Вимір від датчика. Value nullable, щоб відсутнє поле дало зрозумілу помилку, а не 0.</summary>
public sealed record ReadingIn(string? Sensor, double? Value, DateTimeOffset? RecordedAt = null);

public sealed record ReadingOut(long Id, string Sensor, string Kind, int GreenhouseId, double Value, DateTime RecordedAt);

public sealed record SensorOut(string Name, string Kind, string Unit, int GreenhouseId, double ComfortMin, double ComfortMax);

public sealed record LatestReadingOut(
    string Sensor,
    string Kind,
    string Unit,
    int GreenhouseId,
    double? Value,
    DateTime? RecordedAt,
    string Status);

public sealed record AlertOut(
    long Id,
    string Sensor,
    int GreenhouseId,
    string Kind,
    double TriggerValue,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    DateTime? AcknowledgedAt,
    string? AcknowledgedBy);

public sealed record ThresholdOut(string Kind, string Unit, double ComfortMin, double ComfortMax);

public sealed record ThresholdIn(double? ComfortMin, double? ComfortMax);

public sealed record GreenhouseOut(int Id, string Name, string? Location, IReadOnlyList<string> Sensors, IReadOnlyList<ThresholdOut> Thresholds);

public sealed record GreenhouseIn(string? Name, string? Location);

public sealed record NewSensorIn(string? Name, string? Kind);

public sealed record LoginRequest(string? Username, string? Password);

public sealed record RefreshRequest(string? RefreshToken);

public sealed record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt, string Username, string Role);

public sealed record UserIn(string? Username, string? Password, string? Role);

public sealed record UserOut(int Id, string Username, string Role, DateTime CreatedAt);

public static class StatusNames
{
    /// <summary>Назва стану в API: ok, low, high, no_data.</summary>
    public static string ToApi(this ComfortStatus status) => status switch
    {
        ComfortStatus.Ok => "ok",
        ComfortStatus.Low => "low",
        ComfortStatus.High => "high",
        _ => "no_data",
    };
}
