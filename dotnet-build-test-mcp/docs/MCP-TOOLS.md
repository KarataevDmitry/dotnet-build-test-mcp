# Dotnet Build/Test MCP — каталог тулов

<!-- GENERATED:ToolCatalog START -->

> Автогенерация из `ToolCatalog.Build()`. Не править этот блок вручную.
>
> Обновление: из каталога `dotnet-build-test-mcp` выполнить `dotnet run --project tools/ExportMcpManifest -- --write`.
>
> Тексты совпадают с полем `description` у инструментов MCP; полная схема — в `inputSchema`.

### `build_structured`

Запустить dotnet build с single-flight очередью. По умолчанию возвращает компактный summary; полный raw_output только по include_raw_output=true. Поддерживает wait_for_completion=false (асинхронная job), timeout_seconds и queue backpressure (busy + retry_after_seconds). Опции: configuration (-c), framework (-f), no_restore, additional_arguments.

### `run_tests`

Запустить dotnet test с single-flight очередью. По умолчанию возвращает компактный summary; full raw_output только по include_raw_output=true. Поддерживает wait_for_completion=false, timeout_seconds и queue backpressure (busy + retry_after_seconds). Опции: configuration, framework, no_restore, no_build, filter (--filter), additional_arguments.

### `publish_structured`

Запустить dotnet publish с single-flight очередью. Разбор вывода как у build (ошибки/предупреждения MSBuild). Опции: output (-o), configuration, framework, no_restore, no_build, additional_arguments.

### `get_job_status`

Получить статус ранее запущенной job: queued/running/done/failed/cancelled/timed_out + итоговый результат, когда job завершена.

### `get_job_log`

Прочитать лог job чанками (offset/limit) для больших выводов.

### `cancel_job`

Отменить queued/running job с понятной причиной отмены в статусе.

<!-- GENERATED:ToolCatalog END -->

## Примеры аргументов (JSON)

Фрагмент подключается к `MCP-TOOLS.md` при `dotnet run --project tools/ExportMcpManifest -- --write`. Редактируй этот файл, а не сгенерированный блок выше.

### `build_structured` — Release, без restore, доп. аргументы MSBuild

```json
{
  "solution_path": "D:/repo/App.sln",
  "configuration": "Release",
  "no_restore": true,
  "additional_arguments": ["-v", "m"]
}
```

### `build_structured` — решение `.slnx` по пути к файлу

```json
{
  "solution_path": "D:/repo/CascadeIDE.slnx",
  "configuration": "Debug"
}
```

### `run_tests` — фильтр по полному имени, без повторной сборки

```json
{
  "solution_path": "D:/repo/Tests/Tests.csproj",
  "filter": "FullyQualifiedName~MyNamespace.MyClass",
  "no_build": true,
  "configuration": "Debug"
}
```

### `run_tests` — целевой TFM и `--no-restore`

```json
{
  "solution_path": "D:/repo/App.sln",
  "framework": "net10.0",
  "no_restore": true
}
```

### `publish_structured` — вывод в каталог

```json
{
  "solution_path": "D:/repo/App/App.csproj",
  "configuration": "Release",
  "output": "D:/artifacts/publish",
  "no_restore": false
}
```

### `get_job_status` / `get_job_log` / `cancel_job`

```json
{ "job_id": "<id из ответа build_structured, run_tests или publish_structured>" }
```

```json
{ "job_id": "<id>", "offset_lines": 0, "limit_lines": 200 }
```

Очередь single-flight: при `status: busy` и `retry_after_seconds` повтори вызов позже.
