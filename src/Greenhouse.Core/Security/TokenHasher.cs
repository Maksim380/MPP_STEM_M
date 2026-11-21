using System.Security.Cryptography;
using System.Text;

namespace Greenhouse.Core.Security;

/// <summary>Генерація refresh-токенів і їх SHA-256 хеш для зберігання в БД.</summary>
public static class TokenHasher
{
    public static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Порівняння рядків за сталий час (API-ключі, логіни).</summary>
    public static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
