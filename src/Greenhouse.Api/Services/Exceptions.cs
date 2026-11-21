namespace Greenhouse.Api.Services;

/// <summary>Помилка в даних від клієнта. Обробник винятків перетворює її на ProblemDetails.</summary>
public abstract class ClientDataException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>Запит синтаксично неправильний (порожнє поле, порожній пакет) — 400.</summary>
public sealed class InvalidInputException(string message)
    : ClientDataException(message, StatusCodes.Status400BadRequest);

/// <summary>Датчика з такою назвою немає — 404.</summary>
public sealed class UnknownSensorException(string sensor)
    : ClientDataException($"Невідомий датчик: {sensor}", StatusCodes.Status404NotFound);

/// <summary>Значення неможливе фізично або час у майбутньому — 422.</summary>
public sealed class ImplausibleValueException(string message)
    : ClientDataException(message, StatusCodes.Status422UnprocessableEntity);

/// <summary>Запис не знайдено — 404.</summary>
public sealed class NotFoundException(string message)
    : ClientDataException(message, StatusCodes.Status404NotFound);
