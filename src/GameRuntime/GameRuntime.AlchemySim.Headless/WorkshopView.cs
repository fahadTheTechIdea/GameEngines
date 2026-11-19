namespace GameRuntime.AlchemySim.Headless;

/// <summary>
/// What the player knows, built only from events it has received: known resources and recipes, the combinations already tried, the
/// recipes it has already ordered and the requests still waiting for an answer. A policy decides from this view and nothing else.
/// </summary>
public sealed class WorkshopView
{
    public long Tick { get; set; }
    public bool Ended { get; set; }
    public SortedSet<string> Known { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> KnownRecipes { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Tried { get; } = new(StringComparer.Ordinal);
    /// <summary>Recipes already ordered once. The only part of the view a checkpoint must carry, because no event replays it.</summary>
    public SortedSet<string> Ordered { get; } = new(StringComparer.Ordinal);
    public HashSet<string> InFlight { get; } = new(StringComparer.Ordinal);
    public int Awaiting => InFlight.Count;
}
