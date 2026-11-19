using System.Text.Json;

namespace GameRuntime;

/// <summary>
/// Keeps the alchemy book and the sim's knowledge of recipes in step, in both directions, as events:
/// a recipe the book discovers by combination becomes a <c>sim.discover-recipe</c> effect request (the sim's one knowledge writer is
/// <see cref="DiscoverRecipeEffect"/>), and a recipe the sim learns some other way (finished research) is taught to the book with a
/// <see cref="RecipeLearnedExternally"/> event. It holds no knowledge of its own, only the technologies it has already asked for.
/// Everything it raises is queued and takes effect in a later Update.
/// </summary>
public sealed class RecipeUnlockComponent(SimRecipeView view, int company = 0, string id = "recipe-unlock") : IGameComponent
{
    private readonly List<string> _discovered = new();
    private readonly List<string> _taught = new();
    private readonly HashSet<string> _requested = new(StringComparer.Ordinal);
    private IEventPort? _events;

    public string Id { get; } = id;

    /// <summary>True when nothing it has heard is still waiting to be raised; a checkpoint is only consistent then.</summary>
    public bool IsQuiet => _discovered.Count == 0 && _taught.Count == 0;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<DiscoveryMade>(OnDiscovery);
        context.Events.Connect<ObservationPosted>(OnObservation);
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        var discovered = _discovered.ToArray();
        _discovered.Clear();
        foreach (var recipe in discovered)
        {
            var found = view.Recipes.Single(r => r.Id == recipe);
            if (!_requested.Add(found.TechnologyId)) continue;
            events.Emit(new EffectRequested("unlock:" + recipe, "sim." + DiscoverRecipeEffect.EffectName,
                JsonSerializer.Serialize(new { company, recipe }), context.Tick));
        }
        var taught = _taught.ToArray();
        _taught.Clear();
        foreach (var recipe in taught) events.Emit(new RecipeLearnedExternally(recipe, "sim"));
    }

    private void OnDiscovery(DiscoveryMade made)
    {
        // Recipes taught from the sim already are in the sim; only the book's own discoveries go to it.
        if (made.Kind == "recipe" && !made.RequestId.StartsWith("external:", StringComparison.Ordinal) && view.TryRecipe(made.Id, out _))
            _discovered.Add(made.Id);
    }

    private void OnObservation(ObservationPosted posted)
    {
        if (posted.Kind != "sim.event.technology-learned") return;
        using var document = JsonDocument.Parse(posted.PayloadJson);
        var technology = document.RootElement.GetProperty("technology").GetString();
        if (technology is null || !view.TryRecipeForTechnology(technology, out var recipe)) return;
        // A technology this component asked for comes back as an event; the book already has it.
        if (_requested.Contains(technology)) return;
        _requested.Add(technology);
        _taught.Add(recipe.Id);
    }
}
