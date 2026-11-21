namespace Greenhouse.Core.Domain;

/// <summary>Ролі користувачів і що кожна дозволяє.</summary>
public static class Roles
{
    /// <summary>Керує теплицями, датчиками й користувачами; може все, що оператор.</summary>
    public const string Admin = "admin";

    /// <summary>Підтверджує тривоги і змінює пороги норми.</summary>
    public const string Operator = "operator";

    /// <summary>Лише переглядає дані (як анонімний дашборд, але з власним обліковим записом).</summary>
    public const string Viewer = "viewer";

    public static IReadOnlyList<string> All { get; } = [Admin, Operator, Viewer];

    public static bool IsValid(string? role) => role is not null && All.Contains(role);
}
