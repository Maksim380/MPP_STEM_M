using System.Security.Claims;
using System.Text;
using Greenhouse.Core.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Greenhouse.Api.Auth;

/// <summary>Видає короткоживучі JWT access-токени з роллю користувача.</summary>
public sealed class TokenService(IOptions<AuthOptions> options, TimeProvider clock)
{
    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user)
    {
        var o = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(o.AccessTokenMinutes);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.JwtIssuer,
            Audience = o.JwtIssuer,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
            ]),
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(SigningKey(o), SecurityAlgorithms.HmacSha256),
        });
        return (token, expires);
    }

    public static SymmetricSecurityKey SigningKey(AuthOptions o) =>
        new(Encoding.UTF8.GetBytes(o.JwtSigningKey));
}
