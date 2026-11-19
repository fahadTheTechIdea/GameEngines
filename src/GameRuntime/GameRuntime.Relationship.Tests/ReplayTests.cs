using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Relationship.Tests;

/// <summary>Checkpoint, journal and replay (R14): a recorded run replayed from a checkpoint, or from the start, ends in the same state and raises the same events.</summary>
public sealed class ReplayTests
{
    /// <summary>A scripted life: three people, activities, a gift with a witness, a chat, a grant, a memory, an axis move, a meal with noise, a bad request, and the host's season and weather changing.</summary>
    private static RelHost Live(int toTick, int checkpointTick, out byte[]? checkpoint, out int linesAtCheckpoint, out long componentTick)
    {
        var host = new RelHost(ticksPerStep: 10, journal: true);
        checkpoint = null;
        linesAtCheckpoint = 0;
        componentTick = 0;
        for (var tick = 1; tick <= toTick; tick++)
        {
            switch (tick)
            {
                case 1: host.Add("a", "kind"); host.Add("b", "grumpy;glutton"); host.Add("c", "sociable", "flower=2"); break;
                case 3: host.Session.Submit(new RelationshipActivityRequested(host.NextId(), "a", "eat")); host.Session.Submit(new RelationshipActivityRequested(host.NextId(), "b", "sleep")); break;
                case 40: host.HostFacts["cal.season"] = FactValue.From("winter"); break;
                case 70: host.Session.Submit(new RelationshipInteractRequested(host.NextId(), "give-gift", "b", "c", "a", "flowers")); break;
                case 120: host.Session.Submit(new RelationshipInteractRequested(host.NextId(), "chat", "a", "b")); break;
                case 130: host.Session.Submit(new RelationshipGrantRequested(host.NextId(), "c", "festival-joy", 800)); break;
                case 160: host.Session.Submit(new RelationshipActivityRequested(host.NextId(), "nobody", "eat")); break;   // refused: recorded and replayed all the same
                case 200: host.HostFacts["weather.raining"] = FactValue.From(true); host.Session.Submit(new RelationshipRememberRequested(host.NextId(), "a", "c", "was-insulted", 1000)); break;
                case 210: host.Session.Submit(new RelationshipAxisRequested(host.NextId(), "c", "a", "affinity", 300)); break;
                case 260: host.Session.Submit(new RelationshipInteractRequested(host.NextId(), "share-meal", "a", "c")); break;
                case 300: host.Session.Submit(new RelationshipMoodRequested(host.NextId(), "c")); host.Session.Submit(new RelationshipOpinionRequested(host.NextId(), "a", "c", "affinity")); break;
                case 330: host.Session.Submit(new RelationshipBondRequested(host.NextId(), "a", "c", "sworn-rival", true)); break;
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
    public void ARunReplayedFromACheckpointEndsInTheSameStateAndRaisesTheSameEvents()
    {
        var live = Live(400, 150, out var checkpoint, out var linesAtCheckpoint, out var tick);
        var entries = RelationshipJournal.Parse(live.Journal.ToText());
        var replay = RelationshipReplay.Run(RelationshipCheckpoint.Open(Samples.Village, checkpoint!), entries, tick, 400, RelHost.TicksPerSecond);
        Assert.Equal(live.Runtime.StateDigest(), replay.StateDigest);
        var liveTail = Digest.Of(string.Join('\n', live.Transcript.Lines.Skip(linesAtCheckpoint)));
        Assert.Equal(liveTail, replay.TranscriptHash);
        Assert.Equal(live.Transcript.Lines.Count - linesAtCheckpoint, replay.Events);
        Assert.True(replay.Events > 20, "the replay should have raised a real run of events, not " + replay.Events);
        Assert.Contains("answered=", replay.Fingerprint, StringComparison.Ordinal);
    }

    [Fact]
    public void ARunReplayedFromTheStartEndsInTheSameStateAndRaisesExactlyTheSameEvents()
    {
        var live = Live(400, 0, out _, out _, out _);
        var entries = RelationshipJournal.Parse(live.Journal.ToText());
        var replay = RelationshipReplay.Run(RelationshipRuntime.Create(Samples.Village), entries, 0, 400, RelHost.TicksPerSecond);
        Assert.Equal(live.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(live.Transcript.Hash, replay.TranscriptHash);
        Assert.Equal(live.Transcript.Lines.Count, replay.Events);
    }

    [Fact]
    public void AReplayFromEveryCheckpointTickThatIsQuietEndsInTheSameState()
    {
        var live = Live(400, 0, out _, out _, out _);
        var entries = RelationshipJournal.Parse(live.Journal.ToText());
        var tried = 0;
        foreach (var from in new[] { 20, 69, 75, 125, 135, 199, 205, 259, 270, 331, 399 })
        {
            var again = Live(from, from, out var checkpoint, out var lines, out var tick);
            if (checkpoint is null) continue;
            var replay = RelationshipReplay.Run(RelationshipCheckpoint.Open(Samples.Village, checkpoint), entries, tick, 400, RelHost.TicksPerSecond);
            Assert.Equal(live.Runtime.StateDigest(), replay.StateDigest);
            tried++;
            _ = again; _ = lines;
        }
        Assert.True(tried >= 8, "only " + tried + " checkpoints were taken");
    }

    [Fact]
    public void AReplayWithARequestMissingFromTheJournalDoesNotReachTheSameState()
    {
        var live = Live(400, 0, out _, out _, out _);
        var lines = live.Journal.Lines.Where(l => !l.Contains("|give-gift", StringComparison.Ordinal) && !l.Contains("|interact|", StringComparison.Ordinal) || !l.StartsWith("R|", StringComparison.Ordinal)).ToList();
        Assert.True(lines.Count < live.Journal.Lines.Count);
        var replay = RelationshipReplay.Run(RelationshipRuntime.Create(Samples.Village), RelationshipJournal.Parse(string.Join('\n', lines)), 0, 400, RelHost.TicksPerSecond);
        Assert.NotEqual(live.Runtime.StateDigest(), replay.StateDigest);
    }

    [Fact]
    public void AReplayWithADifferentHostFactDoesNotRaiseTheSameEvents()
    {
        var live = Live(400, 0, out _, out _, out _);
        var lines = live.Journal.Lines.Select(l => l.Replace("cal.season=S:winter", "cal.season=S:spring", StringComparison.Ordinal)).ToList();
        Assert.NotEqual(live.Journal.Lines, lines);
        var replay = RelationshipReplay.Run(RelationshipRuntime.Create(Samples.Village), RelationshipJournal.Parse(string.Join('\n', lines)), 0, 400, RelHost.TicksPerSecond);
        Assert.NotEqual(live.Transcript.Hash, replay.TranscriptHash);   // the needs end at 0 either way, so it is the events on the way that differ
    }

    [Fact]
    public void ARefusedRequestIsInTheJournalAndIsRefusedAgainInTheReplay()
    {
        var live = Live(400, 0, out _, out _, out _);
        Assert.Contains(live.Journal.Lines, l => l.StartsWith("R|160|", StringComparison.Ordinal) && l.Contains("|activity|", StringComparison.Ordinal));
        var replay = RelationshipReplay.Run(RelationshipRuntime.Create(Samples.Village), RelationshipJournal.Parse(live.Journal.ToText()), 0, 400, RelHost.TicksPerSecond);
        Assert.Equal(live.Transcript.Hash, replay.TranscriptHash);
        Assert.Single(live.Of<RelationshipRefused>());
    }

    [Fact]
    public void AReplayNeedsATickRateThatDividesAMillionAndAnOrderlyRange()
    {
        var runtime = RelationshipRuntime.Create(Samples.Village);
        Assert.Equal(RelationshipCodes.StepOrder, Fixture.Code(() => RelationshipReplay.Run(runtime, [], 0, 10, 60)));
        Assert.Equal(RelationshipCodes.StepOrder, Fixture.Code(() => RelationshipReplay.Run(runtime, [], 10, 5, 50)));
        Assert.Equal(RelationshipCodes.StepOrder, Fixture.Code(() => RelationshipReplay.Run(runtime, [], -1, 5, 50)));
    }

    // ---- the journal text ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AJournalIsTextThatParsesBackToTheSameEntriesIncludingTextWithSeparatorsInIt()
    {
        var host = new RelHost(ticksPerStep: 10, journal: true);
        host.Session.Submit(new RelationshipActivityRequested("id|x;y=z%", "a;b|c", "d=e%f\ng"));
        host.Tick(25);
        var entries = RelationshipJournal.Parse(host.Journal.ToText());
        Assert.Equal(host.Journal.Lines.Count, entries.Count);
        var request = Assert.Single(entries, e => e.Kind is not null);
        Assert.Equal(("activity", "id|x;y=z%", "a;b|c", "d=e%f\ng"), (request.Kind, request.RequestId, request.A, request.B));
        Assert.Equal(new long?[] { 0, 1, 2 }, entries.Where(e => e.Step is not null).Select(e => e.Step));
        Assert.Contains(entries, e => e.Facts is not null && e.Facts["cal.season"].Text == "spring");
    }

    [Theory]
    [InlineData("x")]
    [InlineData("F|1")]
    [InlineData("F|a|cal.season=S:x")]
    [InlineData("F|1|cal.season")]
    [InlineData("F|1|cal.season=Q:x")]
    [InlineData("F|1|n=I:abc")]
    [InlineData("F|1|n=B:maybe")]
    [InlineData("F|1|n=I:1;n=I:2")]
    [InlineData("S|1")]
    [InlineData("S|1|x")]
    [InlineData("S|1|-5")]
    [InlineData("R|1|c|nokind|id|a|b|c|d|e|1|2")]
    [InlineData("R|1|c|actor-add|id|a|b|c|d|e|x|2")]
    [InlineData("R|1|c|actor-add|id|a|b|c|d|e|1|y")]
    [InlineData("R|1|c|actor-add|id|a|b|c|d|e|1")]
    [InlineData("F|5|a=B:true\nF|4|a=B:false")]
    public void AMalformedJournalIsRefusedWithItsCode(string text) => Assert.Equal(RelationshipJournal.BadJournal, Fixture.Code(() => RelationshipJournal.Parse(text)));

    [Fact]
    public void AnEmptyJournalIsNoEntries()
    {
        Assert.Empty(RelationshipJournal.Parse(""));
        Assert.Throws<ArgumentNullException>(() => RelationshipJournal.Parse(null!));
    }

    [Fact]
    public void TheJournalClockGivesTheStepOfTheLatestEntryAtOrBeforeTheTickAndTheStartBeforeAny()
    {
        var entries = RelationshipJournal.Parse("S|5|3\nS|9|4\nF|9|a=B:true\nS|20|8");
        var clock = new JournalRelationshipClock(entries, 2);
        Assert.Equal([2L, 2, 3, 3, 4, 4, 8, 8], new long[] { 0, 4, 5, 8, 9, 19, 20, 99 }.Select(clock.StepAt));
    }

    [Fact]
    public void ARunWithNoJournalWritesNothingAndOneWithAJournalWritesFactsStepsAndRequestsInOrderOfTick()
    {
        var plain = new RelHost();
        plain.Add("a");
        plain.Tick(3);
        Assert.Empty(plain.Journal.Lines);
        var live = Live(400, 0, out _, out _, out _);
        var ticks = RelationshipJournal.Parse(live.Journal.ToText()).Select(e => e.Tick).ToList();
        Assert.Equal(ticks.OrderBy(t => t), ticks);
        Assert.StartsWith("F|1|", live.Journal.Lines[0], StringComparison.Ordinal);
        Assert.Contains("S|10|1", live.Journal.Lines);
        Assert.True(live.Journal.Lines.Count(l => l.StartsWith("S|", StringComparison.Ordinal)) >= 39);
    }
}
