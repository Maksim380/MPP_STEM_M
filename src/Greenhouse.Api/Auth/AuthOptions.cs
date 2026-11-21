namespace Greenhouse.Api.Auth;

/// <summary>
/// Налаштування безпеки з розділу "Auth" конфігурації. Значення для розробки лежать в
/// appsettings.Development.json; у робочому середовищі їх задають змінними середовища
/// (Auth__SensorApiKey тощо) або через dotnet user-secrets, а не в коді.
/// </summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>Ключ, яким датчики (симулятор, ESP32) підписують запити на запис.</summary>
    public string SensorApiKey { get; set; } = "";

    public string OperatorUsername { get; set; } = "";
    public string OperatorPassword { get; set; } = "";

    /// <summary>Секрет для підпису JWT, щонайменше 32 символи.</summary>
    public string JwtSigningKey { get; set; } = "";

    public string JwtIssuer { get; set; } = "greenhouse-api";
    public int TokenLifetimeMinutes { get; set; } = 60;
}

public static class AuthNames
{
    public const string ApiKeyScheme = "ApiKey";
    public const string ApiKeyHeader = "X-Api-Key";
    public const string SensorPolicy = "SensorWrite";
    public const string OperatorPolicy = "Operator";
    public const string SensorRole = "sensor";
    public const string OperatorRole = "operator";
}
