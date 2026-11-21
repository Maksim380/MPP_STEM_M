"""Робота з базою даних SQLite.

Схема у 3НФ:
  sensor_types(kind PK, unit, діапазони)  — властивості типу датчика
  sensors(id PK, name UNIQUE, kind FK)     — конкретні датчики
  readings(id PK, sensor_id FK, value, recorded_at) — виміри

Усі запити параметризовані (знак ?), тому SQL-ін'єкції неможливі.
"""

import sqlite3
from collections.abc import Iterator
from contextlib import contextmanager

from app import config

SCHEMA = """
CREATE TABLE IF NOT EXISTS sensor_types (
    kind        TEXT PRIMARY KEY,
    unit        TEXT NOT NULL,
    valid_min   REAL NOT NULL,
    valid_max   REAL NOT NULL,
    comfort_min REAL NOT NULL,
    comfort_max REAL NOT NULL,
    CHECK (valid_min < valid_max),
    CHECK (comfort_min <= comfort_max)
);

CREATE TABLE IF NOT EXISTS sensors (
    id   INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    kind TEXT NOT NULL REFERENCES sensor_types(kind)
);

CREATE TABLE IF NOT EXISTS readings (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    sensor_id   INTEGER NOT NULL REFERENCES sensors(id) ON DELETE CASCADE,
    value       REAL NOT NULL,
    recorded_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_readings_sensor_time
    ON readings (sensor_id, recorded_at);
"""


def connect(path: str | None = None) -> sqlite3.Connection:
    """Відкриває з'єднання з увімкненими зовнішніми ключами."""
    conn = sqlite3.connect(path or config.db_path(), check_same_thread=False)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys = ON")
    return conn


@contextmanager
def transaction(conn: sqlite3.Connection) -> Iterator[sqlite3.Connection]:
    """Транзакція: або всі зміни зберігаються, або жодна."""
    try:
        yield conn
        conn.commit()
    except Exception:
        conn.rollback()
        raise


def init_db(conn: sqlite3.Connection) -> None:
    """Створює таблиці і стартові дані, якщо їх ще немає."""
    conn.executescript(SCHEMA)
    with transaction(conn):
        for kind, t in config.SENSOR_TYPES.items():
            conn.execute(
                "INSERT OR IGNORE INTO sensor_types "
                "(kind, unit, valid_min, valid_max, comfort_min, comfort_max) "
                "VALUES (?, ?, ?, ?, ?, ?)",
                (kind, t["unit"], t["valid_min"], t["valid_max"],
                 t["comfort_min"], t["comfort_max"]),
            )
        for name, kind in config.DEFAULT_SENSORS:
            conn.execute(
                "INSERT OR IGNORE INTO sensors (name, kind) VALUES (?, ?)",
                (name, kind),
            )


SENSOR_SELECT = (
    "SELECT s.id, s.name, s.kind, t.unit, t.valid_min, t.valid_max, "
    "t.comfort_min, t.comfort_max "
    "FROM sensors s JOIN sensor_types t ON t.kind = s.kind"
)


def list_sensors(conn: sqlite3.Connection) -> list[sqlite3.Row]:
    return conn.execute(SENSOR_SELECT + " ORDER BY s.id").fetchall()


def get_sensor(conn: sqlite3.Connection, name: str) -> sqlite3.Row | None:
    return conn.execute(SENSOR_SELECT + " WHERE s.name = ?", (name,)).fetchone()


def insert_reading(
    conn: sqlite3.Connection, sensor_id: int, value: float, recorded_at: str
) -> int:
    cur = conn.execute(
        "INSERT INTO readings (sensor_id, value, recorded_at) VALUES (?, ?, ?)",
        (sensor_id, value, recorded_at),
    )
    return int(cur.lastrowid)


def list_readings(
    conn: sqlite3.Connection, sensor_name: str | None, limit: int
) -> list[sqlite3.Row]:
    """Останні виміри (новіші першими), за потреби для одного датчика."""
    sql = (
        "SELECT r.id, s.name AS sensor, s.kind, r.value, r.recorded_at "
        "FROM readings r JOIN sensors s ON s.id = r.sensor_id"
    )
    params: list[object] = []
    if sensor_name is not None:
        sql += " WHERE s.name = ?"
        params.append(sensor_name)
    sql += " ORDER BY r.recorded_at DESC, r.id DESC LIMIT ?"
    params.append(limit)
    return conn.execute(sql, params).fetchall()


def latest_per_sensor(conn: sqlite3.Connection) -> list[sqlite3.Row]:
    """Для кожного датчика — його останній вимір (або NULL, якщо вимірів немає)."""
    return conn.execute(
        "SELECT s.name AS sensor, s.kind, t.unit, t.comfort_min, t.comfort_max, "
        "r.value, r.recorded_at "
        "FROM sensors s JOIN sensor_types t ON t.kind = s.kind "
        "LEFT JOIN readings r ON r.id = ("
        "  SELECT r2.id FROM readings r2 WHERE r2.sensor_id = s.id "
        "  ORDER BY r2.recorded_at DESC, r2.id DESC LIMIT 1"
        ") ORDER BY s.id"
    ).fetchall()
