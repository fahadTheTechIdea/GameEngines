using EngineCommon.TestSupport;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>
/// The economy engine's source rules, on the shared Roslyn guards (<see cref="SyntaxGuards"/>): the core has no clock, no random source, no floating point and no file access; no catch fails to report; no switch
/// has a throwing catch-all arm and nothing throws from the end of a coalesce. Each rule is shown to fail on synthetic sources in the shared guard's own tests (EngineCommon.Tests, DialogueEngine.Tests).
/// </summary>
public sealed class SourceGuardTests
{
    private static readonly string[] Projects = ["EconomyEngine/EconomyEngine.Core", "EconomyEngine/EconomyEngine.Cli"];

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    [Fact]
    public void TheCoreHasNoClockNoRandomSourceNoFloatingPointAndNoFileAccess()
    {
        var sources = SyntaxGuards.Sources(["EconomyEngine/EconomyEngine.Core"]);
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
        Assert.True(sources.Count >= 25);
        Clean(sources.SelectMany(s => SyntaxGuards.ThrowingCoalesces(s.File, s.Text)));
    }

    [Fact]
    public void TheCoreAndTheGuardsAgreeAboutWhatTheyAreLookingAt()
    {
        // A guard that cannot fail says nothing: the same rules, run on the core with a forbidden word added, must find it.
        var core = SyntaxGuards.Sources(["EconomyEngine/EconomyEngine.Core"]).First(s => s.File.EndsWith("EconomyMath.cs", StringComparison.Ordinal));
        Assert.Empty(SyntaxGuards.ClockAndFloatViolations(core.File, core.Text));
        Assert.NotEmpty(SyntaxGuards.ClockAndFloatViolations(core.File, core.Text.Replace("internal static class EconomyMath", "internal static class Spare { static double x = 0.5; }\ninternal static class EconomyMath", StringComparison.Ordinal)));
        Assert.NotEmpty(SyntaxGuards.ThrowingCoalesces(core.File, core.Text.Replace("public const long Permille = 1000;", "public static object F(object a) => a ?? throw new System.Exception();", StringComparison.Ordinal)));
    }
}
