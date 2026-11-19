using System.Text.Json.Nodes;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>Ladders and bonds (R6): named states with hysteresis in both directions and no flicker; exclusive, symmetric and breaking bonds.</summary>
public sealed class LadderBondTests
{
    private static RelationshipRuntime Village(params string[] names)
    {
        var runtime = Fixture.Runtime();
        foreach (var name in names) runtime.AddActor(name, []);
        return runtime;
    }

    private static string Friendship(RelationshipRuntime runtime, string from, string to) => runtime.LadderStateId(from, to, 0);

    [Fact]
    public void ANewEdgeStartsInTheStepOfTheBaseline()
    {
        var runtime = Village("a", "b");
        Assert.Equal("stranger", Friendship(runtime, "a", "b"));
        runtime.AdjustAxis("a", "b", "trust", 0);
        Assert.Equal("unknown", runtime.LadderStateId("a", "b", 1));
    }

    [Fact]
    public void ACrossingUpIsAnnouncedOnceAndTheNewStateIsReadBack()
    {
        var runtime = Village("a", "b");
        var up = runtime.AdjustAxis("a", "b", "affinity", 120);
        var change = Assert.Single(up.States);
        Assert.Equal(("a", "b", "stranger", "acquaintance"), (change.Actor, change.Target, runtime.Set.Ladders[0].Steps[change.From].Id, runtime.Set.Ladders[0].Steps[change.To].Id));
        Assert.Empty(runtime.AdjustAxis("a", "b", "affinity", 5).States);
        Assert.Equal("acquaintance", Friendship(runtime, "a", "b"));
        Assert.Equal("acquaintance", RelationshipFacts.Read(runtime, "a", "b")["edge.state.friendship"].Text);
    }

    [Fact]
    public void LeavingAStepDownNeedsTheValueToFallBelowItsStartByMoreThanTheHysteresis()
    {
        var runtime = Village("a", "b");
        runtime.AdjustAxis("a", "b", "affinity", 120);                       // acquaintance from 100, hysteresis 60: the line down is 40
        runtime.AdjustAxis("a", "b", "affinity", -75);                       // 45
        Assert.Equal("acquaintance", Friendship(runtime, "a", "b"));
        runtime.AdjustAxis("a", "b", "affinity", -5);                        // 40: on the line, still in
        Assert.Equal("acquaintance", Friendship(runtime, "a", "b"));
        var down = runtime.AdjustAxis("a", "b", "affinity", -1);             // 39
        Assert.Equal("stranger", Friendship(runtime, "a", "b"));
        Assert.Single(down.States);
        runtime.AdjustAxis("a", "b", "affinity", 60);                        // 99: not yet back up
        Assert.Equal("stranger", Friendship(runtime, "a", "b"));
        runtime.AdjustAxis("a", "b", "affinity", 1);                         // 100
        Assert.Equal("acquaintance", Friendship(runtime, "a", "b"));
    }

    [Fact]
    public void AMemoryThatFadesLowersTheStateOnlyAtTheDayBoundary()
    {
        var runtime = Village("a", "b");
        runtime.Remember("a", "b", "was-gifted", 1000);                      // +120 affinity, halves every 240 steps
        Assert.Equal("acquaintance", Friendship(runtime, "a", "b"));
        var fell = new List<long>();
        for (var step = 1; step <= 480; step++)
            if (runtime.Advance(step, Fixture.Spring).States.Any(s => s.Ladder == 0 && runtime.Set.Ladders[0].Steps[s.To].Id == "stranger")) fell.Add(step);
        var at = Assert.Single(fell);
        Assert.Equal(0, at % 24);
        Assert.True(runtime.Opinion("a", "b", 0) < 40);
        // At the step before the boundary the opinion was already below the line: nothing was announced in between.
        Assert.True(at > 24);
    }

    [Fact]
    public void AStateNeverDiffersFromThePlainStateByMoreThanTheMarginForRandomWalks()
    {
        var seed = 99UL;
        int Next(int bound) { seed = seed * 6364136223846793005UL + 1442695040888963407UL; return (int)((seed >> 33) % (ulong)bound); }
        var steps = Fixture.Village.Ladders[0].Steps;
        var margin = Fixture.Village.Ladders[0].Hysteresis;
        var transitions = 0;
        var moves = 0;
        for (var walk = 0; walk < 30; walk++)
        {
            var runtime = Village("a", "b");
            var previous = Friendship(runtime, "a", "b");
            for (var i = 0; i < 300; i++)
            {
                runtime.AdjustAxis("a", "b", "affinity", Next(161) - 80);
                moves++;
                var value = runtime.Opinion("a", "b", 0);
                var plain = DecayMath.PlainStep(steps, value);
                var state = runtime.Set.Ladders[0].Steps.ToList().FindIndex(s => s.Id == Friendship(runtime, "a", "b"));
                Assert.True(state >= plain, $"a rise must be immediate: value {value}, state {state}, plain {plain}");
                if (state > plain) Assert.True(value >= steps[state].From - margin, $"value {value} is more than the margin below the start of step {state}");
                if (Friendship(runtime, "a", "b") != previous) transitions++;
                previous = Friendship(runtime, "a", "b");
            }
        }
        // Without the margin a walk of this size would change step on a good part of its moves.
        Assert.True(transitions > 100, "only " + transitions + " changes in " + moves + " moves");
    }

    // ---- bonds ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ASymmetricBondIsHeldByBothAndAnnouncedBothWays()
    {
        var runtime = Village("a", "b");
        var changes = runtime.SetBond("a", "b", "spouse", true);
        Assert.Equal([new BondChanged("a", "b", 0, true), new BondChanged("b", "a", 0, true)], changes.Bonds);
        Assert.True(runtime.HasBond("b", "a", 0));
        Assert.True(RelationshipFacts.Read(runtime, "b", "a")["edge.bond.spouse"].Boolean);
        Assert.Empty(runtime.SetBond("a", "b", "spouse", true).Bonds);    // already so
    }

    [Fact]
    public void AnExclusiveBondMovesWhenAnotherIsSetAndTheOldPartnerLosesItToo()
    {
        var runtime = Village("a", "b", "c", "d");
        runtime.SetBond("a", "b", "spouse", true);
        runtime.SetBond("c", "d", "spouse", true);
        var changes = runtime.SetBond("a", "c", "spouse", true);
        Assert.False(runtime.HasBond("a", "b", 0));
        Assert.False(runtime.HasBond("b", "a", 0));
        Assert.False(runtime.HasBond("c", "d", 0));
        Assert.False(runtime.HasBond("d", "c", 0));
        Assert.True(runtime.HasBond("a", "c", 0));
        Assert.True(runtime.HasBond("c", "a", 0));
        Assert.Equal(4, changes.Bonds.Count(b => !b.Set));
        Assert.Equal(2, changes.Bonds.Count(b => b.Set));
    }

    [Fact]
    public void ANonExclusiveBondMayBeHeldTowardSeveralActors()
    {
        var runtime = Village("a", "b", "c");
        runtime.SetBond("a", "b", "sworn-rival", true);
        runtime.SetBond("a", "c", "sworn-rival", true);
        Assert.True(runtime.HasBond("a", "b", 1));
        Assert.True(runtime.HasBond("a", "c", 1));
    }

    [Fact]
    public void ABondThatIsNotSymmetricIsHeldOneWayOnly()
    {
        var text = Edit.Doc(r => r["bonds"]![1]!["symmetric"] = false);
        var runtime = RelationshipRuntime.Create(RelationshipLoader.Load(text));
        runtime.AddActor("a", []);
        runtime.AddActor("b", []);
        var changes = runtime.SetBond("a", "b", "sworn-rival", true);
        Assert.Equal([new BondChanged("a", "b", 1, true)], changes.Bonds);
        Assert.False(runtime.HasBond("b", "a", 1));
        runtime.SetBond("b", "a", "sworn-rival", true);
        Assert.Equal([new BondChanged("a", "b", 1, false)], runtime.SetBond("a", "b", "sworn-rival", false).Bonds);
        Assert.True(runtime.HasBond("b", "a", 1));
    }

    [Fact]
    public void ABondBreaksWhenItsAxisFallsBelowTheLineOnBothSidesAndIsAnnounced()
    {
        var runtime = Village("a", "b");
        runtime.SetBond("a", "b", "spouse", true);                        // breaks when affinity < -200
        Assert.Empty(runtime.AdjustAxis("a", "b", "affinity", -200).Bonds);
        Assert.True(runtime.HasBond("a", "b", 0));
        var changes = runtime.AdjustAxis("a", "b", "affinity", -1);       // -201
        Assert.Equal([new BondChanged("a", "b", 0, false), new BondChanged("b", "a", 0, false)], changes.Bonds);
        Assert.False(runtime.HasBond("b", "a", 0));
    }

    [Fact]
    public void ABondAlsoBreaksWhenAMemoryPullsTheOpinionBelowItsLine()
    {
        var runtime = Village("a", "b");
        runtime.SetBond("a", "b", "spouse", true);
        runtime.AdjustAxis("a", "b", "affinity", -100);
        runtime.Remember("a", "b", "was-insulted", 1000);                // -150: -250 in all
        Assert.False(runtime.HasBond("a", "b", 0));
    }

    [Fact]
    public void ABondWithNoBreakLineLastsUntilItIsCleared()
    {
        var runtime = Village("a", "b");
        runtime.SetBond("a", "b", "sworn-rival", true);
        runtime.AdjustAxis("a", "b", "affinity", -1000);
        runtime.Advance(240, Fixture.Spring);
        Assert.True(runtime.HasBond("a", "b", 1));
        runtime.SetBond("a", "b", "sworn-rival", false);
        Assert.False(runtime.HasBond("a", "b", 1));
        Assert.False(runtime.HasBond("b", "a", 1));
    }

    [Fact]
    public void ClearingABondThatIsNotHeldChangesNothing()
    {
        var runtime = Village("a", "b");
        Assert.False(runtime.SetBond("a", "b", "spouse", false).Any);
        Assert.Equal(0, runtime.EdgeCount);
    }
}
