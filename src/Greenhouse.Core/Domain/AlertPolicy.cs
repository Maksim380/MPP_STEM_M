namespace Greenhouse.Core.Domain;

/// <summary>Що зробити з тривогою датчика після нового показу.</summary>
public enum AlertAction
{
    /// <summary>Нічого не змінювати.</summary>
    None,

    /// <summary>Відкрити нову тривогу.</summary>
    Open,

    /// <summary>Закрити відкриту тривогу.</summary>
    Close,

    /// <summary>Закрити відкриту тривогу і відкрити нову іншого типу (низько ↔ високо).</summary>
    Replace,
}

/// <summary>
/// Правило тривог: тривога відкривається, коли показ виходить за норму, і
/// закривається, коли повертається в норму. Поки показ поза нормою з тим самим
/// знаком, нова тривога не створюється (без дублікатів).
/// </summary>
public static class AlertPolicy
{
    /// <param name="openAlert">Тип відкритої тривоги (Low/High) або null, якщо її немає.</param>
    /// <param name="newStatus">Стан нового показу.</param>
    public static AlertAction Decide(ComfortStatus? openAlert, ComfortStatus newStatus)
    {
        var outOfRange = newStatus is ComfortStatus.Low or ComfortStatus.High;
        if (openAlert is null)
        {
            return outOfRange ? AlertAction.Open : AlertAction.None;
        }

        if (!outOfRange)
        {
            return newStatus == ComfortStatus.Ok ? AlertAction.Close : AlertAction.None;
        }

        return openAlert == newStatus ? AlertAction.None : AlertAction.Replace;
    }
}
