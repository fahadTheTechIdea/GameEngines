using EngineCommon;
using ScenarioEngine.Contracts;
using TutorialEngine;

namespace GameRuntime;

/// <summary>What a replay ended with: the tick, the state digest, the component fingerprint and the hash of everything it raised.</summary>
public sealed record TutorialReplayResult(long Tick, string StateDigest, string Fingerprint, string TranscriptHash, int Events);

/// <summary>
/// Replays a journal on a fresh session from a checkpoint (or from a new player): the requests of each tick are submitted before that tick, the step of each tick and the host facts are those the journal holds,
/// so the component does what it did. The result digest and transcript hash equal those of the original run from the same point; a difference is a bug in the component or a change the journal did not capture.
/// </summary>
public static class TutorialReplay
{
    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    /// <param name="runtime">The tutorial to replay on: opened from a checkpoint, or new.</param>
    /// <param name="entries">The journal entries of the ticks after that point (earlier facts and steps set where it starts).</param>
    /// <param name="fromTick">The tick the checkpoint was taken at (<see cref="TutorialComponent.Tick"/>); 0 for a new player.</param>
    /// <param name="toTick">The last tick to run.</param>
    /// <param name="ticksPerSecond">The tick rate of the session the journal was recorded on; it must divide 1,000,000, so that one whole number of microseconds is exactly one tick.</param>
    public static TutorialReplayResult Run(TutorialRuntime runtime, IReadOnlyList<TutorialJournalEntry> entries, long fromTick, long toTick, int ticksPerSecond, string componentId = "tutorial", string channel = "", int maxEvents = 2000)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(entries);
        if (ticksPerSecond < 1 || RuntimeClock.MicrosecondsPerSecond % ticksPerSecond != 0) throw new EngineRefusedException(TutorialCodes.StepOrder, "a replay feeds the session whole microseconds per tick, so the tick rate must divide 1,000,000 (50 and 100 do, 60 does not); it is " + ticksPerSecond);
        if (fromTick < 0 || toTick < fromTick) throw new EngineRefusedException(TutorialCodes.StepOrder, "the replay runs from tick " + fromTick + " to tick " + toTick);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        foreach (var entry in entries.Where(e => e.Facts is not null && e.Tick <= fromTick)) { facts.Clear(); foreach (var (id, value) in entry.Facts!) facts[id] = value; }
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(ticksPerSecond, 8, fromTick), reporter);
        var channels = new TutorialChannels();
        channels.Install(session);
        var component = new TutorialComponent(runtime, new JournalTutorialClock(entries, runtime.Step), () => facts, channels, componentId, channel, maxEvents);
        session.AddComponent(component, 1);
        var transcript = new TutorialTranscript(session);
        session.Init();
        var tickMicroseconds = RuntimeClock.MicrosecondsPerSecond / ticksPerSecond;
        var next = 0;
        while (next < entries.Count && entries[next].Tick <= fromTick) next++;
        for (var tick = fromTick + 1; tick <= toTick; tick++)
        {
            while (next < entries.Count && entries[next].Tick == tick)
            {
                var entry = entries[next++];
                if (entry.Facts is { } changed) { facts.Clear(); foreach (var (id, value) in changed) facts[id] = value; }
                else if (entry.Kind is not null && entry.Channel == channel) session.Submit(new TutorialRequested(entry.RequestId!, entry.Kind, entry.Text!, entry.Number, entry.Channel));
            }
            if (session.Update(tickMicroseconds) != 1) throw new InvalidOperationException("the replay session did not run tick " + tick + " (state " + session.State + "): " + string.Join("; ", reporter.Reports));
        }
        if (reporter.Reports.Count > 0) throw new InvalidOperationException("the replay session reported: " + string.Join("; ", reporter.Reports));
        return new TutorialReplayResult(toTick, runtime.StateDigest(), component.Fingerprint(), transcript.Hash, transcript.Lines.Count);
    }
}
