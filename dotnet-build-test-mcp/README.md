# Dotnet Build Test MCP

MCP-сервер с двумя тулами: **build_structured** и **run_tests**. Те же контракты, что в Cascade IDE (ide_build, ide_run_tests), но работают без IDE — в Cursor или любом хосте с MCP.

## Стек

- C#, .NET 10, win-x64, self-contained.
- Библиотека [DotNetBuildTestParsers](../DotNetBuildTestParsers) — разбор вывода dotnet build и dotnet test.

## Публикация

```bash
dotnet publish -c Release -o publish
```

Junction: например `D:\dotnet-build-test-mcp` → каталог `publish`; в Cursor в mcp.json указать `command`: `D:\dotnet-build-test-mcp\DotnetBuildTestMcp.exe`, `args`: `[]`.

## Тулы

| Имя | Описание | Аргументы |
|-----|----------|-----------|
| `build_structured` | Запустить dotnet build по решению. Возвращает JSON: success, exit_code, errors[], warnings[], raw_output. | `solution_path` — путь к .sln или каталогу с .sln |
| `run_tests` | Запустить dotnet test по решению. Возвращает JSON: success, total, passed, failed, skipped, failed_tests[]. | `solution_path` |

Формат ответа совпадает с ide_build и ide_run_tests в Cascade IDE — агент может использовать один и тот же разбор и с IDE, и без неё.

## Проверенные кейсы

Поведение проверено в Cursor с тестовым решением; агент получает структурированный вывод без парсинга лога.

| Кейс | Ожидание | Результат |
|------|----------|-----------|
| `build_structured`, сборка успешна | success: true, errors: [], warnings: [] | ✓ |
| `build_structured`, ошибки компиляции | success: false, errors[] с file, line, column, code, message | ✓ |
| `run_tests`, все тесты прошли | success: true, failed_tests: [] | ✓ |
| `run_tests`, есть упавшие | success: false, failed_tests[] с name, message?, duration_ms? | ✓ |
