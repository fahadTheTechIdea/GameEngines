using EngineCommon.TestSupport;
using Xunit;

namespace GameRuntime.Calendar.Tests;

/// <summary>The same Roslyn rules as the engine, on the game layer: no clock, no random, no floating point in the component and the bridge; every catch hands the failure on or says it as a refusal event; no throwing catch-all arm.</summary>
public sealed class RuntimeSourceGuardTests
{
    private static readonly string[] Projects = ["GameRuntime/GameRuntime.Calendar", "GameRuntime/GameRuntime.Calendar.Ai", "GameRuntime/GameRuntime.Calendar.Headless"];

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    [Theory]
    [InlineData("GameRuntime/GameRuntime.Calendar")]
    [InlineData("GameRuntime/GameRuntime.Calendar.Ai")]
    public void TheComponentAndTheBridgeHaveNoClockNoRandomAndNoFloatingPoint(string project)
    {
        var sources = SyntaxGuards.Sources([project]);
        Assert.NotEmpty(sources);
        Clean(sources.SelectMany(s => SyntaxGuards.ClockAndFloatViolations(s.File, s.Text)));
    }

    [Fact]
    public void NoCatchSwallowsAFailureItRethrowsRaisesARefusalOrPrintsTheCodeAndFails()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Assert.True(sources.Sum(s => SyntaxGuards.CatchCount(s.Text)) >= 1, "the scan found no catch; it may be looking in the wrong place");
        Clean(sources.SelectMany(s => SyntaxGuards.SilentCatches(s.File, s.Text)));
    }

    [Fact]
    public void NoSwitchHasAThrowingCatchAllArm() => Clean(SyntaxGuards.Sources(Projects).SelectMany(s => SyntaxGuards.ThrowingCatchAlls(s.File, s.Text)));
}
