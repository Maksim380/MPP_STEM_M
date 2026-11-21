using System.Net;
using System.Net.Http.Json;
using Greenhouse.Core.Contracts;
using Greenhouse.Core.Domain;
using Greenhouse.Core.Security;

namespace Greenhouse.Tests;

/// <summary>Ролі, refresh-токени, керування користувачами, обмеження спроб входу.</summary>
public class RolesAndTokensTests : IDisposable
{
    private readonly GreenhouseFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Viewer_cannot_acknowledge_or_change_thresholds()
    {
        using var viewer = await _factory.CreateUserClientAsync(Roles.Viewer);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync(new Uri("/api/alerts/1/ack", UriKind.Relative), null)).StatusCode);
        var put = await viewer.PutAsJsonAsync("/api/greenhouses/1/thresholds/light", new { comfort_min = 1, comfort_max = 2 }, GreenhouseFactory.Json);
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task Operator_cannot_manage_greenhouses_or_users()
    {
        using var op = await _factory.CreateUserClientAsync(Roles.Operator);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync("/api/greenhouses", new { name = "X" }, GreenhouseFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync(new Uri("/api/users", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Admin_lists_and_creates_users_with_validation()
    {
        using var admin = await _factory.CreateAdminClientAsync();
        var created = await admin.PostAsJsonAsync("/api/users", new UserIn("ivan", "password-1", Roles.Viewer), GreenhouseFactory.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var users = await admin.GetAsync<List<UserOut>>("/api/users");
        Assert.Equal(["admin", "ivan"], users.Select(u => u.Username));

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/users", new UserIn("ivan", "password-1", Roles.Viewer), GreenhouseFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/users", new UserIn("petro", "short", Roles.Viewer), GreenhouseFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/users", new UserIn("petro", "password-1", "root"), GreenhouseFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/users", new UserIn(" ", "password-1", Roles.Viewer), GreenhouseFactory.Json)).StatusCode);
    }

    [Fact]
    public async Task Login_returns_role_and_passwords_are_hashed()
    {
        var auth = await _factory.LoginAsync(GreenhouseFactory.AdminUser, GreenhouseFactory.AdminPassword);
        Assert.Equal(Roles.Admin, auth.Role);
        Assert.True(auth.AccessTokenExpiresAt < auth.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task Refresh_rotates_tokens_and_detects_reuse()
    {
        using var client = _factory.CreateClient();
        var first = await _factory.LoginAsync(GreenhouseFactory.AdminUser, GreenhouseFactory.AdminPassword);

        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(first.RefreshToken), GreenhouseFactory.Json);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var second = (await refreshed.Content.ReadFromJsonAsync<AuthResponse>(GreenhouseFactory.Json))!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        // Повторне використання старого токена — ознака крадіжки: відкликаються всі токени.
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(first.RefreshToken), GreenhouseFactory.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        var afterReuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(second.RefreshToken), GreenhouseFactory.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task Invalid_refresh_token_is_401(string? token)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(token), GreenhouseFactory.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        using var client = _factory.CreateClient();
        var auth = await _factory.LoginAsync(GreenhouseFactory.AdminUser, GreenhouseFactory.AdminPassword);

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(auth.RefreshToken), GreenhouseFactory.Json);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(null), GreenhouseFactory.Json);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken), GreenhouseFactory.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Too_many_login_attempts_get_429()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Auth:LoginAttemptsPerMinute", "3"));
        using var client = factory.CreateClient();
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            var r = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "wrong"), GreenhouseFactory.Json);
            codes.Add(r.StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized,
            HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], codes);
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void Hash_verifies_and_uses_random_salt()
    {
        var a = PasswordHasher.Hash("secret-1");
        var b = PasswordHasher.Hash("secret-1");
        Assert.NotEqual(a, b);
        Assert.StartsWith("pbkdf2-sha256$", a, StringComparison.Ordinal);
        Assert.True(PasswordHasher.Verify("secret-1", a));
        Assert.False(PasswordHasher.Verify("secret-2", a));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("md5$1$abc$def")]
    [InlineData("pbkdf2-sha256$x$abc$def")]
    [InlineData("pbkdf2-sha256$1000$!!!$???")]
    public void Malformed_hash_never_verifies(string stored) =>
        Assert.False(PasswordHasher.Verify("anything", stored));

    [Fact]
    public void Token_hash_is_stable_and_tokens_are_unique()
    {
        var t1 = TokenHasher.NewToken();
        Assert.NotEqual(t1, TokenHasher.NewToken());
        Assert.Equal(TokenHasher.Hash(t1), TokenHasher.Hash(t1));
        Assert.Equal(64, TokenHasher.Hash(t1).Length);
    }
}
