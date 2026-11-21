namespace Greenhouse.Core.Domain;

/// <summary>Стан показу відносно комфортного діапазону рослин.</summary>
public enum ComfortStatus
{
    /// <summary>Вимірів ще немає.</summary>
    NoData,

    /// <summary>У межах норми.</summary>
    Ok,

    /// <summary>Нижче норми.</summary>
    Low,

    /// <summary>Вище норми.</summary>
    High,
}
