# Автотести, покриття і статичний аналіз (C3, C4)

## C3. Автотести і покриття

- Фреймворк: xUnit, інтеграційні тести через `WebApplicationFactory` (справжній застосунок із тимчасовою SQLite).
- 105 тестів: домен (норми, правило тривог), симулятор, REST API, авторизація, схема БД, обробник винятків.
- Покриття: coverlet + ReportGenerator. Згенерований код міграцій EF Core виключено (`coverlet.runsettings`).
- CI (GitHub Actions) запускає тести на Ubuntu, Windows і macOS і падає, якщо покриття рядків < 80 %. HTML-звіт — артефакт `coverage-<os>` у кожному прогоні.

Знімок звіту (21.11.2026, `dotnet reportgenerator … -reporttypes:TextSummary`):

```
Line coverage:   99.1%  (672 of 678)
Branch coverage: 94.2%  (198 of 210)
Method coverage: 98.4%  (62 of 63)
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
- Результат на 21.11.2026: **0 зауважень** на ~1 300 рядків коду → рівень 1.0.

```bash
dotnet build -c Release --no-incremental   # 0 Warning(s), 0 Error(s)
dotnet format --verify-no-changes          # код відповідає .editorconfig
```

За бажанням викладача можна додатково підключити SonarCloud (безкоштовний для публічних репозиторіїв).
