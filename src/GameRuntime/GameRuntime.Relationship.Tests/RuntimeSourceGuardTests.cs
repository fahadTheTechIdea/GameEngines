using EngineCommon.TestSupport;
using Xunit;

namespace GameRuntime.Relationship.Tests;

/// <summary>The same Roslyn rules as the engine, on the game layer: no clock, no random, no floating point in the component and the bridges; every catch hands the failure on or says it as a refusal event; no throwing catch-all arm.</summary>
public sealed class RuntimeSourceGuardTests
{
    private static readonly string[] Projects = ["GameRuntime/GameRuntime.Relationship", "GameRuntime/GameRuntime.Relationship.Bridges", "GameRuntime/GameRuntime.Relationship.Headless"];

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    [Theory]
    [InlineData("GameRuntime/GameRuntime.Relationship")]
    [InlineData("GameRuntime/GameRuntime.Relationship.Bridges")]
    public void TheComponentAndTheBridgesHaveNoClockNoRandomAndNoFloatingPoint(string project)
    {
        var sources = SyntaxGuards.Sources([project]);
        Assert.NotEmpty(sources);
        Clean(sources.SelectMany(s => SyntaxGuards.ClockAndFloatViolations(s.File, s.Text)));
    }

    [Fact]
    public void NoCatchSwallowsAFailure()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Assert.True(sources.Sum(s => SyntaxGuards.CatchCount(s.Text)) >= 2, "the scan found too few catches; it may be looking in the wrong place");
        Clean(sources.SelectMany(s => SyntaxGuards.SilentCatches(s.File, s.Text)));
    }

    [Fact]
    public void NoSwitchHasAThrowingCatchAllArm() => Clean(SyntaxGuards.Sources(Projects).SelectMany(s => SyntaxGuards.ThrowingCatchAlls(s.File, s.Text)));

    [Fact]
    public void NoProjectNamesArgumentOutOfRangeException() => Clean(SyntaxGuards.Sources(Projects).SelectMany(s => SyntaxGuards.OutOfRangeExceptions(s.File, s.Text)));
}
