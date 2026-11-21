# Крайні випадки і помилки (C2)

| # | Пункт критерію C2 | Крайній випадок | Як оброблено | Оброблено | Підтвердження (тест) |
|---|---|---|---|---|---|
| 1 | Валідація введення | Невідомий датчик; порожня назва; пакет > 1000; `limit` поза 1–1000; `from > to` | 404 / 400 з поясненням, нічого не записується | ✅ так | `Unknown_sensor_is_404_and_nothing_saved`, `Missing_fields_are_400`, `Empty_and_oversized_batches_are_400`, `Invalid_query_is_400` |
| 2 | Порожні / некоректні дані | Відсутнє `value`; `"abc"` замість числа; `NaN`; зламаний JSON; дата «вчора»; порожній пакет; `null` у пакеті | 400 ProblemDetails, сервер працює далі | ✅ так | `Malformed_body_is_400` (5 варіантів), `Missing_fields_are_400` |
| 3 | Винятки без аварійного завершення | Неочікуваний виняток у коді; бекенд недоступний для симулятора; тайм-аут; Ctrl+C | 500 без деталей + запис у лог; симулятор пише повідомлення і пробує знову | ✅ так | `GlobalExceptionHandlerTests`, `Survives_unavailable_backend`, `Survives_timeout`, `Stops_on_cancellation` |
| 4 | Межові значення | 0 % і 100 % вологості, −40 і 80 °C, 150 000 лк приймаються; 100.1 %, −41 °C, ∞ відхиляються; межі норми (18 і 28 °C) — «норма»; час у майбутньому > 5 хв | 201 для меж, 422 за межами | ✅ так | `Boundary_values_are_accepted`, `Value_outside_physical_range_is_422`, `Infinite_value_is_422`, `Classify_uses_inclusive_bounds`, `Future_timestamp_is_422` |
| 5 | Немає необроблених винятків у лог-файлі прогону | Прогін симулятора з аномаліями | У `logs/greenhouse-*.log` немає рядків `[ERR]`/`[FTL]`; CI-джоба `smoke` перевіряє це автоматично | ✅ так | `Errors_are_problem_details_and_logged_to_file`, CI `smoke` |

Додатково: SQL-ін'єкція в назві датчика (`temp-1'; DROP TABLE readings; --`) нічого не ламає — `Sql_injection_in_sensor_name_is_harmless`.
Атомарність пакета: якщо один вимір некоректний, не записується жоден і не створюються тривоги — `Batch_is_saved_atomically`, `Rejected_batch_does_not_create_alerts`.
