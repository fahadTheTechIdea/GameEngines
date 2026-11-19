using CalendarEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>What a replay ended with: the tick, the state digest, the component fingerprint and the hash of everything it raised.</summary>
public sealed record CalendarReplayResult(long Tick, string StateDigest, string Fingerprint, string TranscriptHash, int Events);

/// <summary>
/// Replays a journal on a fresh session from a checkpoint (or from a new calendar): the requests of each tick are submitted before that tick and the host facts are those the journal holds, so the component
/// does what it did. The result's digest and transcript hash equal those of the original run from the same point; a difference is a bug in the component or a change the journal did not capture.
/// </summary>
public static class CalendarReplay
{
    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    /// <param name="runtime">The calendar to replay on: opened from a checkpoint, or new. Its last tick is where the replay starts.</param>
    /// <param name="entries">The journal entries of the ticks after that point.</param>
    /// <param name="toTick">The last tick to run.</param>
    /// <param name="ticksPerSecond">The tick rate of the session the journal was recorded on; it must divide 1,000,000, so that one whole number of microseconds is exactly one tick. The calendar does not depend on the rate: only its ticks count.</param>
    public static CalendarReplayResult Run(CalendarRuntime runtime, IReadOnlyList<CalendarJournalEntry> entries, long toTick, int ticksPerSecond, string componentId = "calendar", string channel = "", int maxAnnouncements = 1000)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(entries);
        if (ticksPerSecond < 1 || RuntimeClock.MicrosecondsPerSecond % ticksPerSecond != 0) throw new EngineRefusedException(CalendarCodes.TickOrder, "a replay feeds the session whole microseconds per tick, so the tick rate must divide 1,000,000 (50 and 100 do, 60 does not); it is " + ticksPerSecond);
        var start = runtime.LastTick;
        if (toTick < start) throw new EngineRefusedException(CalendarCodes.TickOrder, "the replay ends at tick " + toTick + ", before it starts at " + start);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        foreach (var entry in entries.Where(e => e.Facts is not null && e.Tick <= start)) { facts.Clear(); foreach (var (id, value) in entry.Facts!) facts[id] = value; }
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(ticksPerSecond, 8, start), reporter);
        var channels = new CalendarChannels();
        channels.Install(session);
        var component = new CalendarComponent(runtime, () => facts, channels, componentId, channel, maxAnnouncements);
        session.AddComponent(component, 1);
        var transcript = new CalendarTranscript(session);
        session.Init();
        var tickMicroseconds = RuntimeClock.MicrosecondsPerSecond / ticksPerSecond;
        var next = 0;
        while (next < entries.Count && entries[next].Tick <= start) next++;
        for (var tick = start + 1; tick <= toTick; tick++)
        {
            while (next < entries.Count && entries[next].Tick == tick)
            {
                var entry = entries[next++];
                if (entry.Facts is { } changed) { facts.Clear(); foreach (var (id, value) in changed) facts[id] = value; }
                else if (entry.Channel == channel) Submit(session, entry);
            }
            if (session.Update(tickMicroseconds) != 1) throw new InvalidOperationException("the replay session did not run tick " + tick + " (state " + session.State + "): " + string.Join("; ", reporter.Reports));
        }
        if (reporter.Reports.Count > 0) throw new InvalidOperationException("the replay session reported: " + string.Join("; ", reporter.Reports));
        return new CalendarReplayResult(toTick, CalendarSave.StateDigest(runtime), component.Fingerprint(), transcript.Hash, transcript.Lines.Count);
    }

    private static void Submit(RuntimeSession session, CalendarJournalEntry e)
    {
        var named = CalendarJournal.KindOf(e.Kind!);
        if (named is null) throw new EngineRefusedException(CalendarJournal.BadJournal, "unknown request kind '" + e.Kind + "'");
        var kind = named.Value;
        switch (kind)
        {
            case CalendarRequestKind.Scale: session.Submit(new CalendarScaleRequested(e.RequestId!, e.N, e.M, e.Channel)); break;
            case CalendarRequestKind.Pause: session.Submit(new CalendarPauseRequested(e.RequestId!, e.N != 0, e.Channel)); break;
            case CalendarRequestKind.Skip: session.Submit(new CalendarSkipRequested(e.RequestId!, e.N, (int)e.M, (int)e.K, e.Channel)); break;
            case CalendarRequestKind.ActorAdd: session.Submit(new CalendarActorAddRequested(e.RequestId!, e.A!, e.B!, e.Channel)); break;
            case CalendarRequestKind.ActorRemove: session.Submit(new CalendarActorRemoveRequested(e.RequestId!, e.A!, e.Channel)); break;
            case CalendarRequestKind.Interrupt: session.Submit(new CalendarInterruptRequested(e.RequestId!, e.A!, e.B!, e.N, e.Channel)); break;
            case CalendarRequestKind.InterruptClear: session.Submit(new CalendarInterruptClearRequested(e.RequestId!, e.A!, e.Channel)); break;
            case CalendarRequestKind.Where: session.Submit(new CalendarWhereRequested(e.RequestId!, e.A!, e.Channel)); break;
        }
    }
}
