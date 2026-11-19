using System.Text.RegularExpressions;
using EngineCommon.TestSupport;
using Xunit;

namespace GameRuntime.WorldEvents.Tests;

/// <summary>
/// The same Roslyn rules as the engine, on the game layer: no clock, no random, no floating point in the component and the bridges; every catch hands the failure on or says it as a refusal event; no throwing
/// catch-all arm and nothing thrown from the end of a coalesce; the component codes are named by a test; and the bridges meet the other engines through events and facts only.
/// </summary>
public sealed class RuntimeSourceGuardTests
{
    private static readonly string[] Projects = ["GameRuntime/GameRuntime.WorldEvents", "GameRuntime/GameRuntime.WorldEvents.Bridges", "GameRuntime/GameRuntime.WorldEvents.Headless"];

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    [Theory]
    [InlineData("GameRuntime/GameRuntime.WorldEvents")]
    [InlineData("GameRuntime/GameRuntime.WorldEvents.Bridges")]
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
    public void EveryComponentAndBridgeCodeIsNamedByATest()
    {
        var owners = SyntaxGuards.Sources(["GameRuntime/GameRuntime.WorldEvents", "GameRuntime/GameRuntime.WorldEvents.Bridges"]);
        var declared = owners.SelectMany(s => SyntaxGuards.DeclaredCodes(s.Text, new Regex("^WE_[A-Z0-9_]+$", RegexOptions.CultureInvariant))).ToList();
        Assert.True(declared.Count >= 12, "found only " + declared.Count);
        var tests = SyntaxGuards.Sources(["GameRuntime/GameRuntime.WorldEvents.Tests"]);
        Clean(SyntaxGuards.UnreferencedCodes(declared, tests.Select(t => t.Text)));
    }

    [Fact]
    public void TheBridgesNeverReachIntoTheEnginesTheyListenToOrSpeakTo()
    {
        // The story, tutorial and economy bridges meet the other engines through events only: none of them names a runtime or a component of the other side.
        var sources = SyntaxGuards.Sources(["GameRuntime/GameRuntime.WorldEvents.Bridges"]).Where(s => s.File.EndsWith("WorldDialogueStory.cs", StringComparison.Ordinal) || s.File.EndsWith("WorldTutorialObserver.cs", StringComparison.Ordinal) || s.File.EndsWith("WorldEconomyShocks.cs", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, sources.Count);
        Clean(sources.SelectMany(s => SyntaxGuards.ForbiddenNames(s.File, s.Text, "EconomyRuntime", "TutorialRuntime", "DialogueRuntime", "EconomyComponent", "TutorialComponent", "DialogueComponent", "WorldRuntime", "WorldComponent")));
        var story = sources.First(s => s.File.EndsWith("WorldDialogueStory.cs", StringComparison.Ordinal));
        Assert.NotEmpty(SyntaxGuards.ForbiddenNames(story.File, story.Text.Replace("private IEventPort? _events;", "private IEventPort? _events; DialogueRuntime _x;", StringComparison.Ordinal), "DialogueRuntime"));
    }

    [Fact]
    public void OnlyTheBridgesEverReadTheWorldsStateAndTheComponentNeverNamesAnotherEngine()
    {
        var component = SyntaxGuards.Sources(["GameRuntime/GameRuntime.WorldEvents"]);
        Clean(component.SelectMany(s => SyntaxGuards.ForbiddenNames(s.File, s.Text, "EconomyRuntime", "EconomyComponent", "CalendarRuntime", "CalendarComponent", "DialogueRuntime", "TutorialRuntime", "AiRuntime", "CombatResolver")));
    }
}
