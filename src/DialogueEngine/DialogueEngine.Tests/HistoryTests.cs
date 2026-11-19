using EngineCommon;
using System.Text;
using System.Text.Json.Nodes;
using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>
/// The history the engine keeps for a history-log screen (speaker, key, the values shown, the host's tick, the answer taken), its cap, its place in a save and a reload, the query for it, and the
/// skip-seen rule that passes lines already read. Comparable: Ren'Py's "skip read text" preference (source read in pass 31) and Yarn Spinner's visited() (read this pass); a history screen is common in visual-novel
/// engines but no source for one was read, so the kept entries and their cap are this engine's own design (docs/DIALOGUE_ENGINE.md section 9).
/// </summary>
public sealed class HistoryTests
{
    private static string Document(int? limit = null, bool secondLap = true) =>
        $$"""
        {
         "format": 3, "id": "demo.history",{{(limit is null ? "" : " \"historyLimit\": " + limit + ",")}}
         "variables": [ { "id": "n", "type": "int", "initial": 7, "min": 0, "max": 99 } ],
         "externalFacts": [ { "id": "need", "type": "bool" } ],
         "speakers": [ { "id": "cmdr", "portraits": ["calm"] } ], "quests": [],
         "conversations": [
          { "id": "talk", "start": "l1", "nodes": [
            { "id": "l1", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.l1", "args": { "n": "var.n" }, "next": "l2" },
            { "id": "l2", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.l2", "next": "l3" },
            { "id": "l3", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.l3", "next": "ask" },
            { "id": "ask", "kind": "choice", "pick": "player", "options": [
               { "id": "again", "line": "k.again", "effects": [ { "add": "n", "by": 1 } ], "next": "l1" },
               { "id": "stop", "line": "k.stop", "next": "check" } ] },
            { "id": "check", "kind": "branch", "cases": [ { "when": { "fact": "ext.need", "op": "eq", "value": true }, "next": "l4" } ], "otherwise": "bye" },
            { "id": "l4", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.l4", "next": "bye" },
            { "id": "bye", "kind": "end", "result": "done" } ] }
         ]
        }
        """;

    private static readonly IReadOnlyDictionary<string, FactValue> Need = new Dictionary<string, FactValue> { ["ext.need"] = FactValue.From(true) };
    private static readonly IReadOnlyDictionary<string, FactValue> NoNeed = new Dictionary<string, FactValue> { ["ext.need"] = FactValue.From(false) };

    /// <summary>Entries as text, because a record holding a list is not equal to another that holds the same values.</summary>
    private static List<string> Flat(IEnumerable<HistoryEntry> entries) => entries.Select(e => string.Join("|", e.Turn, e.Tick, e.Kind, e.Conversation, e.Node, e.Speaker, e.Key, string.Join(",", e.Args.Select(a => a.Name + "=" + a.Value)), e.Chosen, e.Seen, e.Skipped, e.Replaced)).ToList();

    private static DialogueRuntime Runtime(int? limit = null) => new(FeatureFixture.Load(Document(limit)), 1);

    // ---- what is kept --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void EachLineShownAndEachAnswerTakenIsKeptWithWhoTheKeyTheValuesAndTheHostsTick()
    {
        var runtime = Runtime();
        runtime.Tick = 10;
        runtime.Start("talk", NoNeed);
        runtime.Tick = 11;
        runtime.Next(NoNeed);
        runtime.Tick = 12;
        runtime.Next(NoNeed);
        runtime.Tick = 13;
        runtime.Next(NoNeed);   // the choice is on screen
        runtime.Tick = 14;
        runtime.Choose("stop", NoNeed);
        var history = runtime.History();
        Assert.Equal(4, history.Count);
        Assert.Equal((0L, 10L, HistoryKind.Line, "l1", "cmdr", "k.l1"), (history[0].Turn, history[0].Tick, history[0].Kind, history[0].Node, history[0].Speaker, history[0].Key));
        Assert.Equal([new MessageArg("n", FactValue.From(7L))], history[0].Args);
        Assert.Equal(("l2", 11L), (history[1].Node, history[1].Tick));
        Assert.Equal(("l3", 12L), (history[2].Node, history[2].Tick));
        Assert.Equal((HistoryKind.Answer, "ask", "stop", "k.stop", 14L, 3L), (history[3].Kind, history[3].Node, history[3].Chosen, history[3].Key, history[3].Tick, history[3].Turn));
        Assert.All(history, e => Assert.False(e.Seen));
    }

    [Fact]
    public void ALineShownAgainAfterAnInterruptionIsNotKeptTwice()
    {
        var runtime = new DialogueRuntime(FeatureFixture.Load(), 1);
        runtime.Start("main", FeatureFixture.Facts());
        runtime.Interrupt("alarm", FeatureFixture.Facts());
        runtime.Next(FeatureFixture.Facts());   // the alarm returns and the greeting is shown again
        Assert.Equal(["main/hi", "alarm/a1"], runtime.History().Select(e => e.Conversation + "/" + e.Node));
    }

    [Fact]
    public void ASeededChoiceTakenByTheEngineLeavesNoAnswerBecauseThePlayerSawNone()
    {
        var text = Document().Replace("\"conversations\": [", "\"conversations\": [ { \"id\": \"pick\", \"start\": \"s\", \"nodes\": [ { \"id\": \"s\", \"kind\": \"choice\", \"pick\": \"seeded\", \"options\": [ { \"id\": \"a\", \"line\": \"k.a\", \"weight\": 1, \"next\": \"z\" }, { \"id\": \"b\", \"line\": \"k.b\", \"weight\": 1, \"next\": \"z\" } ] }, { \"id\": \"z\", \"kind\": \"line\", \"speaker\": \"cmdr\", \"portrait\": \"calm\", \"line\": \"k.z\", \"next\": \"e\" }, { \"id\": \"e\", \"kind\": \"end\", \"result\": \"done\" } ] },");
        var runtime = new DialogueRuntime(FeatureFixture.Load(text), 3);
        runtime.Start("pick", NoNeed);
        Assert.Equal(["z"], runtime.History().Select(e => e.Node));
        Assert.DoesNotContain(runtime.History(), e => e.Kind == HistoryKind.Answer);
    }

    [Fact]
    public void TheHistoryKeepsOnlyTheNewestEntriesUpToTheDocumentsLimitAndTurnsNeverRepeat()
    {
        var runtime = Runtime(limit: 3);
        runtime.Start("talk", NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Choose("again", NoNeed);   // l1 again: entries so far l1 l2 l3 again l1
        var history = runtime.History();
        Assert.Equal(["l3", "ask", "l1"], history.Select(e => e.Node));
        Assert.Equal([2L, 3L, 4L], history.Select(e => e.Turn));
        Assert.Equal(5, runtime.State.Recorded);
    }

    [Fact]
    public void ALimitOfZeroKeepsNothingAndTheLimitDefaultsToOneHundred()
    {
        var none = Runtime(limit: 0);
        none.Start("talk", NoNeed);
        none.Next(NoNeed);
        none.Next(NoNeed);
        none.Next(NoNeed);
        none.Choose("again", NoNeed);   // a line and an answer, and neither is kept
        Assert.Empty(none.History());
        Assert.Equal(0, none.State.Recorded);
        Assert.Equal(DialogueSet.DefaultHistoryLimit, Runtime().Set.HistoryLimit);
        Assert.Equal(100, DialogueSet.DefaultHistoryLimit);
        Assert.Equal(1000, Runtime(limit: 1000).Set.HistoryLimit);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1001")]
    [InlineData("\"many\"")]
    public void AHistoryLimitOutsideZeroToOneThousandIsRefused(string value)
    {
        var text = Document(5).Replace("\"historyLimit\": 5", "\"historyLimit\": " + value);
        Assert.StartsWith("DLG_", Fixture.Refusal(() => DialogueLoader.Load(text, Fixture.Catalog)));
    }

    [Fact]
    public void ACallThatIsRefusedAfterRecordingLeavesTheHistoryAndTheCounterAsTheyWere()
    {
        // From the choice, "stop" goes to the branch that reads ext.need; the host did not supply it, so the call is refused after the answer was recorded.
        var runtime = Runtime();
        runtime.Start("talk", NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        var before = DialogueSave.StateDigest(runtime.State);
        var count = runtime.History().Count;
        Assert.Equal(DialogueCodes.FactMissing, Fixture.Refusal(() => runtime.Choose("stop", FeatureFixture.NoFacts)));
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
        Assert.Equal(count, runtime.History().Count);
        Assert.Equal(3, runtime.State.Recorded);
    }

    [Fact]
    public void ARefusedCallThatWouldHavePushedTheOldestEntryOutPutsItBack()
    {
        var runtime = Runtime(limit: 3);
        runtime.Start("talk", NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);   // l1 l2 l3 kept: full
        runtime.Next(NoNeed);
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(["l1", "l2", "l3"], runtime.History().Select(e => e.Node));
        Assert.Equal(DialogueCodes.FactMissing, Fixture.Refusal(() => runtime.Choose("stop", FeatureFixture.NoFacts)));
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
        Assert.Equal(["l1", "l2", "l3"], runtime.History().Select(e => e.Node));
    }

    // ---- the query -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheQueryReturnsTheNewestEntriesOldestFirstAndCanLeaveOutWhatWasAlreadyRead()
    {
        var runtime = Runtime();
        runtime.Start("talk", NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Choose("again", NoNeed);   // second lap: l1 is now a repeat
        runtime.Next(NoNeed);
        Assert.Equal(["l3", "ask", "l1", "l2"], runtime.History(4).Select(e => e.Node));
        Assert.Equal(["l1", "l2", "l3", "ask", "l1", "l2"], runtime.History().Select(e => e.Node));
        Assert.Empty(runtime.History(0));
        Assert.Equal(["l1", "l2", "l3", "ask"], runtime.History(newOnly: true).Select(e => e.Node));
        Assert.Equal(["ask"], runtime.History(1, newOnly: true).Select(e => e.Node));
        Assert.Equal("DLG_RANGE", Fixture.Refusal(() => runtime.History(-1)));
    }

    // ---- save, reload --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheHistoryIsInASaveAndASavedSessionKeepsGrowingItLikeOneThatWasNot()
    {
        var set = FeatureFixture.Load(Document(limit: 4));
        DialogueRuntime Build()
        {
            var r = new DialogueRuntime(set, 1) { Tick = 3 };
            r.Start("talk", NoNeed);
            r.Next(NoNeed);
            r.Tick = 4;
            r.Next(NoNeed);
            r.Next(NoNeed);
            r.Choose("again", NoNeed);
            return r;
        }
        var straight = Build();
        var bytes = DialogueSave.Write(Build());
        var resumed = DialogueSave.Open(set, bytes);
        Assert.Equal(Flat(straight.History()), Flat(resumed.History()));
        Assert.Equal(bytes, DialogueSave.Write(resumed));
        resumed.Tick = straight.Tick = 9;
        straight.Next(NoNeed);
        resumed.Next(NoNeed);
        Assert.Equal(Flat(straight.History()), Flat(resumed.History()));
        Assert.Equal(DialogueSave.StateDigest(straight.State), DialogueSave.StateDigest(resumed.State));
    }

    private static JsonObject Edit(byte[] bytes, Action<JsonObject> edit)
    {
        var node = JsonNode.Parse(bytes)!.AsObject();
        edit(node);
        return node;
    }

    private static string Refuse(DialogueSet set, JsonObject save) => Fixture.Refusal(() => DialogueSave.Open(set, Encoding.UTF8.GetBytes(save.ToJsonString())));

    [Fact]
    public void AForgedHistoryInASaveIsRefusedAsAStateBeforeItsDigestIsLookedAt()
    {
        var set = FeatureFixture.Load(Document(limit: 6));
        var runtime = new DialogueRuntime(set, 1);
        runtime.Start("talk", NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Choose("again", NoNeed);
        var bytes = DialogueSave.Write(runtime);
        Assert.NotNull(DialogueSave.Open(set, bytes));

        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![0]!["Node"] = "nowhere")));
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![0]!["Conversation"] = "other")));
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![1]!["Turn"] = 0)));            // out of order
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![0]!["Turn"] = 99)));           // beyond the counter
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["Recorded"] = 2)));                       // counter below the entries held
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["Recorded"] = -1)));
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![0]!["Key"] = "")));
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![0]!["Key"] = new string('k', 129))));
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![0]!["Tick"] = -1)));
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![0]!["Chosen"] = "stop")));    // a line cannot name an answer
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![3]!["Chosen"] = "nothing")));  // an answer the choice does not have
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![3]!["Node"] = "l1")));         // an answer at a line
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![0]!["Node"] = "ask")));        // a line at a choice
        Assert.Equal(DialogueCodes.SaveFormat, Refuse(set, Edit(bytes, n => n["History"]![0]!["Kind"] = "Poem")));
        Assert.Equal(DialogueCodes.SaveFormat, Refuse(set, Edit(bytes, n => n["History"]![0]!["Extra"] = 1)));
        Assert.Equal(DialogueCodes.SaveFormat, Refuse(set, Edit(bytes, n => n["History"]![0]!["Args"]![0]!["Kind"] = "Float")));
        // More entries than the document keeps.
        var small = FeatureFixture.Load(Document(limit: 2));
        Assert.Equal(DialogueCodes.SaveDocument, Refuse(small, Edit(bytes, n => { })));   // a different document altogether
    }

    [Fact]
    public void AReloadKeepsTheHistoryOfNodesThatSurviveFollowsRenamesAndTrimsToTheNewLimit()
    {
        var runtime = Runtime();
        runtime.Start("talk", NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Choose("again", NoNeed);   // l1 l2 l3 ask l1
        var next = FeatureFixture.Load(Document(limit: 3).Replace("\"id\": \"l2\"", "\"id\": \"mid\"").Replace("\"next\": \"l2\"", "\"next\": \"mid\""));
        var report = runtime.Reload(next, ReloadMode.Refuse, ReloadMap.Parse("""{ "format": 1, "nodes": { "talk/l2": "talk/mid" } }"""));
        Assert.Equal(["l3", "ask", "l1"], runtime.History().Select(e => e.Node));
        Assert.Equal(2, report.DroppedHistory);   // l1 and mid were pushed out by the smaller limit
        Assert.Equal(5, runtime.State.Recorded);
    }

    [Fact]
    public void AReloadDropsHistoryOfNodesThatAreGoneOrAreAnotherKindOfNodeNow()
    {
        var runtime = Runtime();
        runtime.Start("talk", NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Choose("again", NoNeed);
        var changed = Document().Replace("""{ "id": "l2", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.l2", "next": "l3" }""", """{ "id": "l2", "kind": "effects", "effects": [ { "signal": "x" } ], "next": "l3" }""");
        var report = runtime.Reload(FeatureFixture.Load(changed));
        Assert.DoesNotContain(runtime.History(), e => e.Node == "l2");
        Assert.Equal(1, report.DroppedHistory);
    }

    // ---- skip what was read --------------------------------------------------------------------------------------------------------

    private static DialogueRuntime AtTheStartOfTheSecondLap()
    {
        var runtime = Runtime();
        runtime.Start("talk", NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Choose("again", NoNeed);   // l1, seen now
        return runtime;
    }

    [Fact]
    public void SkippingPassesTheLinesAlreadyReadAndStopsAtTheChoice()
    {
        var runtime = AtTheStartOfTheSecondLap();
        runtime.Tick = 20;
        var step = runtime.SkipSeen(NoNeed);
        Assert.Equal(StepKind.Choices, step.Kind);
        Assert.Equal("ask", step.Choices!.Node);
        Assert.Equal(2, step.Events.OfType<SkippedEvent>().Single().Count);   // l2 and l3; l1 was the line showing
        var second = runtime.History().Where(e => e.Kind == HistoryKind.Line).Skip(3).ToList();
        Assert.Equal(["l1", "l2", "l3"], second.Select(e => e.Node));
        Assert.Equal([false, true, true], second.Select(e => e.Skipped));   // l1 was shown and left by the player; l2 and l3 were passed
        Assert.All(second, e => Assert.True(e.Seen));
    }

    [Fact]
    public void SkippingStopsAtTheFirstLineThePlayerHasNotSeen()
    {
        // The variant shows x on the first lap and y on the second; y is new, so a skip from the seen line before it stops there at once, and a second skip passes the seen line after it.
        var text = Document().Replace("{ \"id\": \"l2\", \"kind\": \"line\", \"speaker\": \"cmdr\", \"portrait\": \"calm\", \"line\": \"k.l2\", \"next\": \"l3\" }",
            "{ \"id\": \"l2\", \"kind\": \"variant\", \"mode\": \"cycle\", \"options\": [\"x\", \"y\"] }, { \"id\": \"x\", \"kind\": \"line\", \"speaker\": \"cmdr\", \"portrait\": \"calm\", \"line\": \"k.x\", \"next\": \"l3\" }, { \"id\": \"y\", \"kind\": \"line\", \"speaker\": \"cmdr\", \"portrait\": \"calm\", \"line\": \"k.y\", \"next\": \"l3\" }");
        var runtime = new DialogueRuntime(FeatureFixture.Load(text), 1);
        runtime.Start("talk", NoNeed);
        runtime.Next(NoNeed);   // x
        runtime.Next(NoNeed);   // l3
        runtime.Next(NoNeed);
        runtime.Choose("again", NoNeed);   // l1, seen
        var stopped = runtime.SkipSeen(NoNeed);
        Assert.Equal("y", stopped.Line!.Node);
        Assert.False(stopped.Line.Seen);
        Assert.Empty(stopped.Events.OfType<SkippedEvent>());
        var onward = runtime.SkipSeen(NoNeed);
        Assert.Equal(StepKind.Choices, onward.Kind);
        Assert.Equal(1, onward.Events.OfType<SkippedEvent>().Single().Count);
    }

    [Fact]
    public void SkippingReturnsAnUnseenNextLineImmediatelyWithoutSkippingAnything()
    {
        var runtime = Runtime();
        runtime.Start("talk", NoNeed);   // l1 is new, and so is l2 after it
        var step = runtime.SkipSeen(NoNeed);
        Assert.Equal("l2", step.Line!.Node);
        Assert.Empty(step.Events.OfType<SkippedEvent>());
        Assert.DoesNotContain(runtime.History(), e => e.Skipped);
    }

    [Fact]
    public void SkippingNeverPassesTheEndOfTheConversation()
    {
        var runtime = AtTheStartOfTheSecondLap();
        runtime.SkipSeen(NoNeed);
        var step = runtime.Choose("stop", NoNeed);
        Assert.Equal(StepKind.Ended, step.Kind);
    }

    [Fact]
    public void SkippingIsRefusedWhenNoLineIsShowingAndLeavesTheStateAlone()
    {
        var runtime = Runtime();
        Assert.Equal(DialogueCodes.NotWaiting, Fixture.Refusal(() => runtime.SkipSeen(NoNeed)));
        runtime.Start("talk", NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);   // at the choice
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.NotWaiting, Fixture.Refusal(() => runtime.SkipSeen(NoNeed)));
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
    }

    [Fact]
    public void SkippingStopsAfterTheLimitEvenIfEveryLineIsStillSeen()
    {
        // A conversation of nothing but lines that loop: after the first lap every line is seen, and the skip must still come back.
        var loop = new StringBuilder();
        for (var i = 1; i <= 3; i++) loop.Append("{ \"id\": \"w" + i + "\", \"kind\": \"line\", \"speaker\": \"cmdr\", \"portrait\": \"calm\", \"line\": \"k.w" + i + "\", \"next\": \"w" + (i % 3 + 1) + "\" },");
        loop.Length--;
        var text = Document().Replace("\"conversations\": [", "\"conversations\": [ { \"id\": \"spin\", \"start\": \"w1\", \"nodes\": [" + loop + "] },");
        var runtime = new DialogueRuntime(FeatureFixture.Load(text), 1);
        runtime.Start("spin", NoNeed);
        for (var i = 0; i < 3; i++) runtime.Next(NoNeed);   // one lap and a bit: everything seen
        var step = runtime.SkipSeen(NoNeed);
        Assert.Equal(StepKind.Line, step.Kind);
        Assert.Equal(DialogueRuntime.SkipLimit, step.Events.OfType<SkippedEvent>().Single().Count);
    }

    [Fact]
    public void ASkipThatIsRefusedPartWayPutsEverythingBack()
    {
        // l3 shows a host fact; the skip passes l2, then meets l3 without the fact: the whole call is refused and l2 is not left marked as skipped.
        var text = Document().Replace("\"line\": \"k.l3\", \"next\": \"ask\"", "\"line\": \"k.l3\", \"args\": { \"c\": \"ext.count\" }, \"next\": \"ask\"").Replace("\"externalFacts\": [", "\"externalFacts\": [ { \"id\": \"count\", \"type\": \"int\", \"min\": 0, \"max\": 9 },");
        var runtime = new DialogueRuntime(FeatureFixture.Load(text), 1);
        var facts = new Dictionary<string, FactValue> { ["ext.need"] = FactValue.From(false), ["ext.count"] = FactValue.From(3L) };
        runtime.Start("talk", facts);
        runtime.Next(facts);
        runtime.Next(facts);
        runtime.Next(facts);
        runtime.Choose("again", facts);   // l1 again, every line seen
        var before = DialogueSave.StateDigest(runtime.State);
        var kept = runtime.History();
        Assert.Equal(DialogueCodes.FactMissing, Fixture.Refusal(() => runtime.SkipSeen(new Dictionary<string, FactValue> { ["ext.need"] = FactValue.From(false) })));
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
        Assert.Equal(Flat(kept), Flat(runtime.History()));
        Assert.DoesNotContain(runtime.History(), e => e.Skipped);
    }

    // ---- more about what a save and a reload do with the history ----------------------------------------------------------------------

    [Fact]
    public void ASkippedLineStaysSkippedThroughASaveAndALoad()
    {
        var runtime = AtTheStartOfTheSecondLap();
        runtime.SkipSeen(NoNeed);
        var resumed = DialogueSave.Open(runtime.Set, DialogueSave.Write(runtime));
        Assert.Equal(Flat(runtime.History()), Flat(resumed.History()));
        Assert.Equal(2, resumed.History().Count(e => e.Skipped));
    }

    [Fact]
    public void ASaveWithMoreEntriesThanTheDocumentKeepsIsRefused()
    {
        var set = FeatureFixture.Load(Document(limit: 4));
        var runtime = new DialogueRuntime(set, 1);
        runtime.Start("talk", NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Choose("again", NoNeed);   // l1 l2 l3 ask l1: four are kept
        var bytes = DialogueSave.Write(runtime);
        Assert.Equal(4, runtime.History().Count);
        var crowded = Edit(bytes, n =>
        {
            var history = n["History"]!.AsArray();
            history.Add(JsonNode.Parse(history[3]!.ToJsonString())!);
            history[4]!["Turn"] = 5;
            n["Recorded"] = 6;
        });
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, crowded));
    }

    [Fact]
    public void AReloadMovesTheHistoryToTheNewNamesOfRenamedNodes()
    {
        var runtime = Runtime();
        runtime.Start("talk", NoNeed);
        runtime.Next(NoNeed);
        var next = FeatureFixture.Load(Document().Replace("\"id\": \"l2\"", "\"id\": \"mid\"").Replace("\"next\": \"l2\"", "\"next\": \"mid\""));
        var report = runtime.Reload(next, ReloadMode.Refuse, ReloadMap.Parse("""{ "format": 1, "nodes": { "talk/l2": "talk/mid" } }"""));
        Assert.Equal(["l1", "mid"], runtime.History().Select(e => e.Node));
        Assert.Equal(0, report.DroppedHistory);
        Assert.Equal("mid", runtime.State.Node);
    }

    [Fact]
    public void AReloadDropsHistoryWhoseKeySpeakerOrValuesTheNewNodeNoLongerShows()
    {
        var runtime = Runtime();
        runtime.Start("talk", NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Next(NoNeed);
        runtime.Choose("again", NoNeed);   // l1 l2 l3 ask l1
        var rekeyed = Document().Replace("\"line\": \"k.l2\"", "\"line\": \"k.l2b\"").Replace("\"line\": \"k.again\"", "\"line\": \"k.again2\"").Replace("\"args\": { \"n\": \"var.n\" }", "\"args\": { \"count\": \"var.n\" }");
        var report = runtime.Reload(FeatureFixture.Load(rekeyed));
        Assert.Equal(["l3"], runtime.History().Select(e => e.Node));   // l1 (its placeholder was renamed), l2 (new key) and the answer (new key) no longer match
        Assert.Equal(4, report.DroppedHistory);
    }

    [Fact]
    public void ASaveWhoseHistoryDisagreesWithTheDocumentAboutValuesOrSpeakersOrSkipsIsRefused()
    {
        var set = FeatureFixture.Load(Document(limit: 6));
        var runtime = new DialogueRuntime(set, 1);
        runtime.Start("talk", NoNeed);
        runtime.Next(NoNeed);
        var bytes = DialogueSave.Write(runtime);
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![0]!["Args"]![0]!["Id"] = "other")));             // another placeholder
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![0]!["Args"]!.AsArray().Clear())));                 // a placeholder missing
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![0]!["Speaker"] = "scout")));                        // another speaker
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![0]!["Skipped"] = true)));                          // skipped without having been seen
        Assert.Equal(DialogueCodes.SaveState, Refuse(set, Edit(bytes, n => n["History"]![0]!["Replaced"] = true)));                         // a replacement the line cannot make
    }

    [Fact]
    public void ALineTheSkipPassesWhileAPreemptedConversationComesBackIsNotMistakenForTheLineThatWasShownBefore()
    {
        // The shout was heard once before, so it is seen. Preempting the chat with it and skipping passes its end and puts the chat back, whose line was not recorded again: the entry of the shout line
        // that the player was looking at must not be marked skipped because of that.
        var text = PriorityTests.Document();
        var runtime = new DialogueRuntime(FeatureFixture.Load(text), 1);
        runtime.Start("shout", NoNeed);
        runtime.Next(NoNeed);   // the shout returns from its top: nothing is open, so it ends
        runtime.Start("chat", NoNeed);
        runtime.Start("shout", NoNeed);   // s1, seen now
        var step = runtime.SkipSeen(NoNeed);
        Assert.Equal(StepKind.Choices, step.Kind);   // the skip passed the shout's end, passed the chat line shown again, and stopped at the chat's choice
        Assert.Equal("ask", step.Choices!.Node);
        var last = runtime.History().Last(e => e.Node == "s1");
        Assert.False(last.Skipped);
        Assert.DoesNotContain(runtime.History(), e => e.Skipped && e.Node == "s1");
    }
}
