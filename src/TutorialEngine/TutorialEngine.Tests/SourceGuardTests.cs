using EngineCommon.TestSupport;
using Xunit;

namespace TutorialEngine.Tests;

/// <summary>
/// The tutorial engine's source rules, on the shared Roslyn guards (<see cref="SyntaxGuards"/>): the core has no clock, no random source, no floating point and no file access; no catch fails to report; no switch
/// has a throwing catch-all arm and nothing throws from the end of a coalesce. Each rule is shown to fail on synthetic sources in the shared guard's own tests (EngineCommon.Tests, DialogueEngine.Tests).
/// </summary>
public sealed class SourceGuardTests
{
    private static readonly string[] Projects = ["TutorialEngine/TutorialEngine.Core", "TutorialEngine/TutorialEngine.Cli"];

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    [Fact]
    public void TheCoreHasNoClockNoRandomSourceNoFloatingPointAndNoFileAccess()
    {
        var sources = SyntaxGuards.Sources(["TutorialEngine/TutorialEngine.Core"]);
        Assert.True(sources.Count >= 20, "found only " + sources.Count + " source files");
        Clean(sources.SelectMany(s => SyntaxGuards.ClockAndFloatViolations(s.File, s.Text)));
    }

    [Fact]
    public void NoCatchSwallowsAFailure()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Assert.True(sources.Sum(s => SyntaxGuards.CatchCount(s.Text)) >= 5, "the scan found too few catches; it may be looking in the wrong place");
        Clean(sources.SelectMany(s => SyntaxGuards.SilentCatches(s.File, s.Text)));
    }

    [Fact]
    public void NoSwitchHasAThrowingCatchAllArmAndNoArgumentOutOfRangeExceptionIsNamed()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Clean(sources.SelectMany(s => SyntaxGuards.ThrowingCatchAlls(s.File, s.Text)));
        Clean(sources.SelectMany(s => SyntaxGuards.OutOfRangeExceptions(s.File, s.Text)));
    }

    [Fact]
    public void NothingThrowsFromTheEndOfACoalesce()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Assert.True(sources.Count >= 20);
        Clean(sources.SelectMany(s => SyntaxGuards.ThrowingCoalesces(s.File, s.Text)));
    }

    [Fact]
    public void TheCoreAndTheGuardsAgreeAboutWhatTheyAreLookingAt()
    {
        // A guard that cannot fail says nothing: the same rules, run on the core with a forbidden word added, must find it.
        var core = SyntaxGuards.Sources(["TutorialEngine/TutorialEngine.Core"]).First(s => s.File.EndsWith("TutorialPresentation.cs", StringComparison.Ordinal));
        Assert.Empty(SyntaxGuards.ClockAndFloatViolations(core.File, core.Text));
        var spare = core.Text.Replace("namespace TutorialEngine;", "namespace TutorialEngine;\nstatic class Spare { static double x = 0.5; static object F(object a) => a ?? throw new System.Exception(); }", StringComparison.Ordinal);
        Assert.NotEqual(core.Text, spare);
        Assert.NotEmpty(SyntaxGuards.ClockAndFloatViolations(core.File, spare));
        Assert.NotEmpty(SyntaxGuards.ThrowingCoalesces(core.File, spare));
    }
}
