namespace GameRuntime.Alchemy.Headless;

/// <summary>
/// What the player knows, built only from events it has received: the known elements, the combinations already tried and how many
/// requests are still waiting for an answer. A policy decides from this view and nothing else.
/// </summary>
public sealed class AlchemyView(IReadOnlyList<string> environmentOptions)
{
    public long Tick { get; set; }
    public bool Ended { get; set; }
    public SortedSet<string> Known { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Tried { get; } = new(StringComparer.Ordinal);
    public HashSet<string> InFlight { get; } = new(StringComparer.Ordinal);
    /// <summary>Every environment tag the content mentions, sorted; the player can try each one on offer.</summary>
    public IReadOnlyList<string> EnvironmentOptions { get; } = environmentOptions;
    public int Awaiting => InFlight.Count;
}
