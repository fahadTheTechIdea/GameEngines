using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace TutorialEngine.Tests;

/// <summary>The facts the engine offers (for conditions, dialogue and AI), the localisation keys of a document and their coverage, the presentation settings and the explanations.</summary>
public sealed class FactsNamesTests
{
    [Fact]
    public void TheFactsOfTheSmallDocumentAreTheEnginesOwnPlusTheHosts()
    {
        var facts = Fixture.Mini().Facts;
        foreach (var id in new[] { "event.e1.count", "event.e1.ago", "event.e1.since_step", "step.s1.status", "step.s1.done", "tip.t1.shows", "tip.t1.acknowledged", "tip.t1.visible", "skill.sk.points", "skill.sk.mastered",
                                   "tutorial.phase", "tutorial.progress", "journey.state", "journey.language", "setting.reduced_motion", "setting.text_scale", "setting.tips", "flag", "level" })
            Assert.True(facts.ContainsKey(id), id);
        Assert.Equal(new FactDefinition(FactKind.Integer, 0, 3), facts["skill.sk.points"]);
        Assert.Equal(new FactDefinition(FactKind.Integer, -1, TutorialFacts.Saturate), facts["event.e1.ago"]);
        Assert.Equal(new FactDefinition(FactKind.Integer, 1000, 3000), facts["setting.text_scale"]);
        Assert.Equal(new FactDefinition(FactKind.Integer, 0, 100), facts["level"]);
    }

    [Fact]
    public void TheFactsReadWhatTheRuntimeHas()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(0, Fixture.Host());
        runtime.MoveTo(10);
        runtime.Observe("e1", 2);
        runtime.MoveTo(14);
        var facts = runtime.ReadFacts();
        Assert.Equal(2, facts["event.e1.count"].Integer);
        Assert.Equal(4, facts["event.e1.ago"].Integer);
        Assert.Equal(-1, facts["event.e2.ago"].Integer);
        Assert.Equal("active", facts["step.s1.status"].Text);
        Assert.False(facts["step.s1.done"].Boolean);
        Assert.Equal("running", facts["tutorial.phase"].Text);
        Assert.Equal(0, facts["tutorial.progress"].Integer);
        Assert.Equal("done", facts["journey.state"].Text);
        Assert.Equal("en", facts["journey.language"].Text);
        Assert.False(facts["setting.reduced_motion"].Boolean);
        Assert.Equal(1000, facts["setting.text_scale"].Integer);
        Assert.True(facts["setting.tips"].Boolean);
        Assert.False(facts.ContainsKey("event.e1.since_step"));        // only a step's completion can read that
        Assert.False(facts.ContainsKey("flag"));                        // the host's facts are the host's
        Assert.All(facts, f => Assert.True(runtime.Set.Facts[f.Key].Kind == f.Value.Kind));
    }

    [Fact]
    public void EveryFactTheEngineReadsIsWithinTheRangeItDeclares()
    {
        var runtime = Fixture.Ready(Fixture.Valley);
        for (var i = 0; i < 5; i++) runtime.Observe("moved", 1000);
        runtime.Advance(100, Fixture.ValleyHost());
        foreach (var (id, value) in runtime.ReadFacts())
        {
            var definition = runtime.Set.Facts[id];
            if (definition.Kind == FactKind.Integer) Assert.InRange(value.Integer, definition.Minimum!.Value, definition.Maximum!.Value);
        }
    }

    [Fact]
    public void ACountSaturatesAtTheLimitOfTheSharedFactModel()
    {
        var runtime = Fixture.Ready();
        for (var i = 0; i < 1_000_010; i++) runtime.Observe("e3", 1000);
        Assert.Equal(TutorialFacts.Saturate, runtime.EventCounts[2]);
        runtime.Observe("e3");
        Assert.Equal(TutorialFacts.Saturate, runtime.EventCounts[2]);
        Assert.Equal(TutorialFacts.Saturate, runtime.ReadFacts()["event.e3.count"].Integer);
    }

    [Fact]
    public void ADialogueDocumentCanDeclareTheFactsWithAPrefix()
    {
        var runtime = Fixture.Ready();
        var facts = runtime.ReadFacts("ext.");
        Assert.True(facts.ContainsKey("ext.tutorial.phase"));
        Assert.All(facts.Keys, k => Assert.StartsWith("ext.", k));
    }

    [Fact]
    public void ReservedPrefixesAreTheEnginesAndOthersAreNot()
    {
        foreach (var id in new[] { "event.x", "step.x", "tip.x", "skill.x", "tutorial.x", "journey.x", "setting.x" }) Assert.True(TutorialFacts.IsReserved(id), id);
        foreach (var id in new[] { "events.x", "game.paused", "input.gamepad", "tutorials", "journeys" }) Assert.False(TutorialFacts.IsReserved(id), id);
    }

    // ---- names ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheRequiredKeysAreEveryKeyTheDocumentUsesEachOnceInOrdinalOrder()
    {
        var keys = TutorialNames.RequiredKeys(Fixture.Mini());
        Assert.Equal(keys.OrderBy(k => k, StringComparer.Ordinal), keys);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        foreach (var key in new[] { "k.sk", "k.s1", "k.s1b", "k.h1", "k.h3", "k.h4", "k.t1", "k.p1", "k.p3b", "k.p2a" }) Assert.Contains(key, keys);
        Assert.Equal(21, keys.Count);
        Assert.Equal(63, TutorialNames.RequiredKeys(Fixture.Valley).Count);
    }

    [Fact]
    public void TheSampleTableCoversTheSampleCompletely()
    {
        var table = LocaleTable.Load(Fixture.Text("valley.en.json"));
        var report = TutorialNames.Coverage(Fixture.Valley, table);
        Assert.True(report.IsComplete);
        Assert.Equal(63, report.Total);
        Assert.Equal(63, report.Translated);
        CoverageRules.Check(report);
    }

    [Fact]
    public void ATableThatLacksKeysOrHasStrayOnesIsReported()
    {
        var table = LocaleTable.Load("""{ "format": 1, "locale": "fr", "messages": { "tut.page.welcome": "Bienvenue", "tut.stray": "x" } }""");
        var report = TutorialNames.Coverage(Fixture.Valley, table);
        Assert.False(report.IsComplete);
        Assert.Equal(62, report.Missing.Count);
        Assert.Equal(["tut.stray"], report.Unknown);
        Assert.Equal(1, report.Translated);
        var withPlaceholder = LocaleTable.Load(Fixture.Text("valley.en.json").Replace("\"Walk around\"", "\"Walk {where}\"", StringComparison.Ordinal));
        Assert.Single(TutorialNames.Coverage(Fixture.Valley, withPlaceholder).Problems);
    }

    [Fact]
    public void ANameIsRenderedInTheChainOfThePlayersLanguageWithFallbackToTheDefault()
    {
        var en = LocaleTable.Load(Fixture.Text("valley.en.json"));
        var fr = LocaleTable.Load("""{ "format": 1, "locale": "fr", "messages": { "tut.step.move": "Marchez", "tut.page.welcome": "Bienvenue dans la vallée" } }""");
        var chain = LocaleChain.Create("fr-CA", [en, fr], "en");
        var set = Fixture.Valley;
        Assert.Equal("Marchez", TutorialNames.Step(set, chain, 0));
        Assert.Equal("Bienvenue dans la vallée", TutorialNames.PageTitle(set, chain, 0));
        Assert.Equal("Use the movement keys or the left stick to walk to the river.", TutorialNames.StepBody(set, chain, 0));       // falls back to English
        Assert.Equal("Try the movement keys.", TutorialNames.Hint(set, chain, 0, 1));
        Assert.Equal("Moving around", TutorialNames.Skill(set, chain, 0));
        Assert.Equal("The game is paused. Nothing moves until you press play.", TutorialNames.Tip(set, chain, 0));
        Assert.Equal("A quiet valley is waiting for someone to look after it. That someone is you.", TutorialNames.PageBody(set, chain, 0));
        Assert.Contains(chain.Fallbacks, f => f.Key == "fr:tut.step.move.body");
    }

    [Fact]
    public void TheLanguagesTheJourneyOffersAreCheckedAgainstTheTablesTheGameHas()
    {
        var en = LocaleTable.Load(Fixture.Text("valley.en.json"));
        var fr = LocaleTable.Load("""{ "format": 1, "locale": "fr", "messages": { } }""");
        var (covered, missing) = TutorialNames.LanguageSupport(Fixture.Valley, [en, fr]);
        Assert.Equal(["en", "fr"], covered);
        Assert.Equal(["ar", "es"], missing);
        Assert.True(TextDirection.IsRightToLeft("ar"));
        Assert.False(TextDirection.IsRightToLeft("es"));
    }

    // ---- explain ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ExplainingATaughtStepSaysWhatItWaitsForAndWhereTheHintsAre()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(0, Fixture.Host());
        runtime.Advance(4, Fixture.Host());
        var text = TutorialExplain.Step(runtime, "s1", Fixture.Host());
        Assert.Contains("step s1 is active, began at step 0", text);
        Assert.Contains("teaches sk: 0 of 3", text);
        Assert.Contains("completes when event.e1.since_step gte 1 [0] = false", text);
        Assert.Contains("idle 4 steps, hint level 0 of 3, the next at 10", text);
    }

    [Fact]
    public void ExplainingAWaitingStepNamesItsPrerequisitesAndABranch()
    {
        var runtime = Fixture.Ready(Fixture.Mini(r => Tiny.Step(r, "s2")["when"] = Tiny.Cond("flag", "eq", true)));
        runtime.Advance(0, Fixture.Host());
        var text = TutorialExplain.Step(runtime, "s2", Fixture.Host());
        Assert.Contains("step s2 is pending", text);
        Assert.Contains("requires s1: active", text);
        Assert.Contains("branch flag eq true [false] = false", text);
        Assert.Contains("it is not being taught", text);
    }

    [Fact]
    public void ExplainingATipListsEveryGateAndTheContext()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(5, Fixture.Host(flag: true));
        runtime.Advance(15, Fixture.Host(flag: true));
        var text = TutorialExplain.Tip(runtime, "t1", Fixture.Host(flag: true));
        Assert.Contains("tip t1 does not show, shown 1 of 2 times", text);
        Assert.Contains("ok      tips are on", text);
        Assert.Contains("blocked it ended at step 15, cooldown 20", text);
        Assert.Contains("context flag eq true [true] = true", text);
    }

    [Fact]
    public void ExplainingTheJourneySaysWhereItIsAndWhatItOffers()
    {
        var runtime = TutorialRuntime.Create(Fixture.Mini());
        Assert.Contains("journey is at language, no language chosen", TutorialExplain.Journey(runtime));
        runtime.SelectLanguage("fr");
        runtime.StoryNext();
        runtime.SetIntent("join:1");
        var text = TutorialExplain.Journey(runtime);
        Assert.Contains("journey is at story, language fr, page 2 of 3, intent join:1", text);
        Assert.Contains("languages en, fr, entries sign-in, host, join", text);
        Assert.Contains("settings reduced motion off, text scale 1000, tips on", text);
    }

    [Fact]
    public void ExplainingAnUnknownThingIsRefused()
    {
        var runtime = Fixture.Ready();
        Assert.Equal(TutorialCodes.UnknownStep, Fixture.Refusal(() => TutorialExplain.Step(runtime, "zzz", Fixture.Host())));
        Assert.Equal(TutorialCodes.UnknownTip, Fixture.Refusal(() => TutorialExplain.Tip(runtime, "zzz", Fixture.Host())));
    }
}
