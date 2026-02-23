using System.Collections.Frozen;
using System.Diagnostics;
using System.Text.Json;
using DotNetBuildTestParsers;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tool = ModelContextProtocol.Protocol.Tool;

// MCP-сервер: структурированные сборка и тесты (dotnet build / dotnet test).
// Тулы build_structured и run_tests — те же контракты, что в Cascade IDE (ide_build, ide_run_tests).
// Подключается в Cursor без IDE.

static JsonElement Schema(object schema) => JsonSerializer.SerializeToElement(schema);

var toolsList = new List<Tool>
{
    new()
    {
        Name = "build_structured",
        Description = "Запустить сборку (dotnet build) по пути к решению. Возвращает JSON: success, exit_code, errors[] (file, line, column?, code?, message), warnings[], raw_output (обрезано). Агент получает структурированные ошибки без парсинга лога. Можно вызывать без Cascade IDE.",
        InputSchema = Schema(new
        {
            type = "object",
            properties = new
            {
                solution_path = new { type = "string", description = "Путь к .sln или каталогу, в котором искать .sln." }
            },
            required = new[] { "solution_path" }
        })
    },
    new()
    {
        Name = "run_tests",
        Description = "Запустить тесты (dotnet test) по пути к решению. Возвращает JSON: success, total, passed, failed, skipped, failed_tests[] (name, message?, duration_ms?). При необходимости выполняет сборку. Агент получает упавшие тесты без парсинга лога. Можно вызывать без Cascade IDE.",
        InputSchema = Schema(new
        {
            type = "object",
            properties = new
            {
                solution_path = new { type = "string", description = "Путь к .sln или каталогу, в котором искать .sln." }
            },
            required = new[] { "solution_path" }
        })
    }
};

const int MaxRawChars = 4000;

static string ResolveSolutionPath(string path)
{
    var full = Path.GetFullPath(path.Trim());
    if (File.Exists(full) && full.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        return full;
    if (Directory.Exists(full))
    {
        var sln = Directory.GetFiles(full, "*.sln").FirstOrDefault();
        if (sln is not null) return sln;
        throw new ArgumentException($"No .sln found in directory: {full}");
    }
    throw new ArgumentException($"Path not found or not a solution: {path}");
}

static async Task<(string output, int exitCode)> RunDotnetAsync(string workingDir, string[] args, CancellationToken cancellationToken)
{
    var psi = new ProcessStartInfo("dotnet")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = workingDir
    };
    foreach (var a in args)
        psi.ArgumentList.Add(a);
    using var process = Process.Start(psi);
    if (process is null)
        throw new InvalidOperationException("Failed to start dotnet.");
    var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
    var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
    await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    var outStr = await stdout + "\n" + await stderr;
    if (process.ExitCode != 0)
        outStr += $"\nExit code: {process.ExitCode}";
    return (outStr, process.ExitCode);
}

static async Task<string> HandleBuildStructuredAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken cancellationToken)
{
    if (!args.TryGetValue("solution_path", out var sp) || sp.GetString() is not { } solutionPath || string.IsNullOrWhiteSpace(solutionPath))
        throw new ArgumentException("solution_path is required.");
    var sln = ResolveSolutionPath(solutionPath);
    var dir = Path.GetDirectoryName(sln) ?? "";
    var (output, _) = await RunDotnetAsync(dir, ["build", sln], cancellationToken).ConfigureAwait(false);
    var parsed = BuildOutputParser.Parse(output);
    var rawTruncated = output.Length > MaxRawChars ? output[..MaxRawChars] + "\n... (output truncated)" : output;
    var result = new
    {
        success = parsed.Success,
        exit_code = parsed.ExitCode,
        errors = parsed.Errors.Select(e => new { e.File, e.Line, e.Column, e.Code, e.Message }).ToList(),
        warnings = parsed.Warnings.Select(w => new { w.File, w.Line, w.Column, w.Code, w.Message }).ToList(),
        raw_output = rawTruncated
    };
    return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
}

static async Task<string> HandleRunTestsAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken cancellationToken)
{
    if (!args.TryGetValue("solution_path", out var sp) || sp.GetString() is not { } solutionPath || string.IsNullOrWhiteSpace(solutionPath))
        throw new ArgumentException("solution_path is required.");
    var sln = ResolveSolutionPath(solutionPath);
    var dir = Path.GetDirectoryName(sln) ?? "";
    var (output, _) = await RunDotnetAsync(dir, ["test", sln, "--logger", "console;verbosity=detailed"], cancellationToken).ConfigureAwait(false);
    var parsed = TestOutputParser.Parse(output);
    var result = new
    {
        success = parsed.Success,
        total = parsed.Total,
        passed = parsed.Passed,
        failed = parsed.Failed,
        skipped = parsed.Skipped,
        failed_tests = parsed.FailedTests.Select(t => new { t.Name, t.Message, duration_ms = t.DurationMs }).ToList()
    };
    return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
}

var options = new McpServerOptions
{
    ServerInfo = new Implementation { Name = "DotnetBuildTestMcp", Version = "0.1.0" },
    ProtocolVersion = "2024-11-05",
    Capabilities = new ServerCapabilities { Tools = new ToolsCapability { ListChanged = false } },
    Handlers = new McpServerHandlers
    {
        ListToolsHandler = (_, _) => ValueTask.FromResult(new ListToolsResult { Tools = toolsList }),

        CallToolHandler = async (request, cancellationToken) =>
        {
            var name = request.Params?.Name ?? "";
            var args = request.Params?.Arguments is IReadOnlyDictionary<string, JsonElement> a
                ? a
                : FrozenDictionary<string, JsonElement>.Empty;
            try
            {
                var text = name switch
                {
                    "build_structured" => await HandleBuildStructuredAsync(args, cancellationToken).ConfigureAwait(false),
                    "run_tests" => await HandleRunTestsAsync(args, cancellationToken).ConfigureAwait(false),
                    _ => throw new ArgumentException($"Unknown tool: {name}.")
                };
                return new CallToolResult { Content = [new TextContentBlock { Text = text }], IsError = false };
            }
            catch (ArgumentException ex)
            {
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = $"Error: {ex.Message}" }],
                    IsError = true
                };
            }
            catch (Exception ex)
            {
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = "Error: " + ex.Message }],
                    IsError = true
                };
            }
        }
    }
};

var transport = new StdioServerTransport("DotnetBuildTestMcp");
await using var server = McpServer.Create(transport, options);
await server.RunAsync();
return 0;
