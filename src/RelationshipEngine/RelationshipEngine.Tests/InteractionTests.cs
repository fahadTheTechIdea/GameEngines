using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>Interactions (R7): preconditions, cooldown per pair, gifts and tastes, acceptance with seeded noise, refused effects, witnesses, every effect kind, offers and explanations.</summary>
public sealed class InteractionTests
{
    private static RelationshipRuntime Village(params string[] names)
    {
        var runtime = Fixture.Runtime();
        foreach (var name in names) runtime.AddActor(name, []);
        return runtime;
    }

    private static InteractionResult Do(RelationshipRuntime runtime, string interaction, string actor, string target, IReadOnlyList<string>? witnesses = null, string gift = "", IReadOnlyDictionary<string, FactValue>? host = null) =>
        runtime.Perform(new InteractionRequest(interaction, actor, target, witnesses ?? [], gift), host ?? Fixture.Spring);

    private static long Affinity(RelationshipRuntime runtime, string from, string to) => runtime.Opinion(from, to, 0);

    // ---- chat: axis and need effects, acceptance, refusal -----------------------------------------------------------------------------

    [Fact]
    public void ATargetInAGoodMoodAcceptsAChatAndBothSidesChangeAsTheDocumentSays()
    {
        var runtime = Village("a", "b");
        var result = Do(runtime, "chat", "a", "b");
        Assert.True(result.Accepted);
        Assert.Equal((100L, 50L, 0L), (result.Score, result.Threshold, result.Noise));
        Assert.Equal(15, Affinity(runtime, "a", "b"));
        Assert.Equal(15, Affinity(runtime, "b", "a"));
        Assert.Equal(820, runtime.NeedLevel(runtime.Actor("a"), 2));
        Assert.Equal(820, runtime.NeedLevel(runtime.Actor("b"), 2));
    }

    [Fact]
    public void ATargetInABadMoodRefusesAndTheRefusedEffectsApplyInsteadOfTheAcceptedOnes()
    {
        var runtime = Village("a", "b");
        runtime.AdjustNeed("b", "hunger", -800);
        runtime.AdjustNeed("b", "energy", -900);
        Assert.True(runtime.Mood("b") <= -300);
        var result = Do(runtime, "chat", "a", "b");
        Assert.False(result.Accepted);
        Assert.Equal(0, result.Score);
        Assert.Equal(-5, Affinity(runtime, "a", "b"));
        Assert.Equal(0, Affinity(runtime, "b", "a"));
        Assert.Equal(700, runtime.NeedLevel(runtime.Actor("a"), 2));
    }

    [Fact]
    public void TheCooldownOfAPairIsPerInteractionAndPerDirectionAndEndsAtItsStep()
    {
        var runtime = Village("a", "b", "c");
        Do(runtime, "chat", "a", "b");
        Assert.Equal(RelationshipCodes.Cooldown, Fixture.Refusal(() => Do(runtime, "chat", "a", "b")));
        Do(runtime, "chat", "b", "a");                                   // the other direction has its own cooldown
        Do(runtime, "chat", "a", "c");                                   // another target too
        Do(runtime, "flirt", "a", "b");                                  // another interaction too
        runtime.Advance(5, Fixture.Spring);
        Assert.Equal(RelationshipCodes.Cooldown, Fixture.Refusal(() => Do(runtime, "chat", "a", "b")));
        runtime.Advance(6, Fixture.Spring);                              // ready at step 6
        Assert.True(Do(runtime, "chat", "a", "b").Accepted);
    }

    [Fact]
    public void ARefusedInteractionStillStartsItsCooldown()
    {
        var runtime = Village("a", "b");
        runtime.AdjustNeed("b", "hunger", -800);
        runtime.AdjustNeed("b", "energy", -900);
        Assert.False(Do(runtime, "chat", "a", "b").Accepted);
        Assert.Equal(RelationshipCodes.Cooldown, Fixture.Refusal(() => Do(runtime, "chat", "a", "b")));
    }

    [Fact]
    public void ACooldownThatHasEndedIsDroppedAtTheDayBoundaryAndAnEdgeWithNothingElseIsForgotten()
    {
        var runtime = Village("a", "b");
        runtime.AdjustNeed("b", "hunger", -800);
        runtime.AdjustNeed("b", "energy", -900);
        Do(runtime, "chat", "a", "b");                                   // refused: -5 affinity, and a cooldown of 6
        Assert.Single(runtime.Edge("a", "b")!.Cooldowns);
        runtime.Advance(23, Fixture.Spring);
        Assert.Single(runtime.Edge("a", "b")!.Cooldowns);                // not pruned before the boundary even though it ended at step 6
        runtime.Advance(24, Fixture.Spring);
        var left = runtime.Edge("a", "b")!;                              // the -5 drifted back by 4, so -1 remains and the edge is kept
        Assert.Empty(left.Cooldowns);
        Assert.Equal(-1, left.Base[0]);
        runtime.Advance(48, Fixture.Spring);
        Assert.Null(runtime.Edge("a", "b"));                             // nothing left a day later
    }

    // ---- preconditions ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARefusedPreconditionNamesTheFactsItReadAndChangesNothing()
    {
        var runtime = Village("a", "b");
        runtime.AdjustAxis("a", "b", "trust", -900);
        var before = runtime.StateDigest();
        var refusal = Assert.Throws<EngineRefusedException>(() => Do(runtime, "give-gift", "a", "b", gift: "pie"));
        Assert.Equal(RelationshipCodes.Precondition, refusal.Code);
        Assert.Contains("edge.state.reliability", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("distrusted", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(before, runtime.StateDigest());
    }

    [Fact]
    public void AHostFactThePreconditionNeedsMustBeGivenAndMustHaveItsType()
    {
        var runtime = Village("a", "b");
        Assert.Equal(RelationshipCodes.FactMissing, Fixture.Refusal(() => Do(runtime, "share-meal", "a", "b", host: new Dictionary<string, FactValue>())));
        Assert.Equal(RelationshipCodes.FactInvalid, Fixture.Refusal(() => Do(runtime, "share-meal", "a", "b", host: new Dictionary<string, FactValue> { ["cal.is_day"] = FactValue.From("yes") })));
        Assert.Equal(0, runtime.Rolls);
    }

    [Fact]
    public void ATwoPartPreconditionNeedsBothPartsAndABondAlreadyHeldBlocksIt()
    {
        var runtime = Village("a", "b");
        Assert.Equal(RelationshipCodes.Precondition, Fixture.Refusal(() => Do(runtime, "propose", "a", "b")));
        runtime.AdjustAxis("a", "b", "attraction", 500);
        Assert.Equal(RelationshipCodes.Precondition, Fixture.Refusal(() => Do(runtime, "propose", "a", "b")));
        runtime.AdjustAxis("a", "b", "affinity", 600);
        runtime.SetBond("a", "b", "spouse", true);
        Assert.Equal(RelationshipCodes.Precondition, Fixture.Refusal(() => Do(runtime, "propose", "a", "b")));
    }

    // ---- gifts and tastes ---------------------------------------------------------------------------------------------------------------

    private static RelationshipRuntime Gifts()
    {
        var runtime = Fixture.Runtime();
        runtime.AddActor("a", []);
        runtime.AddActor("lover", [], new Dictionary<string, int> { ["flower"] = 2, ["pretty"] = 1 });
        runtime.AddActor("hater", [], new Dictionary<string, int> { ["food"] = -2 });
        runtime.AddActor("neutral", []);
        runtime.AddActor("witness", []);
        return runtime;
    }

    [Fact]
    public void ALovedGiftScalesEveryTastefulEffectByTheLoveFactor()
    {
        var runtime = Gifts();
        var result = Do(runtime, "give-gift", "a", "lover", gift: "flowers");   // taste 2 + 1 clamps to 2: love, 2000 permille
        Assert.True(result.Accepted);
        var edge = runtime.Edge("lover", "a")!;
        Assert.Equal(2000, edge.Memories.Single().Scale);
        Assert.Equal(160 + 240, Affinity(runtime, "lover", "a"));                 // axis +80 x 2, memory +120 x 2
        Assert.Equal(60, runtime.Opinion("lover", "a", 1));                        // trust +30 x 2
        Assert.Equal(2000, runtime.Actor("lover").Moodlets.Single().Scale);
        Assert.Equal(0, Affinity(runtime, "a", "lover"));
    }

    [Fact]
    public void AHatedGiftTurnsTheGoodEffectsBadAndANeutralOneScalesByTheNeutralFactor()
    {
        var runtime = Gifts();
        Do(runtime, "give-gift", "a", "hater", gift: "pie");                       // food -2: hate, -500
        Assert.Equal(-500, runtime.Edge("hater", "a")!.Memories.Single().Scale);
        Assert.Equal(-40 - 60, Affinity(runtime, "hater", "a"));                   // axis 80 x -0.5, memory 120 x -0.5
        Assert.Equal(-100, runtime.MoodletValue(runtime.Actor("hater").Moodlets.Single()));
        Do(runtime, "give-gift", "a", "neutral", gift: "stone");                   // no taste: neutral, 250
        Assert.Equal(20 + 30, Affinity(runtime, "neutral", "a"));
    }

    [Fact]
    public void TheTasteLevelIsTheClampedSumOfTheTastesForTheGiftsTags()
    {
        var runtime = Gifts();
        var gifts = runtime.Set.GiftById;
        Assert.Equal(2, runtime.GiftTaste(runtime.Actor("lover"), gifts["flowers"]));
        Assert.Equal(-2, runtime.GiftTaste(runtime.Actor("hater"), gifts["pie"]));
        Assert.Equal(0, runtime.GiftTaste(runtime.Actor("neutral"), gifts["flowers"]));
        runtime.AddActor("mixed", [], new Dictionary<string, int> { ["food"] = 2, ["sweet"] = -1 });
        Assert.Equal(1, runtime.GiftTaste(runtime.Actor("mixed"), gifts["pie"]));
    }

    [Fact]
    public void AWitnessRemembersTheGiverWithTheWitnessMemoryAndOnlyAcceptedInteractionsAreSeen()
    {
        var runtime = Gifts();
        Do(runtime, "give-gift", "a", "lover", ["witness"], "flowers");
        var edge = runtime.Edge("witness", "a")!;
        Assert.Equal(["saw-kindness"], edge.Memories.Select(m => runtime.Set.Memories[m.Memory].Id));
        Assert.Equal((40L, 40L), (Affinity(runtime, "witness", "a"), runtime.Opinion("witness", "a", 1)));
        Assert.Null(runtime.Edge("witness", "lover"));
    }

    [Fact]
    public void AGiftNeedsAnItemAndAnItemNeedsAGiftInteraction()
    {
        var runtime = Gifts();
        Assert.Equal(RelationshipCodes.GiftRequired, Fixture.Refusal(() => Do(runtime, "give-gift", "a", "lover")));
        Assert.Equal(RelationshipCodes.GiftUnexpected, Fixture.Refusal(() => Do(runtime, "insult", "a", "lover", gift: "pie")));
        Assert.Equal(RelationshipCodes.UnknownGift, Fixture.Refusal(() => Do(runtime, "give-gift", "a", "lover", gift: "moon")));
    }

    [Fact]
    public void WitnessesAreCheckedNoneListedTwiceNoneAPartyNoneUnknownNoMoreThanTheMaximum()
    {
        var runtime = Gifts();
        runtime.AddActor("w2", []);
        runtime.AddActor("w3", []);
        runtime.AddActor("w4", []);
        Assert.Equal(RelationshipCodes.Witness, Fixture.Refusal(() => Do(runtime, "give-gift", "a", "lover", ["witness", "witness"], "flowers")));
        Assert.Equal(RelationshipCodes.Witness, Fixture.Refusal(() => Do(runtime, "give-gift", "a", "lover", ["a"], "flowers")));
        Assert.Equal(RelationshipCodes.Witness, Fixture.Refusal(() => Do(runtime, "give-gift", "a", "lover", ["nobody"], "flowers")));
        Assert.Equal(RelationshipCodes.Witness, Fixture.Refusal(() => Do(runtime, "give-gift", "a", "lover", ["witness", "w2", "w3", "w4"], "flowers")));
        Assert.Equal(3, Do(runtime, "give-gift", "a", "neutral", ["witness", "w2", "w3"], "stone").Changes.Memories.Count(m => m.Memory == runtime.Set.MemoryById["saw-kindness"].Index));
    }

    // ---- the other effect kinds ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnInsultMakesAMemoryAMoodletAndLowersTheActorsOwnOpinion()
    {
        var runtime = Village("a", "b");
        var result = Do(runtime, "insult", "a", "b");
        Assert.True(result.Accepted);
        Assert.Equal(-150, Affinity(runtime, "b", "a"));
        Assert.Equal(-20, Affinity(runtime, "a", "b"));
        Assert.Equal(-250, runtime.MoodletValue(runtime.Actor("b").Moodlets.Single()));
        Assert.Contains(result.Changes.Memories, m => m is { Holder: "b", Subject: "a" });
        Assert.Contains(result.Changes.Moodlets, m => m.Actor == "b");
    }

    [Fact]
    public void ATheftGivesTheThiefFoodAndEveryWitnessAMemoryOfTheTheftThatIsUnique()
    {
        var runtime = Village("thief", "victim", "w1", "w2");
        runtime.AdjustNeed("thief", "hunger", -800);
        Assert.Equal(0, runtime.NeedLevel(runtime.Actor("thief"), 0));
        Do(runtime, "steal", "thief", "victim", ["w1", "w2"]);
        Assert.Equal(300, runtime.NeedLevel(runtime.Actor("thief"), 0));
        Assert.Equal(-200, runtime.Opinion("victim", "thief", 1));
        Assert.Equal(-200, runtime.Opinion("w1", "thief", 1));
        Assert.Equal(-200, runtime.Opinion("w2", "thief", 1));
        runtime.Advance(48, Fixture.Spring);
        Do(runtime, "steal", "thief", "victim", ["w1"]);
        Assert.Single(runtime.Edge("w1", "thief")!.Memories);                    // unique: the second theft added no second memory
    }

    [Fact]
    public void AProposalTheTargetAcceptsSetsTheSymmetricBondAndGivesBothAMoodlet()
    {
        var runtime = Village("a", "b");
        runtime.AdjustAxis("a", "b", "attraction", 500);
        runtime.AdjustAxis("a", "b", "affinity", 600);
        runtime.AdjustAxis("b", "a", "attraction", 500);
        runtime.AdjustAxis("b", "a", "affinity", 600);
        var accepted = false;
        for (var attempt = 0; attempt < 40 && !accepted; attempt++)
        {
            runtime.Advance(runtime.Step + 240, Fixture.Spring);
            runtime.AdjustAxis("a", "b", "attraction", 500);
            runtime.AdjustAxis("a", "b", "affinity", 600);
            runtime.AdjustAxis("b", "a", "attraction", 500);
            runtime.AdjustAxis("b", "a", "affinity", 600);
            accepted = Do(runtime, "propose", "a", "b").Accepted;
        }
        Assert.True(accepted, "with the noise at most +100 and the score 600, a proposal is accepted about half the time");
        Assert.True(runtime.HasBond("a", "b", 0));
        Assert.True(runtime.HasBond("b", "a", 0));
        Assert.Single(runtime.Actor("a").Moodlets, m => runtime.Set.Moodlets[m.Moodlet].Id == "festival-joy");
        Assert.Single(runtime.Actor("b").Moodlets, m => runtime.Set.Moodlets[m.Moodlet].Id == "festival-joy");
    }

    [Fact]
    public void ARefusedProposalCostsTheProposerTheAttractionAndSetsNoBond()
    {
        var runtime = Village("a", "b");
        runtime.AdjustAxis("a", "b", "attraction", 800);
        runtime.AdjustAxis("a", "b", "affinity", 800);
        var result = Do(runtime, "propose", "a", "b");       // the target feels nothing: score 0 + noise below 600
        Assert.False(result.Accepted);
        Assert.Equal(650, runtime.Opinion("a", "b", 2));
        Assert.False(runtime.HasBond("a", "b", 0));
    }

    // ---- the seeded noise ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheNoiseIsTheSharedSeededRollAndDrawnOnlyWhenTheInteractionHasNoise()
    {
        var runtime = Village("a", "b");
        Do(runtime, "chat", "a", "b");
        Assert.Equal(0, runtime.Rolls);
        var noises = new List<long>();
        for (var i = 0; i < 60; i++)
        {
            runtime.Advance(runtime.Step + 12, Fixture.Spring);
            var result = Do(runtime, "share-meal", "a", "b");
            noises.Add(result.Noise);
            Assert.Equal(DeterministicRoll.Below(Fixture.Village.Seed, "accept", i, 61) - 30, result.Noise);
            Assert.Equal(result.Score + result.Noise >= result.Threshold, result.Accepted);
        }
        Assert.Equal(60, runtime.Rolls);
        Assert.InRange(noises.Min(), -30, -10);
        Assert.InRange(noises.Max(), 10, 30);
    }

    [Fact]
    public void TheSameRequestsInTheSameOrderGiveTheSameResultsAndADifferentSeedGivesDifferentNoise()
    {
        List<long> Run(RelationshipSet set)
        {
            var runtime = RelationshipRuntime.Create(set);
            runtime.AddActor("a", []);
            runtime.AddActor("b", []);
            var noises = new List<long>();
            for (var i = 0; i < 20; i++)
            {
                runtime.Advance(runtime.Step + 12, Fixture.Spring);
                noises.Add(runtime.Perform(new InteractionRequest("share-meal", "a", "b", [], ""), Fixture.Spring).Noise);
            }
            return noises;
        }
        Assert.Equal(Run(Fixture.Village), Run(Fixture.Village));
        Assert.NotEqual(Run(Fixture.Village), Run(RelationshipLoader.Load(Edit.Doc(r => r["seed"] = 77))));
    }

    [Fact]
    public void AnAcceptanceScoreIsTheSumOfTheWeightsOfTheRulesThatHold()
    {
        var runtime = Village("a", "b");
        runtime.AdjustAxis("a", "b", "affinity", 400);          // a thinks b a friend
        runtime.AdjustAxis("b", "a", "affinity", 400);
        runtime.Advance(12, Fixture.Spring);
        var bothRules = 0;
        for (var i = 0; i < 20; i++)
        {
            runtime.Advance(runtime.Step + 12, Fixture.Spring);
            var result = Do(runtime, "share-meal", "a", "b");
            Assert.Equal(100, result.Score);                     // friend (60) + reverse affinity above 100 (40)
            if (result.Accepted) bothRules++;
        }
        Assert.Equal(20, bothRules);   // a score of 100 with noise of at most 30 beats 40 every time
    }

    // ---- offers and explanations --------------------------------------------------------------------------------------------------------

    [Fact]
    public void OffersListEveryInteractionWithItsAvailabilityAndDesireInDocumentOrder()
    {
        var runtime = Village("a", "b");
        runtime.AdjustNeed("a", "social", -450);                 // social 250
        var offers = runtime.Offers("a", "b", Fixture.Spring);
        Assert.Equal(Fixture.Village.Interactions.Select(i => i.Id), offers.Select(o => o.Interaction));
        Assert.Equal(300, offers.Single(o => o.Interaction == "chat").Desire);
        Assert.All(offers.Where(o => o.Interaction is not ("propose" or "steal")), o => Assert.True(o.Available, o.Interaction));
        Assert.False(offers.Single(o => o.Interaction == "propose").Available);
        Assert.Equal(RelationshipCodes.Precondition, offers.Single(o => o.Interaction == "propose").Why);
        Do(runtime, "chat", "a", "b");
        var after = runtime.Offers("a", "b", Fixture.Spring).Single(o => o.Interaction == "chat");
        Assert.False(after.Available);
        Assert.Equal(RelationshipCodes.Cooldown, after.Why);
    }

    [Fact]
    public void DesireAddsTheWeightOfEachRuleThatHoldsAndCanBeNegative()
    {
        var text = Edit.Doc(r => Edit.Interaction(r, "chat")["desire"] = System.Text.Json.Nodes.JsonNode.Parse(
            "[ { \"when\": { \"fact\": \"actor.mood\", \"op\": \"gt\", \"value\": 0 }, \"weight\": 40 }, { \"when\": { \"fact\": \"actor.mood\", \"op\": \"gt\", \"value\": 500 }, \"weight\": -100 }, { \"when\": { \"fact\": \"target.mood\", \"op\": \"lt\", \"value\": 0 }, \"weight\": 7 } ]"));
        var runtime = RelationshipRuntime.Create(RelationshipLoader.Load(text));
        runtime.AddActor("a", []);
        runtime.AddActor("b", []);
        Assert.Equal(40 - 100, runtime.Offers("a", "b", Fixture.Spring).Single(o => o.Interaction == "chat").Desire);
        runtime.AdjustNeed("b", "hunger", -800);
        runtime.AdjustNeed("b", "energy", -900);
        Assert.Equal(40 - 100 + 7, runtime.Offers("a", "b", Fixture.Spring).Single(o => o.Interaction == "chat").Desire);
    }

    [Fact]
    public void TheExplanationOfAnInteractionShowsAvailabilityDesireAndEachRuleWithTheValuesItRead()
    {
        var runtime = Village("a", "b");
        var text = RelationshipExplain.Interaction(runtime, "a", "b", "share-meal", Fixture.Spring);
        Assert.StartsWith("share-meal a -> b: available", text, StringComparison.Ordinal);
        Assert.Contains("requires cal.is_day eq true [true] = true", text, StringComparison.Ordinal);
        Assert.Contains("accept needs 40 (noise -30 to +30)", text, StringComparison.Ordinal);
        Assert.Contains("accept +60 if edge.state.friendship eq \"friend\" [\"stranger\"] = false", text, StringComparison.Ordinal);
        Assert.Contains("accept +40 if reverse.affinity gt 100 [0] = false", text, StringComparison.Ordinal);
        Assert.Contains("desire 0", text, StringComparison.Ordinal);
        Assert.Equal(RelationshipCodes.UnknownInteraction, Fixture.Refusal(() => RelationshipExplain.Interaction(runtime, "a", "b", "wave", Fixture.Spring)));
        Assert.Equal(RelationshipCodes.Self, Fixture.Refusal(() => RelationshipExplain.Interaction(runtime, "a", "a", "chat", Fixture.Spring)));
    }

    [Fact]
    public void AllTheRulesAndEffectsOfAnInteractionLeaveTheRefusedCallsTheyWereNotAskedTo()
    {
        var runtime = Village("a", "b");
        var digest = runtime.StateDigest();
        Assert.Equal(RelationshipCodes.UnknownActor, Fixture.Refusal(() => Do(runtime, "chat", "a", "zed")));
        Assert.Equal(RelationshipCodes.UnknownActor, Fixture.Refusal(() => Do(runtime, "chat", "zed", "b")));
        Assert.Equal(digest, runtime.StateDigest());
    }
}
