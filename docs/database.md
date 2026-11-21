# Цілісність і безпека даних (C5)

Схема задана в `src/Greenhouse.Api/Data/GreenhouseDbContext.cs`, міграція — `Data/Migrations/*_InitialCreate.cs`.

```
sensor_types (kind PK, unit, valid_min, valid_max, comfort_min, comfort_max)
     │ 1
     │ N
sensors (id PK, name UNIQUE, kind FK → sensor_types)
     │ 1                         │ 1
     │ N                         │ N
readings (id PK, sensor_id FK,   alerts (id PK, sensor_id FK, kind, trigger_value,
          value, recorded_at)            opened_at, closed_at, acknowledged_at, acknowledged_by)
```

| Критерій | Виконано | Підтвердження |
|---|---|---|
| Нормалізація ≥ 3НФ | ✅ | Одиниця і діапазони зберігаються один раз у `sensor_types`, а не в кожному датчику чи вимірі; у `readings` і `alerts` лише ключ датчика. Неключових транзитивних залежностей немає. |
| PK/FK та обмеження цілісності | ✅ | PK у всіх таблицях; FK `sensors.kind`, `readings.sensor_id`, `alerts.sensor_id`; `UNIQUE(name)`; `CHECK(valid_min < valid_max)`, `CHECK(comfort_min <= comfort_max)`, `CHECK(kind IN ('low','high'))`, `CHECK(closed_at IS NULL OR closed_at >= opened_at)`. Тест `Integrity_constraints_reject_bad_rows` пробує вставити 5 поганих рядків напряму в SQLite — усі відхилено. |
| Індекси на полях пошуку / JOIN | ✅ | `IX_readings_sensor_id_recorded_at` (історія й останній показ), `IX_sensors_name` (пошук за назвою), `IX_alerts_sensor_id_closed_at` (відкрита тривога датчика), `IX_alerts_opened_at` (журнал). Тест `Indexes_exist_on_search_and_join_columns`. |
| Параметризовані запити | ✅ | Усі запити через LINQ EF Core, значення передаються як параметри; сирого SQL у коді немає. Тест `Sql_injection_in_sensor_name_is_harmless`. |
| Транзакції де потрібно | ✅ | `ReadingService.AddAsync`: покази пакета й оновлення тривог в одній транзакції (`BeginTransactionAsync` → `CommitAsync`). Тести `Batch_is_saved_atomically`, `Rejected_batch_does_not_create_alerts`. |

Перевірити вручну:

```bash
sqlite3 src/Greenhouse.Api/greenhouse.db ".schema"
sqlite3 src/Greenhouse.Api/greenhouse.db "PRAGMA index_list('readings');"
```
