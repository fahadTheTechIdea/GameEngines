using System.Globalization;
using AIEngine;
using ScenarioEngine.Contracts;

namespace GameRuntime.Combat.Headless;

/// <summary>
/// One commander bot: an agent of the skirmish AI document and the thin game-layer work around it. It tells the agent what the commander has learned from events
/// (the groups, who is hostile to whom, the distance, whether it may order now) and turns the intents the agent raises into the orders the commander presses.
/// It holds no rule about what to order; that is the behaviour it was built with.
/// </summary>
public sealed class SkirmishBot
{
    public const string AgentId = "commander";

    private readonly SkirmishPlan _plan;
    private readonly Dictionary<string, string> _told = new(StringComparer.Ordinal);
    private readonly List<AiIntentRaised> _raised = new();
    private bool _added;
    private int _request;

    internal SkirmishBot(string name, SkirmishPlan plan, ulong seed)
    {
        Name = name;
        _plan = plan;
        var set = SkirmishBots.Set;
        Component = new AiComponent(new AiRuntime(set, seed), set.MaxCost, "commander-ai");
    }

    public string Name { get; }

    /// <summary>The AI component that runs the agent; the session adds it right after the commander.</summary>
    public AiComponent Component { get; }

    private string NextRequest() => "bot-" + (_request++).ToString(CultureInfo.InvariantCulture);

    private void Tell(IEventPort events, string key, string value)
    {
        if (_told.TryGetValue("key/" + key, out var told) && told == value) return;
        _told["key/" + key] = value;
        events.Emit(new AiKeyPosted(Component.Id, NextRequest(), AgentId, key, value));
    }

    private void Tell(IEventPort events, string kind, string id, string owner, IEnumerable<KeyValuePair<string, FactValue>> attrs)
    {
        var text = AiValues.Encode(attrs);
        if (_told.TryGetValue(kind + "/" + id, out var told) && told == text) return;
        _told[kind + "/" + id] = text;
        events.Emit(new AiPerceptPosted(Component.Id, NextRequest(), AgentId, kind, id, owner, text));
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Tells the agent what the commander knows now. Only what changed since the last time is sent.</summary>
    public void Sense(IEventPort events, SkirmishView view)
    {
        if (!_added)
        {
            _added = true;
            events.Emit(new AiAgentAddRequested(Component.Id, NextRequest(), AgentId, Name));
            Tell(events, "start", Number(_plan.StartMetres));
            Tell(events, "seconds", Number(_plan.RoundSeconds));
            Tell(events, "terrain", AiValues.Encode(FactValue.From(_plan.Terrain)));
        }
        Tell(events, "can-act", Bool(view.Ready));
        Tell(events, "fighting", Bool(view.Phase == SkirmishPhase.Fight));
        Tell(events, "settled", Bool(view.Distance >= 0));
        Tell(events, "distance", Number(Math.Max(0, view.Distance)));
        foreach (var group in view.Groups.Values)
        {
            Tell(events, "group", group.Id, "", new Dictionary<string, FactValue>
            {
                ["side"] = FactValue.From((long)group.Side), ["health"] = FactValue.From(Math.Max(0, group.Health)), ["range"] = FactValue.From(Math.Max(0, group.RangeMetres)),
                ["standing"] = FactValue.From(group.Standing),
            });
            // Who is hostile to whom is what the commander has learned about the sides: another side's group is a foe, one of its own side is not.
            foreach (var other in view.Groups.Values)
                if (other.Side != group.Side)
                    Tell(events, "foe", group.Id + ">" + other.Id, group.Id, new Dictionary<string, FactValue>
                    {
                        ["group"] = FactValue.From(other.Id), ["health"] = FactValue.From(Math.Max(0, other.Health)), ["standing"] = FactValue.From(other.Standing),
                    });
        }
    }

    public void Collect(AiIntentRaised raised)
    {
        if (raised.Source == Component.Id) _raised.Add(raised);
    }

    /// <summary>The orders the agent decided this tick, from the intents it raised: a march, an engagement, or one round of fire made of every shot it raised.</summary>
    public IReadOnlyList<SkirmishOrder> Orders()
    {
        var orders = new List<SkirmishOrder>();
        var shots = new List<RoundShot>();
        foreach (var raised in _raised)
        {
            var args = AiValues.Decode(SkirmishBots.Set.Intents[raised.Name], raised.Args, AiCodes.ValueType);
            switch (raised.Name)
            {
                case "march":
                    orders.Add(new MarchOrder(args["group"].Text!, (int)args["metres"].Integer, (int)args["seconds"].Integer, args["terrain"].Text!));
                    break;
                case "engage":
                    orders.Add(new EngageOrder(args["attacker"].Text!, args["defender"].Text!, (int)args["metres"].Integer, (int)args["seconds"].Integer, args["cover"].Text!, args["terrain"].Text!, args["mutual"].Boolean));
                    break;
                case "fire":
                    shots.Add(new RoundShot(args["attacker"].Text!, args["defender"].Text!, (int)args["metres"].Integer, args["cover"].Text!, args["terrain"].Text!));
                    break;
                default:
                    // An intent the document declares that this bot has no order for would be accepted and ignored; the document and the bot have to agree.
                    throw new SkirmishUsageException("the bot does not know the intent '" + raised.Name + "'");
            }
        }
        _raised.Clear();
        if (shots.Count > 0) orders.Add(new RoundOrderSet(_plan.RoundSeconds, shots));
        return orders;
    }
}