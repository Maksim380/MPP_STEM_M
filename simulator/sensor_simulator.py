"""Симулятор датчиків температури, вологості й освітлення.

Імітує добовий цикл: вдень тепліше, світліше і сухіше, вночі навпаки.
Щоб не чекати добу, час прискорено (параметр --speed). Покази надсилаються
пакетом на бекенд через POST /api/readings/batch.

Запуск:  python -m simulator.sensor_simulator --interval 2 --speed 600
"""

import argparse
import json
import math
import random
import time
import urllib.error
import urllib.request
from datetime import UTC, datetime

DEFAULT_SENSORS = {"temp-1": "temperature", "hum-1": "humidity", "light-1": "light"}


def day_phase(hour: float) -> float:
    """Від -1 (найхолодніша пора, ~03:00) до 1 (найтепліша, ~15:00)."""
    return math.sin((hour - 9.0) / 24.0 * 2 * math.pi)


def generate_value(kind: str, hour: float, rng: random.Random) -> float:
    """Повертає правдоподібне значення датчика для заданої години доби (0–24)."""
    phase = day_phase(hour)
    if kind == "temperature":
        value = 22.0 + 5.0 * phase + rng.gauss(0, 0.4)
        return round(value, 1)
    if kind == "humidity":
        value = 65.0 - 12.0 * phase + rng.gauss(0, 1.5)
        return round(min(100.0, max(0.0, value)), 1)
    if kind == "light":
        # Світло є лише між 06:00 і 20:00, з максимумом опівдні.
        if not 6.0 <= hour <= 20.0:
            return 0.0
        sun = math.sin((hour - 6.0) / 14.0 * math.pi)
        value = 40000.0 * sun + rng.gauss(0, 800)
        return round(max(0.0, value), 0)
    raise ValueError(f"Невідомий тип датчика: {kind}")


def make_batch(
    sensors: dict[str, str], hour: float, rng: random.Random, anomaly_rate: float = 0.0
) -> list[dict]:
    """Формує пакет показів. З імовірністю anomaly_rate показ «стрибає» (збій/аварія)."""
    batch = []
    for name, kind in sensors.items():
        value = generate_value(kind, hour, rng)
        if anomaly_rate and rng.random() < anomaly_rate:
            value = round(value * rng.choice([0.3, 1.6]), 1)
            if kind == "humidity":
                value = min(100.0, value)
        batch.append({"sensor": name, "value": value})
    return batch


def send_batch(api_url: str, batch: list[dict], timeout: float = 5.0) -> int:
    """Надсилає пакет на бекенд, повертає HTTP-статус."""
    if not api_url.startswith(("http://", "https://")):
        raise ValueError(f"Адреса бекенда має починатися з http:// або https://: {api_url}")
    req = urllib.request.Request(
        api_url.rstrip("/") + "/api/readings/batch",
        data=json.dumps(batch).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return resp.status


def run(api_url: str, interval: float, speed: float, anomaly_rate: float,
        seed: int | None, count: int | None) -> None:
    rng = random.Random(seed)
    start_real = time.monotonic()
    now = datetime.now(UTC)
    start_hour = now.hour + now.minute / 60
    sent = 0
    while count is None or sent < count:
        sim_hours = (time.monotonic() - start_real) * speed / 3600
        hour = (start_hour + sim_hours) % 24
        batch = make_batch(DEFAULT_SENSORS, hour, rng, anomaly_rate)
        try:
            status = send_batch(api_url, batch)
            values = ", ".join(f"{b['sensor']}={b['value']}" for b in batch)
            print(f"[{hour:05.2f} год] {status}: {values}")
        except urllib.error.HTTPError as e:
            print(f"Бекенд відхилив пакет: {e.code} {e.read().decode('utf-8', 'replace')}")
        except urllib.error.URLError as e:
            print(f"Бекенд недоступний ({e.reason}), повтор через {interval} с")
        sent += 1
        time.sleep(interval)


def main(argv: list[str] | None = None) -> None:
    p = argparse.ArgumentParser(description="Симулятор датчиків теплиці")
    p.add_argument("--api", default="http://127.0.0.1:8000", help="адреса бекенда")
    p.add_argument("--interval", type=float, default=2.0, help="секунд між пакетами")
    p.add_argument("--speed", type=float, default=600.0,
                   help="прискорення часу (600 = доба за 2,4 хв)")
    p.add_argument("--anomaly-rate", type=float, default=0.02,
                   help="імовірність аномального показу (0–1)")
    p.add_argument("--seed", type=int, default=None, help="зерно генератора для відтворюваності")
    p.add_argument("--count", type=int, default=None, help="скільки пакетів надіслати (безкінечно)")
    args = p.parse_args(argv)
    try:
        run(args.api, args.interval, args.speed, args.anomaly_rate, args.seed, args.count)
    except KeyboardInterrupt:
        print("\nСимулятор зупинено.")


if __name__ == "__main__":
    main()
