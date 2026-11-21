using System.Security.Claims;
using System.Text.Encodings.Web;
using Greenhouse.Core.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Greenhouse.Api.Auth;

/// <summary>Автентифікація датчиків за заголовком X-Api-Key.</summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AuthOptions> auth)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AuthNames.ApiKeyHeader, out var provided) || provided.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var expected = auth.Value.SensorApiKey;
        if (string.IsNullOrEmpty(expected) || !TokenHasher.FixedTimeEquals(provided.ToString(), expected))
        {
            return Task.FromResult(AuthenticateResult.Fail("Неправильний API-ключ"));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "sensor"), new Claim(ClaimTypes.Role, AuthNames.SensorRole)],
            Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
