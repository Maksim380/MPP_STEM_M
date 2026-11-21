import pytest
from fastapi.testclient import TestClient


@pytest.fixture
def client(tmp_path, monkeypatch):
    """Клієнт API з окремою порожньою БД для кожного тесту."""
    monkeypatch.setenv("GREENHOUSE_DB", str(tmp_path / "test.db"))
    from app.main import app

    with TestClient(app) as c:
        yield c
