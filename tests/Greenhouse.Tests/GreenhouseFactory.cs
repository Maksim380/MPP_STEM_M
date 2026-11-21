using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Greenhouse.Core.Contracts;
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
    public const string AdminUser = "admin";
    public const string AdminPassword = "admin-password";
    public const string UserPassword = "user-password";

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
        builder.UseSetting("Auth:AdminUsername", AdminUser);
        builder.UseSetting("Auth:AdminPassword", AdminPassword);
        builder.UseSetting("Auth:LoginAttemptsPerMinute", "1000");
        builder.UseSetting("Auth:JwtSigningKey", "test-signing-key-0123456789-abcdefghij");
    }

    /// <summary>Клієнт датчика (з API-ключем).</summary>
    public HttpClient CreateSensorClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", SensorKey);
        return client;
    }

    /// <summary>Входить і повертає відповідь з токенами.</summary>
    public async Task<AuthResponse> LoginAsync(string username, string password)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    /// <summary>Клієнт з JWT адміністратора.</summary>
    public async Task<HttpClient> CreateAdminClientAsync() => WithToken(await LoginAsync(AdminUser, AdminPassword));

    /// <summary>Створює користувача з роллю (через API адміністратора) і повертає його клієнт.</summary>
    public async Task<HttpClient> CreateUserClientAsync(string role)
    {
        var username = $"{role}-{Guid.NewGuid():N}"[..20];
        using (var admin = await CreateAdminClientAsync())
        {
            var created = await admin.PostAsJsonAsync("/api/users", new UserIn(username, UserPassword, role), Json);
            created.EnsureSuccessStatusCode();
        }

        return WithToken(await LoginAsync(username, UserPassword));
    }

    private HttpClient WithToken(AuthResponse auth)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
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
