using System.Text.RegularExpressions;
using EngineCommon.TestSupport;
using Xunit;

namespace EngineCommon.Balance.Tests;

/// <summary>
/// The balance harness's boundaries as code: the reducer is integers only and reports every failure; the engine command lines add no floating point; the floating-point tail tool is an offline aid that nothing at runtime (and nothing but a test)
/// references; and the reducer is referenced only by command-line tools and tests.
/// </summary>
public sealed partial class SourceGuardTests
{
    private const string Reducer = "EngineCommon/EngineCommon.Balance";

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    private static List<(string File, string Text)> ReducerSources()
    {
        var sources = SyntaxGuards.Sources([Reducer]);
        Assert.True(sources.Count >= 5, "found only " + sources.Count + " reducer files");
        return sources;
    }

    [Fact]
    public void TheReducerHasNoClockNoRandomSourceNoFloatingPointAndNoFileAccess() => Clean(ReducerSources().SelectMany(s => SyntaxGuards.ClockAndFloatViolations(s.File, s.Text)));

    [Fact]
    public void NoReducerCatchSwallowsAndNothingThrowsFromACoalesceOrACatchAll()
    {
        var sources = ReducerSources();
        Clean(sources.SelectMany(s => SyntaxGuards.SilentCatches(s.File, s.Text)));
        Clean(sources.SelectMany(s => SyntaxGuards.ThrowingCatchAlls(s.File, s.Text)));
        Clean(sources.SelectMany(s => SyntaxGuards.ThrowingCoalesces(s.File, s.Text)));
        Clean(sources.SelectMany(s => SyntaxGuards.OutOfRangeExceptions(s.File, s.Text)));
    }

    [Fact]
    public void TheEngineSweepFilesAddNoFloatingPointOnlyTheFileRead()
    {
        var sources = SyntaxGuards.Sources(["WorldEventsEngine/WorldEventsEngine.Cli", "EconomyEngine/EconomyEngine.Cli", "RelationshipEngine/RelationshipEngine.Cli"]).Where(s => s.File.EndsWith("Cli.Balance.cs", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, sources.Count);
        // The sweep files read their scenario with File.ReadAllText, the one host call a command-line tool is allowed; everything else the shared guard bans stays banned.
        Clean(sources.SelectMany(s => SyntaxGuards.ClockAndFloatViolations(s.File, s.Text)).Where(finding => !finding.EndsWith(": File.ReadAllText", StringComparison.Ordinal)));
    }

    [Fact]
    public void TheGuardsCanFailOnTheReducerWithAForbiddenWordAdded()
    {
        var stats = ReducerSources().First(s => s.File.EndsWith("SampleStats.cs", StringComparison.Ordinal));
        Assert.Empty(SyntaxGuards.ClockAndFloatViolations(stats.File, stats.Text));
        Assert.NotEmpty(SyntaxGuards.ClockAndFloatViolations(stats.File, stats.Text.Replace("public static class SampleStats", "public static class SampleStats { static double x = 0.5; }\npublic static class Spare", StringComparison.Ordinal)));
        Assert.NotEmpty(SyntaxGuards.ClockAndFloatViolations(stats.File, stats.Text.Replace("var k = 0;", "var k = 0; var when = DateTime.UtcNow;", StringComparison.Ordinal)));
        Assert.NotEmpty(SyntaxGuards.ThrowingCoalesces(stats.File, stats.Text.Replace("var k = 0;", "var k = 0; object o = null; var p = o ?? throw new System.Exception();", StringComparison.Ordinal)));
    }

    // ---- who may reference what ---------------------------------------------------------------------------------------------------

    [GeneratedRegex("<ProjectReference\\s+Include=\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceRegex();

    private static string NameOf(string path) => Path.GetFileNameWithoutExtension(path.Replace('\\', '/').Split('/').Last());

    /// <summary>The projects named by a project file's references.</summary>
    internal static List<string> ReferencesOf(string projectText) => ReferenceRegex().Matches(projectText).Select(m => NameOf(m.Groups[1].Value)).ToList();

    /// <summary>
    /// The rules over (project name, referenced names): the tail tool is referenced by no project but its own test project and references nothing; the reducer is referenced only by command-line tools (names ending .Cli), its tests and the tail tool's
    /// tests. A runtime project is anything else.
    /// </summary>
    internal static List<string> ReferenceViolations(IReadOnlyDictionary<string, List<string>> projects)
    {
        var found = new List<string>();
        foreach (var (name, references) in projects)
        {
            if (references.Contains("EngineCommon.TailTool") && name != "EngineCommon.Balance.Tests") found.Add(name + " references the offline tail tool");
            if (references.Contains("EngineCommon.Balance") && !name.EndsWith(".Cli", StringComparison.Ordinal) && !name.EndsWith(".Tests", StringComparison.Ordinal)) found.Add(name + " is not a command-line tool or a test but references the balance reducer");
            if (name == "EngineCommon.TailTool" && references.Count > 0) found.Add("the tail tool references " + string.Join(", ", references));
        }
        return found;
    }

    private static Dictionary<string, List<string>> RepositoryProjects()
    {
        var projects = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(SyntaxGuards.Root(), "src"), "*.csproj", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal) || file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue;
            projects[NameOf(file)] = ReferencesOf(File.ReadAllText(file));
        }
        return projects;
    }

    [Fact]
    public void NoRuntimeProjectReferencesTheTailToolOrTheReducer()
    {
        var projects = RepositoryProjects();
        Assert.True(projects.Count > 50, "found only " + projects.Count + " projects");
        Assert.True(projects.ContainsKey("EngineCommon.TailTool") && projects.ContainsKey("EngineCommon.Balance"));
        Clean(ReferenceViolations(projects));
        Assert.Contains("EngineCommon.Balance", projects["WorldEventsEngine.Cli"]);       // the rule is not met by nobody using the reducer
        Assert.Contains("EngineCommon.Balance", projects["EconomyEngine.Cli"]);
        Assert.Contains("EngineCommon.Balance", projects["RelationshipEngine.Cli"]);
    }

    [Fact]
    public void TheReferenceRuleCanFail()
    {
        Assert.Empty(ReferenceViolations(new Dictionary<string, List<string>> { ["X.Cli"] = ["EngineCommon.Balance"], ["EngineCommon.Balance.Tests"] = ["EngineCommon.TailTool", "EngineCommon.Balance"], ["EngineCommon.TailTool"] = [] }));
        Assert.Single(ReferenceViolations(new Dictionary<string, List<string>> { ["WorldEventsEngine.Core"] = ["EngineCommon.TailTool"] }));
        Assert.Single(ReferenceViolations(new Dictionary<string, List<string>> { ["WorldEventsEngine.Core"] = ["EngineCommon.Balance"] }));
        Assert.Single(ReferenceViolations(new Dictionary<string, List<string>> { ["GameRuntime.WorldEvents"] = ["EngineCommon.Balance"] }));
        Assert.Single(ReferenceViolations(new Dictionary<string, List<string>> { ["EngineCommon.TailTool"] = ["EngineCommon.Core"] }));
        Assert.Equal(["EngineCommon.Core", "EngineCommon.Balance"], ReferencesOf("<Project><ItemGroup><ProjectReference Include=\"..\\EngineCommon.Core\\EngineCommon.Core.csproj\" /><ProjectReference Include=\"../EngineCommon.Balance/EngineCommon.Balance.csproj\" /></ItemGroup></Project>"));
    }
}
