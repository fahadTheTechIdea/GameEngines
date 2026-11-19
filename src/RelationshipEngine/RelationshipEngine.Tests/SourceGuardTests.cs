using EngineCommon.TestSupport;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>
/// The relationship engine's source rules, on the shared Roslyn guards (<see cref="SyntaxGuards"/>): the core has no clock, no random source, no floating point and no file access; no catch fails to report;
/// no switch has a throwing catch-all arm. Each rule is shown to fail on synthetic sources in the shared guard's own tests (EngineCommon.Tests, CalendarEngine.Tests).
/// </summary>
public sealed class SourceGuardTests
{
    private static readonly string[] Projects = ["RelationshipEngine/RelationshipEngine.Core", "RelationshipEngine/RelationshipEngine.Cli"];

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    [Fact]
    public void TheCoreHasNoClockNoRandomSourceNoFloatingPointAndNoFileAccess()
    {
        var sources = SyntaxGuards.Sources(["RelationshipEngine/RelationshipEngine.Core"]);
        Assert.True(sources.Count >= 20, "found only " + sources.Count + " source files");
        Clean(sources.SelectMany(s => SyntaxGuards.ClockAndFloatViolations(s.File, s.Text)));
    }

    [Fact]
    public void NoCatchSwallowsAFailure()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Assert.True(sources.Sum(s => SyntaxGuards.CatchCount(s.Text)) >= 1, "the scan found no catch; it may be looking in the wrong place");
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
    public void TheCoreAndTheGuardsAgreeAboutWhatTheyAreLookingAt()
    {
        // A guard that cannot fail says nothing: the same rule, run on the core with a forbidden word added, must find it.
        var core = SyntaxGuards.Sources(["RelationshipEngine/RelationshipEngine.Core"]).First(s => s.File.EndsWith("DecayMath.cs", StringComparison.Ordinal));
        Assert.Empty(SyntaxGuards.ClockAndFloatViolations(core.File, core.Text));
        Assert.NotEmpty(SyntaxGuards.ClockAndFloatViolations(core.File, core.Text.Replace("public static class DecayMath\n{", "public static class DecayMath\n{ static double x = 0.5;", StringComparison.Ordinal)));
    }
}
