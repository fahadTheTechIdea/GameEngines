using System.Globalization;
using AIEngine;
using CombatEngine;
using ScenarioEngine.Contracts;

namespace GameRuntime.Field.Headless;

/// <summary>
/// The supply service of the field game, as a component. It hands the quartermaster agent (the <c>quartermaster</c> behaviour of the field document) what the
/// service knows: each group still in the fight with its people and tanks, each battery's load, and what each depot holds of each resource (read from the world, which
/// owns the balance). The agent decides when to look, which battery is offered rounds, which tank fuel, which group people and how much, never more than the depot holds
/// after its earlier offers; this component turns each offer into a request to combat and the stock layer records exactly what fitted. It keeps no balance and no
/// threshold and no schedule, so a loaded game continues the same way.
/// </summary>
public sealed class FieldQuartermaster(CombatComponent combat, FieldCommanderState state, ISquadRoster roster, Func<int, string, long> held, Func<bool> stockSettled, AiComponent ai,
    string id = "quartermaster") : IGameComponent
{
    public const string AgentId = "quartermaster";

    private readonly HashSet<string> _outstanding = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _told = new(StringComparer.Ordinal);
    private readonly List<AiIntentRaised> _offers = new();
    private IEventPort? _events;
    private bool _added;
    private int _request;

    public string Id { get; } = id;

    /// <summary>True when no offer is waiting for combat to answer.</summary>
    public bool Settled => _outstanding.Count == 0;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<Resupplied>(answered => _outstanding.Remove(answered.RequestId));
        context.Events.Connect<Refuelled>(answered => _outstanding.Remove(answered.RequestId));
        context.Events.Connect<Reinforced>(answered => _outstanding.Remove(answered.RequestId));
        context.Events.Connect<EngagementRefused>(refused => _outstanding.Remove(refused.RequestId));
        context.Events.Connect<AiIntentRaised>(raised => { if (raised.Source == ai.Id) _offers.Add(raised); });
        context.Events.Connect<AiStepDone>(done => { if (done.Source == ai.Id) Request(done.Tick); });
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        if (!state.Spawned || state.Over) return;
        if (!_added)
        {
            _added = true;
            events.Emit(new AiAgentAddRequested(ai.Id, NextRequest(), AgentId, "quartermaster"));
        }
        // The agent may act only when nothing is in flight: no offer waits for combat, combat has no request open and the stock layer has recorded everything.
        var ready = _outstanding.Count == 0 && combat.IsQuiet && stockSettled();
        if (ready) Sense(events);
        TellKey(events, "ready", ready ? "true" : "false");
    }

    private string NextRequest() => "supply-" + (_request++).ToString(CultureInfo.InvariantCulture);

    private void TellKey(IEventPort events, string key, string value)
    {
        if (_told.TryGetValue("key/" + key, out var told) && told == value) return;
        _told["key/" + key] = value;
        events.Emit(new AiKeyPosted(ai.Id, NextRequest(), AgentId, key, value));
    }

    private void Tell(IEventPort events, string kind, string id, string owner, IEnumerable<KeyValuePair<string, FactValue>> attrs)
    {
        var text = AiValues.Encode(attrs);
        if (_told.TryGetValue(kind + "/" + id, out var told) && told == text) return;
        _told[kind + "/" + id] = text;
        events.Emit(new AiPerceptPosted(ai.Id, NextRequest(), AgentId, kind, id, owner, text));
    }

    internal static long Cap(long value) => Math.Clamp(value, 0, FieldAi.MaxQuantity);

    /// <summary>
    /// Tells the agent how every group in the fight stands and what each depot holds of what they could be given. A group that is destroyed or routed, or that no squad stands for,
    /// is told as not to be supplied. The stock a group draws on is named by the depot and the resource, so the agent can keep count of what it has promised.
    /// </summary>
    private void Sense(IEventPort events)
    {
        var stocks = new Dictionary<string, long>(StringComparer.Ordinal);
        string Stock(int depot, string resource)
        {
            var name = depot.ToString(CultureInfo.InvariantCulture) + ":" + resource;
            if (!stocks.ContainsKey(name)) stocks[name] = Cap(held(depot, resource));
            return name;
        }
        foreach (var group in combat.Groups.Values)
        {
            var supplyable = !group.Destroyed && !group.Routed && roster.TrySquad(group.Id, out _, out _) && roster.TryDepot(group.Id, out _);
            var depot = 0;
            if (supplyable) roster.TryDepot(group.Id, out depot);
            var fuelStock = supplyable && group.FuelType is { } fuel ? Stock(depot, StockResources.Fuel(fuel)) : "";
            var peopleStock = supplyable ? Stock(depot, StockResources.People) : "";
            Tell(events, "unit", group.Id, "", new Dictionary<string, FactValue>
            {
                ["supplyable"] = FactValue.From(supplyable), ["members"] = FactValue.From(Cap(group.Members)), ["initial"] = FactValue.From(Cap(group.InitialMembers)),
                ["people-stock"] = FactValue.From(peopleStock), ["fuel"] = FactValue.From(Cap(group.Fuel)), ["fuel-room"] = FactValue.From(Cap(group.FuelRoom)), ["fuel-stock"] = FactValue.From(fuelStock),
            });
            for (var i = 0; i < group.Batteries.Count; i++)
            {
                var battery = group.Batteries[i];
                var ammoStock = supplyable && battery.Ammo is not null ? Stock(depot, StockResources.Ammo(battery.Ammo.Id)) : "";
                // The place in the group list is part of the id, so the agent meets a group batteries in the order the group lists them.
                Tell(events, "battery", group.Id + "/" + i.ToString("D4", CultureInfo.InvariantCulture), group.Id, new Dictionary<string, FactValue>
                {
                    ["group"] = FactValue.From(group.Id), ["battery"] = FactValue.From(battery.Id), ["ammo"] = FactValue.From(battery.Ammo is not null),
                    ["rounds"] = FactValue.From(Cap(battery.Rounds)), ["capacity"] = FactValue.From(Cap(battery.Capacity)), ["stock"] = FactValue.From(ammoStock),
                });
            }
        }
        foreach (var (name, amount) in stocks) Tell(events, "stock", name, "", new Dictionary<string, FactValue> { ["held"] = FactValue.From(amount) });
    }

    /// <summary>The agent has thought this tick: each offer it raised becomes a request to combat, numbered in the order it was raised.</summary>
    private void Request(long tick)
    {
        var number = 0;
        var events = _events!;
        foreach (var offer in _offers)
        {
            var args = AiValues.Decode(FieldAi.Set.Intents[offer.Name], offer.Args, AIEngine.AiCodes.ValueType);
            var requestId = string.Create(CultureInfo.InvariantCulture, $"supply-t{tick}-{++number}");
            _outstanding.Add(requestId);
            switch (offer.Name)
            {
                case "resupply": events.Emit(new ResupplyRequested(requestId, args["group"].Text!, args["battery"].Text!, args["rounds"].Integer)); break;
                case "refuel": events.Emit(new RefuelRequested(requestId, args["group"].Text!, args["amount"].Integer)); break;
                case "reinforce": events.Emit(new ReinforceRequested(requestId, args["group"].Text!, args["members"].Integer)); break;
                default: throw new InvalidOperationException("the quartermaster has no request for the intent '" + offer.Name + "'");
            }
        }
        _offers.Clear();
    }
}