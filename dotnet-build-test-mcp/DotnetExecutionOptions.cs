using System.Text.Json;

namespace DotnetBuildTestMcp;

/// <summary>Опции CLI, общие для build/test/publish (парсинг из аргументов MCP-тула).</summary>
internal sealed record DotnetExecutionOptions(
    string? Configuration,
    string? Framework,
    bool NoRestore,
    bool NoBuild,
    string? Filter,
    string? PublishOutputPath,
    IReadOnlyList<string> AdditionalArguments)
{
    /// <summary>Компактное представление для <c>get_job_status</c>.</summary>
    public object ToStatusSnapshot() => new
    {
        configuration = Configuration,
        framework = Framework,
        no_restore = NoRestore,
        no_build = NoBuild,
        filter = Filter,
        output = PublishOutputPath,
        additional_arguments = AdditionalArguments
    };

    public static DotnetExecutionOptions Empty { get; } = new(
        Configuration: null,
        Framework: null,
        NoRestore: false,
        NoBuild: false,
        Filter: null,
        PublishOutputPath: null,
        AdditionalArguments: Array.Empty<string>());

    public static DotnetExecutionOptions Parse(IReadOnlyDictionary<string, JsonElement> args)
    {
        TryGetStringTrimmed(args, "configuration", out var configuration);
        TryGetStringTrimmed(args, "framework", out var framework);
        var noRestore = TryGetBool(args, "no_restore", out var nr) && nr;
        var noBuild = TryGetBool(args, "no_build", out var nb) && nb;
        TryGetString(args, "filter", out var filter);
        TryGetStringTrimmed(args, "output", out var output);
        var additional = ParseStringArray(args, "additional_arguments");

        return new DotnetExecutionOptions(
            configuration,
            framework,
            noRestore,
            noBuild,
            string.IsNullOrWhiteSpace(filter) ? null : filter,
            string.IsNullOrWhiteSpace(output) ? null : output,
            additional);
    }

    private static IReadOnlyList<string> ParseStringArray(IReadOnlyDictionary<string, JsonElement> args, string key)
    {
        if (!args.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s && !string.IsNullOrWhiteSpace(s))
                list.Add(s);
        }

        return list;
    }

    private static bool TryGetStringTrimmed(IReadOnlyDictionary<string, JsonElement> args, string key, out string? value)
    {
        if (!TryGetString(args, key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            value = null;
            return false;
        }

        value = raw.Trim();
        return true;
    }

    private static bool TryGetString(IReadOnlyDictionary<string, JsonElement> args, string key, out string? value)
    {
        if (args.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetBool(IReadOnlyDictionary<string, JsonElement> args, string key, out bool value)
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
}
