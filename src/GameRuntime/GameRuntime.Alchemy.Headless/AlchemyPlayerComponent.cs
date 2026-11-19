using System.Globalization;

namespace GameRuntime.Alchemy.Headless;

/// <summary>
/// The player. Like a game's player object in XNA or a Godot node polling Input, it acts only inside its own Update: it polls the
/// <see cref="InputState"/>, turns each pressed combine action into a <see cref="CombineRequested"/> event raised through the event
/// port, and lets its policy (if any) decide from what it learned from earlier events. A policy does not act directly: it presses keys
/// on the same input state, which the next tick polls, so every act of the player is an input edge a journal can record and replay.
/// Nothing it raises does anything before the alchemy component's next Update answers it.
/// </summary>
public sealed class AlchemyPlayerComponent(InputState input, IAlchemyPolicy? policy, IReadOnlyList<string> environmentOptions, string id = "player") : IGameComponent
{
    private readonly AlchemyView _view = new(environmentOptions);
    private IEventPort? _events;
    private int _raisedThisTick;

    public string Id { get; } = id;
    public AlchemyView View => _view;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<KnowledgeRestored>(OnKnowledge);
        context.Events.Connect<Combined>(OnCombined);
        context.Events.Connect<CombineRefused>(refused => _view.InFlight.Remove(refused.RequestId));
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
            if (edge.Action != AlchemyActions.Combine) throw new InvalidOperationException("the player does not know the input action '" + edge.Action + "'");
            Raise(events, context.Tick, edge.Argument, "input");
        }

        if (policy is null) return;
        foreach (var attempt in policy.Decide(_view))
        {
            events.Emit(new AlchemyPlayerDecided(context.Tick, "decide", $"{policy.Name} tries {attempt.Items}{(attempt.Environment.Length == 0 ? "" : " in " + attempt.Environment)} (known {_view.Known.Count}, tried {_view.Tried.Count})"));
            input.Press(AlchemyActions.Combine, attempt.Key);
            input.Release(AlchemyActions.Combine);
        }
    }

    private void Raise(IEventPort events, long tick, string argument, string source)
    {
        var parts = argument.Split('|');
        if (parts.Length > 2) throw new InvalidOperationException("combine input needs items or items|environment but got '" + argument + "'");
        var requestId = string.Create(CultureInfo.InvariantCulture, $"c{tick}-{_raisedThisTick++}");
        _view.InFlight.Add(requestId);
        events.Emit(new AlchemyPlayerDecided(tick, "raise", $"combine {parts[0]}{(parts.Length == 2 && parts[1].Length > 0 ? " in " + parts[1] : "")} as {requestId} from {source}"));
        events.Emit(new CombineRequested(requestId, parts[0], parts.Length == 2 ? parts[1] : ""));
    }

    private void OnKnowledge(KnowledgeRestored restored)
    {
        foreach (var element in restored.Elements.Split(',', StringSplitOptions.RemoveEmptyEntries)) _view.Known.Add(element);
        foreach (var key in restored.Tried.Split(';', StringSplitOptions.RemoveEmptyEntries)) _view.Tried.Add(key);
    }

    private void OnCombined(Combined combined)
    {
        _view.InFlight.Remove(combined.RequestId);
        // A fizzle is chance, not an answer about the combination, so it stays open to another try.
        if (combined.Outcome != "fizzled") _view.Tried.Add(combined.Items + "|" + combined.Environment);
        foreach (var element in combined.Produced.Split(',', StringSplitOptions.RemoveEmptyEntries)) _view.Known.Add(element);
    }
}
