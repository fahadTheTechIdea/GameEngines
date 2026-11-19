using EngineCommon;

namespace GameRuntime;

/// <summary>
/// How an economy component turns the tick of its session into the step the economy lives in. It is a pure function of the tick (and of nothing else a component would have to keep): the session
/// clock stays the only clock. The calendar bridge supplies one that reads the calendar instant; a game with no calendar uses <see cref="TickEconomyClock"/>; a replay uses
/// <see cref="JournalEconomyClock"/>, which reads the steps a recorded run used.
/// </summary>
public interface IEconomyClock
{
    /// <summary>The step the economy should be at when the session is at <paramref name="tick"/>; never before the step of an earlier tick.</summary>
    long StepAt(long tick);
}

/// <summary>A step every <paramref name="ticksPerStep"/> ticks of the session, from <paramref name="firstStep"/> at tick 0: no calendar needed.</summary>
public sealed class TickEconomyClock : IEconomyClock
{
    private readonly long _ticksPerStep;
    private readonly long _firstStep;

    public TickEconomyClock(long ticksPerStep, long firstStep = 0)
    {
        if (ticksPerStep < 1) throw new EngineRefusedException(EconomyEngine.EconomyCodes.Range, "a step is at least one tick, not " + ticksPerStep);
        if (firstStep < 0) throw new EngineRefusedException(EconomyEngine.EconomyCodes.StepOrder, "the first step is not negative");
        _ticksPerStep = ticksPerStep;
        _firstStep = firstStep;
    }

    public long StepAt(long tick) => _firstStep + tick / _ticksPerStep;
}

/// <summary>The steps a recorded run used, by tick: the step of the latest journal entry at or before the tick, and the step the replay starts from before any.</summary>
public sealed class JournalEconomyClock : IEconomyClock
{
    private readonly List<(long Tick, long Step)> _steps;
    private readonly long _start;

    public JournalEconomyClock(IEnumerable<EconomyJournalEntry> entries, long startStep)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _steps = entries.Where(e => e.Step is not null).Select(e => (e.Tick, e.Step!.Value)).ToList();
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
}
