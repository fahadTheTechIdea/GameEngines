using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>D3, D5 and D8: the runtime plays a conversation deterministically, refuses without side effects, and runs quests.</summary>
public sealed class RuntimeTests
{
    private static DialogueRuntime Fresh(ulong seed = 1, string? document = null) => new(Fixture.Load(document), seed);

    private static string Digest(DialogueRuntime runtime) => DialogueSave.StateDigest(runtime.State);

    [Fact]
    public void AConversationPlaysLineByLineToItsEnd()
    {
        var seen = Fixture.Play(Fresh(), "accept");
        Assert.Equal("line hello cmdr/calm brief.hello count=7", seen[0]);
        Assert.Equal("choices accept,refuse,secret!", seen[1]);
        Assert.Equal("event QuestEvent { Quest = hold, Status = Active, Stage = order }", seen[2]);
        Assert.Contains("event SignalEvent { Name = order-accepted }", seen);
        Assert.Contains(seen, s => s.StartsWith("line lucky scout/calm", StringComparison.Ordinal) || s.StartsWith("line plain cmdr/grim", StringComparison.Ordinal));
        Assert.Equal("ended done", seen[^1]);
    }

    [Fact]
    public void TheEffectsOfAnAnswerAreAppliedAndReportedAndASaturatedAddIsNotAChange()
    {
        var runtime = Fresh();
        var seen = Fixture.Play(runtime, "accept");
        Assert.Equal(FactValue.From(3L), runtime.State.Variables["trust"]);
        Assert.Equal(FactValue.From(true), runtime.State.Variables["briefed"]);
        Assert.Contains(seen, s => s.StartsWith("event VariableEvent { Id = trust", StringComparison.Ordinal));
        Assert.Equal(new QuestProgress(QuestStatus.Active, "order"), runtime.State.Quests["hold"]);
        // At the top of its range an add changes nothing and says nothing.
        var full = Fresh(1, Fixture.With("\"initial\": 2, \"min\": 0, \"max\": 5", "\"initial\": 5, \"min\": 0, \"max\": 5"));
        var seenFull = Fixture.Play(full, "accept");
        Assert.Equal(FactValue.From(5L), full.State.Variables["trust"]);
        Assert.DoesNotContain(seenFull, s => s.StartsWith("event VariableEvent { Id = trust", StringComparison.Ordinal));
    }

    [Fact]
    public void ASeedGivesTheSameTranscriptEveryTimeAndSeedsDiffer()
    {
        var outcomes = new HashSet<string>();
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var one = Fixture.Play(Fresh(seed));
            Assert.Equal(one, Fixture.Play(Fresh(seed)));
            outcomes.Add(one.First(s => s.StartsWith("line lucky", StringComparison.Ordinal) || s.StartsWith("line plain", StringComparison.Ordinal))[..10]);
        }
        Assert.Equal(2, outcomes.Count);   // both outcomes of the random node occur across seeds
    }

    [Fact]
    public void TheRollsOfASessionDoNotDependOnTheOrderOtherThingsWereAsked()
    {
        // A session that asked a quest update before the conversation draws the same roll: the roll index is a count of rolls, not of calls.
        var a = Fresh(7);
        var b = Fresh(7);
        b.UpdateQuests(Fixture.Facts());
        b.UpdateQuests(Fixture.Facts());
        Assert.Equal(Fixture.Play(a), Fixture.Play(b));
    }

    [Fact]
    public void ASeededChoiceIsMadeByTheConversationByWeightFromTheRoll()
    {
        var seededDocument = Fixture.With("\"pick\": \"player\", \"options\": [", "\"pick\": \"seeded\", \"options\": [")
            .Replace("\"line\": \"brief.accept\",", "\"line\": \"brief.accept\", \"weight\": 1,", StringComparison.Ordinal)
            .Replace("\"line\": \"brief.refuse\",", "\"line\": \"brief.refuse\", \"weight\": 1,", StringComparison.Ordinal)
            .Replace("\"line\": \"brief.secret\",", "\"line\": \"brief.secret\", \"weight\": 1,", StringComparison.Ordinal)
            .Replace(", \"unavailable\": \"show\"", "", StringComparison.Ordinal);
        var picked = new HashSet<string>();
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var seen = Fixture.Play(Fresh(seed, seededDocument), "unused");
            Assert.DoesNotContain(seen, s => s.StartsWith("choices", StringComparison.Ordinal));   // nobody was asked
            Assert.DoesNotContain(seen, s => s.Contains("secret", StringComparison.Ordinal) && s.StartsWith("event", StringComparison.Ordinal));
            picked.Add(seen.Any(s => s.Contains("order-accepted", StringComparison.Ordinal)) ? "accept" : "refuse");
        }
        Assert.Equal(2, picked.Count);
    }

    [Fact]
    public void ASeededChoiceNeverPicksAnAnswerWhoseConditionIsFalse()
    {
        var seededDocument = Fixture.With("\"pick\": \"player\", \"options\": [", "\"pick\": \"seeded\", \"options\": [")
            .Replace("\"line\": \"brief.accept\",", "\"line\": \"brief.accept\", \"weight\": 1,", StringComparison.Ordinal)
            .Replace("\"line\": \"brief.refuse\",", "\"line\": \"brief.refuse\", \"weight\": 1,", StringComparison.Ordinal)
            .Replace("\"line\": \"brief.secret\",", "\"line\": \"brief.secret\", \"weight\": 1000,", StringComparison.Ordinal)
            .Replace(", \"unavailable\": \"show\"", "", StringComparison.Ordinal);
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var runtime = Fresh(seed, seededDocument);
            Fixture.Play(runtime);
            // Accepting visits the random node, refusing sets the stance; the secret answer does neither, so it was not picked.
            Assert.True(runtime.State.Visits.ContainsKey("briefing/rand") || runtime.State.Variables["stance"].Text == "bold", "seed " + seed + " took the answer whose condition is false");
        }
    }

    // ---- refusals leave nothing behind ---------------------------------------------------------------------------------------------

    [Fact]
    public void AMissingOrBadExternalFactIsARefusalAndChangesNothing()
    {
        var runtime = Fresh();
        var before = Digest(runtime);
        Assert.Equal(DialogueCodes.FactMissing, Fixture.Refusal(() => runtime.Start("briefing", new Dictionary<string, FactValue>())));
        Assert.Equal(DialogueCodes.FactInvalid, Fixture.Refusal(() => runtime.Start("briefing", new Dictionary<string, FactValue> { ["ext.squads"] = FactValue.From("many") })));
        Assert.Equal(DialogueCodes.FactInvalid, Fixture.Refusal(() => runtime.Start("briefing", new Dictionary<string, FactValue> { ["ext.squads"] = FactValue.From(5000L) })));
        Assert.Equal(before, Digest(runtime));
        Assert.False(runtime.InConversation);
    }

    [Fact]
    public void ARefusalPartWayThroughAnAnswerPutsBackWhatTheEarlierEffectsDid()
    {
        var doubled = Fixture.With("{ \"startQuest\": \"hold\" }, { \"add\": \"trust\", \"by\": 1 }", "{ \"add\": \"trust\", \"by\": 1 }, { \"startQuest\": \"hold\" }, { \"startQuest\": \"hold\" }");
        var runtime = Fresh(1, doubled);
        var step = runtime.Start("briefing", Fixture.Facts());
        step = runtime.Next(Fixture.Facts());
        Assert.Equal(StepKind.Choices, step.Kind);
        var before = Digest(runtime);
        Assert.Equal(DialogueCodes.QuestState, Fixture.Refusal(() => runtime.Choose("accept", Fixture.Facts())));
        Assert.Equal(before, Digest(runtime));
        Assert.Equal(FactValue.From(2L), runtime.State.Variables["trust"]);
        Assert.Equal(QuestStatus.NotStarted, runtime.State.Quests["hold"].Status);
        Assert.Equal(Waiting.Choice, runtime.State.WaitingFor);
        // The conversation is still where it was and the other answer works.
        Assert.Equal(StepKind.Ended, runtime.Choose("refuse", Fixture.Facts()).Kind);
    }

    [Fact]
    public void CallsInTheWrongOrderAreRefused()
    {
        var runtime = Fresh();
        Assert.Equal(DialogueCodes.NotWaiting, Fixture.Refusal(() => runtime.Next(Fixture.Facts())));
        Assert.Equal(DialogueCodes.NotWaiting, Fixture.Refusal(() => runtime.Choose("accept", Fixture.Facts())));
        Assert.Equal(DialogueCodes.NoConversation, Fixture.Refusal(() => runtime.Start("nothing", Fixture.Facts())));
        runtime.Start("briefing", Fixture.Facts());
        Assert.Equal(DialogueCodes.Busy, Fixture.Refusal(() => runtime.Start("briefing", Fixture.Facts())));
        Assert.Equal(DialogueCodes.NotWaiting, Fixture.Refusal(() => runtime.Choose("accept", Fixture.Facts())));   // waiting for a line
        runtime.Next(Fixture.Facts());
        Assert.Equal(DialogueCodes.NotWaiting, Fixture.Refusal(() => runtime.Next(Fixture.Facts())));            // waiting for a choice
        Assert.Equal(DialogueCodes.UnknownOption, Fixture.Refusal(() => runtime.Choose("nothing", Fixture.Facts())));
        Assert.Equal(DialogueCodes.OptionUnavailable, Fixture.Refusal(() => runtime.Choose("secret", Fixture.Facts())));
    }

    [Fact]
    public void AnAnswerWhoseConditionHoldsCanBeChosenAndOneThatDoesNotIsShownDisabled()
    {
        var high = Fresh(1, Fixture.With("\"initial\": 2, \"min\": 0, \"max\": 5", "\"initial\": 4, \"min\": 0, \"max\": 5"));
        high.Start("briefing", Fixture.Facts());
        var choices = high.Next(Fixture.Facts()).Choices!;
        Assert.Equal(["accept", "refuse", "secret"], choices.Options.Select(o => o.Id + (o.Enabled ? "" : "!")));
        Assert.Equal(StepKind.Ended, high.Choose("secret", Fixture.Facts()).Kind);
        // Hidden when asked to hide.
        var hidden = Fresh(1, Fixture.With(", \"unavailable\": \"show\"", ", \"unavailable\": \"hide\""));
        hidden.Start("briefing", Fixture.Facts());
        Assert.Equal(["accept", "refuse"], hidden.Next(Fixture.Facts()).Choices!.Options.Select(o => o.Id));
    }

    [Fact]
    public void AChoiceWithNoAvailableAnswerTakesItsFallbackOrIsRefused()
    {
        var onlyHidden = Fixture.With("{ \"id\": \"accept\", \"line\": \"brief.accept\", \"effects\"", "{ \"id\": \"accept\", \"line\": \"brief.accept\", \"when\": { \"fact\": \"var.trust\", \"op\": \"gte\", \"value\": 5 }, \"effects\"")
            .Replace("{ \"id\": \"refuse\", \"line\": \"brief.refuse\", \"effects\"", "{ \"id\": \"refuse\", \"line\": \"brief.refuse\", \"when\": { \"fact\": \"var.trust\", \"op\": \"gte\", \"value\": 5 }, \"effects\"", StringComparison.Ordinal)
            .Replace(", \"unavailable\": \"show\"", ", \"unavailable\": \"hide\"", StringComparison.Ordinal);
        var stuck = Fresh(1, onlyHidden);
        stuck.Start("briefing", Fixture.Facts());
        var before = Digest(stuck);
        Assert.Equal(DialogueCodes.NoOption, Fixture.Refusal(() => stuck.Next(Fixture.Facts())));
        Assert.Equal(before, Digest(stuck));
        var withFallback = Fixture.With("\"pick\": \"player\", \"options\"", "\"pick\": \"player\", \"fallback\": \"bye\", \"options\"", onlyHidden);
        var free = Fresh(1, withFallback);
        free.Start("briefing", Fixture.Facts());
        Assert.Equal("done", free.Next(Fixture.Facts()).Result);
    }

    [Fact]
    public void NodeVisitsAreCountedAndCanBeTestedLikeAnyFact()
    {
        var runtime = Fresh();
        Fixture.Play(runtime, "refuse");
        Fixture.Play(runtime, "refuse");
        Assert.Equal(2, runtime.State.Visits["briefing/hello"]);
        Assert.Equal(2, runtime.State.Visits["briefing/bye"]);
        var counted = Fixture.With("\"fact\": \"var.briefed\", \"op\": \"eq\", \"value\": true", "\"fact\": \"visits.briefing/hello\", \"op\": \"gte\", \"value\": 2");
        Assert.NotNull(Fresh(1, counted));
    }

    [Fact]
    public void VariablesThatChangeAreReportedAsEvents()
    {
        var runtime = Fresh();
        var seen = Fixture.Play(runtime, "refuse");
        Assert.Contains(seen, s => s.Contains("VariableEvent { Id = stance", StringComparison.Ordinal));
        Assert.Equal(FactValue.From("bold"), runtime.State.Variables["stance"]);
    }

    // ---- D8: quests ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AQuestMovesOnlyByItsRulesOrAnEffectAndEndsWhenAStageEndsIt()
    {
        var runtime = Fresh();
        Fixture.Play(runtime, "accept");
        Assert.Empty(runtime.UpdateQuests(Fixture.Facts(hold: "active")));
        Assert.Equal(new QuestProgress(QuestStatus.Active, "order"), runtime.State.Quests["hold"]);
        var events = runtime.UpdateQuests(Fixture.Facts(hold: "completed"));
        Assert.Equal([new QuestEvent("hold", QuestStatus.Completed, "done")], events);
        Assert.Empty(runtime.UpdateQuests(Fixture.Facts(hold: "completed")));   // finished quests are left alone
    }

    [Fact]
    public void AQuestThatHasNotStartedDoesNotMoveEvenIfItsRuleWouldHold()
    {
        var runtime = Fresh();
        Assert.Empty(runtime.UpdateQuests(Fixture.Facts(hold: "completed")));
        Assert.Equal(QuestStatus.NotStarted, runtime.State.Quests["hold"].Status);
        var started = runtime.StartQuest("hold", Fixture.Facts());
        Assert.Equal([new QuestEvent("hold", QuestStatus.Active, "order")], started);
        Assert.Equal(DialogueCodes.QuestState, Fixture.Refusal(() => runtime.StartQuest("hold", Fixture.Facts())));
        Assert.Equal(DialogueCodes.Reference, Fixture.Refusal(() => runtime.StartQuest("nothing", Fixture.Facts())));
    }

    [Fact]
    public void AnUpdateWhoseRulesNeverSettleIsRefusedAndPutsTheQuestBack()
    {
        var cycle = Fixture.With("{ \"id\": \"done\", \"journal\": \"quest.hold.done\", \"advance\": [], \"ends\": \"completed\" }", "{ \"id\": \"done\", \"journal\": \"quest.hold.done\", \"advance\": [ { \"when\": { \"fact\": \"ext.hold-status\", \"op\": \"eq\", \"value\": \"completed\" }, \"to\": \"order\" } ] }");
        var runtime = Fresh(1, cycle);
        runtime.StartQuest("hold", Fixture.Facts());
        var before = Digest(runtime);
        Assert.Equal(DialogueCodes.QuestLoop, Fixture.Refusal(() => runtime.UpdateQuests(Fixture.Facts(hold: "completed"))));
        Assert.Equal(before, Digest(runtime));
    }

    [Fact]
    public void ADialogueEffectCanSetTheStageOfAnActiveQuestAndFinishItAsFailedOrAbandoned()
    {
        var staged = Fixture.With("{ \"signal\": \"order-accepted\" }", "{ \"setStage\": \"hold\", \"stage\": \"done\" }");
        var runtime = Fresh(1, staged);
        Fixture.Play(runtime, "accept");
        Assert.Equal(new QuestProgress(QuestStatus.Completed, "done"), runtime.State.Quests["hold"]);

        var finished = Fixture.With("{ \"signal\": \"order-accepted\" }", "{ \"finishQuest\": \"hold\", \"status\": \"failed\" }");
        var failed = Fresh(1, finished);
        Fixture.Play(failed, "accept");
        Assert.Equal(new QuestProgress(QuestStatus.Failed, "order"), failed.State.Quests["hold"]);

        // A quest that is not active can neither change stage nor be finished.
        var early = Fixture.With("{ \"startQuest\": \"hold\" }, { \"add\": \"trust\", \"by\": 1 }, { \"signal\": \"order-accepted\" }", "{ \"finishQuest\": \"hold\", \"status\": \"failed\" }");
        var wrong = Fresh(1, early);
        wrong.Start("briefing", Fixture.Facts());
        wrong.Next(Fixture.Facts());
        Assert.Equal(DialogueCodes.QuestState, Fixture.Refusal(() => wrong.Choose("accept", Fixture.Facts())));
    }

    [Fact]
    public void TheStateHoldsNoObjectiveStatusOnlyTheStageAndStatusOfNarrativeQuests()
    {
        Assert.Equal(["Stage", "Status"], typeof(QuestProgress).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal));
        var names = typeof(DialogueState).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(names, n => n.Contains("Objective", StringComparison.OrdinalIgnoreCase));
        // An objective reaches a quest only as a fact the host passes in: the same quest reacts to a different value of it and nothing is remembered between calls.
        var runtime = Fresh();
        runtime.StartQuest("hold", Fixture.Facts());
        var digest = Digest(runtime);
        runtime.UpdateQuests(Fixture.Facts(hold: "active"));
        Assert.Equal(digest, Digest(runtime));
    }

    [Fact]
    public void AnEndedConversationCanBeStartedAgain()
    {
        var runtime = Fresh();
        Fixture.Play(runtime, "refuse");
        Assert.False(runtime.InConversation);
        Assert.Equal(StepKind.Line, runtime.Start("briefing", Fixture.Facts()).Kind);
    }
}