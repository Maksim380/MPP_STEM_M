using Greenhouse.Api.Auth;
using Greenhouse.Core.Contracts;
using Greenhouse.Core.Data;
using Greenhouse.Core.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Greenhouse.Api.Endpoints;

/// <summary>/api/auth: вхід, оновлення і завершення сесії.</summary>
public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth").RequireRateLimiting(AuthNames.LoginRateLimit);

        auth.MapPost("/login", async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> (
                LoginRequest request, UserService users, TokenService tokens, CancellationToken ct) =>
            {
                var user = await users.AuthenticateAsync(request, ct);
                return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(await IssueAsync(user, users, tokens, ct));
            })
            .WithSummary("Вхід: access-токен (JWT) і refresh-токен");

        auth.MapPost("/refresh", async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> (
                RefreshRequest request, UserService users, TokenService tokens, CancellationToken ct) =>
            {
                var user = await users.RedeemRefreshTokenAsync(request.RefreshToken, ct);
                return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(await IssueAsync(user, users, tokens, ct));
            })
            .WithSummary("Нова пара токенів за refresh-токеном (старий відкликається)");

        auth.MapPost("/logout", async (RefreshRequest request, UserService users, CancellationToken ct) =>
            {
                await users.RevokeAsync(request.RefreshToken, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Відкликати refresh-токен");

        return api;
    }

    private static async Task<AuthResponse> IssueAsync(User user, UserService users, TokenService tokens, CancellationToken ct)
    {
        var (access, accessExpires) = tokens.CreateAccessToken(user);
        var (refresh, refreshExpires) = await users.IssueRefreshTokenAsync(user, ct);
        return new AuthResponse(access, accessExpires, refresh, refreshExpires, user.Username, user.Role);
    }
}
