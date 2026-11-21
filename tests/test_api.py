"""Тести REST API, зокрема крайніх випадків."""


def test_health(client):
    assert client.get("/api/health").json() == {"status": "ok"}


def test_default_sensors_seeded(client):
    names = {s["name"] for s in client.get("/api/sensors").json()}
    assert names == {"temp-1", "hum-1", "light-1"}


def test_add_and_list_reading(client):
    r = client.post("/api/readings", json={"sensor": "temp-1", "value": 23.4})
    assert r.status_code == 201
    body = r.json()
    assert body["sensor"] == "temp-1" and body["value"] == 23.4
    listed = client.get("/api/readings", params={"sensor": "temp-1"}).json()
    assert [x["value"] for x in listed] == [23.4]


def test_latest_status_classification(client):
    client.post("/api/readings", json={"sensor": "temp-1", "value": 35})
    client.post("/api/readings", json={"sensor": "hum-1", "value": 30})
    latest = {x["sensor"]: x for x in client.get("/api/readings/latest").json()}
    assert latest["temp-1"]["status"] == "high"
    assert latest["hum-1"]["status"] == "low"
    # Крайній випадок: датчик без жодного виміру.
    assert latest["light-1"]["status"] == "no_data"
    assert latest["light-1"]["value"] is None


def test_latest_picks_newest_by_time(client):
    client.post("/api/readings", json={"sensor": "temp-1", "value": 20,
                                       "recorded_at": "2026-01-01T12:00:00Z"})
    client.post("/api/readings", json={"sensor": "temp-1", "value": 25,
                                       "recorded_at": "2026-01-01T10:00:00Z"})
    latest = {x["sensor"]: x for x in client.get("/api/readings/latest").json()}
    assert latest["temp-1"]["value"] == 20


def test_timezone_normalized_to_utc(client):
    r = client.post("/api/readings", json={"sensor": "temp-1", "value": 20,
                                           "recorded_at": "2026-01-01T14:00:00+02:00"})
    assert r.json()["recorded_at"] == "2026-01-01T12:00:00+00:00"


# --- Крайні випадки ---------------------------------------------------------

def test_unknown_sensor_rejected(client):
    r = client.post("/api/readings", json={"sensor": "nope", "value": 1})
    assert r.status_code == 404


def test_value_outside_physical_range_rejected(client):
    assert client.post("/api/readings", json={"sensor": "hum-1", "value": 101}).status_code == 422
    assert client.post("/api/readings", json={"sensor": "temp-1", "value": -41}).status_code == 422


def test_boundary_values_accepted(client):
    assert client.post("/api/readings", json={"sensor": "hum-1", "value": 0}).status_code == 201
    assert client.post("/api/readings", json={"sensor": "hum-1", "value": 100}).status_code == 201


def test_non_numeric_value_rejected(client):
    r = client.post("/api/readings", json={"sensor": "temp-1", "value": "hot"})
    assert r.status_code == 422
    assert client.post("/api/readings", json={"sensor": "temp-1"}).status_code == 422


def test_batch_saved(client):
    batch = [{"sensor": "temp-1", "value": 22}, {"sensor": "light-1", "value": 12000}]
    r = client.post("/api/readings/batch", json=batch)
    assert r.status_code == 201
    assert len(client.get("/api/readings").json()) == 2


def test_batch_is_atomic(client):
    batch = [{"sensor": "temp-1", "value": 22}, {"sensor": "hum-1", "value": 500}]
    assert client.post("/api/readings/batch", json=batch).status_code == 422
    assert client.get("/api/readings").json() == []


def test_empty_batch_rejected(client):
    assert client.post("/api/readings/batch", json=[]).status_code == 422


def test_sql_injection_in_sensor_name_is_harmless(client):
    evil = "temp-1'; DROP TABLE readings; --"
    assert client.get("/api/readings", params={"sensor": evil}).status_code == 404
    assert client.post("/api/readings", json={"sensor": "temp-1", "value": 21}).status_code == 201


def test_limit_bounds(client):
    assert client.get("/api/readings", params={"limit": 0}).status_code == 422
    assert client.get("/api/readings", params={"limit": 1001}).status_code == 422


def test_dashboard_served(client):
    r = client.get("/")
    assert r.status_code == 200
    assert "Моніторинг теплиці" in r.text
