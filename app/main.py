"""FastAPI-застосунок: REST API для датчиків і роздача дашборда."""

import sqlite3
from collections.abc import AsyncIterator, Iterator
from contextlib import asynccontextmanager
from typing import Annotated

from fastapi import Depends, FastAPI, HTTPException, Query
from fastapi.responses import RedirectResponse
from fastapi.staticfiles import StaticFiles

from app import config, db
from app.alerts import classify
from app.models import LatestReading, ReadingIn, ReadingOut, Sensor


@asynccontextmanager
async def lifespan(_: FastAPI) -> AsyncIterator[None]:
    conn = db.connect()
    try:
        db.init_db(conn)
    finally:
        conn.close()
    yield


app = FastAPI(title="Моніторинг теплиці", version="0.1.0", lifespan=lifespan)


def get_conn() -> Iterator[sqlite3.Connection]:
    conn = db.connect()
    try:
        yield conn
    finally:
        conn.close()


Conn = Annotated[sqlite3.Connection, Depends(get_conn)]


def _validate(conn: sqlite3.Connection, reading: ReadingIn) -> sqlite3.Row:
    sensor = db.get_sensor(conn, reading.sensor)
    if sensor is None:
        raise HTTPException(404, f"Невідомий датчик: {reading.sensor}")
    if not sensor["valid_min"] <= reading.value <= sensor["valid_max"]:
        raise HTTPException(
            422,
            f"Значення {reading.value} {sensor['unit']} поза фізичним діапазоном "
            f"[{sensor['valid_min']}; {sensor['valid_max']}] для {reading.sensor}",
        )
    return sensor


@app.get("/api/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.get("/api/sensors", response_model=list[Sensor])
def sensors(conn: Conn) -> list[dict]:
    return [dict(r) for r in db.list_sensors(conn)]


@app.post("/api/readings", response_model=ReadingOut, status_code=201)
def add_reading(reading: ReadingIn, conn: Conn) -> dict:
    sensor = _validate(conn, reading)
    ts = reading.timestamp()
    with db.transaction(conn):
        new_id = db.insert_reading(conn, sensor["id"], reading.value, ts)
    return {"id": new_id, "sensor": sensor["name"], "kind": sensor["kind"],
            "value": reading.value, "recorded_at": ts}


@app.post("/api/readings/batch", response_model=list[ReadingOut], status_code=201)
def add_readings_batch(readings: list[ReadingIn], conn: Conn) -> list[dict]:
    """Пакет вимірів зберігається цілком або не зберігається взагалі."""
    if not readings:
        raise HTTPException(422, "Порожній пакет вимірів")
    checked = [(_validate(conn, r), r) for r in readings]
    out = []
    with db.transaction(conn):
        for sensor, r in checked:
            ts = r.timestamp()
            new_id = db.insert_reading(conn, sensor["id"], r.value, ts)
            out.append({"id": new_id, "sensor": sensor["name"], "kind": sensor["kind"],
                        "value": r.value, "recorded_at": ts})
    return out


@app.get("/api/readings", response_model=list[ReadingOut])
def readings(
    conn: Conn,
    sensor: str | None = None,
    limit: Annotated[int, Query(ge=1, le=1000)] = 100,
) -> list[dict]:
    if sensor is not None and db.get_sensor(conn, sensor) is None:
        raise HTTPException(404, f"Невідомий датчик: {sensor}")
    return [dict(r) for r in db.list_readings(conn, sensor, limit)]


@app.get("/api/readings/latest", response_model=list[LatestReading])
def latest(conn: Conn) -> list[dict]:
    result = []
    for r in db.latest_per_sensor(conn):
        row = dict(r)
        row["status"] = classify(row["value"], row.pop("comfort_min"), row.pop("comfort_max"))
        result.append(row)
    return result


@app.get("/", include_in_schema=False)
def root() -> RedirectResponse:
    return RedirectResponse("/dashboard/")


app.mount("/dashboard", StaticFiles(directory=config.DASHBOARD_DIR, html=True), name="dashboard")
