using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using DotNetBuildTestParsers;
using DotnetBuildTestMcp;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tool = ModelContextProtocol.Protocol.Tool;

var coordinator = new JobCoordinator();
var toolsList = ToolCatalog.Build();

var options = new McpServerOptions
{
    ServerInfo = new Implementation { Name = "DotnetBuildTestMcp", Version = "0.3.0" },
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
                : new Dictionary<string, JsonElement>();

            try
            {
                var text = name switch
                {
                    "build_structured" => await HandleBuildStructuredAsync(coordinator, args, cancellationToken).ConfigureAwait(false),
                    "run_tests" => await HandleRunTestsAsync(coordinator, args, cancellationToken).ConfigureAwait(false),
                    "publish_structured" => await HandlePublishStructuredAsync(coordinator, args, cancellationToken).ConfigureAwait(false),
                    "get_job_status" => HandleGetJobStatus(coordinator, args),
                    "get_job_log" => HandleGetJobLog(coordinator, args),
                    "cancel_job" => HandleCancelJob(coordinator, args),
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

static async Task<string> HandleBuildStructuredAsync(
    JobCoordinator coordinator,
    IReadOnlyDictionary<string, JsonElement> args,
    CancellationToken cancellationToken)
{
    var request = ParseExecutionRequest(args, defaultTimeoutSeconds: 600);
    var sln = SolutionOrProjectPathResolver.Resolve(request.SolutionPath);

    var enqueued = coordinator.TryEnqueue(
        JobKind.BuildStructured,
        sln,
        request.IncludeRawOutput,
        request.TimeoutSeconds,
        request.DotnetOptions);

    if (!enqueued.Accepted)
    {
        return JsonHelper.Serialize(new BusyResponse(
            accepted: false,
            status: "busy",
            retry_after_seconds: enqueued.RetryAfterSeconds,
            message: "Build/test worker is busy. Retry later."));
    }

    if (!request.WaitForCompletion)
    {
        return JsonHelper.Serialize(new
        {
            accepted = true,
            job_id = enqueued.JobId,
            status = "queued"
        });
    }

    var wait = await coordinator.WaitForCompletionAsync(enqueued.JobId!, cancellationToken).ConfigureAwait(false);
    if (wait is null)
    {
        return JsonHelper.Serialize(new
        {
            accepted = true,
            job_id = enqueued.JobId,
            status = "queued",
            message = "Request cancelled while waiting. Use get_job_status."
        });
    }

    return wait;
}

static async Task<string> HandleRunTestsAsync(
    JobCoordinator coordinator,
    IReadOnlyDictionary<string, JsonElement> args,
    CancellationToken cancellationToken)
{
    var request = ParseExecutionRequest(args, defaultTimeoutSeconds: 900);
    var sln = SolutionOrProjectPathResolver.Resolve(request.SolutionPath);

    var enqueued = coordinator.TryEnqueue(
        JobKind.RunTests,
        sln,
        request.IncludeRawOutput,
        request.TimeoutSeconds,
        request.DotnetOptions);

    if (!enqueued.Accepted)
    {
        return JsonHelper.Serialize(new BusyResponse(
            accepted: false,
            status: "busy",
            retry_after_seconds: enqueued.RetryAfterSeconds,
            message: "Build/test worker is busy. Retry later."));
    }

    if (!request.WaitForCompletion)
    {
        return JsonHelper.Serialize(new
        {
            accepted = true,
            job_id = enqueued.JobId,
            status = "queued"
        });
    }

    var wait = await coordinator.WaitForCompletionAsync(enqueued.JobId!, cancellationToken).ConfigureAwait(false);
    if (wait is null)
    {
        return JsonHelper.Serialize(new
        {
            accepted = true,
            job_id = enqueued.JobId,
            status = "queued",
            message = "Request cancelled while waiting. Use get_job_status."
        });
    }

    return wait;
}

static async Task<string> HandlePublishStructuredAsync(
    JobCoordinator coordinator,
    IReadOnlyDictionary<string, JsonElement> args,
    CancellationToken cancellationToken)
{
    var request = ParseExecutionRequest(args, defaultTimeoutSeconds: 900);
    var sln = SolutionOrProjectPathResolver.Resolve(request.SolutionPath);

    var enqueued = coordinator.TryEnqueue(
        JobKind.PublishStructured,
        sln,
        request.IncludeRawOutput,
        request.TimeoutSeconds,
        request.DotnetOptions);

    if (!enqueued.Accepted)
    {
        return JsonHelper.Serialize(new BusyResponse(
            accepted: false,
            status: "busy",
            retry_after_seconds: enqueued.RetryAfterSeconds,
            message: "Build/test worker is busy. Retry later."));
    }

    if (!request.WaitForCompletion)
    {
        return JsonHelper.Serialize(new
        {
            accepted = true,
            job_id = enqueued.JobId,
            status = "queued"
        });
    }

    var wait = await coordinator.WaitForCompletionAsync(enqueued.JobId!, cancellationToken).ConfigureAwait(false);
    if (wait is null)
    {
        return JsonHelper.Serialize(new
        {
            accepted = true,
            job_id = enqueued.JobId,
            status = "queued",
            message = "Request cancelled while waiting. Use get_job_status."
        });
    }

    return wait;
}

static string HandleGetJobStatus(JobCoordinator coordinator, IReadOnlyDictionary<string, JsonElement> args)
{
    if (!TryGetString(args, "job_id", out var jobId) || string.IsNullOrWhiteSpace(jobId))
        throw new ArgumentException("job_id is required.");

    var status = coordinator.GetJobStatus(jobId);
    if (status is null)
    {
        return JsonHelper.Serialize(new { found = false, job_id = jobId, message = "Job not found." });
    }

    return JsonHelper.Serialize(status);
}

static string HandleGetJobLog(JobCoordinator coordinator, IReadOnlyDictionary<string, JsonElement> args)
{
    if (!TryGetString(args, "job_id", out var jobId) || string.IsNullOrWhiteSpace(jobId))
        throw new ArgumentException("job_id is required.");

    var offset = TryGetInt(args, "offset_lines", out var parsedOffset) ? Math.Max(0, parsedOffset) : 0;
    var limit = TryGetInt(args, "limit_lines", out var parsedLimit) ? Math.Clamp(parsedLimit, 1, 2000) : 200;

    var logChunk = coordinator.GetJobLogChunk(jobId, offset, limit);
    if (logChunk is null)
    {
        return JsonHelper.Serialize(new { found = false, job_id = jobId, message = "Job not found." });
    }

    return JsonHelper.Serialize(logChunk);
}

static string HandleCancelJob(JobCoordinator coordinator, IReadOnlyDictionary<string, JsonElement> args)
{
    if (!TryGetString(args, "job_id", out var jobId) || string.IsNullOrWhiteSpace(jobId))
        throw new ArgumentException("job_id is required.");

    var result = coordinator.CancelJob(jobId);
    return JsonHelper.Serialize(result);
}

static ExecutionRequest ParseExecutionRequest(IReadOnlyDictionary<string, JsonElement> args, int defaultTimeoutSeconds)
{
    if (!TryGetString(args, "solution_path", out var solutionPath) || string.IsNullOrWhiteSpace(solutionPath))
        throw new ArgumentException("solution_path is required.");

    var waitForCompletion = !TryGetBool(args, "wait_for_completion", out var waitValue) || waitValue;
    var includeRawOutput = TryGetBool(args, "include_raw_output", out var includeRaw) && includeRaw;
    var timeoutSeconds = TryGetInt(args, "timeout_seconds", out var timeout)
        ? Math.Clamp(timeout, 5, 3600)
        : defaultTimeoutSeconds;

    var dotnetOptions = DotnetExecutionOptions.Parse(args);
    return new ExecutionRequest(solutionPath, waitForCompletion, includeRawOutput, timeoutSeconds, dotnetOptions);
}

static bool TryGetString(IReadOnlyDictionary<string, JsonElement> args, string key, out string? value)
{
    if (args.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.String)
    {
        value = element.GetString();
        return true;
    }

    value = null;
    return false;
}

static bool TryGetBool(IReadOnlyDictionary<string, JsonElement> args, string key, out bool value)
{
    if (args.TryGetValue(key, out var element) &&
        (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
    {
        value = element.GetBoolean();
        return true;
    }

    value = false;
    return false;
}

static bool TryGetInt(IReadOnlyDictionary<string, JsonElement> args, string key, out int value)
{
    if (args.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
        return true;

    value = 0;
    return false;
}

sealed record ExecutionRequest(
    string SolutionPath,
    bool WaitForCompletion,
    bool IncludeRawOutput,
    int TimeoutSeconds,
    DotnetExecutionOptions DotnetOptions);
sealed record BusyResponse(bool accepted, string status, int retry_after_seconds, string message);

enum JobKind
{
    BuildStructured,
    RunTests,
    PublishStructured
}

enum JobState
{
    Queued,
    Running,
    Done,
    Failed,
    Cancelled,
    TimedOut
}

sealed class JobCoordinator
{
    private const int QueueCapacity = 8;
    private const int DefaultRetryAfterSeconds = 3;
    private const int MaxStoredLogLines = 10000;
    private readonly Channel<JobEnvelope> _queue = Channel.CreateBounded<JobEnvelope>(
        new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        });

    private readonly ConcurrentDictionary<string, JobEnvelope> _jobs = new();
    private int _queuedCount;

    public JobCoordinator()
    {
        _ = Task.Run(ProcessQueueAsync);
    }

    public EnqueueResult TryEnqueue(
        JobKind kind,
        string solutionPath,
        bool includeRawOutput,
        int timeoutSeconds,
        DotnetExecutionOptions dotnetOptions)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var envelope = new JobEnvelope(jobId, kind, solutionPath, includeRawOutput, timeoutSeconds, dotnetOptions);
        _jobs[jobId] = envelope;

        if (!_queue.Writer.TryWrite(envelope))
        {
            _jobs.TryRemove(jobId, out _);
            return new EnqueueResult(false, null, DefaultRetryAfterSeconds);
        }

        Interlocked.Increment(ref _queuedCount);
        return new EnqueueResult(true, jobId, DefaultRetryAfterSeconds);
    }

    public async Task<string?> WaitForCompletionAsync(string jobId, CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(jobId, out var envelope))
            return null;

        try
        {
            return await envelope.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public object? GetJobStatus(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var envelope))
            return null;

        return new
        {
            found = true,
            job_id = envelope.Id,
            tool = envelope.Kind switch
            {
                JobKind.BuildStructured => "build_structured",
                JobKind.RunTests => "run_tests",
                JobKind.PublishStructured => "publish_structured",
                _ => "unknown"
            },
            dotnet_options = envelope.DotnetOptions.ToStatusSnapshot(),
            status = envelope.State.ToString().ToLowerInvariant(),
            created_at_utc = envelope.CreatedAtUtc,
            started_at_utc = envelope.StartedAtUtc,
            completed_at_utc = envelope.CompletedAtUtc,
            timeout_seconds = envelope.TimeoutSeconds,
            cancel_requested = envelope.CancelRequested,
            queue_depth = Math.Max(Interlocked.CompareExchange(ref _queuedCount, 0, 0), 0),
            log_lines = envelope.LogLineCount,
            result = envelope.ResultJson is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(envelope.ResultJson)
        };
    }

    public object? GetJobLogChunk(string jobId, int offset, int limit)
    {
        if (!_jobs.TryGetValue(jobId, out var envelope))
            return null;

        var all = envelope.LogLines.ToArray();
        var start = Math.Clamp(offset, 0, all.Length);
        var count = Math.Clamp(limit, 0, all.Length - start);
        var chunk = all.Skip(start).Take(count).ToArray();
        var nextOffset = start + count;

        return new
        {
            found = true,
            job_id = jobId,
            offset_lines = start,
            returned_lines = chunk.Length,
            next_offset_lines = nextOffset,
            has_more = nextOffset < all.Length,
            lines = chunk
        };
    }

    public object CancelJob(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var envelope))
            return new { found = false, job_id = jobId, cancelled = false, message = "Job not found." };

        envelope.CancelRequested = true;
        envelope.RuntimeCancellation?.Cancel();

        return new
        {
            found = true,
            job_id = envelope.Id,
            cancelled = true,
            status = envelope.State.ToString().ToLowerInvariant()
        };
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (var job in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            Interlocked.Decrement(ref _queuedCount);

            if (job.CancelRequested)
            {
                MarkCancelled(job, "Cancelled before execution.");
                continue;
            }

            job.State = JobState.Running;
            job.StartedAtUtc = DateTimeOffset.UtcNow;
            using var runtimeCts = new CancellationTokenSource();
            job.RuntimeCancellation = runtimeCts;

            try
            {
                var resultJson = job.Kind switch
                {
                    JobKind.BuildStructured => await ExecuteBuildAsync(job, runtimeCts.Token).ConfigureAwait(false),
                    JobKind.RunTests => await ExecuteTestsAsync(job, runtimeCts.Token).ConfigureAwait(false),
                    JobKind.PublishStructured => await ExecutePublishAsync(job, runtimeCts.Token).ConfigureAwait(false),
                    _ => throw new InvalidOperationException("Unknown job kind.")
                };

                job.ResultJson = resultJson;
                var parsed = JsonSerializer.Deserialize<JsonElement>(resultJson);
                var success = parsed.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
                job.State = success ? JobState.Done : JobState.Failed;
                job.CompletedAtUtc = DateTimeOffset.UtcNow;
                job.Completion.TrySetResult(resultJson);
            }
            catch (OperationCanceledException)
            {
                MarkCancelled(job, "Cancelled by request.");
            }
            catch (Exception ex)
            {
                var errorResult = JsonHelper.Serialize(new
                {
                    success = false,
                    error = ex.Message,
                    job_id = job.Id,
                    status = "failed"
                });
                job.ResultJson = errorResult;
                job.State = JobState.Failed;
                job.CompletedAtUtc = DateTimeOffset.UtcNow;
                job.Completion.TrySetResult(errorResult);
            }
            finally
            {
                job.RuntimeCancellation = null;
            }
        }
    }

    private static void MarkCancelled(JobEnvelope job, string reason)
    {
        var state = job.CancelRequested ? JobState.Cancelled : JobState.TimedOut;
        var result = JsonHelper.Serialize(new
        {
            success = false,
            job_id = job.Id,
            status = state.ToString().ToLowerInvariant(),
            reason
        });
        job.ResultJson = result;
        job.State = state;
        job.CompletedAtUtc = DateTimeOffset.UtcNow;
        job.Completion.TrySetResult(result);
    }

    private async Task<string> ExecuteBuildAsync(JobEnvelope job, CancellationToken cancellationToken)
    {
        var workingDir = Path.GetDirectoryName(job.SolutionPath) ?? "";
        var run = await DotnetRunner.RunAsync(
            workingDir,
            DotnetCommandBuilder.BuildBuildArgs(job.SolutionPath, job.DotnetOptions),
            job.TimeoutSeconds,
            cancellationToken,
            line => AddLogLine(job, line)).ConfigureAwait(false);

        var parseInput = run.Output + Environment.NewLine + $"Exit code: {run.ExitCode}";
        var parsed = BuildOutputParser.Parse(parseInput);

        var result = new
        {
            success = parsed.Success && !run.TimedOut && !run.Cancelled,
            exit_code = parsed.ExitCode,
            errors = parsed.Errors.Select(e => new { e.File, e.Line, e.Column, e.Code, e.Message }).ToArray(),
            warnings = parsed.Warnings.Select(w => new { w.File, w.Line, w.Column, w.Code, w.Message }).ToArray(),
            job_id = job.Id,
            status = run.TimedOut ? "timed_out" : run.Cancelled ? "cancelled" : "completed",
            timed_out = run.TimedOut,
            cancelled = run.Cancelled,
            failure_reason = run.FailureReason,
            duration_ms = (int)(DateTimeOffset.UtcNow - (job.StartedAtUtc ?? DateTimeOffset.UtcNow)).TotalMilliseconds,
            raw_output = job.IncludeRawOutput ? run.Output : null
        };

        if (run.TimedOut)
            job.CancelRequested = false;

        return JsonHelper.Serialize(result);
    }

    private async Task<string> ExecuteTestsAsync(JobEnvelope job, CancellationToken cancellationToken)
    {
        var workingDir = Path.GetDirectoryName(job.SolutionPath) ?? "";
        var run = await DotnetRunner.RunAsync(
            workingDir,
            DotnetCommandBuilder.BuildTestArgs(job.SolutionPath, job.DotnetOptions),
            job.TimeoutSeconds,
            cancellationToken,
            line => AddLogLine(job, line)).ConfigureAwait(false);

        var parsed = TestOutputParser.Parse(run.Output);
        var result = new
        {
            success = parsed.Success && !run.TimedOut && !run.Cancelled,
            total = parsed.Total,
            passed = parsed.Passed,
            failed = parsed.Failed,
            skipped = parsed.Skipped,
            failed_tests = parsed.FailedTests.Select(t => new { t.Name, t.Message, duration_ms = t.DurationMs }).ToArray(),
            job_id = job.Id,
            status = run.TimedOut ? "timed_out" : run.Cancelled ? "cancelled" : "completed",
            timed_out = run.TimedOut,
            cancelled = run.Cancelled,
            failure_reason = run.FailureReason,
            duration_ms = (int)(DateTimeOffset.UtcNow - (job.StartedAtUtc ?? DateTimeOffset.UtcNow)).TotalMilliseconds,
            raw_output = job.IncludeRawOutput ? run.Output : null
        };

        if (run.TimedOut)
            job.CancelRequested = false;

        return JsonHelper.Serialize(result);
    }

    private async Task<string> ExecutePublishAsync(JobEnvelope job, CancellationToken cancellationToken)
    {
        var workingDir = Path.GetDirectoryName(job.SolutionPath) ?? "";
        var run = await DotnetRunner.RunAsync(
            workingDir,
            DotnetCommandBuilder.BuildPublishArgs(job.SolutionPath, job.DotnetOptions),
            job.TimeoutSeconds,
            cancellationToken,
            line => AddLogLine(job, line)).ConfigureAwait(false);

        var parseInput = run.Output + Environment.NewLine + $"Exit code: {run.ExitCode}";
        var parsed = BuildOutputParser.Parse(parseInput);

        var result = new
        {
            success = parsed.Success && !run.TimedOut && !run.Cancelled,
            exit_code = parsed.ExitCode,
            errors = parsed.Errors.Select(e => new { e.File, e.Line, e.Column, e.Code, e.Message }).ToArray(),
            warnings = parsed.Warnings.Select(w => new { w.File, w.Line, w.Column, w.Code, w.Message }).ToArray(),
            job_id = job.Id,
            status = run.TimedOut ? "timed_out" : run.Cancelled ? "cancelled" : "completed",
            timed_out = run.TimedOut,
            cancelled = run.Cancelled,
            failure_reason = run.FailureReason,
            duration_ms = (int)(DateTimeOffset.UtcNow - (job.StartedAtUtc ?? DateTimeOffset.UtcNow)).TotalMilliseconds,
            raw_output = job.IncludeRawOutput ? run.Output : null
        };

        if (run.TimedOut)
            job.CancelRequested = false;

        return JsonHelper.Serialize(result);
    }

    private static void AddLogLine(JobEnvelope job, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        job.LogLines.Enqueue(line);
        var current = Interlocked.Increment(ref job.LogLineCount);
        while (current > MaxStoredLogLines && job.LogLines.TryDequeue(out _))
            current = Interlocked.Decrement(ref job.LogLineCount);
    }
}

sealed class JobEnvelope
{
    public JobEnvelope(
        string id,
        JobKind kind,
        string solutionPath,
        bool includeRawOutput,
        int timeoutSeconds,
        DotnetExecutionOptions dotnetOptions)
    {
        Id = id;
        Kind = kind;
        SolutionPath = solutionPath;
        IncludeRawOutput = includeRawOutput;
        TimeoutSeconds = timeoutSeconds;
        DotnetOptions = dotnetOptions;
    }

    public string Id { get; }
    public JobKind Kind { get; }
    public string SolutionPath { get; }
    public bool IncludeRawOutput { get; }
    public int TimeoutSeconds { get; }
    public DotnetExecutionOptions DotnetOptions { get; }
    public DateTimeOffset CreatedAtUtc { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public JobState State { get; set; } = JobState.Queued;
    public bool CancelRequested { get; set; }
    public CancellationTokenSource? RuntimeCancellation { get; set; }
    public ConcurrentQueue<string> LogLines { get; } = new();
    public int LogLineCount;
    public string? ResultJson { get; set; }
    public TaskCompletionSource<string> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

sealed record EnqueueResult(bool Accepted, string? JobId, int RetryAfterSeconds);

static class DotnetRunner
{
    public static async Task<CommandExecutionResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> args,
        int timeoutSeconds,
        CancellationToken cancellationToken,
        Action<string>? onLogLine)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start dotnet.");

        var sb = new StringBuilder(4096);
        var stdOutTask = PumpReaderAsync(process.StandardOutput, sb, onLogLine);
        var stdErrTask = PumpReaderAsync(process.StandardError, sb, onLogLine);

        var timedOut = false;
        var cancelled = false;
        try
        {
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            timedOut = timeoutCts.IsCancellationRequested;
            cancelled = !timedOut;
            TryTerminate(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await Task.WhenAll(stdOutTask, stdErrTask).ConfigureAwait(false);
        var output = sb.ToString();
        var reason = timedOut ? "timeout" : cancelled ? "cancelled" : null;
        return new CommandExecutionResult(process.ExitCode, output, timedOut, cancelled, reason);
    }

    private static async Task PumpReaderAsync(StreamReader reader, StringBuilder sb, Action<string>? onLogLine)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line is null)
                break;

            sb.AppendLine(line);
            onLogLine?.Invoke(line);
        }
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // no-op: best effort
        }
    }
}

sealed record CommandExecutionResult(int ExitCode, string Output, bool TimedOut, bool Cancelled, string? FailureReason);

static class JsonHelper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
