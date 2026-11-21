"""Оцінка стану теплиці за показами датчиків."""

from app.models import Status


def classify(value: float | None, comfort_min: float, comfort_max: float) -> Status:
    """Порівнює значення з комфортним діапазоном рослин."""
    if value is None:
        return "no_data"
    if value < comfort_min:
        return "low"
    if value > comfort_max:
        return "high"
    return "ok"
