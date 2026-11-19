namespace GameRuntime.Field.Headless;

/// <summary>A line of the commander's story for the host to print: the field was raised, a round was fought, a request was refused.</summary>
public readonly struct FieldProgress
{
    public FieldProgress(long tick, string kind, string detail) { Tick = tick; Kind = kind; Detail = detail; }
    public long Tick { get; }
    public string Kind { get; }
    public string Detail { get; }
}
