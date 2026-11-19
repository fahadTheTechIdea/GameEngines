namespace GameRuntime;

/// <summary>Read-only facts from the one session clock.</summary>
public interface IRuntimeClock
{
    long Tick { get; }
    int TicksPerSecond { get; }
    int SpeedNumerator { get; }
    int SpeedDenominator { get; }
    bool Paused { get; }
    int CatchUpCap { get; }
    bool IsRunningSlowly { get; }
    double Alpha { get; }
    long PendingTicks { get; }
}

/// <summary>
/// The only pacing implementation of the reference host (Godot uses its physics loop instead). The host passes elapsed
/// time; this never reads a wall clock. Exact integer accumulator: the remainder is carried, so there is no drift.
/// </summary>
public sealed class RuntimeClock : IRuntimeClock
{
    public const long MicrosecondsPerSecond = 1_000_000L;

    private long _budgetMicroTicks;   // earned time in ticks x 1e6
    private long _speedRemainder;     // carried division remainder, always < SpeedDenominator

    public RuntimeClock(int ticksPerSecond, int catchUpCap = 8, long startTick = 0)
    {
        if (ticksPerSecond < 1) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
        if (catchUpCap < 1) throw new ArgumentOutOfRangeException(nameof(catchUpCap));
        if (startTick < 0) throw new ArgumentOutOfRangeException(nameof(startTick));
        Tick = startTick;
        TicksPerSecond = ticksPerSecond;
        CatchUpCap = catchUpCap;
    }

    public long Tick { get; private set; }
    public int TicksPerSecond { get; }
    public int SpeedNumerator { get; private set; } = 1;
    public int SpeedDenominator { get; private set; } = 1;
    public bool Paused { get; private set; }
    public int CatchUpCap { get; }
    public bool IsRunningSlowly { get; private set; }
    public long TickMicroseconds => MicrosecondsPerSecond / TicksPerSecond;
    public double Alpha => Paused ? 0 : (double)(_budgetMicroTicks % MicrosecondsPerSecond) / MicrosecondsPerSecond;
    public long PendingTicks => _budgetMicroTicks / MicrosecondsPerSecond;

    public void Pause() { Paused = true; }
    public void Resume() { Paused = false; }

    /// <summary>Rational time scale, for example 1/2 for half speed or 4/1 for four times.</summary>
    public void SetSpeed(int numerator, int denominator)
    {
        if (numerator < 1 || numerator > 64 || denominator < 1 || denominator > 64)
            throw new ArgumentOutOfRangeException(nameof(numerator), "speed is n/d with both in 1..64");
        SpeedNumerator = numerator;
        SpeedDenominator = denominator;
        _speedRemainder = 0;
    }

    /// <summary>Adds the host's elapsed time and returns the whole ticks to run now (bounded by the catch-up cap).</summary>
    public int Earn(long elapsedMicroseconds)
    {
        if (elapsedMicroseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMicroseconds));
        if (!Paused)
        {
            var scaled = checked(elapsedMicroseconds * TicksPerSecond * SpeedNumerator + _speedRemainder);
            _budgetMicroTicks = checked(_budgetMicroTicks + scaled / SpeedDenominator);
            _speedRemainder = scaled % SpeedDenominator;
        }
        var due = Paused ? 0 : _budgetMicroTicks / MicrosecondsPerSecond;
        IsRunningSlowly = due > CatchUpCap;
        return (int)Math.Min(due, CatchUpCap);
    }

    /// <summary>Consumes one earned tick and returns the new tick number.</summary>
    public long Advance()
    {
        if (_budgetMicroTicks < MicrosecondsPerSecond) throw new InvalidOperationException("no earned tick to advance");
        _budgetMicroTicks -= MicrosecondsPerSecond;
        return ++Tick;
    }

    /// <summary>A terminal session discards pending time.</summary>
    public void DiscardPending()
    {
        _budgetMicroTicks = 0;
        _speedRemainder = 0;
        IsRunningSlowly = false;
    }
}
