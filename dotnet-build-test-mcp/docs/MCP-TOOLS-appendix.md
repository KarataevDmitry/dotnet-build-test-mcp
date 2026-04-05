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
