namespace EngineCommon.TestSupport;

public static partial class SyntaxGuards
{
    // ---- the repository ------------------------------------------------------------------------------------------------------------

    /// <summary>The root of the repository: the nearest folder above the test output that has <c>src/EngineCommon</c>.</summary>
    public static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "EngineCommon"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("the repository (a folder with src/EngineCommon) is not above " + AppContext.BaseDirectory);
    }

    /// <summary>The C# files of the projects (paths relative to the repository), without build output and without the named files.</summary>
    public static List<(string File, string Text)> Sources(IEnumerable<string> projects, params string[] excludeNames)
    {
        var sources = new List<(string, string)>();
        var root = Root();
        foreach (var project in projects)
            foreach (var file in Directory.GetFiles(Path.Combine(root, "src", project), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal) || file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue;
                if (excludeNames.Contains(Path.GetFileName(file), StringComparer.Ordinal)) continue;
                sources.Add((Path.GetRelativePath(root, file), File.ReadAllText(file)));
            }
        return sources;
    }
}
