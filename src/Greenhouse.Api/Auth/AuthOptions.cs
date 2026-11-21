namespace Greenhouse.Api.Auth;

/// <summary>
/// Налаштування безпеки з розділу "Auth". Значення для розробки лежать в appsettings.Development.json;
/// у робочому середовищі їх задають змінними середовища (Auth__JwtSigningKey тощо) або dotnet user-secrets.
/// </summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>Ключ, яким датчики (симулятор, ESP32) підписують запити на запис.</summary>
    public string SensorApiKey { get; set; } = "";

    /// <summary>Адміністратор, який створюється під час першого запуску, якщо в БД ще немає користувачів.</summary>
    public string AdminUsername { get; set; } = "";

    public string AdminPassword { get; set; } = "";

    /// <summary>Секрет для підпису JWT, щонайменше 32 символи.</summary>
    public string JwtSigningKey { get; set; } = "";

    public string JwtIssuer { get; set; } = "greenhouse-api";
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Скільки спроб входу дозволено з однієї IP-адреси за хвилину.</summary>
    public int LoginAttemptsPerMinute { get; set; } = 10;
}

public static class AuthNames
{
    public const string ApiKeyScheme = "ApiKey";
    public const string ApiKeyHeader = "X-Api-Key";
    public const string SensorPolicy = "SensorWrite";
    public const string OperatorPolicy = "OperatorOrAdmin";
    public const string AdminPolicy = "AdminOnly";
    public const string SensorRole = "sensor";
    public const string LoginRateLimit = "login";
}
