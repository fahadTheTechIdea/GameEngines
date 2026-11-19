using System.Text;
using System.Text.Json.Nodes;
using DialogueEngine;
using EngineCommon;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>
/// Conversations that meet while one is open: priority levels, the three policies (refuse, queue, preempt), the stack a preempted conversation returns through, the refusal codes, and the way a
/// save and a hot reload carry the stack and the queue. Comparable systems: Ink's tunnels and Yarn Spinner's detours return through a stack to where they were; no documented
/// system with conversation priorities was reached, so the three policies and the priority rule are this engine's own design (docs/DIALOGUE_ENGINE.md section 9).
/// </summary>
public sealed class PriorityTests
{
    private static string Line(string id, string next, string? key = null) =>
        $$"""{ "id": "{{id}}", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "{{key ?? "k." + id}}", "next": "{{next}}" }""";

    private static string Conversation(string id, string start, string nodes, string extra = "") =>
        $$"""{ "id": "{{id}}", "start": "{{start}}"{{extra}}, "nodes": [ {{nodes}} ] }""";

    private static string End(string id, string result = "done") => $$"""{ "id": "{{id}}", "kind": "end", "result": "{{result}}" }""";

    internal static string Document(string? extraConversations = null, int barks = 3) =>
        $$"""
        {
         "format": 3, "id": "demo.priority", "variables": [], "externalFacts": [],
         "speakers": [ { "id": "cmdr", "portraits": ["calm"] } ], "quests": [],
         "conversations": [
          {{Conversation("chat", "c1", Line("c1", "ask") + "," + """{ "id": "ask", "kind": "choice", "pick": "player", "options": [ { "id": "on", "line": "k.on", "next": "c3" }, { "id": "stop", "line": "k.stop", "next": "bye" } ] }""" + "," + Line("c3", "bye") + "," + End("bye"), ", \"priority\": 1")}},
          {{Conversation("alarm", "a1", Line("a1", "a2") + "," + Line("a2", "aend") + "," + End("aend", "alarm-over"), ", \"priority\": 5, \"onBusy\": \"preempt\"")}},
          {{Conversation("shout", "s1", Line("s1", "send") + "," + """{ "id": "send", "kind": "return", "result": "shout-over" }""", ", \"priority\": 3, \"onBusy\": \"preempt\"")}},
          {{Conversation("vault", "v1", Line("v1", "vend") + "," + End("vend"), ", \"priority\": 4, \"interruptible\": false")}},
          {{Conversation("plain", "p1", Line("p1", "pend") + "," + End("pend"))}},
          {{Conversation("mixed", "m1", """{ "id": "m1", "kind": "call", "conversation": "sub", "next": "m2" }""" + "," + Line("m2", "mend") + "," + End("mend"), ", \"priority\": 6, \"onBusy\": \"preempt\"")}},
          {{Conversation("sub", "u1", Line("u1", "uend") + "," + End("uend", "sub-end"))}},
          {{string.Join(",\n", Enumerable.Range(1, barks).Select(i => Conversation("bark" + i, "b" + i, Line("b" + i, "e" + i) + "," + End("e" + i), ", \"priority\": " + (i == 3 ? 9 : 2) + ", \"onBusy\": \"queue\"")))}}
          {{(extraConversations is null ? "" : "," + extraConversations)}}
         ]
        }
        """;

    private static DialogueRuntime Runtime(string? document = null, int barks = 3) => new(FeatureFixture.Load(document ?? Document(null, barks)), 1);

    private static readonly IReadOnlyDictionary<string, ScenarioEngine.Contracts.FactValue> None = FeatureFixture.NoFacts;

    private static List<(ConversationChange Change, string Conversation, string Other, int Position)> Changes(DialogueStep step) =>
        step.Events.OfType<ConversationEvent>().Select(e => (e.Change, e.Conversation, e.Other, e.Position)).ToList();

    private static string Refusal(Action action) => Fixture.Refusal(action);

    // ---- the policies --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARefusingConversationIsRefusedWhileAnotherIsOpenAndNothingChanges()
    {
        var runtime = Runtime();
        runtime.Start("chat", None);
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.Busy, Refusal(() => runtime.Start("plain", None)));
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
    }

    [Fact]
    public void AnUnknownConversationWhileOneIsOpenIsStillBusyAndWithNoneOpenIsNoConversation()
    {
        var runtime = Runtime();
        Assert.Equal(DialogueCodes.NoConversation, Refusal(() => runtime.Start("nobody", None)));
        runtime.Start("chat", None);
        Assert.Equal(DialogueCodes.Busy, Refusal(() => runtime.Start("nobody", None)));
    }

    [Fact]
    public void AHigherPriorityConversationBreaksInAndTheOpenOneComesBackWhereItWasWhenItEnds()
    {
        var runtime = Runtime();
        var first = runtime.Start("chat", None);
        Assert.Equal("c1", first.Line!.Node);
        var alarm = runtime.Start("alarm", None);
        Assert.Equal("a1", alarm.Line!.Node);
        Assert.Equal([(ConversationChange.Preempted, "chat", "alarm", 0)], Changes(alarm));
        Assert.Equal("alarm", runtime.RootConversation);
        Assert.Equal("a2", runtime.Next(None).Line!.Node);

        // The alarm ends with "end": the chat is put back at its line, shown again and not counted as a new visit.
        var back = runtime.Next(None);
        Assert.Equal(StepKind.Line, back.Kind);
        Assert.Equal("c1", back.Line!.Node);
        Assert.True(back.Line.Seen);
        Assert.Equal([(ConversationChange.Ended, "alarm", "alarm-over", 0), (ConversationChange.Resumed, "chat", "alarm", 0)], Changes(back));
        Assert.Equal(1, runtime.State.Visits["chat/c1"]);
        Assert.Equal("chat", runtime.RootConversation);
        Assert.Empty(runtime.State.Frames);

        var ask = runtime.Next(None);
        Assert.Equal(StepKind.Choices, ask.Kind);
        var last = runtime.Choose("on", None);
        Assert.Equal("c3", last.Line!.Node);
        var ended = runtime.Next(None);
        Assert.Equal(StepKind.Ended, ended.Kind);
        Assert.Equal("chat", ended.EndedConversation);
    }

    [Fact]
    public void ANodeThatWasAChoiceIsOfferedAgainAfterThePreempterReturns()
    {
        var runtime = Runtime();
        runtime.Start("chat", None);
        var choices = runtime.Next(None);
        Assert.Equal("ask", choices.Choices!.Node);
        var shout = runtime.Start("shout", None);
        Assert.Equal("s1", shout.Line!.Node);
        var again = runtime.Next(None);   // the shout returns from its top: the chat is put back
        Assert.Equal([(ConversationChange.Ended, "shout", "shout-over", 0), (ConversationChange.Resumed, "chat", "shout", 0)], Changes(again));
        Assert.Equal(StepKind.Choices, again.Kind);
        Assert.Equal("ask", again.Choices!.Node);
        Assert.Equal(1, runtime.State.Visits["chat/ask"]);
        Assert.Equal("c3", runtime.Choose("on", None).Line!.Node);
    }

    [Fact]
    public void PreemptionsStackAndUnwindOneLevelAtATime()
    {
        var runtime = Runtime();
        runtime.Start("chat", None);
        runtime.Start("shout", None);
        var alarm = runtime.Start("alarm", None);
        Assert.Equal("alarm", runtime.RootConversation);
        Assert.Equal(2, runtime.State.Frames.Count);
        Assert.All(runtime.State.Frames, f => Assert.True(f.Preempted && f.Reenter));
        runtime.Next(None);
        var shoutAgain = runtime.Next(None);   // alarm ends: the shout is back at s1
        Assert.Equal("s1", shoutAgain.Line!.Node);
        Assert.Equal("shout", runtime.RootConversation);
        var chatAgain = runtime.Next(None);    // the shout returns: the chat is back at c1
        Assert.Equal("c1", chatAgain.Line!.Node);
        Assert.Equal("chat", runtime.RootConversation);
        Assert.Equal(StepKind.Choices, runtime.Next(None).Kind);
        _ = alarm;
    }

    [Fact]
    public void AConversationThatDoesNotOutrankTheOpenOneCannotPreemptIt()
    {
        var runtime = Runtime();
        runtime.Start("alarm", None);
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.Priority, Refusal(() => runtime.Start("shout", None)));    // 3 does not outrank 5
        Assert.Equal(DialogueCodes.Priority, Refusal(() => runtime.Start("alarm", None)));    // nor does 5 outrank itself
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
    }

    [Fact]
    public void AConversationThatForbidsInterruptionIsNotBrokenInto()
    {
        var runtime = Runtime();
        runtime.Start("vault", None);
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.NotInterruptible, Refusal(() => runtime.Start("alarm", None)));
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
    }

    [Fact]
    public void PreemptingAlsoTakesThePriorityOfTheLevelNotOfACalleeInsideIt()
    {
        // vault (priority 4, not interruptible) is the level root; the callee "sub" has priority 0 and no rule of its own.
        var runtime = Runtime(Document("""{ "id": "wrap", "start": "w1", "priority": 4, "interruptible": false, "nodes": [ { "id": "w1", "kind": "call", "conversation": "sub", "next": "wend" }, { "id": "wend", "kind": "end", "result": "done" } ] }"""));
        runtime.Start("wrap", None);
        Assert.Equal("sub", runtime.State.Conversation);
        Assert.Equal("wrap", runtime.RootConversation);
        Assert.Equal(DialogueCodes.NotInterruptible, Refusal(() => runtime.Start("alarm", None)));
    }

    [Fact]
    public void PreemptingFromInsideACallPutsTheCalleeBackAndTheCallerStillReturnsToIt()
    {
        var runtime = Runtime(Document("""{ "id": "wrap", "start": "w1", "nodes": [ { "id": "w1", "kind": "call", "conversation": "sub", "next": "w2" }, { "id": "w2", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.w2", "next": "wend" }, { "id": "wend", "kind": "end", "result": "done" } ] }"""));
        runtime.Start("wrap", None);   // wrap calls sub, which waits at u1
        var alarm = runtime.Start("alarm", None);
        Assert.Equal("a1", alarm.Line!.Node);
        Assert.Equal(2, runtime.State.Frames.Count);
        runtime.Next(None);
        var back = runtime.Next(None);
        Assert.Equal(("sub", "u1"), (back.Line!.Conversation, back.Line.Node));
        Assert.Equal("wrap", runtime.RootConversation);
        // sub ends with "end", which ends the whole wrap conversation.
        Assert.Equal(StepKind.Ended, runtime.Next(None).Kind);
    }

    [Fact]
    public void ThePreempterCanCallASubflowThatEndsAndOnlyThePreempterLevelIsDropped()
    {
        var runtime = Runtime();
        runtime.Start("chat", None);
        var mixed = runtime.Start("mixed", None);   // calls "sub", which waits at u1
        Assert.Equal(("sub", "u1"), (mixed.Line!.Conversation, mixed.Line.Node));
        Assert.Equal(2, runtime.State.Frames.Count);
        var back = runtime.Next(None);   // sub ends with "end": the mixed level is over, the chat is back
        Assert.Equal(("chat", "c1"), (back.Line!.Conversation, back.Line.Node));
        Assert.Equal([(ConversationChange.Ended, "mixed", "sub-end", 0), (ConversationChange.Resumed, "chat", "mixed", 0)], Changes(back));
        Assert.Empty(runtime.State.Frames);
    }

    [Fact]
    public void ThePreemptionStackIsBoundedByTheCallDepthLimitAndTheRefusalPutsEverythingBack()
    {
        // Eight levels of strictly rising priority: the ninth start would need a ninth frame.
        var levels = string.Join(",", Enumerable.Range(1, 10).Select(i => Conversation("lv" + i, "l" + i, Line("l" + i, "z" + i) + "," + End("z" + i), ", \"priority\": " + i + ", \"onBusy\": \"preempt\"")));
        var runtime = Runtime(Document(levels.Replace("\"priority\": 1,", "\"priority\": 7,")));
        runtime.Start("plain", None);
        var names = new[] { "lv2", "lv3", "lv4", "lv5", "lv6", "lv7", "lv8", "lv9" };
        foreach (var name in names.Take(DialogueRuntime.CallDepthLimit)) runtime.Start(name, None);
        Assert.Equal(DialogueRuntime.CallDepthLimit, runtime.State.Frames.Count);
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.CallDepth, Refusal(() => runtime.Start("lv10", None)));
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
    }

    // ---- the queue -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AQueueingConversationWaitsAndStartsWhenTheOpenOneEnds()
    {
        var runtime = Runtime();
        runtime.Start("chat", None);
        var queued = runtime.Start("bark1", None);
        Assert.Equal(StepKind.Queued, queued.Kind);
        Assert.Equal("bark1", queued.Result);
        Assert.Equal([(ConversationChange.Queued, "bark1", "", 1)], Changes(queued));
        Assert.Equal(["bark1"], runtime.State.Queue);
        Assert.Equal("c1", runtime.State.Node);   // the chat has not moved

        runtime.Next(None);
        runtime.Choose("stop", None).Kind.ToString();   // the chat ends here and the queue starts
        Assert.Equal("b1", runtime.State.Node);
        Assert.Equal("bark1", runtime.State.Conversation);
        Assert.Empty(runtime.State.Queue);
    }

    [Fact]
    public void TheNextQueuedConversationStartsInTheSameCallAsTheEndOfTheOneBeforeIt()
    {
        var runtime = Runtime();
        runtime.Start("chat", None);
        runtime.Start("bark1", None);
        runtime.Next(None);
        var step = runtime.Choose("stop", None);
        Assert.Equal(StepKind.Line, step.Kind);
        Assert.Equal(("bark1", "b1"), (step.Line!.Conversation, step.Line.Node));
        Assert.Equal([(ConversationChange.Ended, "chat", "done", 0), (ConversationChange.Started, "bark1", "chat", 0)], Changes(step));
        var end = runtime.Next(None);
        Assert.Equal(StepKind.Ended, end.Kind);
        Assert.Equal("bark1", end.EndedConversation);
    }

    [Fact]
    public void QueuedConversationsStartByPriorityThenByArrivalAndTheirPlacesSaySo()
    {
        var runtime = Runtime();
        runtime.Start("chat", None);
        Assert.Equal(1, Changes(runtime.Start("bark1", None)).Single().Position);
        Assert.Equal(2, Changes(runtime.Start("bark2", None)).Single().Position);
        Assert.Equal(1, Changes(runtime.Start("bark3", None)).Single().Position);   // priority 9 goes ahead of the two waiting at 2
        Assert.Equal(["bark1", "bark2", "bark3"], runtime.State.Queue);
        runtime.Next(None);
        var seen = new List<string>();
        var step = runtime.Choose("stop", None);
        for (var guard = 0; guard < 10 && step.Kind == StepKind.Line; guard++)
        {
            seen.Add(step.Line!.Conversation);
            step = runtime.Next(None);
        }
        Assert.Equal(["bark3", "bark1", "bark2"], seen);
        Assert.Equal(StepKind.Ended, step.Kind);
    }

    [Fact]
    public void AConversationAlreadyInTheQueueIsRefusedAndSoIsAnythingBeyondTheLimit()
    {
        var runtime = Runtime(barks: 10);
        runtime.Start("chat", None);
        runtime.Start("bark1", None);
        Assert.Equal(DialogueCodes.AlreadyQueued, Refusal(() => runtime.Start("bark1", None)));
        for (var i = 2; i <= DialogueRuntime.QueueLimit; i++) runtime.Start("bark" + i, None);
        Assert.Equal(DialogueRuntime.QueueLimit, runtime.State.Queue.Count);
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.QueueFull, Refusal(() => runtime.Start("bark9", None)));
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
    }

    [Fact]
    public void ARefusedQueueStartLeavesTheQueueAsItWas()
    {
        var runtime = Runtime();
        runtime.Start("chat", None);
        runtime.Start("bark1", None);
        Assert.Equal(DialogueCodes.AlreadyQueued, Refusal(() => runtime.Start("bark1", None)));
        Assert.Equal(["bark1"], runtime.State.Queue);
    }

    [Fact]
    public void AQueuedStartThatCannotRunRefusesTheCallThatEndedTheOpenOneAndKeepsBoth()
    {
        // The queued conversation reads a host fact the caller did not supply: the call that would have ended the chat is refused whole.
        var gated = Conversation("gated", "g0", """{ "id": "g0", "kind": "branch", "cases": [ { "when": { "fact": "ext.flag", "op": "eq", "value": true }, "next": "g1" } ], "otherwise": "g1" }""" + "," + Line("g1", "gend") + "," + End("gend"), ", \"priority\": 7, \"onBusy\": \"queue\"");
        var runtime = new DialogueRuntime(FeatureFixture.Load(Document(gated).Replace("\"externalFacts\": []", "\"externalFacts\": [ { \"id\": \"flag\", \"type\": \"bool\" } ]")), 1);
        runtime.Start("chat", None);
        runtime.Start("gated", None);
        runtime.Start("bark1", None);
        runtime.Next(None);
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.FactMissing, Refusal(() => runtime.Choose("stop", None)));   // gated has the higher priority, so it is the one that starts
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
        Assert.Equal(["gated", "bark1"], runtime.State.Queue);   // the one that was taken out goes back where it was, not to the end
        var facts = new Dictionary<string, ScenarioEngine.Contracts.FactValue> { ["ext.flag"] = ScenarioEngine.Contracts.FactValue.From(true) };
        Assert.Equal("g1", runtime.Choose("stop", facts).Line!.Node);
    }

    [Fact]
    public void AnEndInsideACallDropsTheCallsAndTheQueuedConversationStartsWithNothingBehindIt()
    {
        var runtime = Runtime(Document("""{ "id": "wrap", "start": "w1", "nodes": [ { "id": "w1", "kind": "call", "conversation": "sub", "next": "wend" }, { "id": "wend", "kind": "end", "result": "done" } ] }"""));
        runtime.Start("wrap", None);   // wrap calls sub, which waits at u1
        runtime.Start("bark1", None);
        Assert.Single(runtime.State.Frames);
        var next = runtime.Next(None);   // sub ends with "end": the whole wrap conversation is over
        Assert.Equal(("bark1", "b1"), (next.Line!.Conversation, next.Line.Node));
        Assert.Empty(runtime.State.Frames);
        Assert.Equal("bark1", runtime.RootConversation);
        Assert.Equal([(ConversationChange.Ended, "wrap", "sub-end", 0), (ConversationChange.Started, "bark1", "wrap", 0)], Changes(next));
    }

    [Fact]
    public void AQueuedConversationWaitsForAPreemptedOneToo()
    {
        var runtime = Runtime();
        runtime.Start("chat", None);
        runtime.Start("alarm", None);
        runtime.Start("bark1", None);   // queued behind the whole stack
        runtime.Next(None);
        var back = runtime.Next(None);   // alarm ends, chat resumes; the queue is still waiting
        Assert.Equal("c1", back.Line!.Node);
        Assert.Equal(["bark1"], runtime.State.Queue);
        runtime.Next(None);
        var next = runtime.Choose("stop", None);
        Assert.Equal("bark1", next.Line!.Conversation);
    }

    // ---- the stack in a save -------------------------------------------------------------------------------------------------------

    [Fact]
    public void ASaveTakenWhilePreemptedAndQueuedContinuesExactlyLikeTheSessionThatWasNotSaved()
    {
        var set = FeatureFixture.Load(Document());
        DialogueRuntime Build()
        {
            var r = new DialogueRuntime(set, 5);
            r.Start("chat", None);
            r.Start("shout", None);
            r.Start("bark1", None);
            return r;
        }
        var straight = Build();
        var saved = Build();
        var bytes = DialogueSave.Write(saved);
        var resumed = DialogueSave.Open(set, bytes);
        Assert.Equal(DialogueSave.StateDigest(straight.State), DialogueSave.StateDigest(resumed.State));
        Assert.Equal(bytes, DialogueSave.Write(resumed));
        List<string> Run(DialogueRuntime r)
        {
            var log = new List<string>();
            var step = r.Next(None);
            for (var guard = 0; guard < 20 && step.Kind != StepKind.Ended; guard++)
            {
                log.Add(step.Kind + ":" + (step.Line?.Node ?? step.Choices?.Node) + ":" + string.Join(",", Changes(step).Select(c => c.Change + c.Conversation)));
                step = step.Kind == StepKind.Choices ? r.Choose("stop", None) : r.Next(None);
            }
            log.Add("ended " + step.EndedConversation);
            return log;
        }
        Assert.Equal(Run(straight), Run(resumed));
    }

    private static JsonObject Edit(byte[] bytes, Action<JsonObject> edit)
    {
        var node = JsonNode.Parse(bytes)!.AsObject();
        edit(node);
        return node;
    }

    private static string Open(DialogueSet set, JsonObject save)
    {
        var bytes = Encoding.UTF8.GetBytes(save.ToJsonString());
        return Fixture.Refusal(() => DialogueSave.Open(set, bytes));
    }

    [Fact]
    public void AForgedStackOrQueueInASaveIsRefusedAsAStateBeforeItsDigestIsLookedAt()
    {
        var set = FeatureFixture.Load(Document());
        var runtime = new DialogueRuntime(set, 1);
        runtime.Start("chat", None);
        runtime.Start("shout", None);
        runtime.Start("bark1", None);
        var bytes = DialogueSave.Write(runtime);
        // Genuine first.
        Assert.NotNull(DialogueSave.Open(set, bytes));

        // A preempter that does not outrank what it broke into.
        Assert.Equal(DialogueCodes.SaveState, Open(set, Edit(bytes, n => n["Conversation"] = "plain")));
        // A preemption that would not show its node again.
        Assert.Equal(DialogueCodes.SaveState, Open(set, Edit(bytes, n => n["Frames"]![0]!["Reenter"] = false)));
        // A preempter that ranks below the one it broke into: the frame is moved to a conversation of a higher priority than the current one.
        Assert.Equal(DialogueCodes.SaveState, Open(set, Edit(bytes, n => { n["Frames"]![0]!["Conversation"] = "alarm"; n["Frames"]![0]!["Node"] = "a1"; })));
        // A queue entry that is not a queueing conversation, a repeat, and one over the limit.
        Assert.Equal(DialogueCodes.SaveState, Open(set, Edit(bytes, n => n["Queue"]![0] = "plain")));
        Assert.Equal(DialogueCodes.SaveState, Open(set, Edit(bytes, n => { n["Queue"]!.AsArray().Add("bark1"); })));
        var big = FeatureFixture.Load(Document(null, 12));
        var crowded = new DialogueRuntime(big, 1);
        crowded.Start("chat", None);
        for (var i = 1; i <= DialogueRuntime.QueueLimit; i++) crowded.Start("bark" + i, None);
        var crowdedBytes = DialogueSave.Write(crowded);
        Assert.NotNull(DialogueSave.Open(big, crowdedBytes));
        Assert.Equal(DialogueCodes.SaveState, Open(big, Edit(crowdedBytes, n => n["Queue"]!.AsArray().Add("bark9"))));
        // A queue with no conversation open.
        var idle = DialogueSave.Write(new DialogueRuntime(set, 1));
        Assert.Equal(DialogueCodes.SaveState, Open(set, Edit(idle, n => n["Queue"]!.AsArray().Add("bark1"))));
        // A stack whose lower level forbids being broken into.
        var vault = new DialogueRuntime(FeatureFixture.Load(Document("""{ "id": "hall", "start": "h1", "priority": 8, "onBusy": "preempt", "nodes": [ { "id": "h1", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.h1", "next": "hend" }, { "id": "hend", "kind": "end", "result": "done" } ] }""")), 1);
        vault.Start("vault", None);
        var forged = Edit(DialogueSave.Write(vault), n =>
        {
            n["Frames"]!.AsArray().Add(new JsonObject { ["Conversation"] = "vault", ["Node"] = "v1", ["Reenter"] = true, ["Preempted"] = true });
            n["Conversation"] = "hall"; n["Node"] = "h1";
        });
        Assert.Equal(DialogueCodes.SaveState, Fixture.Refusal(() => DialogueSave.Open(vault.Set, Encoding.UTF8.GetBytes(forged.ToJsonString()))));
    }

    // ---- hot reload ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AReloadCarriesTheStackAndTheQueueAndFollowsARenameOfTheQueuedConversation()
    {
        var runtime = Runtime();
        runtime.Start("chat", None);
        runtime.Start("alarm", None);
        runtime.Start("bark1", None);
        var renamed = FeatureFixture.Load(Document().Replace("\"id\": \"bark1\"", "\"id\": \"murmur\""));
        var map = ReloadMap.Parse("""{ "format": 1, "conversations": { "bark1": "murmur" } }""");
        runtime.Reload(renamed, ReloadMode.Refuse, map);
        Assert.Equal(["murmur"], runtime.State.Queue);
        Assert.Single(runtime.State.Frames);
        Assert.True(runtime.State.Frames[0].Preempted);
        Assert.Equal("alarm", runtime.RootConversation);
    }

    [Fact]
    public void AReloadThatClosesTheOpenConversationDropsTheQueueAndSaysSo()
    {
        var runtime = Runtime();
        runtime.Start("chat", None);
        runtime.Start("bark1", None);
        runtime.Start("bark2", None);
        var next = FeatureFixture.Load(Document().Replace("""{ "id": "c1", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.c1", "next": "ask" }""", """{ "id": "c1", "kind": "effects", "effects": [ { "signal": "x" } ], "next": "ask" }"""));
        var report = runtime.Reload(next, ReloadMode.RestartConversation);
        Assert.Equal("chat/c1", report.ClosedConversation);
        Assert.Equal(2, report.DroppedQueued);
        Assert.Empty(runtime.State.Queue);
        Assert.False(runtime.InConversation);
    }

    [Fact]
    public void AReloadThatTurnsAQueuedConversationIntoOneThatRefusesIsRefusedWhole()
    {
        var runtime = Runtime();
        runtime.Start("chat", None);
        runtime.Start("bark1", None);
        var next = FeatureFixture.Load(Document().Replace("\"priority\": 2, \"onBusy\": \"queue\"", "\"priority\": 2"));
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.ReloadIncompatible, Refusal(() => runtime.Reload(next)));
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
    }

    // ---- the document --------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("\"priority\": 5, \"onBusy\": \"preempt\"", "\"priority\": 0, \"onBusy\": \"preempt\"", DialogueCodes.Range)]
    [InlineData("\"priority\": 5, \"onBusy\": \"preempt\"", "\"priority\": 1001, \"onBusy\": \"preempt\"", DialogueCodes.Range)]
    [InlineData("\"priority\": 5, \"onBusy\": \"preempt\"", "\"priority\": -1, \"onBusy\": \"preempt\"", DialogueCodes.Range)]
    [InlineData("\"priority\": 5, \"onBusy\": \"preempt\"", "\"priority\": 5, \"onBusy\": \"cut-in\"", DialogueCodes.Format)]
    [InlineData("\"priority\": 5, \"onBusy\": \"preempt\"", "\"priority\": \"high\", \"onBusy\": \"preempt\"", DialogueCodes.Format)]
    [InlineData("\"priority\": 4, \"interruptible\": false", "\"priority\": 4, \"interruptible\": \"no\"", DialogueCodes.Format)]
    public void AConversationsPriorityAndPolicyAreCheckedStrictly(string old, string replacement, string code)
    {
        Assert.Equal(code, Refusal(() => DialogueLoader.Load(Fixture.With(old, replacement, Document()), Fixture.Catalog)));
    }

    [Fact]
    public void TheFieldsAreOptionalAndTheDefaultsAreRefuseAndInterruptibleAndPriorityZero()
    {
        var set = FeatureFixture.Load(Document());
        var plain = set.Conversations["plain"];
        Assert.Equal((0, BusyPolicy.Refuse, true), (plain.Priority, plain.OnBusy, plain.Interruptible));
        var alarm = set.Conversations["alarm"];
        Assert.Equal((5, BusyPolicy.Preempt, true), (alarm.Priority, alarm.OnBusy, alarm.Interruptible));
        Assert.False(set.Conversations["vault"].Interruptible);
    }

    [Fact]
    public void TheHostsForcedInterruptStillWorksAndIsNotCheckedAgainstPriority()
    {
        var runtime = Runtime();
        runtime.Start("alarm", None);
        var forced = runtime.Interrupt("plain", None);   // priority 0 into priority 5: the host decides, as before
        Assert.Equal("p1", forced.Line!.Node);
    }

    [Fact]
    public void TheStateDigestCoversTheStackTheQueueAndTheHistory()
    {
        DialogueRuntime Build(Action<DialogueRuntime>? more = null)
        {
            var r = Runtime();
            r.Start("chat", None);
            more?.Invoke(r);
            return r;
        }
        var plain = DialogueSave.StateDigest(Build().State);
        Assert.NotEqual(plain, DialogueSave.StateDigest(Build(r => r.Start("bark1", None)).State));      // the queue
        Assert.NotEqual(plain, DialogueSave.StateDigest(Build(r => r.Start("shout", None)).State));      // the stack
        var ticked = Runtime();
        ticked.Tick = 9;
        ticked.Start("chat", None);
        Assert.NotEqual(plain, DialogueSave.StateDigest(ticked.State));                                  // the tick on the history entry
        var twice = Build(r => { r.Start("shout", None); r.Next(None); });
        var once = Build(r => r.Start("shout", None));
        Assert.NotEqual(DialogueSave.StateDigest(twice.State), DialogueSave.StateDigest(once.State));
    }
}
