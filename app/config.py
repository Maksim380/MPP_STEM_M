"""Налаштування застосунку.

Значення можна перевизначити змінними середовища, щоб тести і локальний запуск
не заважали один одному.
"""

import os
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parent.parent
DASHBOARD_DIR = ROOT_DIR / "dashboard"


def db_path() -> str:
    """Шлях до файлу SQLite. За замовчуванням greenhouse.db у корені репозиторію."""
    return os.environ.get("GREENHOUSE_DB", str(ROOT_DIR / "greenhouse.db"))


# Типи датчиків: одиниця виміру, фізично можливий діапазон (усе поза ним
# відкидаємо як збій датчика) і комфортний діапазон для рослин (поза ним
# показуємо попередження). Значення орієнтовні, див. README, розділ «Припущення».
SENSOR_TYPES = {
    "temperature": {
        "unit": "°C",
        "valid_min": -40.0,
        "valid_max": 80.0,
        "comfort_min": 18.0,
        "comfort_max": 28.0,
    },
    "humidity": {
        "unit": "%",
        "valid_min": 0.0,
        "valid_max": 100.0,
        "comfort_min": 50.0,
        "comfort_max": 80.0,
    },
    "light": {
        "unit": "lx",
        "valid_min": 0.0,
        "valid_max": 150000.0,
        "comfort_min": 1000.0,
        "comfort_max": 60000.0,
    },
}

# Датчики, які створюються під час першого запуску.
DEFAULT_SENSORS = [
    ("temp-1", "temperature"),
    ("hum-1", "humidity"),
    ("light-1", "light"),
]
