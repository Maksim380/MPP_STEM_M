# Підготовка до захисту (таблиці з «Як захищати проєкт з МПП»)

Таблиці B1–B3, B2 і D1 — індивідуальні, їх заповнює кожен учасник. Нижче — те, що вже є в репозиторії, і місце для решти.

## B1. Виконання індивідуальної зони відповідальності

Ланцюг: issue → гілка → PR → коміти → review → merge.

| Учасник | Зона | Issue | Гілка | PR | Review | Merge |
|---|---|---|---|---|---|---|
| Макс | Бекенд, БД, тривоги, авторизація | [#1](https://github.com/Maksim380/MPP_STEM_M/issues/1), [#2](https://github.com/Maksim380/MPP_STEM_M/issues/2), [#3](https://github.com/Maksim380/MPP_STEM_M/issues/3) | `claude/project-thread-0sq9ub` | [#9](https://github.com/Maksim380/MPP_STEM_M/pull/9) | | |
| … | Симулятор | [#4](https://github.com/Maksim380/MPP_STEM_M/issues/4) | | | | |
| … | Логи, тести, CI, Docker | [#5](https://github.com/Maksim380/MPP_STEM_M/issues/5), [#6](https://github.com/Maksim380/MPP_STEM_M/issues/6), [#7](https://github.com/Maksim380/MPP_STEM_M/issues/7) | | | | |
| … | Документація | [#8](https://github.com/Maksim380/MPP_STEM_M/issues/8) | | | | |

## B2. Виконання зобов'язань і комунікація ризиків

| Учасник | Завдання (issue) | Дедлайн | Виконано вчасно / ризик комуніковано |
|---|---|---|---|
| | | | |

## B3. Результативність code-review

Зараховується рев'ю чужого PR, де знайдено суттєву проблему або запропоновано обґрунтоване покращення, автор виправив, а рецензент перевірив. Потрібно ≥ 3 на учасника.

| Учасник | PR | Що знайдено | Посилання на виправлення |
|---|---|---|---|
| | | | |

## C0. Демонстрація на кількох ОС

| ОС | Як показати |
|---|---|
| Ubuntu 24.04 | README → «Запуск на Ubuntu»; CI-джоби `build-test (ubuntu-latest)` і `smoke` |
| Windows | README → «Запуск на Windows»; CI-джоба `build-test (windows-latest)` |
| macOS | CI-джоба `build-test (macos-latest)` |
| Docker | `docker compose up --build` |

## C1, C2, C3, C4, C5

- C1: [test-cases.md](test-cases.md)
- C2: [edge-cases.md](edge-cases.md)
- C3, C4: [quality.md](quality.md)
- C5: [database.md](database.md)

## D1. Складність і повнота зони

| Учасник | Що робив | Складність |
|---|---|---|
| | | |
