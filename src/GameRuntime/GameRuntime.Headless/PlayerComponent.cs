using System.Globalization;
using System.Text.Json;

namespace GameRuntime.Headless;

/// <summary>The input actions the player understands. A script, a bot or (in Godot) an InputMap action can press them.</summary>
public static class PlayerActions
{
    /// <summary>A tap: one sale of the pressed argument's units.</summary>
    public const string Sell = "sell";
    /// <summary>Held: one sale of the pressed argument's units on every tick until released.</summary>
    public const string AutoSell = "autosell";
    /// <summary>A tap: ask for a game speed, argument <c>n/d</c>.</summary>
    public const string Speed = "speed";
}

/// <summary>
/// The player. Like a game's player object in XNA or a Godot node polling <c>Input</c>, it acts only inside its own Update:
/// it polls the <see cref="InputState"/> (edges once, held state every tick), turns what it reads into events raised through
/// the event port, and tells its bot (if any) what it learned from earlier events; the bot's agent decides in the AI component's Update of the same tick. A bot does not act
/// directly: it presses keys on the same <see cref="InputState"/>, which the next tick polls, so every act of the player
/// is an input edge that a journal can record and replay. Nothing it raises does anything before a later Update delivers it.
/// </summary>
public sealed class PlayerComponent(InputState input, TycoonBot? bot, long goalProfit, string id = "player") : IGameComponent
{
    private readonly PlayerView _view = new(goalProfit);
    private IEventPort? _events;
    private int _raisedThisTick;
    private long _totalMicroseconds;

    public string Id { get; } = id;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<MissionEvent>(OnMissionEvent);
        context.Events.Connect<ObservationPosted>(OnObservation);
        context.Events.Connect<CommandResolved>(OnCommandResolved);
        context.Events.Connect<SessionEnded>(_ => _view.Ended = true);
        context.Events.Connect<AiIntentRaised>(raised => bot?.Collect(raised));
        context.Events.Connect<AiStepDone>(OnDecided);
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        _view.Tick = context.Tick;
        _totalMicroseconds = context.TotalMicroseconds;
        _raisedThisTick = 0;
        var edges = input.Poll();
        if (_view.Ended) return;

        foreach (var edge in edges)
        {
            if (!edge.Pressed) continue;
            switch (edge.Action)
            {
                case PlayerActions.Sell: RaiseSale(events, context, Units(edge), "input"); break;
                case PlayerActions.Speed: RaiseSpeed(events, context, edge.Argument); break;
                case PlayerActions.AutoSell: break; // held state is read below, on this tick and every later one
                default: throw new InvalidOperationException("the player does not know the input action '" + edge.Action + "'");
            }
        }
        if (input.IsHeld(PlayerActions.AutoSell, out var held)) RaiseSale(events, context, Units(held), "held");

        bot?.Sense(events, _view);
        _view.RefusalList.Clear();
    }

    /// <summary>The agent has thought this tick: whatever it decided becomes the keys the player presses.</summary>
    private void OnDecided(AiStepDone done)
    {
        if (bot is null || done.Source != bot.Component.Id || _events is not { } events || _view.Ended) return;
        foreach (var action in bot.Actions(events)) PressKey(events, done.Tick, action);
    }

    private void PressKey(IEventPort events, long tick, PlayerAction action)
    {
        var detail = $"{bot!.Name} decides {Describe(action)} (profit {_view.Profit}/{_view.GoalProfit}, stock {(_view.Stock < 0 ? "?" : _view.Stock.ToString(CultureInfo.InvariantCulture))}, game time {_totalMicroseconds / 1000}ms)";
        events.Emit(new PlayerDecided(tick, "decide", detail));
        switch (action.Kind)
        {
            case PlayerActionKind.Sell:
                input.Press(PlayerActions.Sell, action.A.ToString(CultureInfo.InvariantCulture));
                input.Release(PlayerActions.Sell);
                break;
            case PlayerActionKind.Speed:
                input.Press(PlayerActions.Speed, action.A.ToString(CultureInfo.InvariantCulture) + "/" + action.B.ToString(CultureInfo.InvariantCulture));
                input.Release(PlayerActions.Speed);
                break;
        }
    }

    private void RaiseSale(IEventPort events, in UpdateContext context, int units, string source)
    {
        var commandId = $"p{context.Tick}-{_raisedThisTick++}";
        _view.InFlight[commandId] = (context.Tick, units);
        events.Emit(new PlayerDecided(context.Tick, "raise", $"sell {units} as {commandId} from {source}"));
        events.Emit(new MissionCommandRequested(commandId, HeadlessSession.Controller, TycoonMissionLayer.Sell, "{\"units\":" + units.ToString(CultureInfo.InvariantCulture) + "}"));
    }

    private void RaiseSpeed(IEventPort events, in UpdateContext context, string argument)
    {
        var parts = argument.Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var numerator)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var denominator))
            throw new InvalidOperationException("speed input needs n/d but got '" + argument + "'");
        events.Emit(new PlayerDecided(context.Tick, "raise", $"speed {numerator}/{denominator}"));
        events.Emit(new ClockRequested(numerator, denominator));
    }

    private static int Units(InputEdgeRaised edge) => Units(edge.Argument);

    private static int Units(string argument) =>
        int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out var units)
            ? units : throw new InvalidOperationException("sell input needs a whole number of units but got '" + argument + "'");

    private static string Describe(PlayerAction action) =>
        action.Kind == PlayerActionKind.Sell
            ? "sell " + action.A.ToString(CultureInfo.InvariantCulture)
            : "speed " + action.A.ToString(CultureInfo.InvariantCulture) + "/" + action.B.ToString(CultureInfo.InvariantCulture);

    private void OnMissionEvent(MissionEvent evt)
    {
        if (evt.Kind != "gf_tycoon.founded" && evt.Kind != "gf_tycoon.sold" && evt.Kind != "gf_tycoon.cash") return;
        using var document = JsonDocument.Parse(evt.PayloadJson);
        var payload = document.RootElement;
        switch (evt.Kind)
        {
            case "gf_tycoon.founded":
                _view.Founded = true;
                _view.Stock = payload.GetProperty("stock").GetInt64();
                break;
            case "gf_tycoon.sold":
                _view.Stock = payload.GetProperty("stock").GetInt64();
                break;
            case "gf_tycoon.cash":
                _view.Profit = payload.GetProperty("profit").GetInt64();
                break;
        }
    }

    private void OnObservation(ObservationPosted posted)
    {
        if (posted.Kind != TycoonMissionLayer.CashChanged && posted.Kind != TycoonMissionLayer.ObjectiveChanged) return;
        using var document = JsonDocument.Parse(posted.PayloadJson);
        var payload = document.RootElement;
        if (posted.Kind == TycoonMissionLayer.CashChanged) _view.Cash = payload.GetProperty("after").GetInt64() >> Beep.SimEngine.Fix.FractionalBits;
        else if (payload.GetProperty("state").GetString() == "Completed") _view.ObjectivesCompleted++;
    }

    private void OnCommandResolved(CommandResolved resolved)
    {
        if (resolved.Engine != "mission" || !_view.InFlight.Remove(resolved.CommandId, out var asked)) return;
        if (!resolved.Applied)
            _view.RefusalList.Add(new RefusedSale(resolved.CommandId, asked.Units, asked.Tick, resolved.Code));
    }
}
