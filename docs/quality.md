# Автотести, покриття і статичний аналіз (C3, C4)

## C3. Автотести і покриття

- Фреймворк: xUnit, інтеграційні тести через `WebApplicationFactory` (справжній застосунок із тимчасовою SQLite).
- 147 тестів: домен (норми, правило тривог), симулятор, REST API, теплиці й норми, ролі і токени, SignalR, фоновий сервіс, схема БД, обробник винятків.
- Покриття: coverlet + ReportGenerator. Згенерований код міграцій EF Core виключено (`coverlet.runsettings`).
- CI (GitHub Actions) запускає тести на Ubuntu, Windows і macOS і падає, якщо покриття рядків < 80 %. HTML-звіт — артефакт `coverage-<os>` у кожному прогоні.

Знімок звіту (21.11.2026, після переходу на шари Api/Core, `dotnet reportgenerator … -reporttypes:TextSummary`):

```
Line coverage:   98.4%  (1183 of 1202)
Branch coverage: 92.7%  (306 of 330)
Method coverage: 98.3%  (119 of 121)
```

Як отримати звіт локально:

```bash
dotnet tool restore
dotnet test --settings coverlet.runsettings --results-directory TestResults
dotnet reportgenerator -reports:"TestResults/*/coverage.cobertura.xml" -targetdir:coverage -reporttypes:"Html;TextSummary"
cat coverage/Summary.txt        # відкрити coverage/index.html у браузері
```

## C4. Статичний аналіз

- Інструмент: вбудовані Roslyn-аналізатори .NET (`AnalysisLevel=latest-recommended`, правила CA*/IDE*) + `dotnet format`.
- `TreatWarningsAsErrors=true` у `Directory.Build.props`: будь-яке зауваження аналізатора ламає збірку і CI.
- Результат на 21.11.2026: **0 зауважень** на ~2 600 рядків коду (без міграцій) → рівень 1.0.

```bash
dotnet build -c Release --no-incremental   # 0 Warning(s), 0 Error(s)
dotnet format --verify-no-changes          # код відповідає .editorconfig
```

За бажанням викладача можна додатково підключити SonarCloud (безкоштовний для публічних репозиторіїв).
