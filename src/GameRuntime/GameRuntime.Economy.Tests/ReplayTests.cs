using EconomyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Economy.Tests;

/// <summary>The journal and the replay: a recorded run replayed from a checkpoint (or from the start) ends in the same state and raises the same events, and a journal that is wrong is refused.</summary>
public sealed class ReplayTests
{
    private sealed record Played(EcoHost Host, byte[]? Checkpoint, long CheckpointTick, int LinesAtCheckpoint, EconomySet Set);

    /// <summary>A scripted run of every kind of request, 60 ticks long, taking a checkpoint at the first quiet tick at or after <paramref name="checkpointAt"/>.</summary>
    private static Played Play(int checkpointAt = 20, int maxEvents = 2000, int maxOwed = EconomyComponent.DefaultMaxOwed)
    {
        var set = Samples.Fast();
        var host = new EcoHost(set: set, journal: true, maxEvents: maxEvents, maxOwed: maxOwed);
        byte[]? checkpoint = null;
        long checkpointTick = 0;
        var lines = 0;
        for (var tick = 1; tick <= 60; tick++)
        {
            switch (tick)
            {
                case 3: host.Trade("riverton", "grain", 120); break;
                case 5: host.Session.Submit(new EconomyShockRequested("shock", "drought", "riverton", 800)); break;
                case 9: host.Session.Submit(new EconomyGrowthRequested("growth", "florin", 4000)); break;
                case 12: host.Session.Submit(new EconomyLendRequested("lend", "aid", "world", "hillfort", 5000, 2000, 5, "annuity", "taxday", 2)); break;
                case 15: host.Session.Submit(new EconomyFundRequested("fund", "seaport", -900)); break;
                case 18: host.Session.Submit(new EconomySettleRequested("settle", "decree")); break;
                case 22: host.Trade("seaport", "cloth", -40); break;
                case 25: host.Session.Submit(new EconomyQuoteRequested("quote", "hillfort", "iron", 10)); break;
                case 26: host.Session.Submit(new EconomyPriceRequested("price", "hillfort", "iron")); break;
                case 27: host.Trade("nowhere", "grain", 1); break;
                case 28: host.Session.Submit(new EconomyRecipeUnlockRequested("unlock", "alloy-steel")); break;
                case 31: host.HostFacts["cal.season"] = FactValue.From("winter"); break;
                case 40: host.HostFacts["story.plague"] = FactValue.From(2L); break;
                case 41: host.Session.Submit(new EconomyShockRequested("block", "blockade", "")); break;
            }
            host.Tick();
            if (checkpoint is null && tick >= checkpointAt && host.Component.IsQuiet)
            {
                checkpoint = EconomyCheckpoint.Capture(host.Component);
                checkpointTick = host.Component.Tick;
                lines = host.Transcript.Lines.Count;
            }
        }
        return new Played(host, checkpoint, checkpointTick, lines, set);
    }

    private static string TailHash(Played run) => Digest.Of(string.Join('\n', run.Host.Transcript.Lines.Skip(run.LinesAtCheckpoint)));

    [Fact]
    public void ReplayingFromACheckpointEndsInTheSameStateAndRaisesTheSameEvents()
    {
        var run = Play();
        Assert.NotNull(run.Checkpoint);
        var entries = EconomyJournal.Parse(run.Host.Journal.ToText());
        var resumed = EconomyCheckpoint.Open(run.Set, run.Checkpoint!);
        var replay = EconomyReplay.Run(resumed, entries, run.CheckpointTick, 60, EcoHost.TicksPerSecond);
        Assert.Equal(run.Host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(TailHash(run), replay.TranscriptHash);
        Assert.Equal(run.Host.Transcript.Lines.Count - run.LinesAtCheckpoint, replay.Events);
        Assert.Equal(60, replay.Tick);
        Assert.Contains("economy tick=60", replay.Fingerprint, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayingFromTheStartOfANewEconomyReproducesTheWholeRun()
    {
        var run = Play();
        var entries = EconomyJournal.Parse(run.Host.Journal.ToText());
        var replay = EconomyReplay.Run(EconomyRuntime.Create(run.Set), entries, 0, 60, EcoHost.TicksPerSecond);
        Assert.Equal(run.Host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(run.Host.Transcript.Hash, replay.TranscriptHash);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(13)]
    [InlineData(19)]
    [InlineData(23)]
    [InlineData(28)]
    [InlineData(31)]
    [InlineData(41)]
    [InlineData(35)]
    [InlineData(44)]
    [InlineData(57)]
    public void AnyQuietTickIsAGoodPlaceToResumeFrom(int at)
    {
        var run = Play(at);
        Assert.NotNull(run.Checkpoint);
        var replay = EconomyReplay.Run(EconomyCheckpoint.Open(run.Set, run.Checkpoint!), EconomyJournal.Parse(run.Host.Journal.ToText()), run.CheckpointTick, 60, EcoHost.TicksPerSecond);
        Assert.Equal(run.Host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(TailHash(run), replay.TranscriptHash);
    }

    [Fact]
    public void AReplayWithTheSameBackPressureAsTheRunStillMatches()
    {
        // With three events raised per tick and room for 25 owed the economy lags its clock and is never quiet, so there is no checkpoint: the whole run is replayed from the start.
        var run = Play(20, maxEvents: 3, maxOwed: 25);
        Assert.Null(run.Checkpoint);
        var replay = EconomyReplay.Run(EconomyRuntime.Create(run.Set), EconomyJournal.Parse(run.Host.Journal.ToText()), 0, 60, EcoHost.TicksPerSecond, maxEvents: 3, maxOwed: 25);
        Assert.Equal(run.Host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(run.Host.Transcript.Hash, replay.TranscriptHash);
        var unbounded = EconomyReplay.Run(EconomyRuntime.Create(run.Set), EconomyJournal.Parse(run.Host.Journal.ToText()), 0, 60, EcoHost.TicksPerSecond);
        Assert.Equal(run.Host.Runtime.StateDigest(), unbounded.StateDigest);                 // the state is the journal's steps and requests: the lag only delays the events
        Assert.NotEqual(run.Host.Transcript.Hash, unbounded.TranscriptHash);
    }

    [Fact]
    public void AMissingRequestOrAChangedFactIsDetectedByTheReplay()
    {
        var run = Play();
        var good = EconomyReplay.Run(EconomyCheckpoint.Open(run.Set, run.Checkpoint!), EconomyJournal.Parse(run.Host.Journal.ToText()), run.CheckpointTick, 60, EcoHost.TicksPerSecond);
        var lines = run.Host.Journal.Lines.ToList();
        var request = lines.FindIndex(l => l.StartsWith("R|", StringComparison.Ordinal) && l.Contains("|block|", StringComparison.Ordinal));
        var without = lines.Where((_, i) => i != request).ToList();
        var missing = EconomyReplay.Run(EconomyCheckpoint.Open(run.Set, run.Checkpoint!), EconomyJournal.Parse(string.Join('\n', without)), run.CheckpointTick, 60, EcoHost.TicksPerSecond);
        Assert.NotEqual(good.StateDigest, missing.StateDigest);
        var winter = lines.FindIndex(l => l.StartsWith("F|", StringComparison.Ordinal) && l.Contains("S:winter", StringComparison.Ordinal));
        Assert.True(winter >= 0);
        var autumn = lines.Select((l, i) => i == winter ? l.Replace("S:winter", "S:autumn", StringComparison.Ordinal) : l);
        var changed = EconomyReplay.Run(EconomyCheckpoint.Open(run.Set, run.Checkpoint!), EconomyJournal.Parse(string.Join('\n', autumn)), run.CheckpointTick, 60, EcoHost.TicksPerSecond);
        Assert.NotEqual(good.StateDigest, changed.StateDigest);
    }

    [Fact]
    public void ARateThatDoesNotDivideAMillionAndAnImpossibleRangeAreRefused()
    {
        var run = Play();
        var entries = EconomyJournal.Parse(run.Host.Journal.ToText());
        Assert.Equal(EconomyCodes.StepOrder, Samples.Code(() => EconomyReplay.Run(EconomyRuntime.Create(run.Set), entries, 0, 10, 60)));
        Assert.Equal(EconomyCodes.StepOrder, Samples.Code(() => EconomyReplay.Run(EconomyRuntime.Create(run.Set), entries, 0, 10, 0)));
        Assert.Equal(EconomyCodes.StepOrder, Samples.Code(() => EconomyReplay.Run(EconomyRuntime.Create(run.Set), entries, 10, 5, 50)));
        Assert.Equal(EconomyCodes.StepOrder, Samples.Code(() => EconomyReplay.Run(EconomyRuntime.Create(run.Set), entries, -1, 5, 50)));
    }

    [Fact]
    public void ReplayingNoTicksAtAllReturnsTheStateItWasGiven()
    {
        var run = Play();
        var resumed = EconomyCheckpoint.Open(run.Set, run.Checkpoint!);
        var digest = resumed.StateDigest();
        var replay = EconomyReplay.Run(resumed, EconomyJournal.Parse(run.Host.Journal.ToText()), run.CheckpointTick, run.CheckpointTick, EcoHost.TicksPerSecond);
        Assert.Equal(digest, replay.StateDigest);
        Assert.Equal(0, replay.Events);
    }

    [Fact]
    public void ARequestForAnotherChannelIsNotReplayedHere()
    {
        var run = Play();
        var entries = EconomyJournal.Parse(run.Host.Journal.ToText());
        var elsewhere = EconomyReplay.Run(EconomyRuntime.Create(run.Set), entries, 0, 60, EcoHost.TicksPerSecond, channel: "other");
        Assert.NotEqual(run.Host.Runtime.StateDigest(), elsewhere.StateDigest);
    }

    // ---- the journal text ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AJournalHasFactsStepsAndRequestsAsDocumentedAndRoundTrips()
    {
        var run = Play();
        var lines = run.Host.Journal.Lines;
        Assert.StartsWith("F|1|", lines[0], StringComparison.Ordinal);
        Assert.Contains("cal.season=S:spring", lines[0], StringComparison.Ordinal);
        Assert.Contains(lines, l => l.StartsWith("S|1|1", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("R|3||trade|r1|riverton|grain|player||", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("R|12||lend|lend|aid|world|hillfort|taxday|annuity|5000|2000|5|2", StringComparison.Ordinal));
        var entries = EconomyJournal.Parse(run.Host.Journal.ToText());
        Assert.Equal(lines.Count, entries.Count);
        var trade = entries.First(e => e.Kind == "trade");
        Assert.Equal((3L, "r1", "riverton", "grain", "player", 120L), (trade.Tick, trade.RequestId, trade.A, trade.B, trade.C, trade.N));
        var lend = entries.First(e => e.Kind == "lend");
        Assert.Equal(("aid", "world", "hillfort", "taxday", "annuity", 5000L, 2000L, 5L, 2L), (lend.A, lend.B, lend.C, lend.D, lend.E, lend.N, lend.M, lend.P, lend.Q));
        Assert.Equal(["trade", "shock", "growth", "lend", "fund", "settle", "trade", "quote", "price", "trade", "unlock", "shock"], entries.Where(e => e.Kind is not null).Select(e => e.Kind).ToList());
        Assert.All(entries.Where(e => e.Facts is not null), e => Assert.Contains("cal.season", e.Facts!.Keys));
    }

    [Fact]
    public void TheTextsOfARequestSurviveTheSeparatorsOfALine()
    {
        var host = new EcoHost(journal: true);
        host.Session.Submit(new EconomyTradeRequested("a|b;c=d%e\nf", "trader|x", "riverton", "gr;ain", 1));
        host.Tick();
        var entry = Assert.Single(EconomyJournal.Parse(host.Journal.ToText()), e => e.Kind == "trade");
        Assert.Equal(("a|b;c=d%e\nf", "trader|x", "riverton", "gr;ain"), (entry.RequestId, entry.C, entry.A, entry.B));
        Assert.Single(host.Journal.Lines, l => l.StartsWith("R|", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("F|x|a=B:true")]
    [InlineData("F|1")]
    [InlineData("F|1|a=Q:true")]
    [InlineData("F|1|a=B:maybe")]
    [InlineData("F|1|a=B:true;a=B:false")]
    [InlineData("F|1|a=I:1.5")]
    [InlineData("S|1")]
    [InlineData("S|1|x")]
    [InlineData("S|1|-4")]
    [InlineData("R|1||trade|id|a|b|c|d|e|1|2|3")]
    [InlineData("R|1||fly|id|a|b|c|d|e|1|2|3|4")]
    [InlineData("R|1||trade|id|a|b|c|d|e|one|2|3|4")]
    [InlineData("R|1||trade|id|a|b|c|d|e|1|2|3|four")]
    [InlineData("F|5|a=B:true\nF|4|a=B:false")]
    [InlineData("Z|1|2")]
    public void AJournalThatDoesNotReadIsRefused(string text) => Assert.Equal(EconomyJournal.BadJournal, Samples.Code(() => EconomyJournal.Parse(text)));

    [Fact]
    public void AnEmptyJournalReadsAsNothingAndTheJournalClockReturnsTheLatestStepAtOrBeforeATick()
    {
        Assert.Empty(EconomyJournal.Parse(""));
        var entries = EconomyJournal.Parse("S|2|10\nF|3|a=B:true\nS|5|11\nS|9|40");
        var clock = new JournalEconomyClock(entries, 7);
        Assert.Equal(7, clock.StepAt(0));
        Assert.Equal(7, clock.StepAt(1));
        Assert.Equal(10, clock.StepAt(2));
        Assert.Equal(10, clock.StepAt(4));
        Assert.Equal(11, clock.StepAt(5));
        Assert.Equal(11, clock.StepAt(8));
        Assert.Equal(40, clock.StepAt(9));
        Assert.Equal(40, clock.StepAt(1000));
    }

    [Fact]
    public void ARunWithoutAJournalWritesNothingAndOneWithAJournalWritesOnlyWhatChanged()
    {
        var without = new EcoHost();
        without.Trade("riverton", "grain", 1);
        without.Tick(3);
        Assert.Empty(without.Journal.Lines);
        var with = new EcoHost(journal: true);
        with.Tick(5);
        Assert.Equal(1, with.Journal.Lines.Count(l => l.StartsWith("F|", StringComparison.Ordinal)));     // the facts did not change
        Assert.Equal(5, with.Journal.Lines.Count(l => l.StartsWith("S|", StringComparison.Ordinal)));       // the step did, every tick
        with.HostFacts["cal.season"] = FactValue.From("winter");
        with.Tick();
        Assert.Equal(2, with.Journal.Lines.Count(l => l.StartsWith("F|", StringComparison.Ordinal)));
    }
}
