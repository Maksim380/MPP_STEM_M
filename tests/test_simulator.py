"""Тести симулятора датчиків."""

import random

import pytest

from app import config
from simulator.sensor_simulator import DEFAULT_SENSORS, generate_value, make_batch, send_batch


@pytest.mark.parametrize("kind", ["temperature", "humidity", "light"])
def test_values_always_within_physical_range(kind):
    rng = random.Random(1)
    limits = config.SENSOR_TYPES[kind]
    for step in range(24 * 12):
        v = generate_value(kind, step / 12, rng)
        assert limits["valid_min"] <= v <= limits["valid_max"]


def test_no_light_at_night():
    rng = random.Random(1)
    assert generate_value("light", 2.0, rng) == 0.0
    assert generate_value("light", 23.5, rng) == 0.0


def test_day_is_warmer_than_night():
    rng = random.Random(1)
    assert generate_value("temperature", 15, rng) > generate_value("temperature", 3, rng)


def test_unknown_kind_raises():
    with pytest.raises(ValueError):
        generate_value("pressure", 12, random.Random(1))


def test_batch_with_anomalies_stays_valid():
    rng = random.Random(7)
    for _ in range(200):
        for item in make_batch(DEFAULT_SENSORS, rng.uniform(0, 24), rng, anomaly_rate=1.0):
            limits = config.SENSOR_TYPES[DEFAULT_SENSORS[item["sensor"]]]
            assert limits["valid_min"] <= item["value"] <= limits["valid_max"]


def test_simulator_sensors_match_backend_defaults():
    assert sorted(DEFAULT_SENSORS.items()) == sorted(config.DEFAULT_SENSORS)


def test_send_batch_rejects_non_http_url():
    with pytest.raises(ValueError):
        send_batch("file:///etc/passwd", [])
