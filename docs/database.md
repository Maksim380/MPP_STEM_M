# Цілісність і безпека даних (C5)

Модель — `src/Greenhouse.Core/Data/GreenhouseDbContext.cs`. Міграції окремо для кожної СУБД:
`Data/Migrations/Sqlite` і `Data/Migrations/Postgres`; вибір через `Database:Provider`.
ER-діаграма — у [README](../README.md#архітектура-бази-даних-er-діаграма).

| Таблиця | Що зберігає | Ключі та обмеження |
|---|---|---|
| `users` | логін, хеш пароля PBKDF2, роль | PK `id`, UNIQUE `username`, CHECK `role IN ('admin','operator','viewer')` |
| `refresh_tokens` | SHA-256 хеш токена, строк дії, відкликання | PK `id`, FK `user_id` → users (CASCADE), UNIQUE `token_hash` |
| `greenhouses` | назва, розташування | PK `id`, UNIQUE `name` |
| `sensor_types` | одиниця і фізичний діапазон типу | PK `kind`, CHECK `valid_min < valid_max` |
| `thresholds` | норма типу датчика в теплиці | PK (`greenhouse_id`, `kind`), FK → greenhouses (CASCADE), FK → sensor_types, CHECK `comfort_min <= comfort_max` |
| `sensors` | датчик у теплиці | PK `id`, UNIQUE `name`, FK `kind` → sensor_types, FK `greenhouse_id` → greenhouses (RESTRICT) |
| `readings` | виміри | PK `id`, FK `sensor_id` → sensors (CASCADE) |
| `alerts` | періоди виходу за норму | PK `id`, FK `sensor_id` → sensors (CASCADE), CHECK `kind IN ('low','high')`, CHECK `closed_at IS NULL OR closed_at >= opened_at` |

| Критерій | Виконано | Підтвердження |
|---|---|---|
| Нормалізація ≥ 3НФ | ✅ | Фізичний діапазон зберігається один раз у `sensor_types`, норма — один раз на пару (теплиця, тип) у `thresholds`; у `readings` і `alerts` лише ключ датчика. Неключових транзитивних залежностей немає. |
| PK/FK та обмеження цілісності | ✅ | Див. таблицю вище. Тест `Integrity_constraints_reject_bad_rows` вставляє 8 некоректних рядків напряму в SQLite — усі відхилено. Видалити теплицю з датчиками не можна (RESTRICT + перевірка в сервісі, 409). |
| Індекси на полях пошуку / JOIN | ✅ | `IX_readings_sensor_id_recorded_at`, `IX_sensors_name`, `IX_sensors_greenhouse_id`, `IX_alerts_sensor_id_closed_at`, `IX_alerts_opened_at`, `IX_users_username`, `IX_refresh_tokens_token_hash`. Тест `Indexes_exist_on_search_and_join_columns`. |
| Параметризовані запити | ✅ | Усі запити через LINQ EF Core, значення передаються як параметри; сирого SQL у коді немає. Тест `Sql_injection_in_sensor_name_is_harmless`. |
| Транзакції де потрібно | ✅ | `ReadingService.AddAsync`: покази пакета й оновлення тривог в одній транзакції через `CreateExecutionStrategy` (працює і з повторними спробами PostgreSQL). Тести `Batch_is_saved_atomically`, `Rejected_batch_does_not_create_alerts`. |

Безпека даних користувачів: паролі — PBKDF2-SHA256, 100 000 ітерацій, випадкова сіль; refresh-токени
зберігаються лише як SHA-256 хеш; секрети не лежать у коді.

Перевірити вручну (SQLite):

```bash
sqlite3 src/Greenhouse.Api/greenhouse.db ".schema readings"
sqlite3 src/Greenhouse.Api/greenhouse.db "PRAGMA index_list('readings');"
```

PostgreSQL: `psql -d greenhouse -c '\d+ readings'`.
