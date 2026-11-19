using DiplomacyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Diplomacy.Tests;

/// <summary>Replay: a journal replayed from a checkpoint (or from a new diplomacy) on a fresh session ends in the same state and raises the same events; the journal reads strictly.</summary>
public sealed class ReplayTests
{
    private static DipSet Lively() => Samples.Calm(root =>
    {
        Samples.Realm(root, "cinder")["control"] = "host";
        foreach (var realm in root["realms"]!.AsArray()) realm!["successionPpm"] = 4000;
        Samples.Rules(root, "balance")["interval"] = 7;
        Samples.Rules(root, "balance")["threatLine"] = 250;
        Samples.Rules(root, "balance")["relaxLine"] = 150;
        Samples.Rules(root, "war")["exhaustPpm"] = 20000;
    });

    private static readonly string[] Kinds = ["propose", "respond", "withdraw", "cancel", "declare", "join", "leave", "score", "fulfil", "mission", "recruit", "release", "grant", "deed", "happen", "succeed", "might", "leader", "evaluate", "standing", "estimate", "dance"];
    private static readonly string[] Realms = ["aurel", "brass", "cinder", "dune", "ember", "nowhere"];
    private static readonly string[] Types = ["nap", "trade", "alliance", "vassalage", "peace", "ceasefire", "marriage", "embargo"];

    /// <summary>A seeded stream of requests, valid and not, so the journal has refusals in it too.</summary>
    private static void Ask(DiplomacyHost host, ulong seed, int tick)
    {
        if (DeterministicRoll.Permille(seed, "ask", tick) >= 250) return;
        long N(string name, long bound) => DeterministicRoll.Below(seed, name, tick, bound);
        string Realm(string name) => Realms[N(name, Realms.Length)];
        switch (Kinds[N("kind", Kinds.Length)])
        {
            case "propose": host.Ask("propose", Realm("a"), Realm("b"), Types[N("t", Types.Length)], N("d", 3) == 0 ? 0 : 30 + 30 * N("d2", 3), N("amt", 3) switch { 0 => 0, 1 => 800, _ => -400 }); break;
            case "respond": host.Ask("respond", number: N("p", 5), number2: N("y", 2)); break;
            case "withdraw": host.Ask("withdraw", Realm("a"), number: N("p", 5)); break;
            case "cancel": host.Ask("cancel", Realm("a"), number: N("tr", 5)); break;
            case "declare": host.Ask("declare", Realm("a"), Realm("b"), new[] { "", "rivalry", "border", "balance", "nothing" }[N("cb", 5)]); break;
            case "join": host.Ask("join", Realm("a"), Realm("b"), number: N("w", 3)); break;
            case "leave": host.Ask("leave", Realm("a"), number: N("w", 3)); break;
            case "score": host.Ask("score", Realm("a"), new[] { "battle", "occupation", "luck" }[N("s", 3)], number: N("w", 3), number2: N("am", 1001)); break;
            case "fulfil": host.Ask("fulfil", number: N("tr", 5), number2: N("y", 2)); break;
            case "mission": host.Ask("mission", host.Runtime.Set.Agents[(int)N("ag", host.Runtime.Set.Agents.Count)].Id, Realm("b"), new[] { "spy", "sabotage", "unrest", "guard" }[N("m", 4)]); break;
            case "recruit": host.Ask("recruit", Realm("a"), "recruit-" + N("n", 4), number: N("sk", 1100)); break;
            case "release": host.Ask("release", host.Runtime.Set.Agents[(int)N("ag", host.Runtime.Set.Agents.Count)].Id); break;
            case "grant": host.Ask("grant", Realm("a"), Realm("b"), new[] { "border", "balance", "claim" }[N("cb", 3)], number: 1 + N("days", 30)); break;
            case "deed": host.Ask("deed", Realm("a"), Realm("b"), new[] { "gift", "insult", "spying" }[N("dd", 3)], number: 1 + N("sc", 1000)); break;
            case "happen": host.Ask("happen", Realm("a"), new[] { "disaster", "war_won", "tribute_missed" }[N("ev", 3)], number: 1 + N("sc", 1000)); break;
            case "succeed": host.Ask("succeed", Realm("a")); break;
            case "might": host.Ask("might", Realm("a"), number: N("mg", 700_000)); break;
            case "leader": host.Ask("leader", Realm("a"), Realm("b"), number: N("lr", 2001) - 1000); break;
            case "evaluate": host.Ask("evaluate", Realm("a"), Realm("b"), Types[N("t", Types.Length)], 60, 0); break;
            case "standing": host.Ask("standing", Realm("a"), N("o", 2) == 0 ? "" : Realm("b")); break;
            case "estimate": host.Ask("estimate", Realm("a"), Realm("b")); break;
            default: host.Ask("dance", Realm("a")); break;
        }
    }

    private static void Play(DiplomacyHost host, ulong seed, int from, int to)
    {
        for (var tick = from; tick <= to; tick++)
        {
            Ask(host, seed, tick);
            host.Tick();
            if (tick % 40 == 0) host.HostFacts["game.crisis"] = FactValue.From(DeterministicRoll.Below(seed, "crisis", tick, 11));
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
        var host = new DiplomacyHost(set, journal: true);
        Play(host, seed, 1, 90 + (int)seed * 7);
        // The checkpoint is taken at the first quiet moment.
        while (!host.Component.IsQuiet || !host.Channels.IsQuiet) host.Tick();
        var checkpoint = DiplomacyCheckpoint.Capture(host.Component);
        var checkpointTick = host.Component.Tick;
        var linesAtCheckpoint = host.Transcript.Lines.Count;
        Play(host, seed, (int)checkpointTick + 1, (int)checkpointTick + 250);
        host.Tick(3);
        var entries = DiplomacyJournal.Parse(host.Journal.ToText());
        var replay = DiplomacyReplay.Run(DiplomacyCheckpoint.Open(set, checkpoint), entries, checkpointTick, host.Component.Tick, DiplomacyHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(Digest.Of(string.Join('\n', host.Transcript.Lines.Skip(linesAtCheckpoint))), replay.TranscriptHash);
        Assert.Equal(host.Transcript.Lines.Count - linesAtCheckpoint, replay.Events);
        Assert.Contains(entries, e => e.Kind is not null);          // the journal holds requests
        Assert.Contains(entries, e => e.Facts is not null);
        Assert.True(host.Transcript.Lines.Count - linesAtCheckpoint > 40, "the run was too quiet to prove a replay: " + (host.Transcript.Lines.Count - linesAtCheckpoint));
    }

    [Fact]
    public void AJournalReplayedFromANewDiplomacyReproducesTheWholeRun()
    {
        var set = Lively();
        var host = new DiplomacyHost(set, journal: true);
        Play(host, 9UL, 1, 300);
        host.Tick(2);
        var entries = DiplomacyJournal.Parse(host.Journal.ToText());
        var replay = DiplomacyReplay.Run(DipRuntime.Create(set), entries, 0, host.Component.Tick, DiplomacyHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(host.Transcript.Hash, replay.TranscriptHash);
    }

    [Fact]
    public void AClockThatCatchesUpInPartsIsReplayedStepForStep()
    {
        var set = Lively();
        var host = new DiplomacyHost(set, journal: true, clock: new TickDiplomacyClock(set, ticksPerStep: 1, firstStep: 400), maxSteps: 7);
        Play(host, 3UL, 1, 120);
        host.Tick(2);
        var entries = DiplomacyJournal.Parse(host.Journal.ToText());
        Assert.Contains(entries, e => e.Step is not null && e.Seasons is { Count: > 0 });
        var replay = DiplomacyReplay.Run(DipRuntime.Create(set), entries, 0, host.Component.Tick, DiplomacyHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(host.Transcript.Hash, replay.TranscriptHash);
    }

    [Fact]
    public void ACalendarsOwnSeasonsAreWhatTheJournalKeepsSoAReplayNeedsNoCalendar()
    {
        var set = Lively();
        var host = new DiplomacyHost(set, journal: true, clock: new SeasonsBackwards(set));
        host.Tick(45);
        var entries = DiplomacyJournal.Parse(host.Journal.ToText());
        var replay = DiplomacyReplay.Run(DipRuntime.Create(set), entries, 0, host.Component.Tick, DiplomacyHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(host.Transcript.Hash, replay.TranscriptHash);
        Assert.Equal(3, host.Runtime.Season);
    }

    /// <summary>A clock whose seasons are not the document's own cycle (the calendar's would not be): winter when the document would say spring.</summary>
    private sealed class SeasonsBackwards(DipSet set) : IDiplomacyClock
    {
        public long StepAt(long tick) => tick;
        public string SeasonAt(long step) => set.CycleSeasonAt(step) == "spring" ? "winter" : "spring";
    }

    [Fact]
    public void AReplayOfNoTicksReturnsTheStateItWasGiven()
    {
        var host = new DiplomacyHost();
        host.Tick(5);
        var replay = DiplomacyReplay.Run(host.Runtime, [], 5, 5, DiplomacyHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(0, replay.Events);
    }

    [Fact]
    public void TheReplayRefusesATickRateItCannotFeedExactlyAndARunThatGoesBackwards()
    {
        var set = Samples.Calm();
        Assert.Equal(DipCodes.StepOrder, Samples.Code(() => DiplomacyReplay.Run(DipRuntime.Create(set), [], 0, 10, 60)));
        Assert.Equal(DipCodes.StepOrder, Samples.Code(() => DiplomacyReplay.Run(DipRuntime.Create(set), [], 0, 10, 0)));
        Assert.Equal(DipCodes.StepOrder, Samples.Code(() => DiplomacyReplay.Run(DipRuntime.Create(set), [], 10, 5, 50)));
        Assert.Equal(DipCodes.StepOrder, Samples.Code(() => DiplomacyReplay.Run(DipRuntime.Create(set), [], -1, 5, 50)));
    }

    // ---- the journal ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheJournalIsTextOfThreeShapesAndRoundTripsWithItsEscapes()
    {
        var host = new DiplomacyHost(journal: true);
        host.HostFacts["game.crisis"] = FactValue.From(4L);
        host.Tick(2);
        host.Ask("standing", "a|b;c=d%e\nf", "g", "h", 7, -9);
        host.Tick();
        var text = host.Journal.ToText();
        var entries = DiplomacyJournal.Parse(text);
        Assert.Equal(host.Journal.Lines.Count, entries.Count);
        Assert.StartsWith("F|1|game.crisis=I:4", host.Journal.Lines[0]);
        Assert.Contains(host.Journal.Lines, l => l.StartsWith("S|1|1|0:spring*2", StringComparison.Ordinal));
        var request = entries.Single(e => e.Kind is not null);
        Assert.Equal(("standing", "a|b;c=d%e\nf", "g", "h", 7L, -9L), (request.Kind, request.A, request.B, request.C, request.Number, request.Number2));
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
    [InlineData("R|1|c|k|id|a|b|c|1")]
    [InlineData("R|1|c|k|id|a|b|c|x|1")]
    [InlineData("R|1|c|k|id|a|b|c|1|y")]
    [InlineData("F|5|a=I:1\nF|4|a=I:1")]
    [InlineData("Z|1|a")]
    public void AJournalThatIsNotOneOfTheThreeShapesIsRefusedWithItsLine(string text) => Assert.Equal(DiplomacyJournal.BadJournal, Samples.Code(() => DiplomacyJournal.Parse(text)));

    [Fact]
    public void AnEmptyJournalIsAnEmptyList()
    {
        Assert.Empty(DiplomacyJournal.Parse(""));
        Assert.Throws<ArgumentNullException>(() => DiplomacyJournal.Parse(null!));
    }

    [Fact]
    public void TheJournalClockReadsTheStepsAndSeasonsOfARecordedRun()
    {
        var entries = DiplomacyJournal.Parse("S|3|4|0:warm*3;3:cold*2\nS|9|6|5:cold*1");
        var clock = new JournalDiplomacyClock(entries, 1);
        Assert.Equal(1, clock.StepAt(2));
        Assert.Equal(4, clock.StepAt(3));
        Assert.Equal(4, clock.StepAt(8));
        Assert.Equal(6, clock.StepAt(9));
        Assert.Equal("warm", clock.SeasonAt(2));
        Assert.Equal("cold", clock.SeasonAt(3));
        Assert.Equal("cold", clock.SeasonAt(5));
        Assert.Equal("", clock.SeasonAt(6));              // a step the recorded run never crossed has no season: the diplomacy refuses the empty id
    }
}
