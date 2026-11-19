namespace GameRuntime.Headless;

/// <summary>The player made a decision. Published for the host to show; the decision itself is the input event that follows.</summary>
public readonly struct PlayerDecided
{
    public PlayerDecided(long tick, string kind, string detail) { Tick = tick; Kind = kind; Detail = detail; }
    public long Tick { get; }
    public string Kind { get; }
    public string Detail { get; }
}

/// <summary>The player asked for a different game speed (n/d of real time). The host applies it to the session's clock.</summary>
public readonly struct ClockRequested
{
    public ClockRequested(int numerator, int denominator) { Numerator = numerator; Denominator = denominator; }
    public int Numerator { get; }
    public int Denominator { get; }
}
