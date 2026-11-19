using System.Text.Json.Nodes;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>Opinions (R5): base plus memories, drift, expiry at the duration, traits on the holder, stacking of memories, and the bounds on memories and edges with the stated eviction.</summary>
public sealed class EdgeTests
{
    private static RelationshipRuntime Village(params string[] names)
    {
        var runtime = Fixture.Runtime();
        foreach (var name in names) runtime.AddActor(name, []);
        return runtime;
    }

    private static RelationshipRuntime With(Action<JsonObject> edit, params string[] names)
    {
        var runtime = RelationshipRuntime.Create(RelationshipLoader.Load(Edit.Doc(edit)));
        foreach (var name in names) runtime.AddActor(name, []);
        return runtime;
    }

    private static long Affinity(RelationshipRuntime runtime, string from, string to) => runtime.Opinion(from, to, runtime.Set.AxisById["affinity"].Index);
    private static long Trust(RelationshipRuntime runtime, string from, string to) => runtime.Opinion(from, to, runtime.Set.AxisById["trust"].Index);

    [Fact]
    public void WithNoEdgeTheOpinionIsTheBaselineAndLookingDoesNotMakeOne()
    {
        var runtime = Village("a", "b");
        var digest = runtime.StateDigest();
        Assert.Equal(0, Affinity(runtime, "a", "b"));
        Assert.Null(runtime.Edge("a", "b"));
        RelationshipFacts.Read(runtime, "a", "b");
        RelationshipExplain.Opinion(runtime, "a", "b", "affinity");
        Assert.Equal(digest, runtime.StateDigest());
        Assert.Equal(0, runtime.EdgeCount);
    }

    [Fact]
    public void AnOpinionIsDirectedAndMovesOnlyTheHoldersSide()
    {
        var runtime = Village("a", "b");
        runtime.AdjustAxis("a", "b", "affinity", 100);
        Assert.Equal(100, Affinity(runtime, "a", "b"));
        Assert.Equal(0, Affinity(runtime, "b", "a"));
        Assert.Equal(1, runtime.EdgeCount);
    }

    [Fact]
    public void TheBaseValueStaysInsideTheAxis()
    {
        var runtime = Village("a", "b");
        runtime.AdjustAxis("a", "b", "affinity", 5000);
        Assert.Equal(1000, Affinity(runtime, "a", "b"));
        runtime.AdjustAxis("a", "b", "affinity", -50_000);
        Assert.Equal(-1000, Affinity(runtime, "a", "b"));
        runtime.AdjustAxis("a", "b", "attraction", -5);
        Assert.Equal(0, runtime.Opinion("a", "b", 2));
    }

    [Fact]
    public void AMemoryAddsItsEffectsOnEachAxisItNamesAndFadesByItsHalfLife()
    {
        var runtime = Village("a", "b");
        runtime.Remember("a", "b", "was-gifted", 1000);
        Assert.Equal(120, Affinity(runtime, "a", "b"));
        Assert.Equal(30, Trust(runtime, "a", "b"));
        runtime.Advance(240, Fixture.Spring);
        Assert.Equal(60, Affinity(runtime, "a", "b") - runtime.Edge("a", "b")!.Base[0]);
        runtime.Advance(480, Fixture.Spring);
        Assert.Equal(30, Affinity(runtime, "a", "b"));
        Assert.Equal(7, Trust(runtime, "a", "b"));
    }

    [Fact]
    public void AMemoryEndsExactlyAtItsDurationAndIsGoneFromTheStateAtTheNextDayBoundary()
    {
        var runtime = Village("a", "b");
        runtime.Remember("a", "b", "heard-kindness", 1000);   // 480 steps, linear
        var index = runtime.Set.MemoryById["heard-kindness"].Index;
        runtime.Advance(479, Fixture.Spring);
        Assert.Equal(1, runtime.MemoryCount("a", "b", index));
        Assert.Equal(0, runtime.MemoryValue(runtime.Edge("a", "b")!.Memories[0], 0));
        runtime.Advance(480, Fixture.Spring);
        Assert.Equal(0, runtime.MemoryCount("a", "b", index));
        Assert.Equal(0, Affinity(runtime, "a", "b"));
        Assert.Equal(480, runtime.Step);               // 480 is a day boundary, so the instance is pruned in the same Advance
        Assert.Null(runtime.Edge("a", "b"));          // and an edge with nothing left is forgotten
    }

    [Fact]
    public void TheBaseDriftsTowardItsBaselineByTheAxisRatePerDayAndNeverPastIt()
    {
        var runtime = Village("a", "b");
        runtime.AdjustAxis("a", "b", "affinity", 10);
        runtime.AdjustAxis("a", "b", "trust", -3);
        runtime.AdjustAxis("a", "b", "attraction", 100);
        runtime.Advance(23, Fixture.Spring);
        Assert.Equal(10, runtime.Edge("a", "b")!.Base[0]);
        runtime.Advance(24, Fixture.Spring);
        Assert.Equal(new long[] { 6, -1, 94 }, runtime.Edge("a", "b")!.Base);     // 4, 2 and 6 a day
        runtime.Advance(48, Fixture.Spring);
        Assert.Equal(new long[] { 2, 0, 88 }, runtime.Edge("a", "b")!.Base);
    }

    [Fact]
    public void AnEdgeThatHoldsNothingIsForgottenAtTheDayBoundaryAndOneWithAMemoryOrBondIsNot()
    {
        var runtime = Village("a", "b", "c", "d");
        runtime.AdjustAxis("a", "b", "affinity", 4);
        runtime.Remember("a", "c", "heard-kindness", 1000);
        runtime.SetBond("a", "d", "sworn-rival", true);
        runtime.Advance(24, Fixture.Spring);
        Assert.Null(runtime.Edge("a", "b"));
        Assert.NotNull(runtime.Edge("a", "c"));
        Assert.NotNull(runtime.Edge("a", "d"));
    }

    [Fact]
    public void ATraitOnTheHolderScalesWhatItGainsOrLosesOnAnAxis()
    {
        var runtime = Fixture.Runtime();
        runtime.AddActor("kind", ["kind"]);
        runtime.AddActor("grumpy", ["grumpy"]);
        runtime.AddActor("stoic", ["stoic"]);
        runtime.AddActor("plain", []);
        foreach (var holder in new[] { "kind", "grumpy", "stoic", "plain" })
        {
            runtime.AdjustAxis(holder, "plain" == holder ? "kind" : "plain", "affinity", 100);
            runtime.AdjustAxis(holder, "plain" == holder ? "kind" : "plain", "trust", 100);
        }
        Assert.Equal((120, 100), (Affinity(runtime, "kind", "plain"), Trust(runtime, "kind", "plain")));
        Assert.Equal((70, 80), (Affinity(runtime, "grumpy", "plain"), Trust(runtime, "grumpy", "plain")));
        Assert.Equal((100, 100), (Affinity(runtime, "stoic", "plain"), Trust(runtime, "stoic", "plain")));
        runtime.AdjustAxis("stoic", "plain", "affinity", -100);
        Assert.Equal(100 - 60, Affinity(runtime, "stoic", "plain"));    // a loss is scaled by 600
        runtime.AdjustAxis("kind", "plain", "affinity", -100);
        Assert.Equal(120 - 100, Affinity(runtime, "kind", "plain"));    // kind scales gains only
    }

    // ---- stacking and bounds of memories ----------------------------------------------------------------------------------------------

    [Fact]
    public void MemoriesFollowTheirStackingModes()
    {
        var runtime = Village("a", "b");
        var insulted = runtime.Set.MemoryById["was-insulted"].Index;
        for (var i = 0; i < 4; i++) { runtime.Advance(i + 1, Fixture.Spring); runtime.Remember("a", "b", "was-insulted", 1000); }
        Assert.Equal(3, runtime.MemoryCount("a", "b", insulted));      // stack, max 3
        Assert.Equal(3 * -150, Affinity(runtime, "a", "b") - 0);
        var gifted = runtime.Set.MemoryById["was-gifted"].Index;
        runtime.Remember("a", "b", "was-gifted", 1000);
        runtime.Advance(6, Fixture.Spring);
        runtime.Remember("a", "b", "was-gifted", 1000);
        Assert.Equal(1, runtime.MemoryCount("a", "b", gifted));        // refresh
        Assert.Equal(6, runtime.Edge("a", "b")!.Memories.Single(m => m.Memory == gifted).Start);
        var theft = runtime.Set.MemoryById["saw-theft"].Index;
        runtime.Remember("a", "b", "saw-theft", 1000);
        runtime.Advance(9, Fixture.Spring);
        Assert.Empty(runtime.Remember("a", "b", "saw-theft", 1000).Memories);   // unique
        Assert.Equal(6, runtime.Edge("a", "b")!.Memories.Single(m => m.Memory == theft).Start);
    }

    [Fact]
    public void ARepeatedRememberOfAStrongerReplaceMemoryTakesOverAndAWeakerOneDoesNot()
    {
        var text = Edit.Doc(r => Edit.Memory(r, "shared-feast")["stack"] = JsonNode.Parse("{\"mode\":\"replace\",\"max\":1}"));
        var runtime = RelationshipRuntime.Create(RelationshipLoader.Load(text));
        runtime.AddActor("a", []);
        runtime.AddActor("b", []);
        runtime.Remember("a", "b", "shared-feast", 600);
        Assert.Empty(runtime.Remember("a", "b", "shared-feast", 300).Memories);
        Assert.Single(runtime.Remember("a", "b", "shared-feast", 800).Memories);
        Assert.Equal(800, runtime.Edge("a", "b")!.Memories.Single().Scale);
    }

    [Fact]
    public void MoreMemoriesThanTheLimitEvictTheWeakestNowAndSayWhich()
    {
        var runtime = With(r => r["limits"]!["maxMemoriesPerEdge"] = 2, "a", "b");
        runtime.Remember("a", "b", "was-insulted", 1000);     // strength 150 + 50 = 200
        runtime.Remember("a", "b", "shared-feast", 1000);     // 80
        var changes = runtime.Remember("a", "b", "was-gifted", 1000);   // 120 + 30 = 150 pushes out the weakest: shared-feast
        Assert.Equal([new Evicted("memory", "a", "b:shared-feast")], changes.Evictions);
        Assert.Equal(["was-insulted", "was-gifted"], runtime.Edge("a", "b")!.Memories.Select(m => runtime.Set.Memories[m.Memory].Id));
    }

    // ---- bounds of edges --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnActorWithAsManyEdgesAsAllowedLosesTheLeastSignificantToMakeRoom()
    {
        var runtime = With(r => r["limits"]!["maxEdgesPerActor"] = 2, "a", "b", "c", "d");
        runtime.AdjustAxis("a", "b", "affinity", 300);
        runtime.AdjustAxis("a", "c", "affinity", 100);
        var changes = runtime.AdjustAxis("a", "d", "affinity", 50);
        Assert.Equal([new Evicted("edge", "a", "c")], changes.Evictions);
        Assert.Equal(["b", "d"], runtime.Actor("a").Edges.Keys);
    }

    [Fact]
    public void AnEdgeWithABondIsKeptBeforeAnyWithout()
    {
        var runtime = With(r => r["limits"]!["maxEdgesPerActor"] = 2, "a", "b", "c", "d");
        runtime.AdjustAxis("a", "b", "affinity", 100);
        runtime.SetBond("a", "c", "sworn-rival", true);
        var changes = runtime.AdjustAxis("a", "d", "affinity", 5);
        Assert.Equal([new Evicted("edge", "a", "b")], changes.Evictions);
    }

    [Fact]
    public void EvictingAnEdgeThatHoldsASymmetricBondClearsTheBondOnTheWayBackToo()
    {
        var runtime = With(r => r["limits"]!["maxEdgesPerActor"] = 1, "a", "b", "c");
        runtime.SetBond("a", "b", "sworn-rival", true);
        Assert.True(runtime.HasBond("b", "a", 1));
        var changes = runtime.AdjustAxis("a", "c", "affinity", 50);          // a holds one edge only, so the one to b (the only one, bonded) goes
        Assert.Equal([new Evicted("edge", "a", "b")], changes.Evictions);
        Assert.Contains(new BondChanged("a", "b", 1, false), changes.Bonds);
        Assert.Contains(new BondChanged("b", "a", 1, false), changes.Bonds);
        Assert.False(runtime.HasBond("b", "a", 1));
    }

    [Fact]
    public void AmongEquallySignificantEdgesTheLeastRecentlyTouchedGoesThenTheLowerId()
    {
        var runtime = With(r => r["limits"]!["maxEdgesPerActor"] = 2, "a", "b", "c", "d", "e");
        runtime.AdjustAxis("a", "c", "affinity", 100);
        runtime.Advance(3, Fixture.Spring);
        runtime.AdjustAxis("a", "b", "affinity", 100);
        var older = runtime.AdjustAxis("a", "d", "affinity", 200);
        Assert.Equal([new Evicted("edge", "a", "c")], older.Evictions);      // c was touched at step 0, b at 3
        var tie = With(r => r["limits"]!["maxEdgesPerActor"] = 2, "a", "b", "c", "d");
        tie.AdjustAxis("a", "c", "affinity", 100);
        tie.AdjustAxis("a", "b", "affinity", 100);
        Assert.Equal([new Evicted("edge", "a", "b")], tie.AdjustAxis("a", "d", "affinity", 200).Evictions);   // same step, same size: the lower id
    }

    [Fact]
    public void RemovingAnActorRemovesEveryOpinionOfIt()
    {
        var runtime = Village("a", "b", "c");
        runtime.AdjustAxis("a", "b", "affinity", 50);
        runtime.AdjustAxis("c", "b", "affinity", 50);
        runtime.AdjustAxis("b", "a", "affinity", 50);
        runtime.RemoveActor("b");
        Assert.Equal(0, runtime.EdgeCount);
        Assert.False(runtime.Actors.ContainsKey("b"));
        Assert.Equal(RelationshipCodes.UnknownActor, Fixture.Refusal(() => runtime.Opinion("a", "b", 0)));
    }

    [Fact]
    public void TheEdgeFactsAreTheOpinionsOfTheTwo()
    {
        var runtime = Village("a", "b");
        runtime.AdjustAxis("a", "b", "affinity", 150);
        runtime.AdjustAxis("b", "a", "affinity", -50);
        var facts = RelationshipFacts.Read(runtime, "a", "b");
        Assert.Equal(150, facts["edge.affinity"].Integer);
        Assert.Equal(-50, facts["reverse.affinity"].Integer);
        Assert.Equal("acquaintance", facts["edge.state.friendship"].Text);
        Assert.Equal("stranger", facts["reverse.state.friendship"].Text);
        Assert.True(facts["edge.known"].Boolean);
        Assert.Equal(0, facts["edge.memory.was-gifted"].Integer);
    }
}
