using System.Globalization;

namespace GameRuntime.AlchemySim.Headless;

/// <summary>
/// The player. Like a game's player object in XNA or a Godot node polling Input, it acts only inside its own Update: it polls the
/// <see cref="InputState"/>, turns each pressed combine action into a <see cref="CombineRequested"/> and each order action into an
/// <see cref="OrderTransformationRequested"/> raised through the event port, and lets its policy (if any) decide from what it learned
/// from earlier events. A policy does not act directly: it presses keys on the same input state, which the next tick polls, so every act
/// of the player is an input edge a journal can record and replay. Nothing it raises does anything before a later Update answers it.
/// </summary>
public sealed class WorkshopPlayerComponent(InputState input, AlchemistPolicy? policy, SimRecipeView recipes, IEnumerable<string> alreadyOrdered, string id = "player") : IGameComponent
{
    private readonly WorkshopView _view = Prepared(alreadyOrdered);
    private readonly Dictionary<string, string> _orderRequests = new(StringComparer.Ordinal);
    private IEventPort? _events;
    private int _raisedThisTick;

    public string Id { get; } = id;
    public WorkshopView View => _view;

    private static WorkshopView Prepared(IEnumerable<string> ordered)
    {
        var view = new WorkshopView();
        foreach (var recipe in ordered) view.Ordered.Add(recipe);
        return view;
    }

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<KnowledgeRestored>(OnKnowledge);
        context.Events.Connect<Combined>(OnCombined);
        context.Events.Connect<CombineRefused>(refused => _view.InFlight.Remove(refused.RequestId));
        context.Events.Connect<DiscoveryMade>(made => { if (made.Kind == "recipe") _view.KnownRecipes.Add(made.Id); });
        context.Events.Connect<TransformationOrderResolved>(OnOrderResolved);
        context.Events.Connect<SessionEnded>(_ => _view.Ended = true);
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        _view.Tick = context.Tick;
        _raisedThisTick = 0;
        var edges = input.Poll();
        if (_view.Ended) return;

        foreach (var edge in edges)
        {
            if (!edge.Pressed) continue;
            switch (edge.Action)
            {
                case WorkshopActions.Combine: RaiseCombine(events, context.Tick, edge.Argument, "input"); break;
                case WorkshopActions.Order: RaiseOrder(events, context.Tick, edge.Argument, "input"); break;
                default: throw new InvalidOperationException("the player does not know the input action '" + edge.Action + "'");
            }
        }

        if (policy is null) return;
        foreach (var action in policy.Decide(_view))
        {
            events.Emit(new WorkshopPlayerDecided(context.Tick, "decide", $"{policy.Name} {action.Describe()} (known {_view.Known.Count}, recipes {_view.KnownRecipes.Count}, ordered {_view.Ordered.Count})"));
            var (name, argument) = action.Key;
            input.Press(name, argument);
            input.Release(name);
        }
    }

    private void RaiseCombine(IEventPort events, long tick, string argument, string source)
    {
        var parts = argument.Split('|');
        if (parts.Length > 2) throw new InvalidOperationException("combine input needs items or items|environment but got '" + argument + "'");
        var requestId = string.Create(CultureInfo.InvariantCulture, $"c{tick}-{_raisedThisTick++}");
        _view.InFlight.Add(requestId);
        events.Emit(new WorkshopPlayerDecided(tick, "raise", $"combine {parts[0]}{(parts.Length == 2 && parts[1].Length > 0 ? " in " + parts[1] : "")} as {requestId} from {source}"));
        events.Emit(new CombineRequested(requestId, parts[0], parts.Length == 2 ? parts[1] : ""));
    }

    private void RaiseOrder(IEventPort events, long tick, string argument, string source)
    {
        var parts = argument.Split('|');
        if (parts.Length > 2) throw new InvalidOperationException("order input needs recipe or recipe|count but got '" + argument + "'");
        var count = parts.Length == 2 ? int.Parse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture) : 1;
        if (!recipes.TryRecipe(parts[0], out var recipe)) throw new InvalidOperationException("order input names an unknown recipe '" + parts[0] + "'");
        if (recipe.Processors.Count != 1) throw new InvalidOperationException("recipe '" + recipe.Id + "' has " + recipe.Processors.Count + " processors; this player orders recipes with exactly one");
        var requestId = string.Create(CultureInfo.InvariantCulture, $"o{tick}-{_raisedThisTick++}");
        _view.InFlight.Add(requestId);
        _view.Ordered.Add(recipe.Id);
        _orderRequests[requestId] = recipe.Id;
        events.Emit(new WorkshopPlayerDecided(tick, "raise", $"order {recipe.Id} x{count} as {requestId} from {source}"));
        events.Emit(new OrderTransformationRequested(requestId, recipe.Id, recipe.Processors[0].EntityDefinition, recipe.Processors[0].Capability, count));
    }

    private void OnKnowledge(KnowledgeRestored restored)
    {
        foreach (var element in restored.Elements.Split(',', StringSplitOptions.RemoveEmptyEntries)) _view.Known.Add(element);
        foreach (var recipe in restored.Recipes.Split(',', StringSplitOptions.RemoveEmptyEntries)) _view.KnownRecipes.Add(recipe);
        foreach (var key in restored.Tried.Split(';', StringSplitOptions.RemoveEmptyEntries)) _view.Tried.Add(key);
    }

    private void OnCombined(Combined combined)
    {
        _view.InFlight.Remove(combined.RequestId);
        // A fizzle is chance, not an answer about the combination, so it stays open to another try.
        if (combined.Outcome != "fizzled") _view.Tried.Add(combined.Items + "|" + combined.Environment);
        foreach (var element in combined.Produced.Split(',', StringSplitOptions.RemoveEmptyEntries)) _view.Known.Add(element);
    }

    private void OnOrderResolved(TransformationOrderResolved resolved)
    {
        _view.InFlight.Remove(resolved.RequestId);
        if (!_orderRequests.Remove(resolved.RequestId, out var recipe)) return;
        // A refused order may be placed again later; an accepted one is done.
        if (!resolved.Accepted) _view.Ordered.Remove(recipe);
    }
}
