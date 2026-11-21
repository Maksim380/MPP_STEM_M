namespace Greenhouse.Core.Services;

/// <summary>Вид помилки в даних клієнта; веб-шар перетворює його на HTTP-код.</summary>
public enum ErrorKind
{
    /// <summary>400: запит синтаксично неправильний.</summary>
    InvalidInput,

    /// <summary>404: запис не знайдено.</summary>
    NotFound,

    /// <summary>409: конфлікт з наявними даними.</summary>
    Conflict,

    /// <summary>422: значення неможливе (поза фізичним діапазоном, час у майбутньому).</summary>
    Implausible,
}

/// <summary>Помилка в даних від клієнта. Обробник винятків перетворює її на ProblemDetails.</summary>
public class ClientDataException(string message, ErrorKind kind) : Exception(message)
{
    public ErrorKind Kind { get; } = kind;
}

public sealed class InvalidInputException(string message) : ClientDataException(message, ErrorKind.InvalidInput);

public sealed class NotFoundException(string message) : ClientDataException(message, ErrorKind.NotFound);

public sealed class UnknownSensorException(string sensor) : ClientDataException($"Невідомий датчик: {sensor}", ErrorKind.NotFound);

public sealed class ConflictException(string message) : ClientDataException(message, ErrorKind.Conflict);

public sealed class ImplausibleValueException(string message) : ClientDataException(message, ErrorKind.Implausible);
