using System.Globalization;

namespace GameRuntime.Combat.Headless;

/// <summary>
/// The commander. Like a game's player object in XNA or a Godot node polling Input, it acts only inside its own Update: it polls the
/// <see cref="InputState"/>, turns each pressed action into a request event, and tells its bot (if any) what it learned from earlier events. The bot's agent
/// decides in the AI component's Update of the same tick; the orders it raises are not carried out directly: the commander presses them on the same input state,
/// which the next tick polls, so every order is an input edge a journal can record and replay. Nothing it raises does anything before the combat component's
/// next Update answers it.
/// </summary>
public sealed class SkirmishPlayerComponent(InputState input, SkirmishBot? bot, string id = "commander") : IGameComponent
{
    private readonly SkirmishView _view = new();
    private IEventPort? _events;
    private int _raisedThisTick;

    public string Id { get; } = id;
    public SkirmishView View => _view;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<ForcesRestored>(OnForces);
        context.Events.Connect<Engagement>(OnEngagement);
        context.Events.Connect<Moved>(OnMoved);
        context.Events.Connect<Resupplied>(resupplied => _view.InFlight.Remove(resupplied.RequestId));
        context.Events.Connect<EngagementRefused>(refused => _view.InFlight.Remove(refused.RequestId));
        context.Events.Connect<Standing>(standing =>
        {
            if (!_view.Groups.TryGetValue(standing.Group, out var group)) return;
            group.State = standing.State;
            group.Morale = standing.Morale;
        });
        context.Events.Connect<SessionEnded>(_ => _view.Ended = true);
        context.Events.Connect<AiIntentRaised>(raised => bot?.Collect(raised));
        context.Events.Connect<AiStepDone>(OnDecided);
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
            Raise(events, context.Tick, SkirmishOrder.Parse(edge.Action, edge.Argument), "input");
        }

        bot?.Sense(events, _view);
    }

    /// <summary>The agent has thought this tick: whatever it decided becomes the orders the commander presses.</summary>
    private void OnDecided(AiStepDone done)
    {
        if (bot is null || done.Source != bot.Component.Id || _events is not { } events) return;
        foreach (var order in bot.Orders())
        {
            events.Emit(new SkirmishPlayerDecided(done.Tick, "decide", $"{bot.Name} orders {order.Action} {order.Key} (round {_view.Round + 1})"));
            input.Press(order.Action, order.Key);
            input.Release(order.Action);
        }
    }

    private void Raise(IEventPort events, long tick, SkirmishOrder order, string source)
    {
        var requestId = string.Create(CultureInfo.InvariantCulture, $"e{tick}-{_raisedThisTick++}");
        _view.InFlight.Add(requestId);
        events.Emit(new SkirmishPlayerDecided(tick, "raise", $"{order.Action} {order.Key} as {requestId} from {source}"));
        order.Request(events, requestId, _view);
    }

    private void OnForces(ForcesRestored restored)
    {
        _view.Round = (int)restored.Engagements;
        _view.Distance = restored.DistanceMetres;
        foreach (var text in restored.Groups.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = text.Split(',');
            var health = long.Parse(fields[2], CultureInfo.InvariantCulture);
            _view.Groups[fields[0]] = new SeenGroup(fields[0], int.Parse(fields[1], CultureInfo.InvariantCulture), health, long.Parse(fields[3], CultureInfo.InvariantCulture), long.Parse(fields[5], CultureInfo.InvariantCulture))
            {
                Morale = long.Parse(fields[4], CultureInfo.InvariantCulture),
                State = fields[6],
            };
        }
    }

    private void OnEngagement(Engagement engagement)
    {
        // A round answers with one engagement per order under one request id: the request is finished the first time.
        if (_view.InFlight.Remove(engagement.RequestId))
        {
            _view.Round++;
            _view.Phase = SkirmishPhase.Approach;
        }
        if (_view.Groups.TryGetValue(engagement.Attacker, out var attacker)) attacker.Health = engagement.AttackerHealth;
        if (_view.Groups.TryGetValue(engagement.Defender, out var defender)) defender.Health = engagement.DefenderHealth;
    }

    private void OnMoved(Moved moved)
    {
        _view.InFlight.Remove(moved.RequestId);
        _view.Distance = moved.ToMetres;
        _view.Phase = SkirmishPhase.Fight;
    }
}
