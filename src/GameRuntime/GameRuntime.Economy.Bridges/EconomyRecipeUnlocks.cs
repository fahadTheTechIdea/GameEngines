using EconomyEngine;

namespace GameRuntime;

/// <summary>
/// Lets discovery unlock production. The alchemy book is the discovery layer only: it knows which recipes the player has found, not what they cost or make. When it announces a discovered recipe
/// (<see cref="DiscoveryMade"/> of kind <c>recipe</c>) that the economy document declares locked, this asks the economy to unlock it with an <see cref="EconomyRecipeUnlockRequested"/>; the economy then runs it from the next
/// period, with the amounts, durations and market lines its document gives it. A discovery of a recipe the economy does not have, or never locked, is none of its business and is ignored (alchemy knows many things
/// the economy never produces). The request is raised in the next Update, never inside the discovery event, and the economy answers it in its own Update, like every request.
/// </summary>
/// <remarks>
/// The alchemy recipe ids are the catalog's (the converter keeps them), so a discovery names the economy's recipe directly; no mapping table is kept. A recipe taught to the book from outside
/// (<c>external:</c> request ids) is also forwarded: the economy's unlock is idempotent. Add the component to the session after the alchemy component.
/// </remarks>
public sealed class EconomyRecipeUnlocks : IGameComponent
{
    private readonly EconomySet _set;
    private readonly string _channel;
    private readonly List<string> _discovered = new();
    private readonly HashSet<string> _asked = new(StringComparer.Ordinal);
    private IEventPort? _events;

    public EconomyRecipeUnlocks(string id, EconomySet set, string channel = "")
    {
        ArgumentNullException.ThrowIfNull(set);
        Id = id;
        _set = set;
        _channel = channel;
    }

    public string Id { get; }

    /// <summary>True when nothing it has heard is still waiting to be asked for; a checkpoint is only consistent then.</summary>
    public bool IsQuiet => _discovered.Count == 0;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<DiscoveryMade>(made =>
        {
            if (made.Kind == "recipe" && _set.RecipeById.TryGetValue(made.Id, out var recipe) && recipe.Locked) _discovered.Add(made.Id);
        });
    }

    public void Update(in UpdateContext context)
    {
        if (_events is null) throw new InvalidOperationException("Update before Init");
        var events = _events;
        if (_discovered.Count == 0) return;
        var found = _discovered.ToArray();
        _discovered.Clear();
        foreach (var recipe in found)
            if (_asked.Add(recipe)) events.Emit(new EconomyRecipeUnlockRequested(Id + ":" + context.Tick + ":" + recipe, recipe, _channel));
    }
}
