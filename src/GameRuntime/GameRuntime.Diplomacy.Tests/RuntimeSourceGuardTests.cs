using System.Text.RegularExpressions;
using EngineCommon.TestSupport;
using Xunit;

namespace GameRuntime.Diplomacy.Tests;

/// <summary>
/// The same Roslyn rules as the engine, on the game layer: no clock, no random, no floating point in the component and the bridges; every catch hands the failure on or says it as a refusal event; no throwing
/// catch-all arm and nothing thrown from the end of a coalesce; the component codes are named by a test; and the event-only bridges meet the other engines through events and facts only.
/// </summary>
public sealed class RuntimeSourceGuardTests
{
    private static readonly string[] Projects = ["GameRuntime/GameRuntime.Diplomacy", "GameRuntime/GameRuntime.Diplomacy.Bridges", "GameRuntime/GameRuntime.Diplomacy.Headless"];

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    [Theory]
    [InlineData("GameRuntime/GameRuntime.Diplomacy")]
    [InlineData("GameRuntime/GameRuntime.Diplomacy.Bridges")]
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
        Assert.True(sources.Sum(s => SyntaxGuards.CatchCount(s.Text)) >= 1, "the scan found too few catches; it may be looking in the wrong place");
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
        var owners = SyntaxGuards.Sources(["GameRuntime/GameRuntime.Diplomacy", "GameRuntime/GameRuntime.Diplomacy.Bridges"]);
        var declared = owners.SelectMany(s => SyntaxGuards.DeclaredCodes(s.Text, new Regex("^DIP_[A-Z0-9_]+$", RegexOptions.CultureInvariant))).ToList();
        Assert.True(declared.Count >= 12, "found only " + declared.Count);
        var tests = SyntaxGuards.Sources(["GameRuntime/GameRuntime.Diplomacy.Tests"]);
        Clean(SyntaxGuards.UnreferencedCodes(declared, tests.Select(t => t.Text)));
    }

    [Fact]
    public void TheEventOnlyBridgesNeverReachIntoTheEnginesTheyListenToOrSpeakTo()
    {
        var names = new[] { "DiplomacyDialogueOutcomes.cs", "DiplomacyTutorialObserver.cs", "DiplomacyEconomy.cs", "DiplomacyWorldEvents.cs", "DiplomacyLeaderOutcomes.cs" };
        var sources = SyntaxGuards.Sources(["GameRuntime/GameRuntime.Diplomacy.Bridges"]).Where(s => names.Any(n => s.File.EndsWith(n, StringComparison.Ordinal))).ToList();
        Assert.Equal(names.Length, sources.Count);
        Clean(sources.SelectMany(s => SyntaxGuards.ForbiddenNames(s.File, s.Text, "EconomyRuntime", "TutorialRuntime", "DialogueRuntime", "WorldRuntime", "RelationshipRuntime", "AiRuntime", "CalendarRuntime", "EconomyComponent", "TutorialComponent", "DialogueComponent", "WorldComponent", "RelationshipComponent", "AiComponent", "CalendarComponent")));
        var probe = sources[0];
        Assert.NotEmpty(SyntaxGuards.ForbiddenNames(probe.File, probe.Text + " EconomyRuntime _x;", "EconomyRuntime"));
    }

    [Fact]
    public void TheComponentNeverNamesAnotherEngine()
    {
        var component = SyntaxGuards.Sources(["GameRuntime/GameRuntime.Diplomacy"]);
        Clean(component.SelectMany(s => SyntaxGuards.ForbiddenNames(s.File, s.Text, "EconomyRuntime", "EconomyComponent", "CalendarRuntime", "CalendarComponent", "DialogueRuntime", "TutorialRuntime", "AiRuntime", "RelationshipRuntime", "WorldRuntime", "CombatResolver")));
    }
}
