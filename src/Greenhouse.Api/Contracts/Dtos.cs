using Greenhouse.Domain;

namespace Greenhouse.Api.Contracts;

/// <summary>Вимір від датчика. Value nullable, щоб відсутнє поле дало зрозумілу помилку, а не 0.</summary>
public sealed record ReadingIn(string? Sensor, double? Value, DateTimeOffset? RecordedAt = null);

public sealed record ReadingOut(long Id, string Sensor, string Kind, double Value, DateTime RecordedAt);

public sealed record SensorOut(string Name, string Kind, string Unit, double ComfortMin, double ComfortMax);

public sealed record LatestReadingOut(
    string Sensor,
    string Kind,
    string Unit,
    double? Value,
    DateTime? RecordedAt,
    string Status);

public sealed record AlertOut(
    long Id,
    string Sensor,
    string Kind,
    double TriggerValue,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    DateTime? AcknowledgedAt,
    string? AcknowledgedBy);

public sealed record TokenRequest(string? Username, string? Password);

public sealed record TokenResponse(string AccessToken, DateTime ExpiresAt);

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
