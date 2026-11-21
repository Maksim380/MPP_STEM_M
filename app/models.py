"""Схеми запитів і відповідей API."""

import math
from datetime import UTC, datetime
from typing import Literal

from pydantic import BaseModel, Field, field_validator

Status = Literal["ok", "low", "high", "no_data"]


class ReadingIn(BaseModel):
    sensor: str = Field(min_length=1, max_length=64, examples=["temp-1"])
    value: float = Field(examples=[23.5])
    recorded_at: datetime | None = Field(
        default=None, description="Час виміру; якщо не вказано, береться поточний (UTC)."
    )

    @field_validator("value")
    @classmethod
    def value_must_be_finite(cls, v: float) -> float:
        if math.isnan(v) or math.isinf(v):
            raise ValueError("значення має бути скінченним числом")
        return v

    def timestamp(self) -> str:
        """Час у форматі ISO 8601 UTC, однаковому для всіх записів у БД."""
        ts = self.recorded_at or datetime.now(UTC)
        if ts.tzinfo is None:
            ts = ts.replace(tzinfo=UTC)
        return ts.astimezone(UTC).isoformat(timespec="seconds")


class ReadingOut(BaseModel):
    id: int
    sensor: str
    kind: str
    value: float
    recorded_at: str


class Sensor(BaseModel):
    name: str
    kind: str
    unit: str
    comfort_min: float
    comfort_max: float


class LatestReading(BaseModel):
    sensor: str
    kind: str
    unit: str
    value: float | None
    recorded_at: str | None
    status: Status
