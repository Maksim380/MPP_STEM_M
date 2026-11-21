using Greenhouse.Api.Auth;
using Greenhouse.Core.Contracts;
using Greenhouse.Core.Services;

namespace Greenhouse.Api.Endpoints;

/// <summary>/api/users: керування обліковими записами (лише адміністратор).</summary>
public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/users").WithTags("Users").RequireAuthorization(AuthNames.AdminPolicy);

        users.MapGet("/", (UserService s, CancellationToken ct) => s.ListAsync(ct))
            .WithSummary("Список користувачів");

        users.MapPost("/", async (UserIn input, UserService s, CancellationToken ct) =>
            {
                var user = await s.CreateAsync(input, ct);
                return TypedResults.Created($"/api/users/{user.Id}", user);
            })
            .WithSummary("Створити користувача з роллю admin, operator або viewer");

        return api;
    }
}
