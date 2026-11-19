using System.Text;
using System.Text.Json.Nodes;
using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>
/// Limits that are seen from the file or the bytes alone: a run of nodes that never wait (the runtime passes at most <see cref="DialogueRuntime.SilentLimit"/> + 1 of them in one call),
/// visit counts that stay inside the range the visits.* facts are declared with, and the size of a document and of a save.
/// </summary>
public sealed class HardeningTests
{
    private const int Limit = DialogueRuntime.SilentLimit + 1;

    private static string Effects(string prefix, int count, string last) =>
        string.Join(",\n", Enumerable.Range(1, count).Select(i => $$"""{ "id": "{{prefix}}{{i}}", "kind": "effects", "effects": [ { "add": "n", "by": 1 } ], "next": "{{(i == count ? last : prefix + (i + 1))}}" }"""));

    private static string Head(string conversations) => $$"""
    { "format": 3, "id": "demo.chain",
      "variables": [ { "id": "n", "type": "int", "initial": 0, "min": 0, "max": 1000000 } ], "externalFacts": [],
      "speakers": [ { "id": "cmdr", "portraits": ["calm"] } ], "quests": [],
      "conversations": [ {{conversations}} ] }
    """;

    private static string Chain(int count) => Head($$"""
      { "id": "c", "start": "e1", "nodes": [ {{Effects("e", count, "fin")}}, { "id": "fin", "kind": "end", "result": "done" } ] }
    """);

    [Fact]
    public void ARunOfSixtyFiveNodesThatNeverWaitIsAcceptedAndPlaysAndOneMoreIsRefusedAtLoadNotAtPlay()
    {
        var set = DialogueLoader.Load(Chain(Limit - 1), Fixture.Catalog);   // 64 effects and the end: 65 nodes
        var step = new DialogueRuntime(set, 1).Start("c", FeatureFixture.NoFacts);
        Assert.Equal(StepKind.Ended, step.Kind);
        Assert.Equal(Limit - 1, step.Events.Count);
        Assert.Equal(DialogueCodes.SilentChain, Fixture.Refusal(() => DialogueLoader.Load(Chain(Limit), Fixture.Catalog)));
    }

    private static string AfterACall(int effects) => Head($$"""
      { "id": "callee", "start": "l", "nodes": [
        { "id": "l", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "callee.l", "next": "back" },
        { "id": "back", "kind": "return", "result": "back" } ] },
      { "id": "main", "start": "go", "nodes": [
        { "id": "go", "kind": "call", "conversation": "callee", "next": "e1" },
        {{Effects("e", effects, "fin")}},
        { "id": "fin", "kind": "end", "result": "done" } ] }
    """);

    [Fact]
    public void TheRunAfterACallCountsWhereverTheCalleeIsPickedUpAgain()
    {
        // The callee waits at a line; the player goes on from there: the return, the effects after the call and the end are one run of 1 + 63 + 1 nodes.
        var runtime = new DialogueRuntime(DialogueLoader.Load(AfterACall(Limit - 2), Fixture.Catalog), 1);
        Assert.Equal(StepKind.Line, runtime.Start("main", FeatureFixture.NoFacts).Kind);
        Assert.Equal(StepKind.Ended, runtime.Next(FeatureFixture.NoFacts).Kind);
        Assert.Equal(DialogueCodes.SilentChain, Fixture.Refusal(() => DialogueLoader.Load(AfterACall(Limit - 1), Fixture.Catalog)));
    }

    private static string Interrupter(int effects) => Head($$"""
      { "id": "talk", "start": "l", "nodes": [
        { "id": "l", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "talk.l", "next": "fin" },
        { "id": "fin", "kind": "end", "result": "done" } ] },
      { "id": "shout", "start": "e1", "nodes": [
        {{Effects("e", effects, "back")}},
        { "id": "back", "kind": "return", "result": "back" } ] }
    """);

    [Fact]
    public void AnInterruptionCountsTheNodeItGoesBackToShowAgain()
    {
        var runtime = new DialogueRuntime(DialogueLoader.Load(Interrupter(Limit - 2), Fixture.Catalog), 1);
        runtime.Start("talk", FeatureFixture.NoFacts);
        Assert.Equal(StepKind.Line, runtime.Interrupt("shout", FeatureFixture.NoFacts).Kind);   // 63 effects, the return and the line shown again: 65
        Assert.Equal(DialogueCodes.SilentChain, Fixture.Refusal(() => DialogueLoader.Load(Interrupter(Limit - 1), Fixture.Catalog)));
    }

    [Fact]
    public void TheRuntimeGuardBehindTheLoaderStillRefusesAndPutsTheStateBack()
    {
        // The loader no longer lets such a document through, so the guard is driven with a set built by hand.
        var nodes = new Dictionary<string, Node>(StringComparer.Ordinal);
        for (var i = 1; i <= Limit + 5; i++) nodes["e" + i] = new EffectsNode("e" + i, [new SignalEffect("s")], i == Limit + 5 ? "fin" : "e" + (i + 1));
        nodes["fin"] = new EndNode("fin", "done");
        var empty = new Dictionary<string, IReadOnlyList<string>>();
        var set = new DialogueSet("hand.made", "digest", new Dictionary<string, VariableDef>(), new Dictionary<string, ExternalFactDef>(), new Dictionary<string, SpeakerDef>(),
            new Dictionary<string, ConversationDef> { ["c"] = new ConversationDef("c", "e1", nodes) }, new Dictionary<string, QuestDef>(), empty, empty, new Dictionary<string, ContentWarningDef>(), 100);
        var runtime = new DialogueRuntime(set, 1);
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.StepLimit, Fixture.Refusal(() => runtime.Start("c", FeatureFixture.NoFacts)));
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
        Assert.False(runtime.InConversation);
    }

    // ---- visit counts -----------------------------------------------------------------------------------------------------------------

    private static (byte[] Bytes, string Canonical, DialogueSet Set) LoopSave()
    {
        var set = FeatureFixture.Load(FeatureFixture.VariantDocument("cycle"));
        var runtime = new DialogueRuntime(set, 1);
        runtime.Start("loop", FeatureFixture.NoFacts);
        return (DialogueSave.Write(runtime), DialogueSave.Canonical(runtime.State), set);
    }

    private static void SetCount(JsonObject save, int count) => save["Visits"]!.AsArray().First(r => r!["Key"]!.GetValue<string>() == "loop/v")!["Count"] = count;

    private static string Forged(byte[] bytes, string canonical, string from, string to, DialogueSet set, Action<JsonObject> field, out DialogueRuntime? opened)
    {
        Assert.Contains(from, canonical, StringComparison.Ordinal);
        var digest = EngineCommon.Digest.Of(canonical.Replace(from, to, StringComparison.Ordinal));
        var node = JsonNode.Parse(bytes)!.AsObject();
        field(node);
        node["Digest"] = digest;
        var forged = Encoding.UTF8.GetBytes(node.ToJsonString());
        opened = null;
        try { opened = DialogueSave.Open(set, forged); }
        catch (EngineCommon.EngineRefusedException refusal) { return refusal.Code; }
        return "";
    }

    [Fact]
    public void AVisitCountStopsAtTheBoundTheFactsAreDeclaredWithAndASaveBeyondItIsRefused()
    {
        var (bytes, canonical, set) = LoopSave();
        Assert.Equal(DialogueCodes.SaveState, Forged(bytes, canonical, "visit loop/v=1", "visit loop/v=1000000001", set, n => SetCount(n, 1_000_000_001), out _));
        Assert.Equal("", Forged(bytes, canonical, "visit loop/v=1", "visit loop/v=999999999", set, n => SetCount(n, 999_999_999), out var runtime));
        Assert.NotNull(runtime);
        for (var round = 0; round < 3; round++)
        {
            Assert.Equal(StepKind.Choices, runtime!.Next(FeatureFixture.NoFacts).Kind);
            Assert.Equal(StepKind.Line, runtime.Choose("again", FeatureFixture.NoFacts).Kind);
        }
        Assert.Equal(1_000_000_000, runtime!.State.Visits["loop/v"]);
        // The saturated state saves and opens again like any other.
        Assert.Equal(DialogueSave.StateDigest(runtime.State), DialogueSave.StateDigest(DialogueSave.Open(set, DialogueSave.Write(runtime)).State));
    }

    // ---- sizes ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ASaveAMapAndADocumentBeyondTheirLargestSizeAreRefusedBeforeAnythingIsParsedAndAtTheLimitTheyAreRead()
    {
        var set = Fixture.Load();
        static string Message(Action action) => Assert.Throws<EngineCommon.EngineRefusedException>(action).Message;
        Assert.Contains("at most", Message(() => DialogueSave.Open(set, new byte[DialogueSave.MaxBytes + 1])), StringComparison.Ordinal);
        Assert.DoesNotContain("at most", Message(() => DialogueSave.Open(set, new byte[DialogueSave.MaxBytes])), StringComparison.Ordinal);   // read, and found not to be a save
        Assert.Contains("at most", Message(() => ReloadMap.Parse(new string(' ', ReloadMap.MaxCharacters + 1))), StringComparison.Ordinal);
        Assert.DoesNotContain("at most", Message(() => ReloadMap.Parse(new string(' ', ReloadMap.MaxCharacters))), StringComparison.Ordinal);
        Assert.Equal(DialogueCodes.Range, Fixture.Refusal(() => DialogueLoader.Load(new string(' ', DialogueLoader.MaxCharacters + 1), Fixture.Catalog)));
        Assert.Equal(DialogueCodes.Format, Fixture.Refusal(() => DialogueLoader.Load(new string(' ', DialogueLoader.MaxCharacters), Fixture.Catalog)));
    }

    [Fact]
    public void ANegativeVisitCountInASaveIsRefusedAsAStateTheDocumentCouldNotHaveMade()
    {
        var (bytes, canonical, set) = LoopSave();
        Assert.Equal(DialogueCodes.SaveState, Forged(bytes, canonical, "visit loop/v=1", "visit loop/v=-1", set, n => SetCount(n, -1), out _));
        Assert.Equal(DialogueCodes.SaveState, Forged(bytes, canonical, "visit loop/v=1", "visit loop/v=-2000000000", set, n => SetCount(n, -2_000_000_000), out _));
    }

    // ---- runs that pass through calls ------------------------------------------------------------------------------------------------

    private static string SilentCallee(int after) => Head($$"""
      { "id": "callee", "start": "e", "nodes": [
        { "id": "e", "kind": "effects", "effects": [ { "add": "n", "by": 1 } ], "next": "back" },
        { "id": "back", "kind": "return", "result": "back" } ] },
      { "id": "main", "start": "go", "nodes": [
        { "id": "go", "kind": "call", "conversation": "callee", "next": "x1" },
        {{Effects("x", after, "fin")}},
        { "id": "fin", "kind": "end", "result": "done" } ] }
    """);

    [Fact]
    public void ARunThatGoesThroughACalleeThatNeverWaitsCountsTheCalleeAndWhatFollowsTheCall()
    {
        // go, the callee's two nodes, 59 effects and the end: 63. With 61 effects the run is 65 and plays; with 62 it is 66.
        Assert.Equal(StepKind.Ended, new DialogueRuntime(DialogueLoader.Load(SilentCallee(Limit - 4), Fixture.Catalog), 1).Start("main", FeatureFixture.NoFacts).Kind);
        Assert.Equal(DialogueCodes.SilentChain, Fixture.Refusal(() => DialogueLoader.Load(SilentCallee(Limit - 3), Fixture.Catalog)));
    }

    private static string Nested(int inB, int inA) => Head($$"""
      { "id": "c", "start": "ce", "nodes": [
        { "id": "ce", "kind": "effects", "effects": [ { "add": "n", "by": 1 } ], "next": "cr" },
        { "id": "cr", "kind": "return", "result": "back" } ] },
      { "id": "b", "start": "bs", "nodes": [
        { "id": "bs", "kind": "call", "conversation": "c", "next": "eb1" },
        {{Effects("eb", inB, "br")}},
        { "id": "br", "kind": "return", "result": "back" } ] },
      { "id": "a", "start": "as", "nodes": [
        { "id": "as", "kind": "call", "conversation": "b", "next": "ea1" },
        {{Effects("ea", inA, "fin")}},
        { "id": "fin", "kind": "end", "result": "done" } ] }
    """);

    [Fact]
    public void ACallInsideACallCountsEveryNodeOfTheWholeRunThroughBothReturns()
    {
        // as, bs, ce, cr, the effects of b, br, the effects of a, fin: 6 + inB + inA.
        Assert.Equal(StepKind.Ended, new DialogueRuntime(DialogueLoader.Load(Nested(30, Limit - 6 - 30), Fixture.Catalog), 1).Start("a", FeatureFixture.NoFacts).Kind);
        Assert.Equal(DialogueCodes.SilentChain, Fixture.Refusal(() => DialogueLoader.Load(Nested(30, Limit - 5 - 30), Fixture.Catalog)));
    }

    private static string DeepWait(int inB, int inA) => Head($$"""
      { "id": "c", "start": "cl", "nodes": [
        { "id": "cl", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "c.l", "next": "cr" },
        { "id": "cr", "kind": "return", "result": "back" } ] },
      { "id": "b", "start": "bs", "nodes": [
        { "id": "bs", "kind": "call", "conversation": "c", "next": "eb1" },
        {{Effects("eb", inB, "br")}},
        { "id": "br", "kind": "return", "result": "back" } ] },
      { "id": "a", "start": "as", "nodes": [
        { "id": "as", "kind": "call", "conversation": "b", "next": "ea1" },
        {{Effects("ea", inA, "fin")}},
        { "id": "fin", "kind": "end", "result": "done" } ] }
    """);

    [Fact]
    public void ARunPickedUpInsideTheInnerCallAfterALineCountsThroughTheReturnsOfEveryCallerAbove()
    {
        // From the line in the innermost conversation the run is cr, the effects of b, br, the effects of a and fin: 3 + inB + inA.
        var ok = new DialogueRuntime(DialogueLoader.Load(DeepWait(30, Limit - 3 - 30), Fixture.Catalog), 1);
        Assert.Equal(StepKind.Line, ok.Start("a", FeatureFixture.NoFacts).Kind);
        Assert.Equal(StepKind.Ended, ok.Next(FeatureFixture.NoFacts).Kind);
        Assert.Equal(DialogueCodes.SilentChain, Fixture.Refusal(() => DialogueLoader.Load(DeepWait(30, Limit - 2 - 30), Fixture.Catalog)));
    }
}
