using System.Security.Claims;
using System.Text;
using Greenhouse.Api.Contracts;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Greenhouse.Api.Auth;

/// <summary>Видає JWT оператору теплиці.</summary>
public sealed class TokenService(IOptions<AuthOptions> options, TimeProvider clock)
{
    public TokenResponse? TryIssue(TokenRequest request)
    {
        var o = options.Value;
        if (string.IsNullOrEmpty(o.OperatorUsername) || string.IsNullOrEmpty(o.OperatorPassword)
            || request.Username is null || request.Password is null)
        {
            return null;
        }

        var userOk = ApiKeyAuthenticationHandler.FixedTimeEquals(request.Username, o.OperatorUsername);
        var passOk = ApiKeyAuthenticationHandler.FixedTimeEquals(request.Password, o.OperatorPassword);
        if (!(userOk & passOk))
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(o.TokenLifetimeMinutes);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.JwtIssuer,
            Audience = o.JwtIssuer,
            Subject = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, request.Username), new Claim(ClaimTypes.Role, AuthNames.OperatorRole)]),
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(SigningKey(o), SecurityAlgorithms.HmacSha256),
        });
        return new TokenResponse(token, expires);
    }

    public static SymmetricSecurityKey SigningKey(AuthOptions o) =>
        new(Encoding.UTF8.GetBytes(o.JwtSigningKey));
}
