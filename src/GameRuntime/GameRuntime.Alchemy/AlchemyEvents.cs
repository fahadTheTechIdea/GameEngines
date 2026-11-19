namespace GameRuntime;

// Events of the alchemy component. They are flat: primitive fields only. Item lists are element ids joined by '+', environment
// tags and produced elements are joined by ',', so a Godot signal or a journal can carry them unchanged.

/// <summary>A request to combine items. It is only queued; the alchemy component acts on it in its next Update.</summary>
public readonly struct CombineRequested
{
    public CombineRequested(string requestId, string items, string environment) { RequestId = requestId; Items = items; Environment = environment; }
    public string RequestId { get; }
    /// <summary>Element ids joined by '+', in the order the player put them in.</summary>
    public string Items { get; }
    /// <summary>Environment tags joined by ','; empty for none.</summary>
    public string Environment { get; }
}

/// <summary>
/// A recipe became known by some other route (research done in a sim, a teacher). Queued like a request; the component
/// teaches its book in its next Update and reports what was new with <see cref="DiscoveryMade"/> events whose request id starts with "external:".
/// </summary>
public readonly struct RecipeLearnedExternally
{
    public RecipeLearnedExternally(string recipeId, string source) { RecipeId = recipeId; Source = source; }
    public string RecipeId { get; }
    public string Source { get; }
}

/// <summary>A valid combination was tried. The outcome is "reacted", "no-reaction" or "fizzled".</summary>
public readonly struct Combined
{
    public Combined(string requestId, long tick, string outcome, string code, string recipeId, string items, string environment, string produced, string kept)
    { RequestId = requestId; Tick = tick; Outcome = outcome; Code = code; RecipeId = recipeId; Items = items; Environment = environment; Produced = produced; Kept = kept; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Outcome { get; }
    public string Code { get; }
    public string RecipeId { get; }
    public string Items { get; }
    public string Environment { get; }
    public string Produced { get; }
    public string Kept { get; }
}

/// <summary>The request itself was invalid (unknown or undiscovered element, too many items, ...). Nothing was tried.</summary>
public readonly struct CombineRefused
{
    public CombineRefused(string requestId, long tick, string code) { RequestId = requestId; Tick = tick; Code = code; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Code { get; }
}

/// <summary>The player found something new. The kind is "element" or "recipe".</summary>
public readonly struct DiscoveryMade
{
    public DiscoveryMade(string requestId, long tick, string kind, string id) { RequestId = requestId; Tick = tick; Kind = kind; Id = id; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Kind { get; }
    public string Id { get; }
}

/// <summary>
/// Raised once at Init with everything the book already knows (a new game: the starting elements), so a listener that was
/// created after a load rebuilds its view from events instead of reading the book.
/// </summary>
public readonly struct KnowledgeRestored
{
    public KnowledgeRestored(string elements, string recipes, string tried, long attempts) { Elements = elements; Recipes = recipes; Tried = tried; Attempts = attempts; }
    /// <summary>Known element ids joined by ','.</summary>
    public string Elements { get; }
    /// <summary>Known recipe ids joined by ','.</summary>
    public string Recipes { get; }
    /// <summary>Tried combinations (<c>items|environment</c>) joined by ';'.</summary>
    public string Tried { get; }
    public long Attempts { get; }
}
