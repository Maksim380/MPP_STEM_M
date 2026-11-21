using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Greenhouse.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Greenhouse.Tests;

/// <summary>
/// Піднімає справжній застосунок у пам'яті з окремою тимчасовою БД SQLite і лог-файлом,
/// тож кожен тестовий клас працює з чистими даними, а міграції перевіряються на кожному запуску.
/// </summary>
public sealed class GreenhouseFactory : WebApplicationFactory<Program>
{
    public const string SensorKey = "test-sensor-key";
    public const string OperatorUser = "operator";
    public const string OperatorPassword = "test-password";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "greenhouse-tests", Guid.NewGuid().ToString("N"));

    public string DbPath => Path.Combine(_dir, "test.db");
    public string LogDir => Path.Combine(_dir, "logs");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_dir);
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Greenhouse", $"Data Source={DbPath};Pooling=False");
        builder.UseSetting("Logging:FilePath", Path.Combine(LogDir, "greenhouse-.log"));
        builder.UseSetting("Auth:SensorApiKey", SensorKey);
        builder.UseSetting("Auth:OperatorUsername", OperatorUser);
        builder.UseSetting("Auth:OperatorPassword", OperatorPassword);
        builder.UseSetting("Auth:JwtSigningKey", "test-signing-key-0123456789-abcdefghij");
    }

    /// <summary>Клієнт датчика (з API-ключем).</summary>
    public HttpClient CreateSensorClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", SensorKey);
        return client;
    }

    /// <summary>Клієнт оператора (з JWT).</summary>
    public async Task<HttpClient> CreateOperatorClientAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/token",
            new TokenRequest(OperatorUser, OperatorPassword), Json);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Лог-файл на Windows може бути ще відкритий; тимчасову теку прибере ОС.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
