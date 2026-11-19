using CalendarEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Calendar.Tests;

/// <summary>Journals and replays: a session can be rebuilt from a checkpoint and its journal and ends in the same state, having raised the same events; the journal text is strict.</summary>
public sealed class CalendarReplayTests
{
    private static CalendarSet Set => Samples.Gregorian;

    private static long At(string month, int day, int hour = 0, int minute = 0, long year = 2024) => Samples.At(Set, year, month, day, hour, minute);

    /// <summary>Runs a scripted session: requests and host fact changes at chosen ticks, a checkpoint at a chosen tick.</summary>
    private sealed class Run
    {
        public CalendarHost Host { get; }
        public byte[]? Checkpoint { get; private set; }
        public int LinesAtCheckpoint { get; private set; }
        public long CheckpointTick { get; private set; }

        private readonly bool _foreign;

        public Run(long start, long numerator, long denominator, long lastTick, long checkpointTick, int maxAnnouncements = 1000, bool foreign = false)
        {
            _foreign = foreign;
            Host = new CalendarHost(Set, start, numerator, denominator, maxAnnouncements, journal: true);
            for (long tick = 1; tick <= lastTick; tick++)
            {
                Script(tick);
                Host.Tick();
                if (tick == checkpointTick)
                {
                    Checkpoint = CalendarCheckpoint.Capture(Host.Component);
                    LinesAtCheckpoint = Host.Transcript.Lines.Count;
                    CheckpointTick = tick;
                }
            }
        }

        private void Script(long tick)
        {
            var s = Host.Session;
            if (tick == 1)
            {
                foreach (var (actor, schedule) in new[] { ("anna", "shopkeeper"), ("bert", "shopkeeper"), ("gus", "guard") }) s.Submit(new CalendarActorAddRequested("add-" + actor, actor, schedule));
            }
            if (tick == 40) s.Submit(new CalendarInterruptRequested("alarm", "gus", "alarm", 500));
            if (tick == 90) s.Submit(new CalendarWhereRequested("where", "anna"));
            if (tick == 150) s.Submit(new CalendarWhereRequested("at-checkpoint", "anna"));
            if (tick == 120) Host.HostFacts["weather.raining"] = FactValue.From(true);
            if (tick == 130) s.Submit(new CalendarSkipRequested("sleep", 1, 6, 0));
            if (tick == 170) s.Submit(new CalendarScaleRequested("fast", 30, 7));
            if (tick == 210) s.Submit(new CalendarPauseRequested("pause", true));
            if (tick == 230) s.Submit(new CalendarPauseRequested("resume", false));
            if (tick == 250) s.Submit(new CalendarActorRemoveRequested("leave", "bert"));
            if (tick == 260) s.Submit(new CalendarActorAddRequested("bad", "zoe", "nobody"));
            if (tick == 270 && _foreign) s.Submit(new CalendarScaleRequested("wrong-channel", 3, 1, "elsewhere"));
            if (tick == 280) Host.HostFacts["weather.raining"] = FactValue.From(false);
        }
    }

    [Fact]
    public void ReplayingTheJournalFromACheckpointEndsInTheSameStateWithTheSameEvents()
    {
        var run = new Run(At("january", 9, 22, 0), 5, 2, lastTick: 400, checkpointTick: 150);
        var entries = CalendarJournal.Parse(run.Host.Journal.ToText());
        var resumed = CalendarCheckpoint.Open(Set, run.Checkpoint!);
        var result = CalendarReplay.Run(resumed, entries, 400, CalendarHost.TicksPerSecond);
        Assert.Equal(CalendarSave.StateDigest(run.Host.Runtime), result.StateDigest);
        Assert.Equal(Digest.Of(string.Join('\n', run.Host.Transcript.Lines.Skip(run.LinesAtCheckpoint))), result.TranscriptHash);
        Assert.Equal(run.Host.Transcript.Lines.Count - run.LinesAtCheckpoint, result.Events);
        Assert.Equal(400, result.Tick);
    }

    [Fact]
    public void ReplayingTheWholeJournalFromANewCalendarEndsInTheSameStateToo()
    {
        var run = new Run(At("january", 9, 22, 0), 5, 2, lastTick: 400, checkpointTick: 150);
        var entries = CalendarJournal.Parse(run.Host.Journal.ToText());
        var fresh = CalendarRuntime.Create(Set, At("january", 9, 22, 0), 5, 2);
        var result = CalendarReplay.Run(fresh, entries, 400, CalendarHost.TicksPerSecond);
        Assert.Equal(CalendarSave.StateDigest(run.Host.Runtime), result.StateDigest);
        Assert.Equal(run.Host.Transcript.Hash, result.TranscriptHash);
    }

    [Fact]
    public void TheJournalHoldsEveryRequestAtTheTickItWasAppliedAndTheFactsWhenTheyChanged()
    {
        var run = new Run(At("january", 9, 22, 0), 5, 2, lastTick: 300, checkpointTick: 150);
        var entries = CalendarJournal.Parse(run.Host.Journal.ToText());
        Assert.Equal(["add-anna", "add-bert", "add-gus", "alarm", "where", "sleep", "at-checkpoint", "fast", "pause", "resume", "leave", "bad"], entries.Where(e => e.Kind is not null && e.Channel == "").Select(e => e.RequestId!).ToArray());
        Assert.Equal([1L, 1, 1, 40, 90, 130, 150, 170, 210, 230, 250, 260], entries.Where(e => e.Kind is not null).Select(e => e.Tick).ToArray());
        Assert.Equal([(1L, false), (120L, true), (280L, false)], entries.Where(e => e.Facts is not null).Select(e => (e.Tick, e.Facts!["weather.raining"].Boolean)).ToArray());
        var skip = entries.Single(e => e.Kind == "skip");
        Assert.Equal((1L, 6L, 0L), (skip.N, skip.M, skip.K));
    }

    [Fact]
    public void ARequestForAnotherChannelIsNotInThisComponentsJournal()
    {
        var run = new Run(At("january", 9, 22, 0), 5, 2, lastTick: 300, checkpointTick: 150, foreign: true);
        Assert.DoesNotContain(run.Host.Journal.Lines, l => l.Contains("wrong-channel", StringComparison.Ordinal));
        Assert.Equal(1, run.Host.Channels.Refused);
    }

    [Fact]
    public void AJournalOfASessionWithAnnouncementsStillOwedReplaysTheSame()
    {
        var run = new Run(At("march", 6, 6, 55), 1, 1, lastTick: 200, checkpointTick: 2, maxAnnouncements: 1);
        var entries = CalendarJournal.Parse(run.Host.Journal.ToText());
        var result = CalendarReplay.Run(CalendarCheckpoint.Open(Set, run.Checkpoint!), entries, 200, CalendarHost.TicksPerSecond, maxAnnouncements: 1);
        Assert.Equal(CalendarSave.StateDigest(run.Host.Runtime), result.StateDigest);
        Assert.Equal(Digest.Of(string.Join('\n', run.Host.Transcript.Lines.Skip(run.LinesAtCheckpoint))), result.TranscriptHash);
    }

    [Fact]
    public void AReplayThatDoesNotSayHowFarOrAtWhatRateIsRefused()
    {
        var runtime = CalendarRuntime.Create(Set, 0, 1, 1, startTick: 10);
        Assert.Equal(CalendarCodes.TickOrder, Fixture.CodeOf(() => CalendarReplay.Run(runtime, [], 9, 50)));
        Assert.Equal(CalendarCodes.TickOrder, Fixture.CodeOf(() => CalendarReplay.Run(runtime, [], 20, 60)));
        Assert.Equal(CalendarCodes.TickOrder, Fixture.CodeOf(() => CalendarReplay.Run(runtime, [], 20, 0)));
        Assert.Equal(20, CalendarReplay.Run(runtime, [], 20, 50).Tick);
    }

    // ---- the journal text ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TextWithEveryAwkwardCharacterSurvivesTheJournal()
    {
        var host = new CalendarHost(Set, At("march", 6, 10), journal: true);
        host.Session.Submit(new CalendarActorAddRequested("id|with;odd=chars%25\nand a line", "wei|rd;=%\n", "shopkeeper"));
        host.Tick();
        host.HostFacts["weather.raining"] = FactValue.From(true);
        host.Tick();
        var entries = CalendarJournal.Parse(host.Journal.ToText());
        var request = entries.First(e => e.Kind == "actor-add");
        Assert.Equal(("id|with;odd=chars%25\nand a line", "wei|rd;=%\n", "shopkeeper"), (request.RequestId, request.A, request.B));
        Assert.Equal(3, entries.Count);
        var facts = CalendarJournal.CanonicalFacts(new Dictionary<string, FactValue> { ["a;b=c|d"] = FactValue.From("x;y=z|%\n"), ["n"] = FactValue.From(-5), ["b"] = FactValue.From(true) });
        Assert.Equal("a%3Bb%3Dc%7Cd=S:x%3By%3Dz%7C%25%0A;b=B:true;n=I:-5", facts);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("F|1")]
    [InlineData("F|x|a=I:1")]
    [InlineData("F|-1|a=I:1")]
    [InlineData("F|1|a")]
    [InlineData("F|1|a=I:x")]
    [InlineData("F|1|a=B:maybe")]
    [InlineData("F|1|a=Q:1")]
    [InlineData("F|1|a=I:1;a=I:2")]
    [InlineData("F|1|=I:1")]
    [InlineData("R|1||scale|id|||5|1|0|extra")]
    [InlineData("F|1|a=I:1|extra")]
    [InlineData("R|1||scale|id|||1|1")]
    [InlineData("R|1||warp|id|||1|1|1")]
    [InlineData("R|1||scale|id|||x|1|1")]
    [InlineData("R|1||scale|id|||1|y|1")]
    [InlineData("R|1||scale|id|||1|1|z")]
    [InlineData("F|5|a=I:1\nF|4|a=I:1")]
    [InlineData("Z|1|a")]
    public void AJournalLineThatIsNotOneIsRefused(string text)
    {
        Assert.Equal(CalendarJournal.BadJournal, Fixture.CodeOf(() => CalendarJournal.Parse(text)));
    }

    [Fact]
    public void AnEmptyJournalIsNoEntriesAndFactsMayBeEmpty()
    {
        Assert.Empty(CalendarJournal.Parse(""));
        var entry = CalendarJournal.Parse("F|3|").Single();
        Assert.Empty(entry.Facts!);
        Assert.Equal(3, entry.Tick);
    }
}

/// <summary>Helpers shared by the test classes of this assembly.</summary>
internal static class Fixture
{
    public static EngineRefusedException Refused(Action action)
    {
        var refusal = Assert.Throws<EngineRefusedException>(action);
        Assert.False(string.IsNullOrWhiteSpace(refusal.Message));
        return refusal;
    }

    public static string CodeOf(Action action) => Refused(action).Code;
}
