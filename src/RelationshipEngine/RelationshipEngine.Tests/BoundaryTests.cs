using System.Text.Json.Nodes;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>
/// Boundaries found by mutation checking (docs/RELATIONSHIP_ENGINE.md section 10.4): each test pins an edge that the first round of tests let a one-character change slip through, in documents made different
/// from the sample in one place.
/// </summary>
public sealed class BoundaryTests
{
    private static RelationshipRuntime With(Action<JsonObject> edit, params string[] names)
    {
        var runtime = RelationshipRuntime.Create(RelationshipLoader.Load(Edit.Doc(edit)));
        foreach (var name in names) runtime.AddActor(name, []);
        return runtime;
    }

    // ---- needs ------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(150, true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    [InlineData(1000, false)]
    public void ANeedThatStartsAtOrBelowItsCriticalLineStartsCritical(int initial, bool critical)
    {
        var runtime = With(r => Edit.Need(r, "hunger")["initial"] = initial, "a");
        Assert.Equal(critical, runtime.Actor("a").Critical[0]);
    }

    [Fact]
    public void AnIntegerHostFactIsCheckedAgainstItsRangeAtBothEnds()
    {
        var set = RelationshipLoader.Load(Edit.Doc(r =>
        {
            r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "weather.wind", ["type"] = "int", ["min"] = 0, ["max"] = 10 });
            Edit.Need(r, "fun")["decayModifiers"]!.AsArray().Add(JsonNode.Parse("{ \"when\": { \"fact\": \"weather.wind\", \"op\": \"gt\", \"value\": 5 }, \"permille\": 2000 }"));
        }));
        FactValue Wind(long value) => FactValue.From(value);
        IReadOnlyDictionary<string, FactValue> Host(long wind) => new Dictionary<string, FactValue>(Fixture.Spring) { ["weather.wind"] = Wind(wind) };
        var runtime = RelationshipRuntime.Create(set);
        runtime.Advance(1, Host(0));
        runtime.Advance(2, Host(10));
        Assert.Equal(RelationshipCodes.FactInvalid, Fixture.Refusal(() => runtime.Advance(3, Host(-1))));
        Assert.Equal(RelationshipCodes.FactInvalid, Fixture.Refusal(() => runtime.Advance(3, Host(11))));
        Assert.Equal(2, runtime.Step);
    }

    // ---- baselines ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void WithNoEdgeTheOpinionIsTheAxisBaselineEvenWhenTheBaselineIsNotZero()
    {
        var runtime = With(r => Edit.Obj(r, "axes", 0)["baseline"] = 100, "a", "b");
        Assert.Equal(100, runtime.Opinion("a", "b", 0));
        Assert.Equal("acquaintance", runtime.LadderStateId("a", "b", 0));
        Assert.Equal(100, RelationshipFacts.Read(runtime, "a", "b")["edge.affinity"].Integer);
        runtime.AdjustAxis("a", "b", "affinity", 10);
        Assert.Equal(110, runtime.Opinion("a", "b", 0));
        Assert.Equal(new long[] { 110 }, runtime.Edge("a", "b")!.Base.Take(1));
        Assert.Equal(new long[] { 0, 0 }, runtime.Edge("a", "b")!.Base.Skip(1));
    }

    [Fact]
    public void DriftBringsABaseValueBackToANonZeroBaselineAndNoFurther()
    {
        var runtime = With(r => { Edit.Obj(r, "axes", 0)["baseline"] = 100; Edit.Obj(r, "axes", 0)["driftPerDay"] = 30; }, "a", "b");
        runtime.AdjustAxis("a", "b", "affinity", -50);       // 50, below the baseline
        runtime.Advance(24, Fixture.Spring);
        Assert.Equal(80, runtime.Edge("a", "b")!.Base[0]);
        runtime.Advance(48, Fixture.Spring);
        Assert.Equal(100, runtime.Edge("a", "b")?.Base[0] ?? 100);
    }

    // ---- exact ends of memories ---------------------------------------------------------------------------------------------------

    [Fact]
    public void AMemoryIsCountedUpToTheStepBeforeItsDurationAndNotAtItInTheCountAndInTheFact()
    {
        var runtime = With(r => Edit.Memory(r, "heard-kindness")["duration"] = 30, "a", "b");
        runtime.Remember("a", "b", "heard-kindness", 1000);
        var index = runtime.Set.MemoryById["heard-kindness"].Index;
        runtime.Advance(29, Fixture.Spring);
        Assert.Equal(1, runtime.MemoryCount("a", "b", index));
        Assert.Equal(1, RelationshipFacts.Read(runtime, "a", "b")["edge.memory.heard-kindness"].Integer);
        runtime.Advance(30, Fixture.Spring);
        Assert.Equal(0, runtime.MemoryCount("a", "b", index));
        Assert.Equal(0, RelationshipFacts.Read(runtime, "a", "b")["edge.memory.heard-kindness"].Integer);
        Assert.Single(runtime.Edge("a", "b")!.Memories);   // not pruned yet: that waits for the day boundary
    }

    [Fact]
    public void ARepeatedReplaceMemoryOfEqualStrengthRestartsTheClock()
    {
        var runtime = With(r => Edit.Memory(r, "shared-feast")["stack"] = JsonNode.Parse("{\"mode\":\"replace\",\"max\":1}"), "a", "b");
        runtime.Remember("a", "b", "shared-feast", 600);
        runtime.Advance(5, Fixture.Spring);
        Assert.Single(runtime.Remember("a", "b", "shared-feast", 600).Memories);
        Assert.Equal(5, runtime.Edge("a", "b")!.Memories.Single().Start);
    }

    // ---- cooldowns and what keeps an edge ----------------------------------------------------------------------------------------------

    private static RelationshipRuntime Flirting(int cooldown)
    {
        return With(r =>
        {
            var flirt = Edit.Interaction(r, "flirt");
            flirt["effects"] = new JsonArray();
            flirt["cooldown"] = cooldown;
        }, "a", "b");
    }

    [Fact]
    public void AnEdgeHoldingOnlyACooldownThatEndsAtTheDayBoundaryIsForgottenAtIt()
    {
        var runtime = Flirting(24);
        runtime.Perform(new InteractionRequest("flirt", "a", "b", [], ""), Fixture.Spring);
        Assert.Single(runtime.Edge("a", "b")!.Cooldowns);
        runtime.Advance(23, Fixture.Spring);
        Assert.NotNull(runtime.Edge("a", "b"));
        runtime.Advance(24, Fixture.Spring);
        Assert.Null(runtime.Edge("a", "b"));
    }

    [Fact]
    public void AnEdgeHoldingOnlyACooldownThatIsStillRunningAtTheDayBoundaryIsKeptUntilItEnds()
    {
        var runtime = Flirting(48);
        runtime.Perform(new InteractionRequest("flirt", "a", "b", [], ""), Fixture.Spring);
        runtime.Advance(24, Fixture.Spring);
        Assert.NotNull(runtime.Edge("a", "b"));
        Assert.Equal(RelationshipCodes.Cooldown, Fixture.Refusal(() => runtime.Perform(new InteractionRequest("flirt", "a", "b", [], ""), Fixture.Spring)));
        runtime.Advance(48, Fixture.Spring);
        Assert.Null(runtime.Edge("a", "b"));
    }

    // ---- eviction by size, sign-blind ------------------------------------------------------------------------------------------------

    [Fact]
    public void AnEdgeWithAStrongNegativeOpinionIsMoreSignificantThanAWeakPositiveOne()
    {
        var runtime = With(r => r["limits"]!["maxEdgesPerActor"] = 2, "a", "b", "c", "d");
        runtime.AdjustAxis("a", "b", "affinity", -300);
        runtime.AdjustAxis("a", "c", "affinity", 100);
        var changes = runtime.AdjustAxis("a", "d", "affinity", 50);
        Assert.Equal([new Evicted("edge", "a", "c")], changes.Evictions);
        Assert.Equal(["b", "d"], runtime.Actor("a").Edges.Keys);
    }

    // ---- witnesses only see what was accepted ------------------------------------------------------------------------------------------

    [Fact]
    public void AWitnessSeesNothingWhenTheTargetRefuses()
    {
        var runtime = With(r => Edit.Interaction(r, "give-gift")["acceptAt"] = 100, "giver", "receiver", "witness");
        var result = runtime.Perform(new InteractionRequest("give-gift", "giver", "receiver", ["witness"], "pie"), Fixture.Spring);
        Assert.False(result.Accepted);
        Assert.Null(runtime.Edge("witness", "giver"));
        Assert.Null(runtime.Edge("receiver", "giver"));
        Assert.Equal(0, runtime.EdgeCount - 1);   // only the giver's cooldown edge to the receiver
    }

    // ---- gossip chance at the roll --------------------------------------------------------------------------------------------------------

    [Fact]
    public void AGossipRollEqualToTheChanceDoesNotSucceedAndOneBelowItDoes()
    {
        var seed = Fixture.Village.Seed;
        var roll = (int)DeterministicRoll.Below(seed, "gossip", 0, 1000);
        Assert.InRange(roll, 1, 998);   // for this seed and counter; the test needs room on both sides
        int Heard(int chance)
        {
            var runtime = With(r => Edit.Memory(r, "was-gifted")["spread"]!["chance"] = chance, "t", "s", "h");
            runtime.Remember("t", "s", "was-gifted", 1000);
            runtime.AdjustAxis("t", "h", "trust", 500);
            runtime.Advance(24, Fixture.Spring);
            Assert.Equal(1, runtime.Rolls);
            return runtime.MemoryCount("h", "s", runtime.Set.MemoryById["heard-kindness"].Index);
        }
        Assert.Equal(0, Heard(roll));
        Assert.Equal(1, Heard(roll + 1));
        Assert.Equal(0, Heard(roll - 1));
    }

    // ---- the document, at its edges ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnAxisWhoseMinimumEqualsItsMaximumIsRefusedAndAHalfLifeLongerThanTheDurationToo()
    {
        var flat = Assert.Throws<EngineRefusedException>(() => RelationshipLoader.Load(Edit.Doc(r => { Edit.Obj(r, "axes", 0)["min"] = 0; Edit.Obj(r, "axes", 0)["max"] = 0; Edit.Obj(r, "axes", 0)["driftPerDay"] = 0; })));
        Assert.Equal(RelationshipCodes.Range, flat.Code);
        Assert.Contains("axes[0] needs min below max", flat.Message, StringComparison.Ordinal);   // named for what it is, not caught later as a ladder or a condition outside the range
        Assert.Equal(RelationshipCodes.Range, Fixture.Refusal(() => RelationshipLoader.Load(Edit.Doc(r => Edit.Moodlet(r, "gift-received")["decay"]!["halfLife"] = 73))));
        RelationshipLoader.Load(Edit.Doc(r => Edit.Moodlet(r, "gift-received")["decay"]!["halfLife"] = 72));
    }

    [Fact]
    public void ABetterTasteMayScaleAnEffectTheSameAsAWorseOne()
    {
        var set = RelationshipLoader.Load(Edit.Doc(r =>
        {
            var tastes = r["tastes"]!.AsArray();
            for (var i = 0; i < 5; i++) tastes[i] = 700;
        }));
        Assert.Equal([700, 700, 700, 700, 700], set.Tastes);
    }
}
