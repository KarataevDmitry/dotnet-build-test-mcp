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
| ----- | ---------- | ----------- |
| `build_structured` | Запустить `dotnet build` через single-flight очередь. Поддерживает sync/async режим и timeout. | `solution_path` (required), `wait_for_completion` (default `true`), `include_raw_output` (default `false`), `timeout_seconds` (default `600`) |
| `run_tests` | Запустить `dotnet test` через single-flight очередь. Поддерживает sync/async режим и timeout. | `solution_path` (required), `wait_for_completion` (default `true`), `include_raw_output` (default `false`), `timeout_seconds` (default `900`) |
| `get_job_status` | Вернуть статус job: `queued/running/done/failed/cancelled/timed_out` + итоговый result после завершения. | `job_id` |
| `get_job_log` | Читать лог job чанками (offset/limit), чтобы не отдавать гигантский payload одним JSON. | `job_id`, `offset_lines` (default `0`), `limit_lines` (default `200`, max `2000`) |
| `cancel_job` | Отменить queued/running job. | `job_id` |

## Поведение очереди (single-flight)

- Тяжёлые операции (`build_structured`, `run_tests`) выполняются **строго по одной**.
- Очередь bounded (`capacity=8`).
- При перегрузе возвращается компактный ответ:
  - `accepted: false`
  - `status: "busy"`
  - `retry_after_seconds`
- По умолчанию heavy-тулы возвращают **summary**; полный `raw_output` только при `include_raw_output=true`.

## Примеры

Синхронный build (дождаться завершения):

```json
{
  "solution_path": "D:/repo/MyApp.sln"
}
```

Асинхронный test + опрос статуса:

```json
{
  "solution_path": "D:/repo/MyApp.sln",
  "wait_for_completion": false,
  "timeout_seconds": 1200
}
```

Далее:

```json
{
  "job_id": "<id-from-run_tests>"
}
```

для `get_job_status` и `get_job_log`.

Формат ответа совпадает с ide_build и ide_run_tests в Cascade IDE — агент может использовать один и тот же разбор и с IDE, и без неё.

## Проверенные кейсы

Поведение проверено в Cursor с тестовым решением; агент получает структурированный вывод без парсинга лога.

| Кейс | Ожидание | Результат |
| ------ | ---------- | ----------- |
| `build_structured`, сборка успешна | success: true, errors: [], warnings: [] | ✓ |
| `build_structured`, ошибки компиляции | success: false, errors[] с file, line, column, code, message | ✓ |
| `run_tests`, все тесты прошли | success: true, failed_tests: [] | ✓ |
| `run_tests`, есть упавшие | success: false, failed_tests[] с name, message?, duration_ms? | ✓ |
| Параллельные heavy-вызовы | не падают, второй уходит в очередь/busy | ✓ |
| Большой лог | читается через `get_job_log` чанками | ✓ |
