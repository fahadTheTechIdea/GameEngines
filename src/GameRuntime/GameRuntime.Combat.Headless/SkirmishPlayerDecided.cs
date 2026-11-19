namespace GameRuntime.Combat.Headless;

/// <summary>The commander made a decision. Published for the host to show; the decision itself is the input edge that follows.</summary>
public readonly struct SkirmishPlayerDecided
{
    public SkirmishPlayerDecided(long tick, string kind, string detail) { Tick = tick; Kind = kind; Detail = detail; }
    public long Tick { get; }
    public string Kind { get; }
    public string Detail { get; }
}
