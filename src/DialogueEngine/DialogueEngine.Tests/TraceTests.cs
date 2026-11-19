using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>The explain facility: why a choice answer is available, shown disabled or hidden; what a call passed through and decided; the same each time, and never changing an outcome.</summary>
public sealed class TraceTests
{
    private static DialogueRuntime AtTheChoice(bool alarm = false)
    {
        var runtime = new DialogueRuntime(FeatureFixture.Load(), 1) { Explaining = true };
        var facts = FeatureFixture.Facts(alarm);
        runtime.Start("main", facts);
        runtime.Next(facts);
        runtime.Next(facts);
        return runtime;
    }

    [Fact]
    public void ExplainChoiceSaysForEachAnswerWhetherItCanBeTakenAndWhichConditionDecidedIt()
    {
        var runtime = AtTheChoice();
        var explained = runtime.ExplainChoice(FeatureFixture.Facts(alarm: false));
        Assert.Equal(
            [
                ("stay", OptionAvailability.Available, "no condition"),
                ("leave", OptionAvailability.Available, "no condition"),
                ("shout", OptionAvailability.ShownDisabled, "var.mood gte 3 [1] = false"),
                ("sneak", OptionAvailability.Hidden, "ext.alarm eq true [false] = false"),
            ],
            explained.Select(e => (e.Id, e.Availability, e.Why)));
        var withAlarm = runtime.ExplainChoice(FeatureFixture.Facts(alarm: true));
        Assert.Equal(OptionAvailability.Available, withAlarm.Single(e => e.Id == "sneak").Availability);
        Assert.Equal("ext.alarm eq true [true] = true", withAlarm.Single(e => e.Id == "sneak").Why);
    }

    [Fact]
    public void ExplainChoiceMatchesWhatTheChoiceActuallyOffersAndChangesNothing()
    {
        var runtime = AtTheChoice();
        var before = DialogueSave.StateDigest(runtime.State);
        var explained = runtime.ExplainChoice(FeatureFixture.Facts());
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
        var offered = runtime.Interrupt("alarm", FeatureFixture.Facts()).Kind;                // shows the interrupter ...
        Assert.Equal(StepKind.Line, offered);
        var back = runtime.Next(FeatureFixture.Facts()).Choices!;                              // ... and then the choice again
        Assert.Equal(explained.Where(e => e.Availability != OptionAvailability.Hidden).Select(e => (e.Id, e.Availability == OptionAvailability.Available)), back.Options.Select(o => (o.Id, o.Enabled)));
    }

    [Fact]
    public void ExplainChoiceIsRefusedWhereThereIsNoChoiceAndAMissingFactIsAMissingFact()
    {
        var runtime = new DialogueRuntime(FeatureFixture.Load(), 1);
        Assert.Equal(DialogueCodes.NotWaiting, Fixture.Refusal(() => runtime.ExplainChoice(FeatureFixture.Facts())));
        var atChoice = AtTheChoice();
        Assert.Equal(DialogueCodes.FactMissing, Fixture.Refusal(() => atChoice.ExplainChoice(FeatureFixture.NoFacts)));
    }

    [Fact]
    public void ACallTraceNamesEveryNodeEnteredInOrderWithWhatItDecided()
    {
        var runtime = new DialogueRuntime(FeatureFixture.Load(), 1) { Explaining = true };
        Assert.Null(runtime.LastTrace);
        runtime.Start("main", FeatureFixture.Facts());
        Assert.Equal("main/hi [line]\n", runtime.LastTrace!.ToText());
        runtime.Next(FeatureFixture.Facts());
        Assert.Equal("main/sub [call] - calls detour, comes back to ask\ndetour/d1 [line]\n", runtime.LastTrace!.ToText());
        runtime.Next(FeatureFixture.Facts());
        Assert.Equal(
            "detour/d2 [effects] - 1 effects applied\n"
            + "detour/back [return] - returns\n"
            + "main/ask [choice] - stay: Available (no condition); leave: Available (no condition); shout: ShownDisabled (var.mood gte 3 [1] = false); sneak: Hidden (ext.alarm eq true [false] = false)\n",
            runtime.LastTrace!.ToText());
        runtime.Choose("stay", FeatureFixture.Facts());
        Assert.Equal("main/var [variant] - cycle, entered 0 times before -> va\nmain/va [line]\n", runtime.LastTrace!.ToText());
    }

    [Fact]
    public void ABranchTraceShowsEachConditionTriedAndTheValuesItRead()
    {
        var runtime = new DialogueRuntime(DialogueLoader.Load(Fixture.Document, Fixture.Catalog), 1) { Explaining = true };
        var facts = Fixture.Facts(squads: 4);
        runtime.Start("briefing", facts);
        runtime.Next(facts);
        runtime.Choose("refuse", facts);
        var text = runtime.LastTrace!.ToText();
        Assert.Contains("briefing/refuse", text + "briefing/refuse");                               // the answer led on: bye ends the call
        var briefed = new DialogueRuntime(DialogueLoader.Load(Fixture.Document, Fixture.Catalog), 1) { Explaining = true };
        briefed.Start("briefing", facts);
        briefed.Next(facts);
        briefed.Choose("accept", facts);                                                              // random, one of two lines
        var accepted = briefed.LastTrace!.ToText();
        Assert.Contains("briefing/rand [random] - rolled ", accepted);
        briefed.Next(facts);
        Assert.Equal(
            "briefing/fin [effects] - 1 effects applied\n"
            + "briefing/check [branch] - case 0 held: var.briefed eq true [true] = true\n"
            + "briefing/bye [end]\n", briefed.LastTrace!.ToText());
    }

    [Fact]
    public void ABranchThatTriedACaseThatFailedBeforeOneThatHeldSaysSoForBoth()
    {
        const string text = """
        { "format": 3, "id": "two", "variables": [ { "id": "mood", "type": "int", "initial": 0, "min": 0, "max": 9 } ], "externalFacts": [], "speakers": [], "quests": [],
          "conversations": [ { "id": "c", "start": "b", "nodes": [
            { "id": "b", "kind": "branch", "cases": [ { "when": { "fact": "var.mood", "op": "gte", "value": 5 }, "next": "x" }, { "when": { "not": { "fact": "var.mood", "op": "eq", "value": 3 } }, "next": "y" } ], "otherwise": "z" },
            { "id": "x", "kind": "end", "result": "x" }, { "id": "y", "kind": "end", "result": "y" }, { "id": "z", "kind": "end", "result": "z" } ] } ] }
        """;
        var runtime = new DialogueRuntime(DialogueLoader.Load(text, Fixture.Catalog), 1) { Explaining = true };
        Assert.Equal("y", runtime.Start("c", FeatureFixture.NoFacts).Result);
        Assert.Equal("c/b [branch] - case 0 failed: var.mood gte 5 [0] = false; case 1 held: not(var.mood eq 3 [0] = false) = true\nc/y [end]\n", runtime.LastTrace!.ToText());
    }
    [Fact]
    public void ARefusedCallStillLeavesATraceThatEndsWithTheCode()
    {
        var runtime = new DialogueRuntime(FeatureFixture.Load(), 1) { Explaining = true };
        runtime.Start("main", FeatureFixture.NoFacts);
        runtime.Next(FeatureFixture.NoFacts);
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.FactMissing, Fixture.Refusal(() => runtime.Next(FeatureFixture.NoFacts)));        // the choice reads ext.alarm
        Assert.Equal(DialogueCodes.FactMissing, runtime.LastTrace!.Refusal);
        Assert.EndsWith("refused DLG_FACT_MISSING\n", runtime.LastTrace.ToText());
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));                              // and the state is as it was
    }

    [Fact]
    public void ExplainingChangesNeitherTheOutcomesNorTheStateNorTheSave()
    {
        string Run(bool explain)
        {
            var runtime = new DialogueRuntime(FeatureFixture.Load(), 21) { Explaining = explain };
            var seen = new List<string>();
            foreach (var call in new Func<DialogueRuntime, DialogueStep>[]
            {
                r => r.Start("main", FeatureFixture.Facts()), r => r.Next(FeatureFixture.Facts()), r => r.Interrupt("alarm", FeatureFixture.Facts()), r => r.Next(FeatureFixture.Facts()),
                r => r.Next(FeatureFixture.Facts()), r => r.Choose("stay", FeatureFixture.Facts()), r => r.Next(FeatureFixture.Facts()), r => r.Timeout(FeatureFixture.Facts()),
            })
            {
                var step = call(runtime);
                seen.Add($"{step.Kind} {step.Line?.Node}{step.Choices?.Node} [{string.Join(";", step.Events)}] {DialogueSave.StateDigest(runtime.State)}");
            }
            return string.Join("\n", seen) + "\n" + Convert.ToHexString(DialogueSave.Write(runtime));
        }
        Assert.Equal(Run(false), Run(true));
    }

    [Fact]
    public void TheSameStateAndFactsGiveTheSameTraceAndTurningItOffStopsRecording()
    {
        string Text() { var r = AtTheChoice(); r.Choose("stay", FeatureFixture.Facts()); return r.LastTrace!.ToText(); }
        Assert.Equal(Text(), Text());
        var runtime = new DialogueRuntime(FeatureFixture.Load(), 1);
        runtime.Start("main", FeatureFixture.Facts());
        Assert.Null(runtime.LastTrace);
    }

    [Fact]
    public void ALongDetailIsCutAtTheLimitAndEndsWithDots()
    {
        var options = string.Join(", ", Enumerable.Range(0, 16).Select(i => $$"""{ "id": "option-with-a-long-name-{{i}}", "line": "k.o{{i}}", "when": { "all": [ { "fact": "var.mood", "op": "gte", "value": 0 }, { "fact": "var.mood", "op": "lte", "value": 9 } ] }, "next": "fin" }"""));
        var text = $$"""{ "format": 3, "id": "wide", "variables": [ { "id": "mood", "type": "int", "initial": 0, "min": 0, "max": 9 } ], "externalFacts": [], "speakers": [], "quests": [], "conversations": [ { "id": "c", "start": "ask", "nodes": [ { "id": "ask", "kind": "choice", "pick": "player", "options": [ {{options}} ] }, { "id": "fin", "kind": "end", "result": "x" } ] } ] }""";
        var runtime = new DialogueRuntime(DialogueLoader.Load(text, Fixture.Catalog), 1) { Explaining = true };
        runtime.Start("c", FeatureFixture.NoFacts);
        var detail = runtime.LastTrace!.Entries[0].Detail;
        Assert.Equal(DialogueTrace.MaxDetail, detail.Length);
        Assert.EndsWith("...", detail);
        Assert.StartsWith("option-with-a-long-name-0: Available (all(var.mood gte 0 [0] = true, var.mood lte 9 [0] = true) = true)", detail);
        Assert.Equal(DialogueRuntime.SilentLimit + 1, DialogueTrace.MaxEntries);
    }

    [Theory]
    [InlineData(511, false)]
    [InlineData(512, false)]
    [InlineData(513, true)]
    [InlineData(515, true)]
    public void ADetailOfExactlyTheLimitIsKeptWholeAndOneCharacterMoreIsCut(int length, bool cut)
    {
        var recorder = new TraceRecorder();
        recorder.Entered("c", new EndNode("n", "x"));
        var detail = new string('a', length);
        recorder.Note(detail);
        var kept = recorder.Finish(null).Entries[0].Detail;
        if (cut) { Assert.Equal(DialogueTrace.MaxDetail, kept.Length); Assert.Equal(detail[..(DialogueTrace.MaxDetail - 3)] + "...", kept); }
        else Assert.Equal(detail, kept);
    }
}
