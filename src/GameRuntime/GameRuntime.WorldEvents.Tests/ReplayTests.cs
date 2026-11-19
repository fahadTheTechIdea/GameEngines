using System.Text.Json.Nodes;
using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;
using Xunit;

namespace GameRuntime.WorldEvents.Tests;

/// <summary>Replay: a journal replayed from a checkpoint (or from a new world) on a fresh session ends in the same state and raises the same events; the journal reads strictly.</summary>
public sealed class ReplayTests
{
    private static WorldSet Lively() => Samples.Edited(root =>
    {
        root["hazards"]!.AsArray()[0]!["ratePpm"] = 60_000;
        root["diseases"]!.AsArray()[0]!["seedPpm"] = 30_000;
        root["diseases"]!.AsArray()[0]!["seedCases"] = 3;
    });

    private static readonly string[] Kinds = ["intervene", "storyteller", "difficulty", "hazard", "outbreak", "card", "weather", "disease", "dance"];

    /// <summary>A seeded stream of requests, valid and not, so the journal has refusals in it too.</summary>
    private static void Ask(WorldHost host, ulong seed, int tick)
    {
        if (DeterministicRoll.Permille(seed, "ask", tick) >= 120) return;
        var kind = Kinds[DeterministicRoll.Below(seed, "kind", tick, Kinds.Length)];
        var region = new[] { "a", "b", "zz" }[DeterministicRoll.Below(seed, "region", tick, 3)];
        switch (kind)
        {
            case "intervene": host.Ask(kind, region, new[] { "quar", "vacc", "auto" }[DeterministicRoll.Below(seed, "iv", tick, 3)], number: DeterministicRoll.Below(seed, "on", tick, 2)); break;
            case "storyteller": host.Ask(kind, new[] { "t", "calm" }[DeterministicRoll.Below(seed, "st", tick, 2)]); break;
            case "difficulty": host.Ask(kind, new[] { "normal", "hard" }[DeterministicRoll.Below(seed, "df", tick, 2)]); break;
            case "hazard": host.Ask(kind, region, "h", new[] { "low", "high" }[DeterministicRoll.Below(seed, "sv", tick, 2)]); break;
            case "outbreak": host.Ask(kind, region, "d", number: 1 + DeterministicRoll.Below(seed, "cases", tick, 30)); break;
            case "card": host.Ask(kind, new[] { "c-bad", "c-good", "c-global" }[DeterministicRoll.Below(seed, "cd", tick, 3)], region); break;
            case "weather": host.Ask(kind, region); break;
            case "disease": host.Ask(kind, region, "d"); break;
            default: host.Ask(kind, region); break;
        }
    }

    private static void Play(WorldHost host, ulong seed, int from, int to)
    {
        for (var tick = from; tick <= to; tick++)
        {
            Ask(host, seed, tick);
            host.Tick();
            if (tick % 40 == 0) host.HostFacts["ext.alert"] = FactValue.From(DeterministicRoll.Below(seed, "alert", tick, 10));
        }
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    [InlineData(6UL)]
    public void AJournalReplayedFromACheckpointEndsInTheSameStateAndRaisesTheSameEvents(ulong seed)
    {
        var set = Lively();
        var host = new WorldHost(set, journal: true);
        Play(host, seed, 1, 90 + (int)seed * 7);
        // The checkpoint is taken at the first quiet moment.
        while (!host.Component.IsQuiet || !host.Channels.IsQuiet) host.Tick();
        var checkpoint = WorldCheckpoint.Capture(host.Component);
        var checkpointTick = host.Component.Tick;
        var linesAtCheckpoint = host.Transcript.Lines.Count;
        Play(host, seed, (int)checkpointTick + 1, (int)checkpointTick + 250);
        host.Tick(3);
        var entries = WorldJournal.Parse(host.Journal.ToText());
        var replay = WorldReplay.Run(WorldCheckpoint.Open(set, checkpoint), entries, checkpointTick, host.Component.Tick, WorldHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(Digest.Of(string.Join('\n', host.Transcript.Lines.Skip(linesAtCheckpoint))), replay.TranscriptHash);
        Assert.Equal(host.Transcript.Lines.Count - linesAtCheckpoint, replay.Events);
        Assert.Contains(entries, e => e.Kind is not null);          // the journal holds requests
        Assert.Contains(entries, e => e.Facts is not null);
    }

    [Fact]
    public void AJournalReplayedFromANewWorldReproducesTheWholeRun()
    {
        var set = Lively();
        var host = new WorldHost(set, journal: true);
        Play(host, 9UL, 1, 300);
        host.Tick(2);
        var entries = WorldJournal.Parse(host.Journal.ToText());
        var replay = WorldReplay.Run(WorldRuntime.Create(set), entries, 0, host.Component.Tick, WorldHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(host.Transcript.Hash, replay.TranscriptHash);
    }

    [Fact]
    public void AClockThatCatchesUpInPartsIsReplayedStepForStep()
    {
        var set = Lively();
        var host = new WorldHost(set, journal: true, clock: new TickWorldClock(set, ticksPerStep: 1, firstStep: 400), maxSteps: 7);
        Play(host, 3UL, 1, 120);
        host.Tick(2);
        var entries = WorldJournal.Parse(host.Journal.ToText());
        Assert.Contains(entries, e => e.Step is not null && e.Seasons is { Count: > 0 });
        var replay = WorldReplay.Run(WorldRuntime.Create(set), entries, 0, host.Component.Tick, WorldHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(host.Transcript.Hash, replay.TranscriptHash);
    }

    [Fact]
    public void ACalendarsOwnSeasonsAreWhatTheJournalKeepsSoAReplayNeedsNoCalendar()
    {
        var set = Lively();
        var clock = new SeasonsBackwards(set);
        var host = new WorldHost(set, journal: true, clock: clock);
        host.Tick(45);
        var entries = WorldJournal.Parse(host.Journal.ToText());
        var replay = WorldReplay.Run(WorldRuntime.Create(set), entries, 0, host.Component.Tick, WorldHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(host.Transcript.Hash, replay.TranscriptHash);
    }

    /// <summary>A clock whose seasons are not the document's own cycle (the calendar's would not be): cold when the document would say warm.</summary>
    private sealed class SeasonsBackwards(WorldSet set) : IWorldClock
    {
        public long StepAt(long tick) => tick;
        public string SeasonAt(long step) => set.Seasons[1 - set.CycleSeason(step)].Id;
    }

    [Fact]
    public void TheReplayRefusesATickRateItCannotFeedExactlyAndARunThatGoesBackwards()
    {
        var set = Samples.Tiny;
        Assert.Equal(WorldCodes.StepOrder, Samples.Code(() => WorldReplay.Run(WorldRuntime.Create(set), [], 0, 10, 60)));
        Assert.Equal(WorldCodes.StepOrder, Samples.Code(() => WorldReplay.Run(WorldRuntime.Create(set), [], 0, 10, 0)));
        Assert.Equal(WorldCodes.StepOrder, Samples.Code(() => WorldReplay.Run(WorldRuntime.Create(set), [], 10, 5, 50)));
        Assert.Equal(WorldCodes.StepOrder, Samples.Code(() => WorldReplay.Run(WorldRuntime.Create(set), [], -1, 5, 50)));
    }

    [Fact]
    public void TheJournalIsTextOfThreeShapesAndRoundTripsWithItsEscapes()
    {
        var host = new WorldHost(journal: true);
        host.HostFacts["ext.alert"] = FactValue.From(4L);
        host.Tick(2);
        host.Ask("weather", "a|b;c=d%e\nf", "g", "h");
        host.Tick();
        var text = host.Journal.ToText();
        var entries = WorldJournal.Parse(text);
        Assert.Equal(host.Journal.Lines.Count, entries.Count);
        Assert.StartsWith("F|1|ext.alert=I:4", host.Journal.Lines[0]);
        Assert.Contains(host.Journal.Lines, l => l.StartsWith("S|1|1|0:warm*2", StringComparison.Ordinal));
        var request = entries.Single(e => e.Kind is not null);
        Assert.Equal(("weather", "a|b;c=d%e\nf", "g", "h", 1L), (request.Kind, request.A, request.B, request.C, request.Number));
        Assert.Equal(3, request.Tick);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("F|1")]
    [InlineData("F|x|a=I:1")]
    [InlineData("F|1|a=Q:1")]
    [InlineData("F|1|a=I:1;a=I:2")]
    [InlineData("F|1|a=I:one")]
    [InlineData("S|1|2")]
    [InlineData("S|1|x|0:warm*2")]
    [InlineData("S|1|2|nope")]
    [InlineData("S|1|2|0:warm*0")]
    [InlineData("S|1|2|x:warm*1")]
    [InlineData("R|1|c|k|id|a|b|c")]
    [InlineData("R|1|c|k|id|a|b|c|x")]
    [InlineData("F|5|a=I:1\nF|4|a=I:1")]
    [InlineData("Z|1|a")]
    public void AJournalThatIsNotOneOfTheThreeShapesIsRefusedWithItsLine(string text)
    {
        var code = Samples.Code(() => WorldJournal.Parse(text));
        Assert.Equal(WorldJournal.BadJournal, code);
    }

    [Fact]
    public void AnEmptyJournalIsAnEmptyList()
    {
        Assert.Empty(WorldJournal.Parse(""));
        Assert.Throws<ArgumentNullException>(() => WorldJournal.Parse(null!));
    }

    [Fact]
    public void TheJournalClockReadsTheStepsAndSeasonsOfARecordedRun()
    {
        var entries = WorldJournal.Parse("S|3|4|0:warm*3;3:cold*2\nS|9|6|5:cold*1");
        var clock = new JournalWorldClock(entries, 1);
        Assert.Equal(1, clock.StepAt(2));
        Assert.Equal(4, clock.StepAt(3));
        Assert.Equal(4, clock.StepAt(8));
        Assert.Equal(6, clock.StepAt(9));
        Assert.Equal("warm", clock.SeasonAt(2));
        Assert.Equal("cold", clock.SeasonAt(3));
        Assert.Equal("cold", clock.SeasonAt(5));
        Assert.Equal("", clock.SeasonAt(6));              // a step the recorded run never crossed has no season: the world refuses the empty id
    }
}
