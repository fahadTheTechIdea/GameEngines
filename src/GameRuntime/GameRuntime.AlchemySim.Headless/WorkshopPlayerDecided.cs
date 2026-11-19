namespace GameRuntime.AlchemySim.Headless;

/// <summary>The player made a decision. Published for the host to show; the decision itself is the input edge that follows.</summary>
public readonly struct WorkshopPlayerDecided
{
    public WorkshopPlayerDecided(long tick, string kind, string detail) { Tick = tick; Kind = kind; Detail = detail; }
    public long Tick { get; }
    public string Kind { get; }
    public string Detail { get; }
}
