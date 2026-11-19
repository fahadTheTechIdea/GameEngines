using System.Text.Json.Nodes;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>The director and the deck: the pacing phases, the spacing of cards, tiers and difficulty, cooldowns, groups, conditions, chains, timed modifiers and the bounds of the agenda.</summary>
public sealed class DirectorTests
{
    private static List<WorldChange> Cards(IEnumerable<WorldChange> changes) => changes.Where(c => c.Kind == ChangeKind.Card).ToList();

    /// <summary>The calm teller (it draws no card) with the phase numbers of the ordinary one, so the phases can be driven by hand with forced cards.</summary>
    private static WorldRuntime Driven(Action<JsonObject>? more = null)
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            var calm = Tiny.Teller(root, "calm");
            calm["peakAt"] = 500;
            calm["relaxBelow"] = 200;
            calm["peakHold"] = 2;
            calm["restMin"] = 3;
            calm["decay"] = 100;
            root["start"]!["storyteller"] = "calm";
            more?.Invoke(root);
        });
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        return runtime;
    }

    [Fact]
    public void TheDirectorBuildsPeaksHoldsRelaxesAndBuildsAgainOnTheStress()
    {
        var runtime = Driven();
        runtime.PlayCard("c-bad", "a");
        runtime.PlayCard("c-bad", "a");                     // 600 stress
        var phases = new List<(long Step, DirectorPhase From, DirectorPhase To, long Stress)>();
        for (var step = 1; step <= 25; step++)
            foreach (var c in runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host()).Items.Where(c => c.Kind == ChangeKind.Director)) phases.Add((c.Step, (DirectorPhase)c.Sub, (DirectorPhase)c.Index, c.Amount));
        // Step 1: the stress decays 600 to 540, still at or above 500: peak. Held for two steps (1, 2), relax at step 3.
        Assert.Equal((1L, DirectorPhase.Build, DirectorPhase.Peak, 540L), phases[0]);
        Assert.Equal((3L, DirectorPhase.Peak, DirectorPhase.Relax), (phases[1].Step, phases[1].From, phases[1].To));
        // Then it rests until the stress is at most 200 and the rest has lasted more than 3 steps.
        Assert.Equal(DirectorPhase.Build, phases[2].To);
        Assert.True(phases[2].Stress <= 200);
        Assert.True(phases[2].Step - phases[1].Step > 3);
        Assert.Equal(3, phases.Count);
    }

    [Fact]
    public void AStorytellerWhoseLineIsAboveTheMaximumNeverPeaks()
    {
        var runtime = Driven(root => { Tiny.Teller(root, "calm")["peakAt"] = 1001; Tiny.Teller(root, "calm")["decay"] = 0; });
        for (var i = 0; i < 6; i++) runtime.PlayCard("c-bad", "a");
        Assert.Equal(1000, runtime.Stress);
        var changes = Fixture.Live(runtime, 40);
        Assert.Empty(changes.Of(ChangeKind.Director));
        Assert.Equal(DirectorPhase.Build, runtime.Phase);
    }

    [Fact]
    public void TheStressIsKeptBetweenZeroAndAThousand()
    {
        var runtime = Driven(root => Tiny.Teller(root, "calm")["decay"] = 0);
        for (var i = 0; i < 10; i++) runtime.PlayCard("c-bad", "a");
        Assert.Equal(1000, runtime.Stress);
        for (var i = 0; i < 10; i++) runtime.PlayCard("c-good", "a");
        Assert.Equal(0, runtime.Stress);
    }

    [Fact]
    public void TheDifficultyScalesWhatAddsStressButNotWhatRelievesIt()
    {
        var normal = Driven(root => Tiny.Teller(root, "calm")["decay"] = 0);
        normal.PlayCard("c-bad", "a");
        Assert.Equal(300, normal.Stress);
        var hard = Driven(root => Tiny.Teller(root, "calm")["decay"] = 0);
        hard.UseDifficulty("hard");
        hard.PlayCard("c-bad", "a");
        Assert.Equal(600, hard.Stress);
        hard.PlayCard("c-good", "a");
        Assert.Equal(400, hard.Stress);
    }

    [Fact]
    public void CardsAreSpacedByTheGapAndNeverCloserThanItsMinimum()
    {
        var runtime = Fixture.MiniWorld(root => { Tiny.Teller(root, "t")["gapMin"] = 3; Tiny.Teller(root, "t")["gapMax"] = 8; });
        var drawn = Cards(Fixture.Live(runtime, 2000)).Where(c => c.Sub2 == (int)CardReason.Drawn).Select(c => c.Step).ToList();
        Assert.True(drawn.Count > 200);
        for (var i = 1; i < drawn.Count; i++) Assert.True(drawn[i] - drawn[i - 1] >= 3, "cards at " + drawn[i - 1] + " and " + drawn[i]);
        Assert.InRange((double)(drawn[^1] - drawn[0]) / (drawn.Count - 1), 3.0, 9.0);
    }

    [Fact]
    public void ARandomStorytellerPlaysAgainstNoPaceAndAPeacefulOneRestsLonger()
    {
        int Count(string teller)
        {
            var runtime = Fixture.MiniWorld(root =>
            {
                var calm = Tiny.Teller(root, "calm");
                calm["gapMin"] = teller == "random" ? 0 : 6;
                calm["gapMax"] = teller == "random" ? 2 : 40;
                calm["peakAt"] = teller == "random" ? 1001 : 400;
                calm["share"] = teller == "random" ? new JsonArray(1000, 1000, 1000) : new JsonArray(500, 1000, 1500);
                root["start"]!["storyteller"] = "calm";
            });
            return Cards(Fixture.Live(runtime, 1500)).Count(c => c.Sub2 == (int)CardReason.Drawn);
        }
        Assert.True(Count("random") > 2 * Count("peaceful"));
    }

    [Fact]
    public void BadCardsAreDrawnOnlyWhileBuildingAndTheTierSharesWeighThem()
    {
        var runtime = Fixture.MiniWorld();
        var bad = 0;
        for (var step = 0; step < 1500; step++)
        {
            var changes = runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            foreach (var c in changes.Items.Where(c => c.Kind == ChangeKind.Card && c.Sub2 == (int)CardReason.Drawn && runtime.Set.Cards[c.Index].Tier == Tier.Bad))
            {
                bad++;
                Assert.Equal(DirectorPhase.Build, runtime.Phase);          // the director settles its phase before it draws: bad cards need one that is building
            }
        }
        Assert.True(bad > 20);
        var none = Fixture.MiniWorld(root => Tiny.Teller(root, "t")["share"] = new JsonArray(0, 1000, 1000));
        Assert.DoesNotContain(Cards(Fixture.Live(none, 800)), c => none.Set.Cards[c.Index].Tier == Tier.Bad);
    }

    [Fact]
    public void ACardsConditionDecidesWhenAndWhereItCanBeDrawn()
    {
        var runtime = Fixture.MiniWorld();
        var goods = Cards(Fixture.Live(runtime, 1500)).Where(c => runtime.Set.Cards[c.Index].Id == "c-good" && c.Sub2 == (int)CardReason.Drawn).ToList();
        Assert.True(goods.Count > 10);
        Assert.All(goods, c => Assert.Equal("warm", Fixture.Season(c.Step)));
        var hereOnly = Fixture.MiniWorld(root => Tiny.Card(root, "c-good")["when"] = Tiny.Cond("ext.alert", "gt", 5));
        Assert.DoesNotContain(Cards(Fixture.Live(hereOnly, 600, Fixture.Host(0))), c => hereOnly.Set.Cards[c.Index].Id == "c-good");
        Assert.Contains(Cards(Fixture.Live(hereOnly, 1200, Fixture.Host(9))), c => hereOnly.Set.Cards[c.Index].Id == "c-good");
    }

    [Fact]
    public void ARegionalCardIsPlayedWhereItsConditionHolds()
    {
        var runtime = Fixture.MiniWorld(root => Tiny.Card(root, "c-good")["when"] = Tiny.Cond("population", "lt", 7000));      // only region b
        var goods = Cards(Fixture.Live(runtime, 1200)).Where(c => runtime.Set.Cards[c.Index].Id == "c-good").ToList();
        Assert.NotEmpty(goods);
        Assert.All(goods, c => Assert.Equal(1, c.Region));
    }

    [Fact]
    public void ACooldownKeepsACardFromBeingDrawnAgainAtOnce()
    {
        var runtime = Fixture.MiniWorld(root => Tiny.Card(root, "c-global")["cooldown"] = 25);
        var steps = Cards(Fixture.Live(runtime, 1500)).Where(c => runtime.Set.Cards[c.Index].Id == "c-global").Select(c => c.Step).ToList();
        Assert.True(steps.Count > 5);
        for (var i = 1; i < steps.Count; i++) Assert.True(steps[i] - steps[i - 1] >= 26);
    }

    [Fact]
    public void PlayingACardOfAGroupHoldsTheGroupForTheDurationAndStartsTheCardsCooldown()
    {
        var runtime = Driven();
        runtime.PlayCard("c-bad", "a");            // at step 0: group g held for 3 steps, the card rests for 5 and the step it was played
        Assert.Equal(3, runtime.GroupLockedUntil[0]);
        Assert.Equal(6, runtime.CardReadyAt[0]);
        runtime.PlayCard("c-after", "a");           // a card of no group holds nothing
        Assert.Equal(3, runtime.GroupLockedUntil[0]);
    }

    [Fact]
    public void AGroupNeverHoldsTwoOfItsCardsAtOnce()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            var other = Tiny.Copy(Tiny.Card(root, "c-bad")).AsObject();
            other["id"] = "c-bad2";
            other["key"] = "c.bad2";
            other["chain"] = new JsonArray();
            Tiny.Array(root, "cards").Add(other);
            Tiny.Card(root, "c-bad")["cooldown"] = 0;
            other["cooldown"] = 0;
            Tiny.Teller(root, "t")["gapMin"] = 0;
            Tiny.Teller(root, "t")["gapMax"] = 1;
            Tiny.Teller(root, "t")["peakAt"] = 1001;
        });
        var played = Cards(Fixture.Live(runtime, 600)).Where(c => runtime.Set.Cards[c.Index].Group == 0 && c.Sub2 == (int)CardReason.Drawn).Select(c => c.Step).ToList();
        Assert.True(played.Count > 20);
        for (var i = 1; i < played.Count; i++) Assert.True(played[i] - played[i - 1] >= 3, "group cards at " + played[i - 1] + " and " + played[i]);
    }

    [Fact]
    public void AChainPlaysItsCardAfterTheDelayInTheSameRegion()
    {
        var runtime = Driven();
        runtime.PlayCard("c-bad", "b");
        Assert.Single(runtime.Agenda);
        var changes = Fixture.Live(runtime, 4);
        var after = Cards(changes).Single(c => runtime.Set.Cards[c.Index].Id == "c-after");
        Assert.Equal(2, after.Step);
        Assert.Equal(1, after.Region);
        Assert.Equal((int)CardReason.Chained, after.Sub2);
        Assert.Empty(runtime.Agenda);
        Assert.Contains(changes, c => c.Kind == ChangeKind.Effect && c.Name == "sig.after" && c.Region == 1);
    }

    [Fact]
    public void AChainEntryWhoseConditionNoLongerHoldsIsDroppedWhenItComesDue()
    {
        var runtime = Driven(root => Tiny.Card(root, "c-bad")["chain"]![0]!["when"] = Tiny.Cond("ext.alert", "gt", 5));
        runtime.PlayCard("c-bad", "a");
        var changes = Fixture.Live(runtime, 5, Fixture.Host(0));
        Assert.DoesNotContain(Cards(changes), c => runtime.Set.Cards[c.Index].Id == "c-after");
        Assert.Empty(runtime.Agenda);
        var yes = Driven(root => Tiny.Card(root, "c-bad")["chain"]![0]!["when"] = Tiny.Cond("ext.alert", "gt", 5));
        yes.PlayCard("c-bad", "a");
        Assert.Contains(Cards(Fixture.Live(yes, 5, Fixture.Host(9))), c => yes.Set.Cards[c.Index].Id == "c-after");
    }

    [Fact]
    public void ACardsTimedModifierHoldsForItsDurationThenGoes()
    {
        var runtime = Driven(root => { Tiny.Array(root, "weathers")[1]!["modifiers"] = new JsonArray(); });         // no weather moves it
        runtime.PlayCard("c-bad", "a");
        Assert.Equal(800, runtime.Modifier(0, 0));
        Assert.Equal(1000, runtime.Modifier(1, 0));                     // only the region it was played in
        Fixture.Live(runtime, 3);
        Assert.Equal(800, runtime.Modifier(0, 0));
        Fixture.Live(runtime, 4);
        Assert.Equal(1000, runtime.Modifier(0, 0));
        Assert.Empty(runtime.TimedModifiers);
    }

    [Fact]
    public void AGlobalCardsModifierAndShockReachEveryRegion()
    {
        var runtime = Driven();
        var changes = runtime.PlayCard("c-global", "").Items;
        Assert.Equal(1100, runtime.Modifier(0, 1));
        Assert.Equal(1100, runtime.Modifier(1, 1));
        Assert.Contains(changes, c => c.Kind == ChangeKind.Effect && c.Name == "sh1" && c.Region == -1 && c.Amount == 500);
        Assert.Contains(changes, c => c.Kind == ChangeKind.Card && c.Region == -1);
    }

    [Fact]
    public void TheAgendaAndTheTimedListsAreBoundedAndWhatDoesNotFitIsCounted()
    {
        var runtime = Driven(root => { root["limits"]!["maxAgenda"] = 1; root["limits"]!["maxEffects"] = 1; });
        runtime.PlayCard("c-bad", "a");
        runtime.PlayCard("c-bad", "b");
        Assert.Single(runtime.Agenda);
        Assert.Single(runtime.TimedModifiers);
        Assert.Equal(2, runtime.Dropped);
    }

    [Fact]
    public void AForcedCardIgnoresTheCooldownTheConditionAndThePaceButStartsItsCooldown()
    {
        var runtime = Driven(root => Tiny.Card(root, "c-good")["when"] = Tiny.Cond("ext.alert", "gt", 5));
        runtime.PlayCard("c-good", "a");
        Assert.Equal(runtime.Step + 1 + 5, runtime.CardReadyAt[2]);
        runtime.PlayCard("c-good", "a");             // twice in a row, with the condition false: forced
        Assert.Equal(2, runtime.Played);
    }

    [Fact]
    public void ACardCanStartAHazardAndAnOutbreak()
    {
        var runtime = Driven(root =>
        {
            Tiny.Card(root, "c-bad")["effects"]!.AsArray().Add(new JsonObject { ["hazard"] = "h", ["severity"] = "high" });
            Tiny.Card(root, "c-bad")["effects"]!.AsArray().Add(new JsonObject { ["outbreak"] = "d", ["cases"] = 7 });
        });
        var changes = runtime.PlayCard("c-bad", "a").Items;
        Assert.Contains(changes, c => c.Kind == ChangeKind.Hazard && c.Region == 0 && c.Sub == 1 && c.Sub2 == (int)HazardChange.Started);
        Assert.Equal(7, runtime.Regions[0].Exposed[0]);
        Assert.Contains(changes, c => c.Kind == ChangeKind.Outbreak && c.Region == 0 && c.Sub2 == (int)OutbreakChange.Introduced);
    }

    [Fact]
    public void TheStorytellerAndTheDifficultyCanBeChangedBetweenStepsWithoutLosingTheStress()
    {
        var runtime = Driven(root => Tiny.Teller(root, "calm")["decay"] = 0);
        runtime.PlayCard("c-bad", "a");
        runtime.UseStoryteller("t");
        runtime.UseDifficulty("hard");
        Assert.Equal(300, runtime.Stress);
        Assert.Equal(0, runtime.Storyteller);
        Assert.Equal(1, runtime.Difficulty);
    }
}
