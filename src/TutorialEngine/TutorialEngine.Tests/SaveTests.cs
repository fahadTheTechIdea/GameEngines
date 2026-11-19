using System.Text;
using EngineCommon;
using Xunit;

namespace TutorialEngine.Tests;

/// <summary>The save: a round trip, a digest that sees every change, ranges checked before the digest, the document checked after it, and a long list of forgeries with recomputed digests.</summary>
public sealed class SaveTests
{
    [Fact]
    public void ASaveOpensToTheSameStateAndTheTwoGoOnIdentically()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = TutorialSave.Write(runtime);
        var opened = TutorialSave.Open(set, bytes);
        Assert.Equal(runtime.StateDigest(), opened.StateDigest());
        Assert.Equal(bytes, TutorialSave.Write(opened));
        foreach (var r in new[] { runtime, opened })
        {
            r.Advance(45, Fixture.Host(flag: true));
            r.Observe("e2", 2);
            r.DismissTip("t1");
            r.Advance(60, Fixture.Host(flag: true));
        }
        Assert.Equal(runtime.StateDigest(), opened.StateDigest());
        Assert.Equal(TutorialSave.Write(runtime), TutorialSave.Write(opened));
    }

    [Fact]
    public void EverythingTheSaveHoldsComesBack()
    {
        var runtime = Tamper.Played(out var set);
        var opened = TutorialSave.Open(set, TutorialSave.Write(runtime));
        Assert.Equal((runtime.Step, runtime.LastActivity, runtime.LastTipShown, runtime.Phase, runtime.IgnoreMastery, runtime.ReducedMotion, runtime.TextScale, runtime.TipsEnabled),
            (opened.Step, opened.LastActivity, opened.LastTipShown, opened.Phase, opened.IgnoreMastery, opened.ReducedMotion, opened.TextScale, opened.TipsEnabled));
        Assert.Equal(runtime.EventCounts, opened.EventCounts);
        Assert.Equal(runtime.EventLastAt, opened.EventLastAt);
        Assert.Equal(runtime.SkillPoints, opened.SkillPoints);
        Assert.Equal("active", Fixture.Status(opened, "s2"));
        Assert.Equal(1, opened.StateOf("s2").Hint);
        Assert.True(opened.Tips[0].Visible);
        Assert.Equal(runtime.Journey.Entry, opened.Journey.Entry);
        Assert.Equal(runtime.Journey.Language, opened.Journey.Language);
        Assert.Equal(runtime.StateOf("s1").Baseline, opened.StateOf("s1").Baseline);
    }

    [Fact]
    public void TheDigestChangesWithEveryKindOfChange()
    {
        var runtime = Fixture.Ready();
        var seen = new HashSet<string> { runtime.StateDigest() };
        void Changed(string what, Action action)
        {
            action();
            Assert.True(seen.Add(runtime.StateDigest()), what + " did not change the digest");
        }
        Changed("time", () => runtime.MoveTo(3));
        Changed("evaluating", () => runtime.Evaluate(Fixture.Host()));
        Changed("an observation", () => runtime.Observe("e3"));
        Changed("activity", () => { runtime.MoveTo(9); runtime.Touch(); });
        Changed("a hint", () => { runtime.MoveTo(25); runtime.Evaluate(Fixture.Host()); });
        Changed("a tip", () => runtime.Evaluate(Fixture.Host(flag: true)));
        Changed("reduced motion", () => runtime.SetReducedMotion(true));
        Changed("text scale", () => runtime.SetTextScale(1200));
        Changed("tips", () => runtime.SetTipsEnabled(false));
        Changed("a language", () => runtime.SelectLanguage("fr"));
        Changed("a page", () => { runtime.ReplayStory(); runtime.StoryNext(); });
        Changed("an intent", () => runtime.SetIntent("x"));
        Changed("a skip", () => { runtime.StorySkip(); runtime.SkipStep("s1"); });
        Changed("a replay", () => runtime.Replay());
    }

    [Fact]
    public void ARestoredRuntimeRefusesAWrongDocument()
    {
        var runtime = Tamper.Played(out _);
        var bytes = TutorialSave.Write(runtime);
        Assert.Equal(TutorialCodes.SaveDocument, Fixture.Refusal(() => TutorialSave.Open(Fixture.Valley, bytes)));
        var other = Fixture.Mini(r => r["id"] = "other");
        Assert.Equal(TutorialCodes.SaveDocument, Fixture.Refusal(() => TutorialSave.Open(other, bytes)));
        var changed = Fixture.Mini(r => Tiny.Tip(r, "t1")["cooldown"] = 21);
        Assert.Equal(TutorialCodes.SaveDocument, Fixture.Refusal(() => TutorialSave.Open(changed, bytes)));
    }

    [Fact]
    public void BytesThatAreNotASaveAreRefusedWithTheFormatCode()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = TutorialSave.Write(runtime);
        Assert.Equal(TutorialCodes.SaveFormat, Fixture.Refusal(() => TutorialSave.Open(set, Encoding.UTF8.GetBytes("not json"))));
        Assert.Equal(TutorialCodes.SaveFormat, Fixture.Refusal(() => TutorialSave.Open(set, Encoding.UTF8.GetBytes("null"))));
        Assert.Equal(TutorialCodes.SaveFormat, Fixture.Refusal(() => TutorialSave.Open(set, Array.Empty<byte>())));
        Assert.Equal(TutorialCodes.SaveFormat, Fixture.Refusal(() => TutorialSave.Open(set, new byte[TutorialSave.MaxBytes + 1])));
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Equal(TutorialCodes.SaveFormat, Fixture.Refusal(() => TutorialSave.Open(set, Encoding.UTF8.GetBytes(text.Replace("\"Digest\"", "\"Digests\"", StringComparison.Ordinal)))));     // a field missing
        Assert.Equal(TutorialCodes.SaveFormat, Fixture.Refusal(() => TutorialSave.Open(set, Encoding.UTF8.GetBytes(text.Replace("\"Format\":1,", "\"Format\":1,\"Extra\":1,", StringComparison.Ordinal)))));    // a field unknown
        Assert.Equal(TutorialCodes.SaveFormat, Fixture.Refusal(() => TutorialSave.Open(set, Tamper.Stale(bytes, i => i.Format = 2))));
        Assert.Equal(TutorialCodes.SaveFormat, Fixture.Refusal(() => TutorialSave.Open(set, Encoding.UTF8.GetBytes(text.Replace("\"Step\":40", "\"Step\":\"x\"", StringComparison.Ordinal)))));
    }

    [Fact]
    public void ASaveWhoseStateWasEditedKeepingTheOldDigestIsRefusedByTheDigest()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = TutorialSave.Write(runtime);
        Assert.Equal(TutorialCodes.SaveDigest, Fixture.Refusal(() => TutorialSave.Open(set, Tamper.Stale(bytes, i => i.Events[0].Count = 5))));
        Assert.Equal(TutorialCodes.SaveDigest, Fixture.Refusal(() => TutorialSave.Open(set, Tamper.Stale(bytes, i => i.Journey.Intent = "elsewhere"))));
        Assert.Equal(TutorialCodes.SaveDigest, Fixture.Refusal(() => TutorialSave.Open(set, Tamper.Stale(bytes, i => i.Digest = new string('0', 64)))));
        Assert.Equal(TutorialCodes.SaveDigest, Fixture.Refusal(() => TutorialSave.Open(set, Tamper.Stale(bytes, i => i.Tips[0].Shows = 2))));
    }

    [Fact]
    public void RangesAreCheckedBeforeTheDigestSoAForgedValueIsAStateEvenWithAStaleDigest()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = TutorialSave.Write(runtime);
        Assert.Equal(TutorialCodes.SaveState, Fixture.Refusal(() => TutorialSave.Open(set, Tamper.Stale(bytes, i => i.Step = -1))));
        Assert.Equal(TutorialCodes.SaveState, Fixture.Refusal(() => TutorialSave.Open(set, Tamper.Stale(bytes, i => i.TextScale = 5000))));
    }

    internal static List<(string Name, Action<TutorialImage> Edit)> Forgeries()
    {
        void Case(List<(string, Action<TutorialImage>)> list, string name, Action<TutorialImage> edit) => list.Add((name, edit));
        var cases = new List<(string Name, Action<TutorialImage> Edit)>();
        Case(cases, "negative step", i => i.Step = -1);
        Case(cases, "activity in the future", i => i.LastActivity = i.Step + 1);
        Case(cases, "negative activity", i => i.LastActivity = -1);
        Case(cases, "latest tip in the future", i => i.LastTipShown = i.Step + 1);
        Case(cases, "latest tip before -1", i => i.LastTipShown = -2);
        Case(cases, "unknown phase", i => i.Phase = 9);
        Case(cases, "negative phase", i => i.Phase = -1);
        Case(cases, "text scale below normal", i => i.TextScale = 999);
        Case(cases, "text scale above the limit", i => i.TextScale = 3001);
        Case(cases, "journey state unknown", i => i.Journey.State = 7);
        Case(cases, "language not offered", i => i.Journey.Language = "de");
        Case(cases, "past the language with none chosen", i => i.Journey.Language = "");
        Case(cases, "page below 0", i => i.Journey.Page = -1);
        Case(cases, "page beyond the story", i => i.Journey.Page = 3);
        Case(cases, "entry not offered", i => i.Journey.Entry = "register");
        Case(cases, "replaying while done", i => i.Journey.Replaying = true);
        Case(cases, "done without an entry", i => i.Journey.Entry = "");
        Case(cases, "skipped story at the story", i => { i.Journey.State = (int)JourneyState.Story; i.Journey.StorySkipped = true; });
        Case(cases, "entry chosen at the story of a first run", i => i.Journey.State = (int)JourneyState.Story);
        Case(cases, "language state with a page", i => { i.Journey.State = (int)JourneyState.Language; i.Journey.Entry = ""; i.Journey.Page = 1; });
        Case(cases, "intent of other characters", i => i.Journey.Intent = "A B");
        Case(cases, "intent too long", i => i.Journey.Intent = new string('a', 129));
        Case(cases, "too few events", i => i.Events.RemoveAt(0));
        Case(cases, "too many events", i => i.Events.Add(new ImageEvent { Count = 0, LastAt = -1 }));
        Case(cases, "negative event count", i => i.Events[2].Count = -1);
        Case(cases, "event count beyond the limit", i => i.Events[0].Count = 1_000_000_001);
        Case(cases, "event in the future", i => { i.Events[2].Count = 1; i.Events[2].LastAt = i.Step + 1; });
        Case(cases, "event counted but never timed", i => i.Events[2].Count = 1);
        Case(cases, "event timed but never counted", i => i.Events[2].LastAt = 3);
        Case(cases, "too few skills", i => i.Skills.Clear());
        Case(cases, "skill beyond its mastery", i => i.Skills[0] = 4);
        Case(cases, "negative skill points", i => i.Skills[0] = -1);
        Case(cases, "skill points the events cannot have given", i => i.Skills[0] = 2);
        Case(cases, "too few steps", i => i.Steps.RemoveAt(2));
        Case(cases, "unknown step status", i => i.Steps[2].Status = 12);
        Case(cases, "step began in the future", i => i.Steps[1].ActivatedAt = i.Step + 1);
        Case(cases, "step ended in the future", i => i.Steps[0].DoneAt = i.Step + 1);
        Case(cases, "step before time", i => i.Steps[1].ActivatedAt = -2);
        Case(cases, "hint the step does not have", i => i.Steps[1].Hint = 2);
        Case(cases, "negative hint", i => i.Steps[1].Hint = -1);
        Case(cases, "baseline of the wrong size", i => i.Steps[0].Baseline.Add(0));
        Case(cases, "negative baseline", i => i.Steps[0].Baseline[0] = -1);
        Case(cases, "pending step that has begun", i => i.Steps[2].ActivatedAt = 3);
        Case(cases, "pending step with a hint", i => i.Steps[2].Hint = 1);
        Case(cases, "pending step that remembers counts", i => { var s = i.Steps[1]; s.Status = 0; s.ActivatedAt = -1; s.DoneAt = -1; s.Hint = 0; s.Baseline[0] = 1; });
        Case(cases, "taught step that never began", i => i.Steps[1].ActivatedAt = -1);
        Case(cases, "taught step that has ended", i => i.Steps[1].DoneAt = 6);
        Case(cases, "hint on a done step", i => i.Steps[0].Hint = 1);
        Case(cases, "done step with no end", i => i.Steps[0].DoneAt = -1);
        Case(cases, "done step that never began", i => i.Steps[0].ActivatedAt = -1);
        Case(cases, "step that ended before it began", i => { i.Steps[0].ActivatedAt = 4; i.Steps[0].DoneAt = 3; });
        Case(cases, "mastered step with no skill", i => { var s = i.Steps[1]; s.Status = (int)StepStatus.Mastered; s.Hint = 0; s.DoneAt = 6; });
        Case(cases, "too few tips", i => i.Tips.Clear());
        Case(cases, "tip shown more often than allowed", i => i.Tips[0].Shows = 3);
        Case(cases, "negative shows", i => i.Tips[0].Shows = -1);
        Case(cases, "tip ended in the future", i => i.Tips[0].EndedAt = i.Step + 1);
        Case(cases, "showing tip that began in the future", i => i.Tips[0].ShownAt = i.Step + 1);
        Case(cases, "showing tip that never showed", i => i.Tips[0].Shows = 0);
        Case(cases, "showing tip that lasts less than its duration", i => i.Tips[0].Until = i.Tips[0].ShownAt + 9);
        Case(cases, "showing tip that lasts more than text can enlarge it", i => i.Tips[0].Until = i.Tips[0].ShownAt + 32);
        Case(cases, "showing tip the player acknowledged", i => i.Tips[0].Acknowledged = true);
        Case(cases, "tip not showing with times", i => { i.Tips[0].Visible = false; });
        Case(cases, "two steps taught at once", i => { i.Steps[2].Status = (int)StepStatus.Active; i.Steps[2].ActivatedAt = 6; });
        Case(cases, "idle tutorial with a moved step", i => i.Phase = (int)TutorialPhase.Idle);
        Case(cases, "skipped tutorial with a step waiting", i => i.Phase = (int)TutorialPhase.Skipped);
        Case(cases, "finished tutorial with a step waiting", i => i.Phase = (int)TutorialPhase.Finished);
        Case(cases, "step taught in a tutorial that is not running", i => { i.Phase = (int)TutorialPhase.Finished; });
        Case(cases, "mastered step in a full replay", i => { i.Steps[0].Status = (int)StepStatus.Mastered; i.IgnoreMastery = true; });
        Case(cases, "replay flag in a tutorial never started", i => { i.Phase = (int)TutorialPhase.Idle; i.IgnoreMastery = true; i.Steps.ForEach(s => { s.Status = 0; s.ActivatedAt = -1; s.DoneAt = -1; s.Hint = 0; }); });
        Case(cases, "step begun without its prerequisite", i => { i.Steps[0].Status = (int)StepStatus.Pending; i.Steps[0].ActivatedAt = -1; i.Steps[0].DoneAt = -1; });
        Case(cases, "tips showing while tips are off", i => i.TipsEnabled = false);
        Case(cases, "tips showing before the journey is done", i => { i.Journey.State = (int)JourneyState.Entry; i.Journey.Entry = ""; });
        return cases;
    }

    [Fact]
    public void EveryForgedStateIsRefusedAsAStateEvenWhenItsDigestWasRecomputed()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = TutorialSave.Write(runtime);
        var forgeries = Forgeries();
        Assert.True(forgeries.Count >= 60, "only " + forgeries.Count + " forgeries");
        var wrong = new List<string>();
        foreach (var (name, edit) in forgeries)
        {
            var code = Fixture.Refusal(() => TutorialSave.Open(set, Tamper.Forge(bytes, edit)), name);
            if (code != TutorialCodes.SaveState) wrong.Add(name + " -> " + code);
        }
        Assert.True(wrong.Count == 0, string.Join("; ", wrong));
    }

    [Fact]
    public void TheUnforgedStateOpensSoTheForgeriesAreTheOnlyDifference()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = TutorialSave.Write(runtime);
        Assert.Equal(runtime.StateDigest(), TutorialSave.Open(set, Tamper.Forge(bytes, _ => { })).StateDigest());
    }

    [Fact]
    public void ASaveOfEveryPhaseAndPlaceOpens()
    {
        var set = Fixture.Mini();
        var runtime = Fixture.Ready(set);
        var steps = new List<Action>
        {
            () => runtime.Advance(0, Fixture.Host()),
            () => runtime.SkipStep("s1"),
            () => runtime.Advance(1, Fixture.Host()),
            () => runtime.SkipTutorial(),
            () => runtime.Replay(),
            () => runtime.Advance(2, Fixture.Host(flag: true)),
            () => runtime.ReplayStory(),
            () => runtime.StoryNext(),
            () => runtime.StorySkip(),
        };
        foreach (var step in steps)
        {
            step();
            Assert.Equal(runtime.StateDigest(), TutorialSave.Open(set, TutorialSave.Write(runtime)).StateDigest());
        }
    }

    [Fact]
    public void ASaveTakenRightAfterATipOrAHintOrASkipOpens()
    {
        var set = Fixture.Mini();
        var runtime = Fixture.Ready(set);
        runtime.Advance(0, Fixture.Host(flag: true));
        runtime.SetTipsEnabled(false);
        Assert.Equal(runtime.StateDigest(), TutorialSave.Open(set, TutorialSave.Write(runtime)).StateDigest());
        runtime.SetTipsEnabled(true);
        runtime.Advance(100, Fixture.Host(flag: true));
        runtime.SkipStep("s1");
        Assert.Equal(runtime.StateDigest(), TutorialSave.Open(set, TutorialSave.Write(runtime)).StateDigest());
    }
}
