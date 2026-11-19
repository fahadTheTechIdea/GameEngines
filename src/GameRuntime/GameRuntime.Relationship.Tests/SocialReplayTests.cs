using EngineCommon;
using RelationshipEngine;
using Xunit;

namespace GameRuntime.Relationship.Tests;

/// <summary>
/// The derived social state in the game layer (communities, standing, balance pressure): the component raises its changes as events at the day boundary, in order and none lost, a checkpoint taken before a boundary
/// and replayed across several ends in the same state and the same events, and the facts a dialogue condition reads are the engine's.
/// </summary>
public sealed class SocialReplayTests
{
    private const int TicksPerDay = 240;   // ticksPerStep 10, 24 steps a day

    /// <summary>Six villagers in two circles, a cross-circle friendship that presses p0's opinion of p3 (through p1), and at tick 900 p1 turns on p3.</summary>
    private static RelHost Live(int toTick, int checkpointTick, out byte[]? checkpoint, out int linesAtCheckpoint, out long componentTick, RelationshipSet? set = null)
    {
        var host = new RelHost(set, ticksPerStep: 10, journal: true);
        checkpoint = null;
        linesAtCheckpoint = 0;
        componentTick = 0;
        for (var tick = 1; tick <= toTick; tick++)
        {
            switch (tick)
            {
                case 1:
                    for (var i = 0; i < 6; i++) host.Add("p" + i, i == 0 ? "kind" : "");
                    break;
                case 2:
                    foreach (var circle in new[] { new[] { "p0", "p1", "p2" }, new[] { "p3", "p4", "p5" } })
                        foreach (var a in circle)
                            foreach (var b in circle.Where(b => b != a))
                            {
                                host.Session.Submit(new RelationshipAxisRequested(host.NextId(), a, b, "affinity", 500));
                                host.Session.Submit(new RelationshipAxisRequested(host.NextId(), a, b, "trust", 300));
                            }
                    host.Session.Submit(new RelationshipAxisRequested(host.NextId(), "p1", "p3", "affinity", 500));
                    host.Session.Submit(new RelationshipAxisRequested(host.NextId(), "p0", "p3", "affinity", 50));
                    break;
                case 3:
                    foreach (var from in new[] { "p1", "p2", "p3", "p4", "p5" }) host.Session.Submit(new RelationshipAxisRequested(host.NextId(), from, "p0", "trust", 500));
                    break;
                case 900: host.Session.Submit(new RelationshipAxisRequested(host.NextId(), "p1", "p3", "affinity", -900)); break;
                case 1100: host.Session.Submit(new RelationshipInteractRequested(host.NextId(), "chat", "p2", "p4")); break;
            }
            host.Tick();
            if (tick == checkpointTick)
            {
                checkpoint = RelationshipCheckpoint.Capture(host.Component);
                linesAtCheckpoint = host.Transcript.Lines.Count;
                componentTick = host.Component.Tick;
            }
        }
        return host;
    }

    [Fact]
    public void TheDayBoundaryRaisesTheCommunityStandingAndBalanceChangesAsEventsInTheirOrder()
    {
        var host = Live(3 * TicksPerDay + 1, 0, out _, out _, out _);
        var communities = host.Of<RelationshipCommunityChanged>().Select(e => e.Actor + ">" + e.To).ToList();
        Assert.Equal(["p1>p0", "p2>p0", "p4>p3", "p5>p3"], communities.Order(StringComparer.Ordinal));
        Assert.Contains(host.Of<RelationshipStandingChanged>(), e => e is { Actor: "p0" } && e.To > e.From);
        var shift = host.Of<RelationshipBalanceShifted>().First();
        // p0 is kind (its gains of affinity count 1.2x: 600) and every affinity drifts 4 toward the baseline on the way to the boundary: 596 and 496.
        Assert.Equal(("p0", "p3", "p1", 596L, 496L, 1000), (shift.Holder, shift.Subject, shift.Via, shift.HolderVia, shift.ViaSubject, shift.Scale));
        // Order in one Update: the changes of a boundary are community changes, then standing changes, then balance pushes, after the step changes.
        var order = host.All.Select(e => e switch { RelationshipCommunityChanged => 1, RelationshipStandingChanged => 2, RelationshipBalanceShifted => 3, RelationshipStepped => 0, _ => -1 }).Where(k => k >= 0).ToList();
        var firstBoundary = order.IndexOf(1);
        Assert.True(order.Skip(firstBoundary).TakeWhile(k => k != 0).SequenceEqual(order.Skip(firstBoundary).TakeWhile(k => k != 0).Order()), "community, standing, balance in that order");
    }

    [Fact]
    public void TheDocumentFactsOfTheSocialStateAreWhatTheEngineSays()
    {
        var host = Live(3 * TicksPerDay + 1, 0, out _, out _, out _);
        var facts = RelationshipFacts.Read(host.Runtime, "p2", "p4", "ext.");
        Assert.Equal("p0", facts["ext.actor.community"].Text);
        Assert.Equal(3, facts["ext.actor.community_size"].Integer);
        Assert.Equal("p3", facts["ext.target.community"].Text);
        Assert.False(facts["ext.edge.same_community"].Boolean);
        Assert.Equal(host.Runtime.Actor("p2").Standing, facts["ext.actor.standing"].Integer);
        Assert.True(host.Runtime.Actor("p0").Standing > host.Runtime.Actor("p5").Standing);   // the circle that trusts p0 and the pre-trusted kind one
    }

    [Theory]
    [InlineData(100)]      // before the first boundary
    [InlineData(239)]      // the tick before it
    [InlineData(241)]      // the tick after it
    [InlineData(700)]
    [InlineData(1079)]     // between the flip of p1 and the next boundary
    public void ARunReplayedFromACheckpointAcrossSeveralDayBoundariesEndsInTheSameStateAndEvents(int checkpointTick)
    {
        var live = Live(1500, checkpointTick, out var checkpoint, out var linesAtCheckpoint, out var tick);
        Assert.NotNull(checkpoint);
        var entries = RelationshipJournal.Parse(live.Journal.ToText());
        var replay = RelationshipReplay.Run(RelationshipCheckpoint.Open(Samples.Village, checkpoint!), entries, tick, 1500, RelHost.TicksPerSecond);
        Assert.Equal(live.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(Digest.Of(string.Join('\n', live.Transcript.Lines.Skip(linesAtCheckpoint))), replay.TranscriptHash);
        Assert.True(live.Of<RelationshipCommunityChanged>().Any() && live.Of<RelationshipStandingChanged>().Any() && live.Of<RelationshipBalanceShifted>().Any(), "the run exercised all three");
    }


    [Theory]
    [InlineData(241)]      // step 24 lived: the upkeep only
    [InlineData(251)]      // step 25: communities done, standing, balance and gossip still to come
    [InlineData(261)]      // step 26
    [InlineData(271)]      // step 27
    [InlineData(281)]      // step 28: the last stage done
    [InlineData(291)]
    [InlineData(1451)]     // the same, a few days on
    public void ASpreadDayCheckpointedBetweenTwoStagesReplaysToTheSameStateAndEvents(int checkpointTick)
    {
        var live = Live(1500, checkpointTick, out var checkpoint, out var linesAtCheckpoint, out var tick, Samples.Shipped);
        Assert.NotNull(checkpoint);
        var replay = RelationshipReplay.Run(RelationshipCheckpoint.Open(Samples.Shipped, checkpoint!), RelationshipJournal.Parse(live.Journal.ToText()), tick, 1500, RelHost.TicksPerSecond);
        Assert.Equal(live.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(Digest.Of(string.Join('\n', live.Transcript.Lines.Skip(linesAtCheckpoint))), replay.TranscriptHash);
        Assert.True(live.Of<RelationshipCommunityChanged>().Any() && live.Of<RelationshipStandingChanged>().Any() && live.Of<RelationshipBalanceShifted>().Any(), "the run exercised all three");
    }
    [Fact]
    public void ARunReplayedFromTheStartEndsInTheSameState()
    {
        var live = Live(1500, 0, out _, out _, out _);
        var replay = RelationshipReplay.Run(RelationshipRuntime.Create(Samples.Village), RelationshipJournal.Parse(live.Journal.ToText()), 0, 1500, RelHost.TicksPerSecond);
        Assert.Equal(live.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(live.Transcript.Hash, replay.TranscriptHash);
    }

    [Fact]
    public void ThePressureTurnsWhenTheFriendTurnsAndTheStateOfTheCheckpointRestoresThePressureMemory()
    {
        var host = Live(1500, 0, out _, out _, out _);
        var turned = host.Of<RelationshipBalanceShifted>().Where(e => e.Holder == "p0" && e.Subject == "p3").ToList();
        Assert.Equal([1000, -1000], turned.Select(e => e.Scale));              // first toward liking, then away once p1 dislikes p3
        var opened = RelationshipSave.Open(host.Runtime.Set, RelationshipSave.Write(host.Runtime));
        Assert.Equal(host.Runtime.StateDigest(), opened.StateDigest());
    }
}
