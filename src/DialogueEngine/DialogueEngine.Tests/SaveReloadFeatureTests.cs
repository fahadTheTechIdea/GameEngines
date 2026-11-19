using System.Text.Json.Nodes;
using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>Saves taken at every kind of moment (mid line, mid choice, mid subflow, mid interruption), forged saves that are refused before their digest is read, and hot reload with live conversations.</summary>
public sealed class SaveReloadFeatureTests
{
    private static readonly IReadOnlyDictionary<string, FactValue> Facts = FeatureFixture.Facts();

    /// <summary>One scripted session: every call a host could make, in an order that goes through a subflow, an interruption, a timed choice and the variant.</summary>
    private static readonly Func<DialogueRuntime, DialogueStep>[] Script =
    [
        r => r.Start("main", Facts),                       // 0 hi
        r => r.Next(Facts),                                // 1 detour line (inside the subflow)
        r => r.Interrupt("alarm", Facts),                  // 2 interrupted in the subflow
        r => r.Next(Facts),                                // 3 back at the detour line
        r => r.Next(Facts),                                // 4 the choice in main
        r => r.Interrupt("alarm", Facts),                  // 5 interrupted at the choice
        r => r.Next(Facts),                                // 6 the choice again
        r => r.Choose("stay", Facts),                      // 7 variant: va
        r => r.Next(Facts),                                // 8 the choice
        r => r.Timeout(Facts),                             // 9 timed out: stay again, variant vb
        r => r.Next(Facts),                                // 10 the choice
        r => r.Choose("stay", Facts),                      // 11 variant: vc
        r => r.Next(Facts),                                // 12 the choice
        r => r.Choose("leave", Facts),                     // 13 the end
    ];

    private static string Describe(DialogueStep step, DialogueRuntime runtime) =>
        $"{step.Kind} {step.Line?.Node}{step.Choices?.Node} {string.Join(",", step.Choices?.Options.Select(o => o.Id + (o.Enabled ? "" : "!")) ?? [])} {step.Result} [{string.Join(";", step.Events)}] "
        + $"frames={runtime.State.Frames.Count} {DialogueSave.StateDigest(runtime.State)[..12]}";

    private static List<string> Whole()
    {
        var runtime = new DialogueRuntime(FeatureFixture.Load(), 77);
        return Script.Select(call => Describe(call(runtime), runtime)).ToList();
    }

    [Fact]
    public void ASaveTakenAfterAnyCallOfTheSessionContinuesExactlyLikeTheUninterruptedSession()
    {
        var whole = Whole();
        for (var cut = 0; cut < Script.Length - 1; cut++)
        {
            var first = new DialogueRuntime(FeatureFixture.Load(), 77);
            var seen = new List<string>();
            for (var i = 0; i <= cut; i++) seen.Add(Describe(Script[i](first), first));
            var resumed = DialogueSave.Open(FeatureFixture.Load(), DialogueSave.Write(first));
            Assert.Equal(DialogueSave.StateDigest(first.State), DialogueSave.StateDigest(resumed.State));
            for (var i = cut + 1; i < Script.Length; i++) seen.Add(Describe(Script[i](resumed), resumed));
            Assert.Equal(whole, seen);
        }
    }

    [Fact]
    public void TheSessionPassesThroughEachKindOfMomentTheSaveTestsRelyOn()
    {
        var runtime = new DialogueRuntime(FeatureFixture.Load(), 77);
        var moments = new HashSet<string>();
        foreach (var call in Script)
        {
            call(runtime);
            moments.Add($"{runtime.State.WaitingFor}/{runtime.State.Frames.Count}/{string.Join("", runtime.State.Frames.Select(f => f.Reenter ? "i" : "c"))}");
        }
        Assert.Contains("Line/0/", moments);       // mid line
        Assert.Contains("Choice/0/", moments);     // mid choice
        Assert.Contains("Line/1/c", moments);      // mid subflow
        Assert.Contains("Line/2/ci", moments);     // interrupted inside a subflow
        Assert.Contains("Line/1/i", moments);      // interrupted at a choice
    }

    [Fact]
    public void TheDigestOfTheStateCoversTheCallsItIsInside()
    {
        var a = new DialogueRuntime(FeatureFixture.Load(), 1);
        a.Start("main", Facts);
        var b = new DialogueRuntime(FeatureFixture.Load(), 1);
        b.Start("main", Facts);
        Assert.Equal(DialogueSave.StateDigest(a.State), DialogueSave.StateDigest(b.State));
        a.Interrupt("alarm", Facts);
        b.Interrupt("alarm", Facts);
        Assert.Equal(DialogueSave.StateDigest(a.State), DialogueSave.StateDigest(b.State));
        Assert.Contains("frame main|hi|reenter", DialogueSave.Canonical(a.State));
        var c = new DialogueRuntime(FeatureFixture.Load(), 1);
        c.Start("main", Facts);
        Assert.NotEqual(DialogueSave.StateDigest(a.State), DialogueSave.StateDigest(c.State));       // the interruption is part of the state
    }

    // ---- forged saves -------------------------------------------------------------------------------------------------------------

    private static string Open(byte[] bytes) => Fixture.Refusal(() => DialogueSave.Open(FeatureFixture.Load(), bytes));

    private static byte[] Edit(byte[] bytes, Action<JsonNode> change)
    {
        var node = JsonNode.Parse(bytes)!;
        change(node);
        return System.Text.Encoding.UTF8.GetBytes(node.ToJsonString());
    }

    private static byte[] SavedInside()
    {
        var runtime = new DialogueRuntime(FeatureFixture.Load(), 5);
        runtime.Start("main", Facts);
        runtime.Next(Facts);                                    // inside the detour: one call frame
        return DialogueSave.Write(runtime);
    }

    private static JsonObject Frame(string conversation, string node, bool reenter) => new() { ["Conversation"] = conversation, ["Node"] = node, ["Reenter"] = reenter };

    [Fact]
    public void AStateWithCallsTheDocumentCouldNotHaveMadeIsRefusedAsAStateBeforeAnyDigestIsRead()
    {
        var bytes = SavedInside();
        Assert.NotNull(DialogueSave.Open(FeatureFixture.Load(), bytes));
        // none of these edits repairs the digest: each is refused for what it is, so the digest is not what stands between a forger and the engine
        Assert.Equal(DialogueCodes.SaveDigest, Open(Edit(bytes, n => n["Frames"]!.AsArray().Clear())));                                          // the detour started on its own is a state the document can make; only the digest is wrong
        Assert.Equal(DialogueCodes.SaveState, Open(Edit(bytes, n => n["Frames"]![0]!["Node"] = "ask")));                                         // not a call node
        Assert.Equal(DialogueCodes.SaveState, Open(Edit(bytes, n => n["Frames"]![0]!["Node"] = "ghost")));                                       // no such node
        Assert.Equal(DialogueCodes.SaveState, Open(Edit(bytes, n => n["Frames"]![0]!["Conversation"] = "ghost")));
        Assert.Equal(DialogueCodes.SaveState, Open(Edit(bytes, n => n["Frames"]![0]!["Reenter"] = true)));                                       // interrupted a call node, which does not wait
        Assert.Equal(DialogueCodes.SaveState, Open(Edit(bytes, n => n["Frames"]!.AsArray().Add(Frame("main", "sub", false)))));                  // the chain no longer reaches the detour
        Assert.Equal(DialogueCodes.SaveState, Open(Edit(bytes, n => { n["Conversation"] = null; n["Node"] = null; n["Waiting"] = "nothing"; })));    // calls open, no conversation
        Assert.Equal(DialogueCodes.SaveState, Open(Edit(bytes, n => { n["Frames"]!.AsArray().Clear(); for (var i = 0; i < 9; i++) n["Frames"]!.AsArray().Add(Frame("main", "hi", true)); })));   // deeper than the limit
        Assert.Equal(DialogueCodes.SaveDigest, Open(Edit(bytes, n => n["Counter"] = 3)));                                                        // a state that fits, a digest that does not
    }

    [Fact]
    public void ADigestForgedOverAStateThatFitsIsStillRefusedAndAnOldOrFutureFormatIsRefusedFirst()
    {
        var bytes = SavedInside();
        Assert.Equal(DialogueCodes.SaveDigest, Open(Edit(bytes, n => n["Digest"] = new string('a', 64))));
        Assert.Equal(DialogueCodes.SaveFormat, Open(Edit(bytes, n => n["Format"] = 1)));
        Assert.Equal(DialogueCodes.SaveFormat, Open(Edit(bytes, n => n["Format"] = 2)));
        Assert.Equal(DialogueCodes.SaveFormat, Open(Edit(bytes, n => n["Format"] = 4)));
        Assert.Equal(DialogueCodes.SaveFormat, Open(Edit(bytes, n => n["Frames"]![0]!["Extra"] = 1)));
        Assert.Equal(3, DialogueSave.Format);
    }

    [Fact]
    public void AnOldDocumentFormatIsRefusedByTheLoader()
    {
        Assert.Equal(DialogueCodes.Format, Fixture.Refusal(() => FeatureFixture.Load(FeatureFixture.With("\"format\": 3", "\"format\": 1"))));
        Assert.Equal(3, DialogueLoader.Format);
    }

    // ---- hot reload ---------------------------------------------------------------------------------------------------------------

    private static DialogueRuntime InsideTheDetour()
    {
        var runtime = new DialogueRuntime(FeatureFixture.Load(), 5);
        runtime.Start("main", Facts);
        runtime.Next(Facts);
        return runtime;
    }

    [Fact]
    public void AReloadThatKeepsTheConversationGoingMigratesTheStateInsideTheSubflow()
    {
        var runtime = InsideTheDetour();
        var edited = FeatureFixture.Load(FeatureFixture.With("\"line\": \"detour.d1\"", "\"line\": \"detour.d1b\""));      // the text key of the line in the detour changes
        var report = runtime.Reload(edited);
        Assert.Null(report.ClosedConversation);
        Assert.Equal([new DialogueFrame("main", "sub", false)], runtime.State.Frames);
        Assert.Equal("detour", runtime.State.Conversation);
        Assert.Equal(StepKind.Choices, runtime.Next(Facts).Kind);                      // the subflow goes on and returns
        Assert.Equal(FactValue.From(1L), runtime.State.Variables["mood"]);
        Assert.Same(edited, runtime.Set);
    }

    [Fact]
    public void AReloadThatRemovesWhereTheConversationIsInsideIsRefusedAndChangesNothing()
    {
        var gone = FeatureFixture.With("{ \"id\": \"detour\", \"start\": \"d1\"", "{ \"id\": \"detour2\", \"start\": \"d1\"").Replace("\"conversation\": \"detour\"", "\"conversation\": \"detour2\"");
        var asEffects = FeatureFixture.With("{ \"id\": \"d1\", \"kind\": \"line\", \"speaker\": \"cmdr\", \"portrait\": \"calm\", \"line\": \"detour.d1\", \"next\": \"d2\" }", "{ \"id\": \"d1\", \"kind\": \"effects\", \"effects\": [ { \"add\": \"mood\", \"by\": 1 } ], \"next\": \"d2\" }");
        foreach (var text in new[] { gone, asEffects })
        {
            var runtime = InsideTheDetour();
            var before = DialogueSave.StateDigest(runtime.State);
            var set = runtime.Set;
            Assert.Equal(DialogueCodes.ReloadIncompatible, Fixture.Refusal(() => runtime.Reload(FeatureFixture.Load(text))));
            Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
            Assert.Same(set, runtime.Set);
        }
    }

    [Fact]
    public void AReloadThatIsAskedToRestartClosesTheConversationAndKeepsTheRest()
    {
        var gone = FeatureFixture.With("{ \"id\": \"detour\", \"start\": \"d1\"", "{ \"id\": \"detour2\", \"start\": \"d1\"").Replace("\"conversation\": \"detour\"", "\"conversation\": \"detour2\"");
        var runtime = InsideTheDetour();
        runtime.Interrupt("alarm", Facts);
        var visits = runtime.State.Visits["main/hi"];
        var report = runtime.Reload(FeatureFixture.Load(gone), ReloadMode.RestartConversation);
        Assert.Equal("alarm/a1", report.ClosedConversation);                           // it was in the interrupter, which still exists... its caller chain does not
        Assert.False(runtime.InConversation);
        Assert.Empty(runtime.State.Frames);
        Assert.Equal(Waiting.Nothing, runtime.State.WaitingFor);
        Assert.Equal(visits, runtime.State.Visits["main/hi"]);
        Assert.Equal(FactValue.From(0L), runtime.State.Variables["mood"]);
        Assert.Equal(StepKind.Line, runtime.Start("main", Facts).Kind);                // and the game can start it again
    }

    [Fact]
    public void RestartingNeverRescuesAReloadThatTheRestOfTheStateRefuses()
    {
        var withoutMood = FeatureFixture.With("\"variables\": [ { \"id\": \"mood\", \"type\": \"int\", \"initial\": 0, \"min\": 0, \"max\": 9 } ]", "\"variables\": [ { \"id\": \"other\", \"type\": \"int\", \"initial\": 0, \"min\": 0, \"max\": 9 } ]")
            .Replace("{ \"add\": \"mood\", \"by\": 1 }", "{ \"add\": \"other\", \"by\": 1 }").Replace("\"fact\": \"var.mood\"", "\"fact\": \"var.other\"");
        var runtime = InsideTheDetour();
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.ReloadIncompatible, Fixture.Refusal(() => runtime.Reload(FeatureFixture.Load(withoutMood), ReloadMode.RestartConversation)));
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
        Assert.True(runtime.InConversation);
    }

    [Fact]
    public void ARestartReloadThatDidNotNeedToRestartLeavesTheConversationOpen()
    {
        var runtime = InsideTheDetour();
        var report = runtime.Reload(FeatureFixture.Load(FeatureFixture.With("\"line\": \"main.hi\"", "\"line\": \"main.hello\"")), ReloadMode.RestartConversation);
        Assert.Null(report.ClosedConversation);
        Assert.True(runtime.InConversation);
        Assert.Single(runtime.State.Frames);
    }
}
