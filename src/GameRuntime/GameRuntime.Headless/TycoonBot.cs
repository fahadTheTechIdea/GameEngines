using System.Globalization;
using AIEngine;
using ScenarioEngine.Contracts;

namespace GameRuntime.Headless;

/// <summary>
/// One player bot: an agent of the tycoon AI document and the thin game-layer work around it. It tells the agent what the player has learned from events (the company,
/// the stock, the profit, the sales in flight, each refusal) and turns the intents the agent raises into the actions the player presses. It holds no rule about what to sell or when.
/// </summary>
public sealed class TycoonBot
{
    public const string AgentId = "player";
    private const long Clamp = 100_000_000_000;

    private readonly long _goal;
    private readonly long _lotStart;
    private readonly Dictionary<string, string> _told = new(StringComparer.Ordinal);
    private readonly List<string> _refusals = new();
    private readonly List<AiIntentRaised> _raised = new();
    private bool _added;
    private int _request;
    private int _refusalNumber;

    internal TycoonBot(string name, PlayerRandom random, ulong seed, long goalProfit)
    {
        Name = name;
        _goal = Math.Clamp(goalProfit, -Clamp, Clamp);
        var set = TycoonBots.Set;
        // The draw is the seed's only say: the first lot, inside the range the document declares for this behaviour.
        _lotStart = random.Next((int)Initial(set, name + "-lot-min"), (int)Initial(set, name + "-lot-max"));
        Component = new AiComponent(new AiRuntime(set, seed), set.MaxCost, "player-ai");
    }

    private static long Initial(AiSet set, string key) => set.TryKey(key, out var def) ? def.Initial.Number : throw new InvalidOperationException("the tycoon document has no key '" + key + "'");

    public string Name { get; }

    /// <summary>The AI component that runs the agent; the session adds it right after the player.</summary>
    public AiComponent Component { get; }

    private string NextRequest() => "bot-" + (_request++).ToString(CultureInfo.InvariantCulture);

    private void Tell(IEventPort events, string key, string value)
    {
        if (_told.TryGetValue(key, out var told) && told == value) return;
        _told[key] = value;
        events.Emit(new AiKeyPosted(Component.Id, NextRequest(), AgentId, key, value));
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Tells the agent what the player knows now: only what changed since the last time for the keys, and every refusal once.</summary>
    public void Sense(IEventPort events, PlayerView view)
    {
        if (!_added)
        {
            _added = true;
            events.Emit(new AiAgentAddRequested(Component.Id, NextRequest(), AgentId, Name));
            Tell(events, "lot-start", Number(_lotStart));
            Tell(events, "goal", Number(_goal));
        }
        Tell(events, "founded", view.Founded ? "true" : "false");
        Tell(events, "stock", Number(Math.Clamp(view.Stock, -1, 1_000_000_000_000)));
        Tell(events, "profit", Number(Math.Clamp(view.Profit, -Clamp, Clamp)));
        Tell(events, "in-flight", Number(view.SalesInFlight));
        foreach (var refusal in view.Refusals)
        {
            var id = "r" + (_refusalNumber++).ToString("D9", CultureInfo.InvariantCulture);
            _refusals.Add(id);
            events.Emit(new AiPerceptPosted(Component.Id, NextRequest(), AgentId, "refusal", id, "",
                AiValues.Encode(new Dictionary<string, FactValue> { ["no-stock"] = FactValue.From(refusal.Code == "NO_STOCK"), ["units"] = FactValue.From((long)refusal.Units) })));
        }
    }

    public void Collect(AiIntentRaised raised)
    {
        if (raised.Source == Component.Id) _raised.Add(raised);
    }

    /// <summary>The agent has thought: the actions it decided, and the refusals it has now taken in are forgotten, so each is acted on once.</summary>
    public IReadOnlyList<PlayerAction> Actions(IEventPort events)
    {
        var actions = new List<PlayerAction>();
        foreach (var raised in _raised)
        {
            var args = AiValues.Decode(TycoonBots.Set.Intents[raised.Name], raised.Args, AiCodes.ValueType);
            switch (raised.Name)
            {
                case "sell": actions.Add(PlayerAction.Sell((int)args["units"].Integer)); break;
                case "speed": actions.Add(PlayerAction.Speed((int)args["numerator"].Integer, (int)args["denominator"].Integer)); break;
                default: throw new InvalidOperationException("the player has no action for the intent '" + raised.Name + "'");
            }
        }
        _raised.Clear();
        foreach (var id in _refusals) events.Emit(new AiPerceptLost(Component.Id, NextRequest(), AgentId, "refusal", id));
        _refusals.Clear();
        return actions;
    }
}