namespace DotnetBuildTestMcp;

/// <summary>Сопоставляет параметр <c>solution_path</c> с путём для <c>dotnet build/test</c> (как CLI: .sln или .csproj).</summary>
internal static class SolutionOrProjectPathResolver
{
    public static string Resolve(string path)
    {
        var full = Path.GetFullPath(path.Trim());

        if (File.Exists(full))
        {
            if (full.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                return full;
            if (full.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                return full;

            throw new ArgumentException($"Not a .sln or .csproj file: {path}");
        }

        if (Directory.Exists(full))
        {
            var sln = Directory.GetFiles(full, "*.sln").OrderBy(static p => p, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (sln is not null)
                return sln;

            var csprojs = Directory.GetFiles(full, "*.csproj").OrderBy(static p => p, StringComparer.OrdinalIgnoreCase).ToArray();
            if (csprojs.Length == 1)
                return csprojs[0];
            if (csprojs.Length > 1)
                throw new ArgumentException(
                    $"Multiple .csproj in directory; specify a .sln, a .csproj, or a folder with a single project: {full}");

            throw new ArgumentException($"No .sln or .csproj found in directory: {full}");
        }

        throw new ArgumentException($"Path not found: {path}");
    }
}
