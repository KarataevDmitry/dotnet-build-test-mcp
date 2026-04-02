# Dotnet Build/Test MCP — каталог тулов

<!-- GENERATED:ToolCatalog START -->

> Автогенерация из `ToolCatalog.Build()`. Не править этот блок вручную.
>
> Обновление: из каталога `dotnet-build-test-mcp` выполнить `dotnet run --project tools/ExportMcpManifest -- --write`.
>
> Тексты совпадают с полем `description` у инструментов MCP; полная схема — в `inputSchema`.

### `build_structured`

Запустить dotnet build с single-flight очередью. По умолчанию возвращает компактный summary; полный raw_output только по include_raw_output=true. Поддерживает wait_for_completion=false (асинхронная job), timeout_seconds и queue backpressure (busy + retry_after_seconds).

### `run_tests`

Запустить dotnet test с single-flight очередью. По умолчанию возвращает компактный summary; full raw_output только по include_raw_output=true. Поддерживает wait_for_completion=false, timeout_seconds и queue backpressure (busy + retry_after_seconds).

### `get_job_status`

Получить статус ранее запущенной job: queued/running/done/failed/cancelled/timed_out + итоговый результат, когда job завершена.

### `get_job_log`

Прочитать лог job чанками (offset/limit) для больших выводов.

### `cancel_job`

Отменить queued/running job с понятной причиной отмены в статусе.

<!-- GENERATED:ToolCatalog END -->

