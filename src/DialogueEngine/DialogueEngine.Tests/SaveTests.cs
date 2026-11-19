using System.Text;
using System.Text.Json.Nodes;
using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>D6 and D7: a save is digest-checked and bound to its document, and a document can be reloaded under a running session only if the state still fits.</summary>
public sealed class SaveTests
{
    private static DialogueRuntime AtTheChoice(ulong seed = 3)
    {
        var runtime = new DialogueRuntime(Fixture.Load(), seed);
        runtime.Start("briefing", Fixture.Facts());
        runtime.Next(Fixture.Facts());
        return runtime;
    }

    private static byte[] Edit(byte[] bytes, Action<JsonObject> change)
    {
        var node = JsonNode.Parse(bytes)!.AsObject();
        change(node);
        return Encoding.UTF8.GetBytes(node.ToJsonString());
    }

    private static string Open(byte[] bytes) => Fixture.Refusal(() => DialogueSave.Open(Fixture.Load(), bytes));

    [Fact]
    public void ASessionSavedMidConversationContinuesExactlyLikeOneThatWasNeverSaved()
    {
        var original = AtTheChoice();
        var loaded = DialogueSave.Open(Fixture.Load(), DialogueSave.Write(original));
        Assert.Equal(DialogueSave.StateDigest(original.State), DialogueSave.StateDigest(loaded.State));
        Assert.Equal(Waiting.Choice, loaded.State.WaitingFor);
        var one = Continue(original);
        var two = Continue(loaded);
        Assert.Equal(one, two);
        Assert.Equal(DialogueSave.StateDigest(original.State), DialogueSave.StateDigest(loaded.State));
    }

    private static List<string> Continue(DialogueRuntime runtime)
    {
        var seen = new List<string>();
        var step = runtime.Choose("accept", Fixture.Facts());
        while (step.Kind != StepKind.Ended)
        {
            seen.Add(step.Kind + " " + step.Line?.Node);
            step = step.Kind == StepKind.Line ? runtime.Next(Fixture.Facts()) : runtime.Choose("accept", Fixture.Facts());
        }
        seen.Add("ended " + step.Result);
        return seen;
    }

    [Fact]
    public void ASaveBetweenConversationsAndOneAfterQuestsAlsoRoundTrips()
    {
        var runtime = new DialogueRuntime(Fixture.Load(), 9);
        var empty = DialogueSave.Open(Fixture.Load(), DialogueSave.Write(runtime));
        Assert.False(empty.InConversation);
        Fixture.Play(runtime, "accept");
        runtime.UpdateQuests(Fixture.Facts(hold: "completed"));
        var again = DialogueSave.Open(Fixture.Load(), DialogueSave.Write(runtime));
        Assert.Equal(new QuestProgress(QuestStatus.Completed, "done"), again.State.Quests["hold"]);
        Assert.Equal(DialogueSave.StateDigest(runtime.State), DialogueSave.StateDigest(again.State));
    }

    [Fact]
    public void TheSameStateAlwaysWritesTheSameBytes() =>
        Assert.Equal(DialogueSave.Write(AtTheChoice()), DialogueSave.Write(AtTheChoice()));

    [Fact]
    public void EachKindOfDamageToASaveIsRefusedWithItsOwnCode()
    {
        var bytes = DialogueSave.Write(AtTheChoice());
        Assert.Equal(DialogueCodes.SaveFormat, Open(bytes[..(bytes.Length / 2)]));
        Assert.Equal(DialogueCodes.SaveFormat, Open(Encoding.UTF8.GetBytes("[]")));
        Assert.Equal(DialogueCodes.SaveFormat, Open(Edit(bytes, n => n["Format"] = 1)));
        Assert.Equal(DialogueCodes.SaveFormat, Open(Edit(bytes, n => n["Extra"] = 1)));
        Assert.Equal(DialogueCodes.SaveFormat, Open(Edit(bytes, n => n["Seed"] = "many")));
        Assert.Equal(DialogueCodes.SaveFormat, Open(Edit(bytes, n => n["Waiting"] = "asleep")));
        Assert.Equal(DialogueCodes.SaveFormat, Open(Edit(bytes, n => n["Variables"]![0]!["Kind"] = "Float")));
        Assert.Equal(DialogueCodes.SaveFormat, Open(Edit(bytes, n => n["Quests"]![0]!["Status"] = "dead")));
        Assert.Equal(DialogueCodes.SaveDocument, Open(Edit(bytes, n => n["Document"] = new string('A', 64))));
        Assert.Equal(DialogueCodes.SaveDocument, Open(Edit(bytes, n => n["Id"] = "another")));
        Assert.Equal(DialogueCodes.SaveDigest, Open(Edit(bytes, n => n["Counter"] = 99)));
        Assert.Equal(DialogueCodes.SaveDigest, Open(Edit(bytes, n => n["Seed"] = "4")));
        Assert.Equal(DialogueCodes.SaveDigest, Open(Edit(bytes, n => n["Variables"]![2]!["Integer"] = 3)));
        Assert.Equal(DialogueCodes.SaveState, Open(Edit(bytes, n => n["Quests"]![0]!["Status"] = "active")));   // a state the document could not have made is refused before the digest is looked at
        Assert.Equal(DialogueCodes.SaveState, Open(Edit(bytes, n => n["Waiting"] = "line")));
        Assert.Equal(DialogueCodes.SaveState, Open(Edit(bytes, n => n["Node"] = "bye")));
        Assert.Equal(DialogueCodes.SaveDigest, Open(Edit(bytes, n => n["Visits"]![0]!["Count"] = 7)));
        Assert.Equal(DialogueCodes.SaveDigest, Open(Edit(bytes, n => n["Digest"] = new string('0', 64))));
    }

    [Fact]
    public void AForgedDigestDoesNotBringInAStateTheDocumentCouldNotHaveMade()
    {
        var runtime = AtTheChoice();
        var bytes = DialogueSave.Write(runtime);
        var canonical = DialogueSave.Canonical(runtime.State);
        string Forge(string from, string to, Action<JsonObject> field)
        {
            Assert.Contains(from, canonical, StringComparison.Ordinal);
            var digest = EngineCommon.Digest.Of(canonical.Replace(from, to, StringComparison.Ordinal));
            return Open(Edit(bytes, n => { field(n); n["Digest"] = digest; }));
        }
        // Trust is declared 0 to 5.
        Assert.Equal(DialogueCodes.SaveState, Forge("var trust=Integer:\"2\"", "var trust=Integer:\"99\"", n => n["Variables"]![2]!["Integer"] = 99));
        // A node count for a node that does not exist.
        Assert.Equal(DialogueCodes.SaveState, Forge("visit briefing/ask=1", "visit briefing/ghost=1", n => n["Visits"]![0]!["Key"] = "briefing/ghost"));
        // A cursor that waits for a choice at a line.
        Assert.Equal(DialogueCodes.SaveState, Forge("cursor=briefing|ask|choice", "cursor=briefing|hello|choice", n => n["Node"] = "hello"));
        // A quest at a stage it does not have.
        Assert.Equal(DialogueCodes.SaveState, Forge("quest hold=not-started|", "quest hold=active|nowhere", n => { n["Quests"]![0]!["Status"] = "active"; n["Quests"]![0]!["Stage"] = "nowhere"; }));
        // A text variable outside its allowed values.
        Assert.Equal(DialogueCodes.SaveState, Forge("var stance=String:\"neutral\"", "var stance=String:\"timid\"", n => n["Variables"]![1]!["Text"] = "timid"));
        // A negative roll counter.
        Assert.Equal(DialogueCodes.SaveState, Forge("counter=0", "counter=-5", n => n["Counter"] = -5));
    }

    [Fact]
    public void ASaveOpenedAgainstAnEditedDocumentIsRefused()
    {
        var bytes = DialogueSave.Write(AtTheChoice());
        var edited = Fixture.Load(Fixture.With("\"line\": \"brief.plain\"", "\"line\": \"brief.plain\" "));
        Assert.Equal(DialogueCodes.SaveDocument, Fixture.Refusal(() => DialogueSave.Open(edited, bytes)));
    }

    // ---- D7: hot reload -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ACompatibleEditedDocumentIsAcceptedUnderARunningSessionAndTheStateGoesOn()
    {
        var runtime = AtTheChoice();
        runtime.Choose("accept", Fixture.Facts());   // now at a line after the random node; trust 3, quest active
        var digestBefore = DialogueSave.StateDigest(runtime.State);
        var edited = Fixture.Load(Fixture.With("\"variables\": [", "\"variables\": [ { \"id\": \"fame\", \"type\": \"int\", \"initial\": 1, \"min\": 0, \"max\": 9 },")
            .Replace("\"quests\": [", "\"quests\": [ { \"id\": \"scout\", \"title\": \"quest.scout.title\", \"start\": \"go\", \"stages\": [ { \"id\": \"go\", \"journal\": \"quest.scout.go\", \"advance\": [] } ] },", StringComparison.Ordinal));
        var report = runtime.Reload(edited);
        Assert.Equal(["fame"], report.AddedVariables);
        Assert.Equal(["scout"], report.AddedQuests);
        Assert.Equal(0, report.DroppedVisits);
        Assert.Same(edited, runtime.Set);
        Assert.Equal(FactValue.From(3L), runtime.State.Variables["trust"]);
        Assert.Equal(FactValue.From(1L), runtime.State.Variables["fame"]);
        Assert.Equal(QuestStatus.NotStarted, runtime.State.Quests["scout"].Status);
        Assert.NotEqual(digestBefore, DialogueSave.StateDigest(runtime.State));   // new variables are part of the state now
        Assert.Equal(StepKind.Ended, runtime.Next(Fixture.Facts()).Kind);   // the line was the last before the end, and the conversation carries on under the new document
        // The saved state of the reloaded session opens against the new document.
        Assert.NotNull(DialogueSave.Open(edited, DialogueSave.Write(runtime)));
    }

    [Fact]
    public void ADocumentThatNoLongerFitsTheStateIsRefusedAndNothingChanges()
    {
        // A document with a spare variable nothing uses, so it can be removed or retyped without breaking the document itself.
        var spare = "{ \"id\": \"spare\", \"type\": \"bool\", \"initial\": false },";
        var withSpare = Fixture.With("\"variables\": [", "\"variables\": [ " + spare);
        var runtime = new DialogueRuntime(Fixture.Load(withSpare), 3);
        runtime.Start("briefing", Fixture.Facts());
        runtime.Next(Fixture.Facts());
        var before = DialogueSave.StateDigest(runtime.State);
        var original = runtime.Set;
        string Reload(string document) => Fixture.Refusal(() => runtime.Reload(Fixture.Load(document)));
        // A variable the session holds is gone.
        Assert.Equal(DialogueCodes.ReloadIncompatible, Reload(Fixture.Document));
        // A variable changed type.
        Assert.Equal(DialogueCodes.ReloadIncompatible, Reload(Fixture.With(spare, "{ \"id\": \"spare\", \"type\": \"int\", \"initial\": 1, \"min\": 0, \"max\": 2 },", withSpare)));
        // Its range no longer holds the value (trust is 2).
        Assert.Equal(DialogueCodes.ReloadIncompatible, Reload(Fixture.With("\"initial\": 4, \"min\": 3, \"max\": 5", "\"initial\": 4, \"min\": 3, \"max\": 5", Fixture.With("\"initial\": 2, \"min\": 0, \"max\": 5", "\"initial\": 4, \"min\": 3, \"max\": 5", withSpare))));
        // The allowed values of a text variable no longer include the one held.
        Assert.Equal(DialogueCodes.ReloadIncompatible, Reload(Fixture.With("\"allowed\": [\"neutral\", \"bold\"]", "\"allowed\": [\"bold\", \"calm\"]", Fixture.With("\"initial\": \"neutral\"", "\"initial\": \"bold\"", withSpare))));
        // The node the session waits at is now a node of another kind.
        var text = withSpare;
        var from = text.IndexOf("{ \"id\": \"ask\", \"kind\": \"choice\"", StringComparison.Ordinal);
        var to = text.IndexOf("{ \"id\": \"rand\"", StringComparison.Ordinal);
        var asBranch = text[..from] + "{ \"id\": \"ask\", \"kind\": \"branch\", \"cases\": [ { \"when\": { \"fact\": \"var.trust\", \"op\": \"gte\", \"value\": 0 }, \"next\": \"rand\" } ], \"otherwise\": \"bye\" },\n" + text[to..];
        asBranch = asBranch.Replace("\"brief.accept\"", "\"x\"", StringComparison.Ordinal);
        Assert.Equal(DialogueCodes.ReloadIncompatible, Reload(asBranch));
        Assert.Same(original, runtime.Set);
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
        Assert.Equal(Waiting.Choice, runtime.State.WaitingFor);
        Assert.Equal(StepKind.Ended, runtime.Choose("refuse", Fixture.Facts()).Kind);   // and it still runs
    }

    [Fact]
    public void ADocumentThatLosesTheQuestStageOrTheCursorNodeKindIsRefused()
    {
        var runtime = AtTheChoice();
        runtime.Choose("accept", Fixture.Facts());   // the hold quest is active at "order" and the session waits at a line
        var original = runtime.Set;
        var before = DialogueSave.StateDigest(runtime.State);
        // The stage the quest is at disappears (the quest starts at "done" instead); the document itself is valid.
        var noOrder = Fixture.With("\"start\": \"order\"", "\"start\": \"done\"").Replace("{ \"id\": \"order\", \"journal\": \"quest.hold.order\", \"advance\": [ { \"when\": { \"fact\": \"ext.hold-status\", \"op\": \"eq\", \"value\": \"completed\" }, \"to\": \"done\" } ] },", "", StringComparison.Ordinal);
        var other = Fixture.Load(noOrder);
        Assert.Equal(DialogueCodes.ReloadIncompatible, Fixture.Refusal(() => runtime.Reload(other)));
        // The line the session waits at becomes an effects node.
        var cursor = runtime.State.Node!;
        var line = cursor == "lucky"
            ? "{ \"id\": \"lucky\", \"kind\": \"line\", \"speaker\": \"scout\", \"portrait\": \"calm\", \"line\": \"brief.lucky\", \"next\": \"fin\" }"
            : "{ \"id\": \"plain\", \"kind\": \"line\", \"speaker\": \"cmdr\", \"portrait\": \"grim\", \"line\": \"brief.plain\", \"next\": \"fin\" }";
        var changed = Fixture.Load(Fixture.With(line, "{ \"id\": \"" + cursor + "\", \"kind\": \"effects\", \"effects\": [ { \"signal\": \"x\" } ], \"next\": \"fin\" }"));
        Assert.Equal(DialogueCodes.ReloadIncompatible, Fixture.Refusal(() => runtime.Reload(changed)));
        Assert.Same(original, runtime.Set);
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
    }

    [Fact]
    public void CountsOfNodesThatWereRemovedAreDroppedAndReportedAndTheRestAreKept()
    {
        var runtime = new DialogueRuntime(Fixture.Load(), 3);
        Fixture.Play(runtime, "accept");   // visits hello, ask, rand, lucky or plain, fin, check, bye
        var kept = runtime.State.Visits["briefing/hello"];
        // The "check" branch goes: "fin" leads straight to the end.
        var smaller = Fixture.With("\"effects\": [ { \"set\": \"briefed\", \"value\": true } ], \"next\": \"check\" }", "\"effects\": [ { \"set\": \"briefed\", \"value\": true } ], \"next\": \"bye\" }")
            .Replace("{ \"id\": \"check\", \"kind\": \"branch\", \"cases\": [ { \"when\": { \"fact\": \"var.briefed\", \"op\": \"eq\", \"value\": true }, \"next\": \"bye\" } ], \"otherwise\": \"bye\" },", "", StringComparison.Ordinal);
        var report = runtime.Reload(Fixture.Load(smaller));
        Assert.Equal(1, report.DroppedVisits);
        Assert.False(runtime.State.Visits.ContainsKey("briefing/check"));
        Assert.Equal(kept, runtime.State.Visits["briefing/hello"]);
        Fixture.Play(runtime, "refuse");   // it still plays under the smaller document
        Assert.Equal(2, runtime.State.Visits["briefing/hello"]);
    }
}
