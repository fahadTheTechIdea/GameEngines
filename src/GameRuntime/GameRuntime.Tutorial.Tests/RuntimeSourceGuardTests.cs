using EngineCommon.TestSupport;
using Xunit;

namespace GameRuntime.Tutorial.Tests;

/// <summary>
/// The same Roslyn rules as the engine, on the game layer: no clock, no random, no floating point in the component and the bridges; every catch hands the failure on or says it as a refusal event; no throwing
/// catch-all arm and nothing thrown from the end of a coalesce; and the bridges meet the other engines through events only.
/// </summary>
public sealed class RuntimeSourceGuardTests
{
    private static readonly string[] Projects = ["GameRuntime/GameRuntime.Tutorial", "GameRuntime/GameRuntime.Tutorial.Bridges", "GameRuntime/GameRuntime.Tutorial.Headless"];

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    [Theory]
    [InlineData("GameRuntime/GameRuntime.Tutorial")]
    [InlineData("GameRuntime/GameRuntime.Tutorial.Bridges")]
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
        Assert.True(sources.Sum(s => SyntaxGuards.CatchCount(s.Text)) >= 3, "the scan found too few catches; it may be looking in the wrong place");
        Clean(sources.SelectMany(s => SyntaxGuards.SilentCatches(s.File, s.Text)));
    }

    [Fact]
    public void NoSwitchHasAThrowingCatchAllArm() => Clean(SyntaxGuards.Sources(Projects).SelectMany(s => SyntaxGuards.ThrowingCatchAlls(s.File, s.Text)));

    [Fact]
    public void NoProjectNamesArgumentOutOfRangeException() => Clean(SyntaxGuards.Sources(Projects).SelectMany(s => SyntaxGuards.OutOfRangeExceptions(s.File, s.Text)));

    [Fact]
    public void NothingThrowsFromTheEndOfACoalesce()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Assert.True(sources.Count >= 20, "found only " + sources.Count + " source files");
        Clean(sources.SelectMany(s => SyntaxGuards.ThrowingCoalesces(s.File, s.Text)));
    }

    [Fact]
    public void TheBridgesNeverReachIntoTheEnginesTheyListenTo()
    {
        // The observers and the speaker meet the economy, the relationships and the dialogue through events only: none of them names a runtime or a component of the other side.
        var sources = SyntaxGuards.Sources(["GameRuntime/GameRuntime.Tutorial.Bridges"]).Where(s => s.File.EndsWith("Observer.cs", StringComparison.Ordinal) || s.File.EndsWith("Speaker.cs", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, sources.Count);
        Clean(sources.SelectMany(s => SyntaxGuards.ForbiddenNames(s.File, s.Text, "EconomyRuntime", "RelationshipRuntime", "DialogueRuntime", "EconomyComponent", "RelationshipComponent", "DialogueComponent")));
        var speaker = sources.First(s => s.File.EndsWith("Speaker.cs", StringComparison.Ordinal));
        Assert.NotEmpty(SyntaxGuards.ForbiddenNames(speaker.File, speaker.Text.Replace("private IEventPort? _events;", "private IEventPort? _events; DialogueRuntime _x;", StringComparison.Ordinal), "DialogueRuntime"));
    }
}
