using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>Subflows with return, interruptions, timed choices, variants (sequence, cycle, once, shuffle), and the tags, voice and emotion ids a line carries.</summary>
public sealed class FeatureTests
{
    private static DialogueRuntime Runtime(ulong seed = 1) => new(FeatureFixture.Load(), seed);

    private static IReadOnlyDictionary<string, FactValue> F => FeatureFixture.NoFacts;

    private static DialogueStep ToTheChoice(DialogueRuntime runtime)
    {
        runtime.Start("main", FeatureFixture.Facts());         // hi
        runtime.Next(FeatureFixture.Facts());                  // detour d1 (inside the subflow)
        return runtime.Next(FeatureFixture.Facts());           // back in main: the choice
    }

    // ---- tags, voice, emotion --------------------------------------------------------------------------------------------------------

    [Fact]
    public void ALineCarriesItsTagsVoiceEmotionAndAutoAdvanceAndAnAnswerItsTags()
    {
        var runtime = Runtime();
        var line = runtime.Start("main", FeatureFixture.Facts()).Line!;
        Assert.Equal(["greeting", "loud"], line.Tags);
        Assert.Equal("vo-hi", line.Voice);
        Assert.Equal("happy", line.Emotion);
        Assert.Equal(1500, line.AutoAdvanceMs);
        var plain = runtime.Next(FeatureFixture.Facts()).Line!;      // the first line of the detour has none of them
        Assert.Empty(plain.Tags);
        Assert.Null(plain.Voice);
        Assert.Null(plain.Emotion);
        Assert.Equal(0, plain.AutoAdvanceMs);
        var choices = ToTheChoice(Runtime()).Choices!;
        Assert.Equal(["quiet"], choices.Options.Single(o => o.Id == "stay").Tags);
        Assert.Empty(choices.Options.Single(o => o.Id == "leave").Tags);
    }

    [Theory]
    [InlineData("\"emotion\": \"happy\"", "\"emotion\": \"sleepy\"", DialogueCodes.Reference)]                    // not an emotion of the speaker
    [InlineData("\"emotions\": [\"angry\", \"happy\"]", "\"emotions\": [\"angry\", \"angry\"]", DialogueCodes.Duplicate)]
    [InlineData("\"tags\": [\"greeting\", \"loud\"]", "\"tags\": [\"greeting\", \"greeting\"]", DialogueCodes.Duplicate)]
    [InlineData("\"tags\": [\"greeting\", \"loud\"]", "\"tags\": []", DialogueCodes.Range)]
    [InlineData("\"tags\": [\"greeting\", \"loud\"]", "\"tags\": [\"a\",\"b\",\"c\",\"d\",\"e\",\"f\",\"g\",\"h\",\"i\"]", DialogueCodes.Range)]
    [InlineData("\"tags\": [\"greeting\", \"loud\"]", "\"tags\": [\"Loud Tag\"]", DialogueCodes.Format)]
    [InlineData("\"autoAdvanceMs\": 1500", "\"autoAdvanceMs\": -1", DialogueCodes.Range)]
    [InlineData("\"voice\": \"vo-hi\"", "\"voice\": \"VO\"", DialogueCodes.Format)]
    public void ABadTagVoiceEmotionOrTimeIsRefusedWithItsCode(string old, string replacement, string code) =>
        Assert.Equal(code, Fixture.Refusal(() => FeatureFixture.Load(FeatureFixture.With(old, replacement))));

    [Fact]
    public void AnEmotionNeedsASpeakerThatDeclaresEmotions()
    {
        var text = FeatureFixture.With("\"emotions\": [\"angry\", \"happy\"]", "\"emotions\": [\"angry\"]");
        Assert.Equal(DialogueCodes.Reference, Fixture.Refusal(() => FeatureFixture.Load(text)));         // the hi line says happy
        var none = FeatureFixture.With(", \"emotions\": [\"angry\", \"happy\"]", "").Replace(", \"emotion\": \"happy\"", "").Replace(", \"emotion\": \"angry\"", "");
        Assert.NotNull(FeatureFixture.Load(none));
    }

    // ---- subflows ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ACallRunsTheOtherConversationAndComesBackToTheNodeAfterTheCall()
    {
        var runtime = Runtime();
        runtime.Start("main", FeatureFixture.Facts());
        var inside = runtime.Next(FeatureFixture.Facts());
        Assert.Equal("detour", inside.Line!.Conversation);
        Assert.Equal("detour", runtime.State.Conversation);
        Assert.Equal([new DialogueFrame("main", "sub", false)], runtime.State.Frames);
        var back = runtime.Next(FeatureFixture.Facts());                       // d2 adds to mood, back returns to the node after the call
        Assert.Equal(StepKind.Choices, back.Kind);
        Assert.Equal("ask", back.Choices!.Node);
        Assert.Equal("main", runtime.State.Conversation);
        Assert.Empty(runtime.State.Frames);
        Assert.Equal(FactValue.From(1L), runtime.State.Variables["mood"]);
        Assert.Contains(back.Events, e => e is VariableEvent { Id: "mood" });
    }

    [Fact]
    public void ReturnWithNothingToReturnToEndsTheConversationAndEndInsideASubflowEndsEverything()
    {
        var runtime = Runtime();
        var step = runtime.Start("detour", F);
        step = runtime.Next(F);
        Assert.Equal((StepKind.Ended, "back"), (step.Kind, step.Result));
        Assert.False(runtime.InConversation);
        var swapped = FeatureFixture.With("{ \"id\": \"back\", \"kind\": \"return\", \"result\": \"back\" }", "{ \"id\": \"back\", \"kind\": \"end\", \"result\": \"stopped\" }");
        var runtime2 = new DialogueRuntime(FeatureFixture.Load(swapped), 1);
        runtime2.Start("main", FeatureFixture.Facts());
        runtime2.Next(FeatureFixture.Facts());                                  // the detour line
        var ended = runtime2.Next(FeatureFixture.Facts());
        Assert.Equal((StepKind.Ended, "stopped"), (ended.Kind, ended.Result));
        Assert.Empty(runtime2.State.Frames);
        Assert.Null(runtime2.State.Conversation);
    }

    [Fact]
    public void ConversationsThatCallEachOtherInACircleOrCallNothingAreRefused()
    {
        var twice = FeatureFixture.With("{ \"id\": \"back\", \"kind\": \"return\", \"result\": \"back\" }", "{ \"id\": \"back\", \"kind\": \"call\", \"conversation\": \"main\", \"next\": \"d1\" }");
        Assert.Equal(DialogueCodes.CallCycle, Fixture.Refusal(() => FeatureFixture.Load(twice)));
        var self = FeatureFixture.With("\"conversation\": \"detour\"", "\"conversation\": \"main\"");
        Assert.Equal(DialogueCodes.CallCycle, Fixture.Refusal(() => FeatureFixture.Load(self)));
        var missing = FeatureFixture.With("\"conversation\": \"detour\"", "\"conversation\": \"nowhere\"");
        Assert.Equal(DialogueCodes.Reference, Fixture.Refusal(() => FeatureFixture.Load(missing)));
    }

    [Fact]
    public void CallsNestToTheLimitAndNoFurtherAndAFailedCallLeavesTheStateAsItWas()
    {
        // c0 calls c1 calls ... c9; the limit is 8 frames.
        var conversations = string.Join(",", Enumerable.Range(0, 10).Select(i => i == 9
            ? """{ "id": "c9", "start": "x", "nodes": [ { "id": "x", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "c.x", "next": "y" }, { "id": "y", "kind": "return", "result": "r" } ] }"""
            : $$"""{ "id": "c{{i}}", "start": "x", "nodes": [ { "id": "x", "kind": "call", "conversation": "c{{i + 1}}", "next": "y" }, { "id": "y", "kind": "return", "result": "r" } ] }"""));
        var document = $$"""{ "format": 3, "id": "deep", "variables": [], "externalFacts": [], "speakers": [ { "id": "cmdr", "portraits": ["calm"] } ], "quests": [], "conversations": [ {{conversations}} ] }""";
        var set = DialogueLoader.Load(document, Fixture.Catalog);
        var runtime = new DialogueRuntime(set, 1);
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.CallDepth, Fixture.Refusal(() => runtime.Start("c0", F)));            // 9 calls deep
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
        var ok = runtime.Start("c1", F);                                                                    // 8 calls deep: allowed
        Assert.Equal("c9", ok.Line!.Conversation);
        Assert.Equal(DialogueRuntime.CallDepthLimit, runtime.State.Frames.Count);
    }

    // ---- interruption --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnInterruptionShowsTheInterrupterAndThenTheSameChoiceAgainWithoutAnotherVisit()
    {
        var runtime = Runtime();
        ToTheChoice(runtime);
        var visits = runtime.State.Visits["main/ask"];
        var shown = runtime.Interrupt("alarm", FeatureFixture.Facts());
        Assert.Equal("alarm", shown.Line!.Conversation);
        Assert.Equal("angry", shown.Line.Emotion);
        Assert.Equal([new DialogueFrame("main", "ask", true)], runtime.State.Frames);
        var back = runtime.Next(FeatureFixture.Facts());
        Assert.Equal(StepKind.Choices, back.Kind);
        Assert.Equal("ask", back.Choices!.Node);
        Assert.Equal(visits, runtime.State.Visits["main/ask"]);
        Assert.Empty(runtime.State.Frames);
        Assert.Equal(Waiting.Choice, runtime.State.WaitingFor);
        Assert.Equal(StepKind.Ended, runtime.Choose("leave", FeatureFixture.Facts()).Kind);
    }

    [Fact]
    public void ALineCanBeInterruptedToo()
    {
        var runtime = Runtime();
        runtime.Start("main", FeatureFixture.Facts());
        runtime.Interrupt("alarm", FeatureFixture.Facts());
        var back = runtime.Next(FeatureFixture.Facts());
        Assert.Equal("hi", back.Line!.Node);                                    // the greeting is said again
        Assert.Equal(1, runtime.State.Visits["main/hi"]);
        Assert.Equal("detour", runtime.Next(FeatureFixture.Facts()).Line!.Conversation);
    }

    [Fact]
    public void AnInterruptionNeedsAnOpenWaitingConversationAndAKnownOneAndNestsToTheLimit()
    {
        var runtime = Runtime();
        Assert.Equal(DialogueCodes.NotWaiting, Fixture.Refusal(() => runtime.Interrupt("alarm", F)));
        runtime.Start("main", FeatureFixture.Facts());
        Assert.Equal(DialogueCodes.NoConversation, Fixture.Refusal(() => runtime.Interrupt("nobody", F)));
        for (var i = 0; i < DialogueRuntime.CallDepthLimit; i++) runtime.Interrupt("alarm", FeatureFixture.Facts());
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.CallDepth, Fixture.Refusal(() => runtime.Interrupt("alarm", FeatureFixture.Facts())));
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
    }

    [Fact]
    public void AnInterrupterThatEndsEndsTheWholeConversation()
    {
        var runtime = Runtime();
        runtime.Start("main", FeatureFixture.Facts());
        var step = runtime.Interrupt("quit", FeatureFixture.Facts());
        Assert.Equal("q1", step.Line!.Node);
        var ended = runtime.Next(FeatureFixture.Facts());
        Assert.Equal((StepKind.Ended, "quit"), (ended.Kind, ended.Result));
        Assert.Empty(runtime.State.Frames);
    }

    // ---- timed choices ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AChoiceNamesItsTimeoutAndTheHostsTimerTakesThatAnswer()
    {
        var runtime = Runtime();
        var choices = ToTheChoice(runtime).Choices!;
        Assert.Equal((5000, "stay"), (choices.TimeoutMs, choices.TimeoutOption));
        var after = runtime.Timeout(FeatureFixture.Facts());
        Assert.Equal("va", after.Line!.Node);                                       // stay leads to the variant, whose first pick is a
    }

    [Fact]
    public void ATimeoutIsRefusedWhereThereIsNoTimedChoice()
    {
        var runtime = Runtime();
        Assert.Equal(DialogueCodes.NotWaiting, Fixture.Refusal(() => runtime.Timeout(F)));
        runtime.Start("main", FeatureFixture.Facts());
        Assert.Equal(DialogueCodes.NotWaiting, Fixture.Refusal(() => runtime.Timeout(F)));            // at a line
        var plain = new DialogueRuntime(FeatureFixture.Load(FeatureFixture.With(", \"timeout\": { \"afterMs\": 5000, \"option\": \"stay\" }", "")), 1);
        plain.Start("main", FeatureFixture.Facts()); plain.Next(FeatureFixture.Facts()); plain.Next(FeatureFixture.Facts());
        Assert.Equal(DialogueCodes.NoTimeout, Fixture.Refusal(() => plain.Timeout(FeatureFixture.Facts())));
    }

    [Fact]
    public void ATimeoutWhoseAnswerIsNotAvailableNowIsRefusedAndChangesNothing()
    {
        var text = FeatureFixture.With("\"option\": \"stay\"", "\"option\": \"shout\"");     // shout needs mood 3 and the player has 1
        var runtime = new DialogueRuntime(FeatureFixture.Load(text), 1);
        ToTheChoice(runtime);
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.OptionUnavailable, Fixture.Refusal(() => runtime.Timeout(FeatureFixture.Facts())));
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
    }

    [Theory]
    [InlineData("\"option\": \"stay\" }", "\"option\": \"ghost\" }", DialogueCodes.Reference)]
    [InlineData("\"afterMs\": 5000", "\"afterMs\": 50", DialogueCodes.Range)]
    [InlineData("\"afterMs\": 5000", "\"afterMs\": 4000000", DialogueCodes.Range)]
    [InlineData("\"afterMs\": 5000, \"option\": \"stay\" }", "\"afterMs\": 5000 }", DialogueCodes.MissingField)]
    [InlineData("\"afterMs\": 5000, \"option\": \"stay\" }", "\"afterMs\": 5000, \"option\": \"stay\", \"more\": 1 }", DialogueCodes.UnknownField)]
    public void ABadTimeoutIsRefusedWithItsCode(string old, string replacement, string code) =>
        Assert.Equal(code, Fixture.Refusal(() => FeatureFixture.Load(FeatureFixture.With(old, replacement))));

    // ---- variants -------------------------------------------------------------------------------------------------------------------

    private static DialogueRuntime Loop(string mode, bool exhausted = false, ulong seed = 1) =>
        new(DialogueLoader.Load(FeatureFixture.VariantDocument(mode, exhausted), Fixture.Catalog), seed);

    [Fact]
    public void ASequenceStaysOnItsLastContentAndACycleGoesRound()
    {
        Assert.Equal(["a", "b", "c", "c", "c", "c"], FeatureFixture.Rounds(Loop("sequence"), 6));
        Assert.Equal(["a", "b", "c", "a", "b", "c", "a"], FeatureFixture.Rounds(Loop("cycle"), 7));
    }

    [Fact]
    public void OnceShowsEachContentOnceAndThenTheExhaustedNode()
    {
        Assert.Equal(["a", "b", "c", "spent", "spent"], FeatureFixture.Rounds(Loop("once", true), 5));
    }

    [Fact]
    public void AShuffleShowsEveryContentOnceInEachRoundInAnOrderTheSeedDecides()
    {
        var orders = new HashSet<string>();
        foreach (var seed in Enumerable.Range(1, 40))
        {
            var shown = FeatureFixture.Rounds(Loop("shuffle", seed: (ulong)seed), 9);
            for (var round = 0; round < 3; round++) Assert.Equal(["a", "b", "c"], shown.Skip(round * 3).Take(3).Order().ToArray());
            Assert.Equal(shown, FeatureFixture.Rounds(Loop("shuffle", seed: (ulong)seed), 9));            // the same seed, the same order
            orders.Add(string.Join("", shown));
        }
        Assert.True(orders.Count > 20, "only " + orders.Count + " different orders over 40 seeds");
        var rounds = FeatureFixture.Rounds(Loop("shuffle", seed: 3), 60).Chunk(3).Select(c => string.Join("", c)).Distinct().Count();
        Assert.True(rounds > 2, "the order did not change from round to round");
    }

    [Fact]
    public void ASavedGameContinuesWithTheVariantItWouldHaveShown()
    {
        foreach (var mode in new[] { "sequence", "cycle", "shuffle" })
        {
            var expected = FeatureFixture.Rounds(Loop(mode, seed: 9), 8);
            var first = Loop(mode, seed: 9);
            var lines = FeatureFixture.Rounds(first, 3);                      // ends showing the fourth variant line
            var resumed = DialogueSave.Open(first.Set, DialogueSave.Write(first));
            Assert.Equal(Waiting.Line, resumed.State.WaitingFor);
            lines.Add(resumed.State.Node!);
            for (var i = 0; i < 4; i++)
            {
                resumed.Next(FeatureFixture.NoFacts);
                lines.Add(resumed.Choose("again", FeatureFixture.NoFacts).Line!.Node);
            }
            Assert.Equal(expected, lines);
        }
    }
}