using EngineCommon;
using WorldEventsEngine;

namespace GameRuntime;

/// <summary>
/// How a world-events component turns the tick of its session into the step the world lives in, and which season each step is in. Both are pure functions (of the tick, and of the step) and of nothing else a component
/// would have to keep: the session clock stays the only clock. A game with no calendar uses <see cref="TickWorldClock"/>; the calendar bridge supplies one that reads the calendar's days and seasons; a replay uses
/// <see cref="JournalWorldClock"/>, which reads the steps and seasons a recorded run used.
/// </summary>
public interface IWorldClock
{
    /// <summary>The step the world should be at when the session is at <paramref name="tick"/>; never before the step of an earlier tick.</summary>
    long StepAt(long tick);

    /// <summary>The id of the season a step is in.</summary>
    string SeasonAt(long step);
}

/// <summary>A step every <paramref name="ticksPerStep"/> ticks of the session, from <paramref name="firstStep"/> at tick 0, and the document's own cycle of seasons: no calendar needed.</summary>
public sealed class TickWorldClock : IWorldClock
{
    private readonly WorldSet _set;
    private readonly long _ticksPerStep;
    private readonly long _firstStep;

    public TickWorldClock(WorldSet set, long ticksPerStep = 1, long firstStep = 0)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (ticksPerStep < 1) throw new EngineRefusedException(WorldCodes.Range, "a step is at least one tick, not " + ticksPerStep);
        if (firstStep < 0) throw new EngineRefusedException(WorldCodes.StepOrder, "the first step is not negative");
        _set = set;
        _ticksPerStep = ticksPerStep;
        _firstStep = firstStep;
    }

    public long StepAt(long tick) => _firstStep + tick / _ticksPerStep;

    public string SeasonAt(long step) => _set.Seasons[_set.CycleSeason(step)].Id;
}

/// <summary>The steps a recorded run used, by tick (the latest journal entry at or before the tick, and the step the replay starts from before any), and the season it gave to each step.</summary>
public sealed class JournalWorldClock : IWorldClock
{
    private readonly List<(long Tick, long Step)> _steps;
    private readonly Dictionary<long, string> _seasons = new();
    private readonly long _start;

    public JournalWorldClock(IEnumerable<WorldJournalEntry> entries, long startStep)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _steps = entries.Where(e => e.Step is not null).Select(e => (e.Tick, e.Step!.Value)).ToList();
        foreach (var run in entries.Where(e => e.Seasons is not null).SelectMany(e => e.Seasons!))
            for (var i = 0; i < run.Count; i++) _seasons[run.From + i] = run.Season;
        _start = startStep;
    }

    public long StepAt(long tick)
    {
        var result = _start;
        foreach (var (at, step) in _steps)
        {
            if (at > tick) break;
            result = step;
        }
        return result;
    }

    /// <summary>The season the recorded run gave the step. A step the journal never crossed has none: the empty id, which the world refuses as unknown (a replay never asks for one a live run did not).</summary>
    public string SeasonAt(long step) => _seasons.TryGetValue(step, out var season) ? season : "";
}
