using System.Text.Json.Nodes;
using ScenarioEngine.Contracts;
using Xunit;

namespace TutorialEngine.Tests;

/// <summary>The edges of every limit and comparison: exactly at a limit, one inside and one beyond. Written from the mutants of the first round that the other tests did not kill.</summary>
public sealed class BoundaryTests
{
    [Fact]
    public void ACooldownIsCountedFromATipThatEndedAtStepZero()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(0, Fixture.Host(flag: true));                 // shows at step 0
        runtime.Advance(0, Fixture.Host(flag: false));                // withdrawn at step 0: it ended at step 0
        Assert.Equal(0, runtime.Tips[0].EndedAt);
        runtime.Advance(1, Fixture.Host(flag: true));
        Assert.False(runtime.Tips[0].Visible);                        // the cooldown is 20 steps from step 0
        runtime.Advance(19, Fixture.Host(flag: true));
        Assert.False(runtime.Tips[0].Visible);
        runtime.Advance(20, Fixture.Host(flag: true));
        Assert.True(runtime.Tips[0].Visible);
    }

    [Fact]
    public void AHostFactAtItsLimitsIsAcceptedAndOneBeyondIsNot()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(0, Fixture.Host(level: 0));
        runtime.Advance(1, Fixture.Host(level: 100));
        Assert.Equal(TutorialCodes.FactInvalid, Fixture.Refusal(() => runtime.Evaluate(Fixture.Host(level: 101))));
        Assert.Equal(TutorialCodes.FactInvalid, Fixture.Refusal(() => runtime.Evaluate(Fixture.Host(level: -1))));
    }

    [Fact]
    public void AHostTextOfExactlyTheLongestLengthIsAcceptedAndOneLongerIsNot()
    {
        var set = Fixture.Mini(r => Tiny.Array(r, "facts").Add(new JsonObject { ["id"] = "note", ["type"] = "string" }));
        var runtime = Fixture.Ready(set);
        IReadOnlyDictionary<string, FactValue> With(int length) => new Dictionary<string, FactValue>
        {
            ["flag"] = FactValue.From(false), ["level"] = FactValue.From(0), ["note"] = FactValue.From(new string('x', length)),
        };
        runtime.Advance(0, With(4096));
        Assert.Equal(TutorialCodes.FactInvalid, Fixture.Refusal(() => runtime.Evaluate(With(4097))));
        runtime.Evaluate(With(0));
    }

    [Fact]
    public void ATextScaleAtItsLimitsIsAcceptedAndOneBeyondIsNot()
    {
        var runtime = Fixture.Ready();
        Assert.False(runtime.SetTextScale(1000).Any);                 // the normal size: accepted, nothing changed
        Assert.Equal([new SettingChanged("text_scale", 3000)], runtime.SetTextScale(3000).Settings);
        Assert.Equal(3000, runtime.TextScale);
        Assert.Equal([new SettingChanged("text_scale", 1000)], runtime.SetTextScale(1000).Settings);
        Assert.Equal(TutorialCodes.Setting, Fixture.Refusal(() => runtime.SetTextScale(999)));
        Assert.Equal(TutorialCodes.Setting, Fixture.Refusal(() => runtime.SetTextScale(3001)));
        Assert.Equal(1000, runtime.TextScale);
    }

    private static TutorialSet Alternatives() => Fixture.Mini(r =>
    {
        Tiny.Step(r, "s1")["when"] = Tiny.Cond("flag", "eq", true);
        Tiny.Step(r, "s1")["skill"] = "";
        Tiny.Step(r, "s2")["requires"] = new JsonArray();
        Tiny.Step(r, "s2")["when"] = Tiny.Cond("flag", "eq", true);
        Tiny.Step(r, "s3")["requires"] = new JsonArray();
        Tiny.Step(r, "s3")["requiresAny"] = new JsonArray("s1", "s2");
        r["limits"]!["maxActiveSteps"] = 2;
    });

    [Fact]
    public void WhenEveryAlternativeWasABranchNotTakenTheStepGoesOnInsteadOfWaitingForEver()
    {
        var runtime = Fixture.Ready(Alternatives());
        runtime.Advance(0, Fixture.Host(flag: false));
        Assert.Equal(["bypassed", "bypassed", "active"], new[] { "s1", "s2", "s3" }.Select(id => Fixture.Status(runtime, id)));
    }

    [Fact]
    public void ABypassedAlternativeAloneDoesNotLetTheStepGoOnWhileAnotherIsStillToDo()
    {
        var set = Fixture.Mini(r =>
        {
            Tiny.Step(r, "s1")["when"] = Tiny.Cond("flag", "eq", true);
            Tiny.Step(r, "s1")["skill"] = "";
            Tiny.Step(r, "s2")["requires"] = new JsonArray();
            Tiny.Step(r, "s3")["requires"] = new JsonArray();
            Tiny.Step(r, "s3")["requiresAny"] = new JsonArray("s1", "s2");
            r["limits"]!["maxActiveSteps"] = 2;
        });
        var runtime = Fixture.Ready(set);
        runtime.Advance(0, Fixture.Host(flag: false));
        Assert.Equal(["bypassed", "active", "pending"], new[] { "s1", "s2", "s3" }.Select(id => Fixture.Status(runtime, id)));
        runtime.MoveTo(1); runtime.Observe("e2", 2);
        runtime.Evaluate(Fixture.Host(flag: false));
        Assert.Equal("active", Fixture.Status(runtime, "s3"));              // the one that was left was really done
    }

    [Fact]
    public void ASkippedAlternativeCountsAsDone()
    {
        var runtime = Fixture.Ready(Fixture.Mini(r =>
        {
            Tiny.Step(r, "s2")["requires"] = new JsonArray();
            Tiny.Step(r, "s3")["requires"] = new JsonArray();
            Tiny.Step(r, "s3")["requiresAny"] = new JsonArray("s1", "s2");
            Tiny.Step(r, "s2")["skippable"] = true;
            r["limits"]!["maxActiveSteps"] = 3;
        }));
        runtime.Advance(0, Fixture.Host());
        Assert.Equal("pending", Fixture.Status(runtime, "s3"));
        runtime.SkipStep("s2");
        runtime.Evaluate(Fixture.Host());
        Assert.Equal("active", Fixture.Status(runtime, "s3"));
    }

    [Fact]
    public void ADoneAlternativeLetsTheStepGoOnWhenTheOtherWasBypassed()
    {
        var runtime = Fixture.Ready(Alternatives());
        runtime.Advance(0, Fixture.Host(flag: true));
        Assert.Equal(["active", "active", "pending"], new[] { "s1", "s2", "s3" }.Select(id => Fixture.Status(runtime, id)));
        runtime.MoveTo(1); runtime.Observe("e1");
        runtime.Evaluate(Fixture.Host(flag: true));
        Assert.Equal("done", Fixture.Status(runtime, "s3"));       // it began, and its own completion (the flag) already holds
    }

    [Fact]
    public void TheAlternativesRuleIsOneFunctionOfTheStatuses()
    {
        var all = Enum.GetValues<StepStatus>();
        Assert.True(TutorialFacts.AlternativesMet([]));
        foreach (var status in all)
            Assert.Equal(status is StepStatus.Done or StepStatus.Skipped or StepStatus.Mastered or StepStatus.Bypassed, TutorialFacts.AlternativesMet([status]));
        Assert.True(TutorialFacts.AlternativesMet([StepStatus.Bypassed, StepStatus.Bypassed]));
        Assert.False(TutorialFacts.AlternativesMet([StepStatus.Bypassed, StepStatus.Pending]));
        Assert.False(TutorialFacts.AlternativesMet([StepStatus.Bypassed, StepStatus.Active]));
        Assert.True(TutorialFacts.AlternativesMet([StepStatus.Pending, StepStatus.Done]));
        Assert.True(TutorialFacts.AlternativesMet([StepStatus.Pending, StepStatus.Skipped]));
        Assert.True(TutorialFacts.AlternativesMet([StepStatus.Active, StepStatus.Mastered]));
    }

    [Fact]
    public void SatisfiedMeansNeitherPendingNorActive()
    {
        foreach (var status in Enum.GetValues<StepStatus>())
            Assert.Equal(status is not (StepStatus.Pending or StepStatus.Active), TutorialFacts.Satisfied(status));
    }

    [Fact]
    public void ATipLastsAtLeastOneStepWhateverTheArguments()
    {
        Assert.Equal(1, TutorialPresentation.TipDuration(0, 1000));
        Assert.Equal(1, TutorialPresentation.TipDuration(1, 1000));
        Assert.Equal(10, TutorialPresentation.TipDuration(10, 1000));
    }

    [Fact]
    public void TheLimitsOfTheDocumentAreAcceptedAtTheirEdgesAndRefusedBeyond()
    {
        void Accepted(Action<JsonObject> edit) => Fixture.Mini(edit);
        void Refused(Action<JsonObject> edit) => Assert.Equal(TutorialCodes.Range, Fixture.Refusal(() => Fixture.Mini(edit)));
        Refused(r => r["limits"]!["maxVisibleTips"] = 0);
        Accepted(r => r["limits"]!["maxVisibleTips"] = 1);
        Accepted(r => r["limits"]!["maxVisibleTips"] = 64);
        Refused(r => r["limits"]!["maxVisibleTips"] = 65);
        Refused(r => r["limits"]!["maxActiveSteps"] = 0);
        Accepted(r => r["limits"]!["maxActiveSteps"] = 1);
        Accepted(r => r["limits"]!["maxActiveSteps"] = 8);
        Refused(r => r["limits"]!["maxActiveSteps"] = 9);
        Accepted(r => r["limits"]!["tipGap"] = 0);
        Refused(r => r["limits"]!["tipGap"] = -1);
        Refused(r => r["time"]!["stepsPerDay"] = 0);
        Accepted(r => r["time"]!["stepsPerDay"] = 1);
        Accepted(r => r["time"]!["stepsPerDay"] = 1_000_000);
        Refused(r => r["time"]!["stepsPerDay"] = 1_000_001);
        Refused(r => r["journey"]!["entries"] = new JsonArray());
        Accepted(r => r["journey"]!["entries"] = new JsonArray("guest"));
        Accepted(r => r["journey"]!["entries"] = new JsonArray("sign-in", "register", "host", "join", "guest"));
        Refused(r => r["journey"]!["pages"] = new JsonArray());
        Refused(r => r["steps"] = new JsonArray());
        Accepted(r => Tiny.Tip(r, "t1")["maxShows"] = 1000);
        Refused(r => Tiny.Tip(r, "t1")["maxShows"] = 1001);
        Accepted(r => Tiny.Tip(r, "t1")["priority"] = -1000);
        Refused(r => Tiny.Tip(r, "t1")["priority"] = -1001);
        Accepted(r => Tiny.Array(r, "skills")[0]!["mastered"] = 1);
        Accepted(r => Tiny.Array(r, "skills")[0]!["mastered"] = 1_000_000);
        Refused(r => Tiny.Array(r, "skills")[0]!["mastered"] = 1_000_001);
        Accepted(r => Tiny.Array(r, "skills")[0]!["evidence"]![0]!["points"] = 1000);
        Refused(r => Tiny.Array(r, "skills")[0]!["evidence"]![0]!["points"] = 1001);
    }

    [Fact]
    public void ACompletionThatReadsTheSameEventTwiceRemembersItsBaselineOnce()
    {
        var set = Fixture.Mini(r => Tiny.Step(r, "s1")["complete"] = new JsonObject
        {
            ["all"] = new JsonArray(Tiny.Cond("event.e1.since_step", "gte", 1), Tiny.Cond("event.e1.since_step", "lte", 3), Tiny.Cond("event.e2.since_step", "gte", 0)),
        });
        Assert.Equal([0, 1], set.StepById["s1"].Since);
        Assert.Equal(2, Fixture.Ready(set).StepState("s1").Baseline.Length);
    }

    [Fact]
    public void ASaveOfExactlyTheLongestLengthOpensAndOneByteMoreIsRefused()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = TutorialSave.Write(runtime);
        var padded = new byte[TutorialSave.MaxBytes];
        bytes.CopyTo(padded, 0);
        Array.Fill(padded, (byte)32, bytes.Length, padded.Length - bytes.Length);       // trailing white space is not part of the JSON
        Assert.Equal(runtime.StateDigest(), TutorialSave.Open(set, padded).StateDigest());
        var longer = new byte[TutorialSave.MaxBytes + 1];
        padded.CopyTo(longer, 0);
        longer[^1] = 32;
        Assert.Equal(TutorialCodes.SaveFormat, Fixture.Refusal(() => TutorialSave.Open(set, longer)));
    }

    [Fact]
    public void EveryFieldTheSaveHoldsIsInItsDigest()
    {
        var bytes = TutorialSave.Write(Tamper.Played(out _));
        var baseline = TutorialImage.Canonical(Tamper.Image(bytes));
        var edits = new List<(string Name, Action<TutorialImage> Edit)>
        {
            ("step", i => i.Step++), ("activity", i => i.LastActivity--), ("tip shown", i => i.LastTipShown--), ("phase", i => i.Phase++), ("ignore mastery", i => i.IgnoreMastery = !i.IgnoreMastery),
            ("reduced motion", i => i.ReducedMotion = !i.ReducedMotion), ("text scale", i => i.TextScale++), ("tips", i => i.TipsEnabled = !i.TipsEnabled),
            ("journey state", i => i.Journey.State++), ("language", i => i.Journey.Language += "x"), ("page", i => i.Journey.Page++), ("story skipped", i => i.Journey.StorySkipped = !i.Journey.StorySkipped),
            ("replaying", i => i.Journey.Replaying = !i.Journey.Replaying), ("entry", i => i.Journey.Entry += "x"), ("intent", i => i.Journey.Intent += "x"),
            ("event count", i => i.Events[0].Count++), ("event time", i => i.Events[0].LastAt++), ("skill", i => i.Skills[0]++),
            ("step status", i => i.Steps[0].Status++), ("step began", i => i.Steps[0].ActivatedAt++), ("step ended", i => i.Steps[0].DoneAt++), ("step hint", i => i.Steps[0].Hint++), ("step baseline", i => i.Steps[0].Baseline[0]++),
            ("tip shows", i => i.Tips[0].Shows++), ("tip acknowledged", i => i.Tips[0].Acknowledged = !i.Tips[0].Acknowledged), ("tip visible", i => i.Tips[0].Visible = !i.Tips[0].Visible),
            ("tip began", i => i.Tips[0].ShownAt++), ("tip until", i => i.Tips[0].Until++), ("tip ended", i => i.Tips[0].EndedAt++),
            ("document", i => i.Document += "x"), ("id", i => i.Id += "x"),
        };
        foreach (var (name, edit) in edits)
        {
            var image = Tamper.Image(bytes);
            edit(image);
            Assert.True(baseline != TutorialImage.Canonical(image), name + " is not in the digest");
        }
    }

    [Fact]
    public void ExplainingAStepAtItsLastHintDoesNotNameANextOne()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(0, Fixture.Host());
        runtime.Advance(100, Fixture.Host());
        var text = TutorialExplain.Step(runtime, "s1", Fixture.Host());
        Assert.Contains("hint level 3 of 3", text);
        Assert.DoesNotContain("the next at", text);
        runtime.Replay();
        runtime.Advance(101, Fixture.Host());
        runtime.Advance(112, Fixture.Host());
        Assert.Contains("hint level 1 of 3, the next at 30", TutorialExplain.Step(runtime, "s1", Fixture.Host()));
    }

    [Fact]
    public void ExplainingATipAtExactlyTheEndOfItsCooldownSaysItIsOpen()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(0, Fixture.Host(flag: true));
        runtime.Advance(10, Fixture.Host(flag: false));                 // ended at step 10, cooldown 20
        Assert.Contains("blocked it ended at step 10, cooldown 20", TutorialExplain.Tip(runtime, "t1", Fixture.Host(flag: true)));
        runtime.MoveTo(29);
        Assert.Contains("blocked it ended", TutorialExplain.Tip(runtime, "t1", Fixture.Host(flag: true)));
        runtime.MoveTo(30);
        Assert.Contains("ok      it ended at step 10, cooldown 20", TutorialExplain.Tip(runtime, "t1", Fixture.Host(flag: true)));
    }

    [Fact]
    public void AReplayLeavesNoCountsInAStepThatHasNotBegun()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(0, Fixture.Host());
        runtime.MoveTo(1); runtime.Observe("e1"); runtime.Evaluate(Fixture.Host());
        Assert.NotEmpty(runtime.StepState("s1").Baseline);
        runtime.Replay();
        foreach (var step in new[] { "s1", "s2", "s3" })
            Assert.All(runtime.StepState(step).Baseline, b => Assert.Equal(0, b));
        Assert.Equal(runtime.StateDigest(), TutorialSave.Open(runtime.Set, TutorialSave.Write(runtime)).StateDigest());
    }
}
