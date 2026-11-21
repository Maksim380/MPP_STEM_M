# Приймальні тест-кейси (C1)

Фіксований список кейсів. Кожен кейс автоматизовано (колонка «Автотест»), і його можна показати вручну через Swagger/curl або дашборд.
Ручна перевірка: запустити бекенд і симулятор за README.

Підсумок останнього прогону: **26 / 26 пройдено** (`dotnet test`, 147 тестів, 0 падінь).

| # | Кейс | Кроки | Очікуваний результат | Автотест | Результат |
|---|---|---|---|---|---|
| TC1 | Сервер працює | `GET /api/health` | 200, `{"status":"ok"}` | `ApiTests.Health_is_ok` | ✅ |
| TC2 | Довідник датчиків | `GET /api/sensors` | 3 датчики з одиницями та нормою | `Default_sensors_are_seeded_by_migration` | ✅ |
| TC2a | Теплиці | `GET /api/greenhouses` | «Теплиця №1» з 3 датчиками і стандартними нормами | `Seeded_greenhouse_has_sensors_and_default_thresholds` | ✅ |
| TC3 | Додати вимір | `POST /api/readings` з `X-Api-Key`, `{"sensor":"temp-1","value":23.4}` | 201, вимір є в `GET /api/readings` | `Add_and_list_reading` | ✅ |
| TC4 | Пакет вимірів | `POST /api/readings/batch` з 3 вимірами | 201, збережено 3 | `Valid_batch_is_saved` | ✅ |
| TC5 | Стан відносно норми | надіслати 35 °C і 30 % | `latest`: temp-1 = high, hum-1 = low | `Latest_classifies_status_and_handles_sensor_without_data` | ✅ |
| TC6 | Останній показ за часом | надіслати 20 о 12:00, потім 25 о 10:00 | останній = 20 | `Latest_picks_newest_by_time_not_by_insert_order` | ✅ |
| TC7 | Історія за період | 3 виміри в різні дні, `?from=…&to=…` | лише вимір у межах періоду | `History_can_be_filtered_by_period` | ✅ |
| TC8 | Тривога відкривається і закривається | 22 → 30 → 31 → 25 °C | одна тривога high, закрита на 25 °C | `Alert_opens_once_and_closes_when_back_to_normal` | ✅ |
| TC9 | Стрибок високо → низько | 30 → 10 °C | high закрита, low відкрита | `Jump_from_high_to_low_replaces_alert` | ✅ |
| TC10 | Підтвердження тривоги | `POST /api/auth/login` оператором, потім `POST /api/alerts/{id}/ack` з JWT | 200, `acknowledged_by` = логін оператора; повтор нічого не змінює | `Operator_acknowledges_alert_once` | ✅ |
| TC11 | Запис без ключа | `POST /api/readings` без `X-Api-Key` | 401 | `AuthTests.Writing_without_api_key_is_401` | ✅ |
| TC12 | Підтвердження без JWT | `POST /api/alerts/1/ack` з ключем датчика | 401 | `Sensor_key_cannot_acknowledge_alerts` | ✅ |
| TC13 | Неправильний пароль | `POST /api/auth/login` з хибним паролем | 401 | `Wrong_credentials_get_401` | ✅ |
| TC19 | Нова теплиця з датчиком | admin: `POST /api/greenhouses`, `POST /api/greenhouses/{id}/sensors`; пристрій шле показ | дані видно з фільтром `greenhouse_id`, тривога відкрита | `Admin_creates_greenhouse_with_sensor_and_it_receives_data` | ✅ |
| TC20 | Оператор змінює норму | `PUT /api/greenhouses/1/thresholds/temperature` 10–15, показ 20 °C | статус high, тривога high | `Operator_changes_threshold_and_it_affects_status_and_alerts` | ✅ |
| TC21 | Ролі | viewer: ack і зміна норми; operator: створення теплиці і список користувачів | 403 | `Viewer_cannot_…`, `Operator_cannot_manage_greenhouses_or_users` | ✅ |
| TC22 | Оновлення сесії | `POST /api/auth/refresh` двічі тим самим токеном | вдруге 401 і всі сесії відкликано | `Refresh_rotates_tokens_and_detects_reuse` | ✅ |
| TC23 | Вихід | `POST /api/auth/logout`, потім refresh | 401 | `Logout_revokes_refresh_token` | ✅ |
| TC24 | Захист від перебору паролів | 5 невдалих входів поспіль при ліміті 3 | 4-й і 5-й — 429 | `Too_many_login_attempts_get_429` | ✅ |
| TC25 | Живі оновлення | підключитися до `/hubs/telemetry`, надіслати показ | подія `readingsAdded`, а для 35 °C ще й `alertsChanged` | `Clients_receive_new_readings_and_alerts` | ✅ |
| TC26 | Вбудований симулятор | запуск з `Simulator:Enabled=true` | у БД з'являються виміри всіх датчиків | `Enabled_worker_runs_in_background` | ✅ |
| TC14 | Дашборд і OpenAPI | відкрити `/` і `/openapi/v1.json` | 200 | `Dashboard_and_openapi_are_served` | ✅ |
| TC15 | Симулятор надсилає дані | `dotnet run --project src/Greenhouse.Simulator -- --count 3` | 3 пакети, 201 | `SimulatorRunnerTests.Sends_requested_number_of_batches_with_api_key` + CI smoke | ✅ |
| TC16 | Симулятор переживає падіння сервера | запустити симулятор без бекенда | повідомлення «Бекенд недоступний», без аварії | `Survives_unavailable_backend` | ✅ |
| TC17 | Міграції створюють БД з нуля | видалити `greenhouse.db`, запустити бекенд | БД створена, 3 датчики | `DatabaseSchemaTests.No_pending_migrations` | ✅ |
| TC18 | Помилки пишуться в лог-файл | надіслати некоректний вимір | рядок «відхилено» у `logs/greenhouse-*.log`, без `[ERR]` | `Errors_are_problem_details_and_logged_to_file` | ✅ |

Скріншот дашборда після прогону симулятора: [dashboard.png](dashboard.png).
