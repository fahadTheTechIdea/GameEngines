using EngineCommon.TestSupport;
using Xunit;

namespace EngineCommon.Tests;

/// <summary>
/// The failure rules every shipped engine and game-layer project shares, applied to all of them from one place (the project list is <see cref="GuardedProjects"/>): no catch that can finish without reporting,
/// no throwing catch-all arm, no throw hidden at the end of a coalesce, no ArgumentOutOfRangeException. The per-engine source-guard classes apply the same rules to their own projects; this class exists so a project nobody wrote a
/// guard class for (a new library, a command-line tool, a bridge) is held to them too. Each rule is shown to fail in <c>SyntaxRuleTests</c> and the per-engine guard classes, and here on a real file with a violation added.
/// </summary>
public sealed class RepositoryRuleTests
{
    private static readonly string[] EngineFamilies = ["Calendar", "Relationship", "Economy", "Tutorial", "WorldEvents", "Diplomacy"];

    /// <summary>
    /// The projects built or extended by the engine passes 34 onward: the shared graph, balance and tail libraries, the AI command line, and the six game-layer engines (command lines included) with their components, bridges and demos.
    /// The older projects (the AI core reader, the early demos and the sim and scenario layers) predate the no-throwing-arm rule and are not in this set; the gap audit in docs/RUNTIME_PIPELINE_INTEGRATION_PLAN.md lists them.
    /// </summary>
    internal static List<string> GuardedProjects()
    {
        var shipped = DuplicationGuardTests.ShippedProjects();
        var guarded = new List<string> { "EngineCommon/EngineCommon.Balance", "EngineCommon/EngineCommon.TailTool", "EngineCommon/EngineCommon.Graph", "AIEngine/AIEngine.Cli" };
        foreach (var family in EngineFamilies)
            guarded.AddRange(shipped.Where(p => p.StartsWith(family + "Engine/", StringComparison.Ordinal) || p.StartsWith("GameRuntime/GameRuntime." + family, StringComparison.Ordinal)));
        return guarded;
    }

    private static List<(string File, string Text)> Shipped()
    {
        var sources = SyntaxGuards.Sources(GuardedProjects());
        Assert.True(sources.Count > 200, "the scan found few sources (" + sources.Count + "); it may be looking in the wrong place");
        return sources;
    }

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    [Fact]
    public void TheScanReachesTheProjectsAddedAfterTheFirstEngines()
    {
        var projects = GuardedProjects();
        foreach (var expected in new[]
        {
            "EngineCommon/EngineCommon.Balance", "EngineCommon/EngineCommon.TailTool", "EngineCommon/EngineCommon.Graph", "AIEngine/AIEngine.Cli",
            "RelationshipEngine/RelationshipEngine.Cli", "EconomyEngine/EconomyEngine.Cli", "WorldEventsEngine/WorldEventsEngine.Cli", "DiplomacyEngine/DiplomacyEngine.Cli", "TutorialEngine/TutorialEngine.Cli",
            "GameRuntime/GameRuntime.Relationship.Bridges", "GameRuntime/GameRuntime.Economy.Bridges", "GameRuntime/GameRuntime.WorldEvents.Bridges", "GameRuntime/GameRuntime.Diplomacy.Bridges", "GameRuntime/GameRuntime.Tutorial.Bridges",
        })
            Assert.Contains(expected, projects);
    }

    [Fact]
    public void NoShippedCatchCanFinishWithoutReporting() => Clean(Shipped().SelectMany(s => SyntaxGuards.SilentCatches(s.File, s.Text)));

    /// <summary>
    /// Files the gap audit of docs/RUNTIME_PIPELINE_INTEGRATION_PLAN.md found breaking these two rules and reported to the pass that owns them (the tail tool: an option switch that throws from its default section, an ArgumentOutOfRangeException). The exemption must still be needed: when the file is
    /// fixed, <see cref="TheOpenFindingsAreStillOpen"/> fails and the entry is removed.
    /// </summary>
    private static readonly string[] OpenFindings = ["TailCli.cs", "SilvermanTest.cs"];

    private static bool IsOpenFinding(string file) => OpenFindings.Contains(Path.GetFileName(file), StringComparer.Ordinal);

    [Fact]
    public void NoShippedSwitchHasAThrowingCatchAllArm() => Clean(Shipped().Where(s => !IsOpenFinding(s.File)).SelectMany(s => SyntaxGuards.ThrowingCatchAlls(s.File, s.Text)));

    [Fact]
    public void TheOpenFindingsAreStillOpen()
    {
        foreach (var name in OpenFindings)
        {
            var file = Shipped().First(s => Path.GetFileName(s.File) == name);
            Assert.True(SyntaxGuards.ThrowingCatchAlls(file.File, file.Text).Count + SyntaxGuards.OutOfRangeExceptions(file.File, file.Text).Count > 0, name + " no longer breaks the rules: remove it from OpenFindings");
        }
    }

    [Fact]
    public void NoShippedCodeThrowsFromACoalesce() => Clean(Shipped().SelectMany(s => SyntaxGuards.ThrowingCoalesces(s.File, s.Text)));

    [Fact]
    public void NoShippedProjectNamesArgumentOutOfRangeException() => Clean(Shipped().Where(s => !IsOpenFinding(s.File)).SelectMany(s => SyntaxGuards.OutOfRangeExceptions(s.File, s.Text)));

    [Fact]
    public void TheSharedRulesCanFailOnARealShippedFileWithAViolationAdded()
    {
        var tail = Shipped().First(s => s.File.EndsWith("TailCli.cs", StringComparison.Ordinal));
        var spare = tail.Text + "\ninternal static class Spare { static object F(object a) => a ?? throw new System.Exception(); static void G() { try { F(null!); } catch (System.Exception) { } } static int H(int v) => v switch { 1 => 1, _ => throw new System.ArgumentOutOfRangeException(nameof(v)) }; }\n";
        Assert.Empty(SyntaxGuards.ThrowingCoalesces(tail.File, tail.Text));
        Assert.NotEmpty(SyntaxGuards.ThrowingCoalesces(tail.File, spare));
        Assert.NotEmpty(SyntaxGuards.SilentCatches(tail.File, spare));
        Assert.NotEmpty(SyntaxGuards.ThrowingCatchAlls(tail.File, spare));
        Assert.NotEmpty(SyntaxGuards.OutOfRangeExceptions(tail.File, spare));
    }

    // ---- refusal codes ----------------------------------------------------------------------------------------------------------------

    /// <summary>The code families that have no source-guard class of their own to say it: every constant of these files is a refusal a test must name.</summary>
    private static readonly string[] CodeFiles = ["AiCodes.cs", "TreeCodes.cs", "GraphCodes.cs", "CalendarCodes.cs", "RelationshipCodes.cs", "TutorialCodes.cs"];

    private static readonly System.Text.RegularExpressions.Regex CodeText = new("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static List<string> TestProjects()
    {
        var src = Path.Combine(SyntaxGuards.Root(), "src");
        var projects = new List<string>();
        foreach (var area in Directory.GetDirectories(src))
            foreach (var project in Directory.GetDirectories(area))
                if (Path.GetFileName(project).EndsWith(".Tests", StringComparison.Ordinal) && Directory.GetFiles(project, "*.csproj").Length > 0)
                    projects.Add(Path.GetFileName(area) + "/" + Path.GetFileName(project));
        return projects;
    }

    [Fact]
    public void EveryDeclaredRefusalCodeOfTheseFamiliesIsNamedByATest()
    {
        var owners = SyntaxGuards.Sources(DuplicationGuardTests.ShippedProjects()).Where(s => CodeFiles.Contains(Path.GetFileName(s.File), StringComparer.Ordinal)).ToList();
        Assert.Equal(CodeFiles.Length, owners.Count);
        var declared = owners.SelectMany(s => SyntaxGuards.DeclaredCodes(s.Text, CodeText)).ToList();
        Assert.True(declared.Count >= 140, "found only " + declared.Count + " declared codes");
        var tests = SyntaxGuards.Sources(TestProjects(), "RepositoryRuleTests.cs");
        Assert.True(tests.Count > 200, "the scan found few test sources");
        Clean(SyntaxGuards.UnreferencedCodes(declared, tests.Select(t => t.Text)));
    }

    [Fact]
    public void TheCodeRuleCanFailOnARealCodesFileWithACodeNobodyNamesAdded()
    {
        var owner = SyntaxGuards.Sources(["EngineCommon/EngineCommon.Graph"]).First(s => Path.GetFileName(s.File) == "GraphCodes.cs");
        var withNew = owner.Text.Replace("public const string Cycle = \"GRAPH_CYCLE\";", "public const string Cycle = \"GRAPH_CYCLE\";\n    public const string Unnamed = \"GRAPH_NOBODY_NAMES_THIS\";", StringComparison.Ordinal);
        Assert.NotEqual(owner.Text, withNew);
        var tests = SyntaxGuards.Sources(TestProjects(), "RepositoryRuleTests.cs").Select(t => t.Text).ToList();
        Assert.Empty(SyntaxGuards.UnreferencedCodes(SyntaxGuards.DeclaredCodes(owner.Text, CodeText), tests));
        Assert.Single(SyntaxGuards.UnreferencedCodes(SyntaxGuards.DeclaredCodes(withNew, CodeText), tests));
    }

    // ---- the older runtimes stay clear of the engines built after them ---------------------------------------------------------------

    private static readonly string[] LaterEngineNames = ["EconomyRuntime", "EconomyComponent", "TutorialRuntime", "TutorialComponent", "WorldRuntime", "WorldComponent", "DiplomacyRuntime", "DiplomacyComponent"];

    /// <summary>The calendar and relationship runtimes (components, bridges, demos) were built before the economy, tutorial, world-events and diplomacy engines; a link from them to those is a dependency in the wrong direction (the later engines bridge to the older ones).</summary>
    [Fact]
    public void TheCalendarAndRelationshipRuntimesNameNoLaterEngine()
    {
        var sources = Shipped().Where(s => s.File.Contains("GameRuntime.Calendar", StringComparison.Ordinal) || s.File.Contains("GameRuntime.Relationship", StringComparison.Ordinal)).ToList();
        Assert.True(sources.Count >= 30, "found only " + sources.Count + " calendar and relationship runtime files");
        Clean(sources.SelectMany(s => SyntaxGuards.ForbiddenNames(s.File, s.Text, LaterEngineNames)));
        var bridge = sources.First(s => s.File.EndsWith("RelationshipDialogue.cs", StringComparison.Ordinal));
        Assert.NotEmpty(SyntaxGuards.ForbiddenNames(bridge.File, bridge.Text + " class Spare { EconomyRuntime _x; }", LaterEngineNames));
    }
}
