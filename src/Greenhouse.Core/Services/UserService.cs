using Greenhouse.Core.Contracts;
using Greenhouse.Core.Data;
using Greenhouse.Core.Domain;
using Greenhouse.Core.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Greenhouse.Core.Services;

/// <summary>Користувачі, перевірка паролів і refresh-токени з ротацією.</summary>
public sealed partial class UserService(GreenhouseDbContext db, TimeProvider clock, ILogger<UserService> logger)
{
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    public const int MinPasswordLength = 8;

    public async Task<IReadOnlyList<UserOut>> ListAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().OrderBy(u => u.Id)
            .Select(u => new UserOut(u.Id, u.Username, u.Role, u.CreatedAt))
            .ToListAsync(ct);

    public async Task<UserOut> CreateAsync(UserIn input, CancellationToken ct)
    {
        var username = input.Username?.Trim();
        if (string.IsNullOrEmpty(username) || username.Length > 64)
        {
            throw new InvalidInputException("Логін обов'язковий, до 64 символів");
        }

        if (input.Password is null || input.Password.Length < MinPasswordLength)
        {
            throw new InvalidInputException($"Пароль має містити щонайменше {MinPasswordLength} символів");
        }

        if (!Roles.IsValid(input.Role))
        {
            throw new InvalidInputException($"Роль має бути однією з: {string.Join(", ", Roles.All)}");
        }

        if (await db.Users.AnyAsync(u => u.Username == username, ct))
        {
            throw new ConflictException($"Користувач «{username}» вже існує");
        }

        var user = new User
        {
            Username = username,
            PasswordHash = PasswordHasher.Hash(input.Password),
            Role = input.Role!,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        LogUserCreated(logger, username, user.Role);
        return new UserOut(user.Id, user.Username, user.Role, user.CreatedAt);
    }

    /// <summary>Створює адміністратора під час першого запуску, якщо користувачів ще немає.</summary>
    public async Task EnsureAdminAsync(string? username, string? password, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return;
        }

        await CreateAsync(new UserIn(username, password, Roles.Admin), ct);
    }

    /// <summary>Повертає користувача, якщо логін і пароль правильні; інакше null (без підказки, що саме не так).</summary>
    public async Task<User?> AuthenticateAsync(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return null;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username, ct);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            LogLoginFailed(logger, request.Username);
            return null;
        }

        return user;
    }

    public async Task<(string Token, DateTime ExpiresAt)> IssueRefreshTokenAsync(User user, CancellationToken ct)
    {
        var token = TokenHasher.NewToken();
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now + RefreshTokenLifetime;
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = TokenHasher.Hash(token), CreatedAt = now, ExpiresAt = expires });
        await db.SaveChangesAsync(ct);
        return (token, expires);
    }

    /// <summary>
    /// Ротація: старий токен відкликається і видається новий. Повторне використання вже відкликаного
    /// токена (ознака крадіжки) відкликає всі токени користувача.
    /// </summary>
    public async Task<User?> RedeemRefreshTokenAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var hash = TokenHasher.Hash(token);
        var stored = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if (stored is null)
        {
            return null;
        }

        if (stored.RevokedAt is not null)
        {
            LogTokenReuse(logger, stored.User.Username);
            await db.RefreshTokens.Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
            return null;
        }

        if (stored.ExpiresAt <= now)
        {
            return null;
        }

        stored.RevokedAt = now;
        await db.SaveChangesAsync(ct);
        return stored.User;
    }

    public async Task RevokeAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        var hash = TokenHasher.Hash(token);
        var now = clock.GetUtcNow().UtcDateTime;
        await db.RefreshTokens.Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Створено користувача {Username} з роллю {Role}")]
    private static partial void LogUserCreated(ILogger logger, string username, string role);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Невдалий вхід для {Username}")]
    private static partial void LogLoginFailed(ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Повторне використання відкликаного refresh-токена користувача {Username}; усі сесії відкликано")]
    private static partial void LogTokenReuse(ILogger logger, string username);
}
