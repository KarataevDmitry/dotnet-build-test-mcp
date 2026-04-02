using System.Text.Json;
using ModelContextProtocol.Protocol;
using Tool = ModelContextProtocol.Protocol.Tool;

namespace DotnetBuildTestMcp;

/// <summary>Каталог MCP-тулов. Согласован с <c>mcp-tools.manifest.json</c> и <c>docs/MCP-TOOLS.md</c> (генерация: <c>tools/ExportMcpManifest</c>).</summary>
internal static class ToolCatalog
{
    private static JsonElement Schema(object schema) => JsonSerializer.SerializeToElement(schema);

    internal static List<Tool> Build() =>
    [
        new()
        {
            Name = "build_structured",
            Description =
                "Запустить dotnet build с single-flight очередью. По умолчанию возвращает компактный summary; полный raw_output только по include_raw_output=true. Поддерживает wait_for_completion=false (асинхронная job), timeout_seconds и queue backpressure (busy + retry_after_seconds).",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    solution_path = new { type = "string", description = "Путь к .sln или каталогу, в котором искать .sln." },
                    wait_for_completion = new { type = "boolean", description = "Ждать завершения операции (по умолчанию true)." },
                    include_raw_output = new { type = "boolean", description = "Включить полный raw_output в ответе (по умолчанию false)." },
                    timeout_seconds = new { type = "integer", description = "Таймаут в секундах (по умолчанию 600)." }
                },
                required = new[] { "solution_path" }
            })
        },
        new()
        {
            Name = "run_tests",
            Description =
                "Запустить dotnet test с single-flight очередью. По умолчанию возвращает компактный summary; full raw_output только по include_raw_output=true. Поддерживает wait_for_completion=false, timeout_seconds и queue backpressure (busy + retry_after_seconds).",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    solution_path = new { type = "string", description = "Путь к .sln или каталогу, в котором искать .sln." },
                    wait_for_completion = new { type = "boolean", description = "Ждать завершения операции (по умолчанию true)." },
                    include_raw_output = new { type = "boolean", description = "Включить полный raw_output в ответе (по умолчанию false)." },
                    timeout_seconds = new { type = "integer", description = "Таймаут в секундах (по умолчанию 900)." }
                },
                required = new[] { "solution_path" }
            })
        },
        new()
        {
            Name = "get_job_status",
            Description =
                "Получить статус ранее запущенной job: queued/running/done/failed/cancelled/timed_out + итоговый результат, когда job завершена.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    job_id = new { type = "string", description = "Идентификатор job из build_structured/run_tests." }
                },
                required = new[] { "job_id" }
            })
        },
        new()
        {
            Name = "get_job_log",
            Description = "Прочитать лог job чанками (offset/limit) для больших выводов.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    job_id = new { type = "string", description = "Идентификатор job." },
                    offset_lines = new { type = "integer", description = "С какой строки читать (0-based)." },
                    limit_lines = new { type = "integer", description = "Сколько строк вернуть (по умолчанию 200, макс 2000)." }
                },
                required = new[] { "job_id" }
            })
        },
        new()
        {
            Name = "cancel_job",
            Description = "Отменить queued/running job с понятной причиной отмены в статусе.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    job_id = new { type = "string", description = "Идентификатор job." }
                },
                required = new[] { "job_id" }
            })
        }
    ];
}
