using System.Globalization;
using CombatEngine;

namespace GameRuntime;

/// <summary>
/// Carries what combat reports into the stock of the squads and their depots. It listens only to events: rounds fired become consumed stock,
/// members lost become destroyed people, a group wiped out loses what it still held, a group that routed is withdrawn with all it held, rounds a
/// battery took in and members a group took in move from the depot of their side to the squad, and once the squads are raised each is issued its
/// people and rounds and each depot its reserve. What leaves is a few requests a tick, at most linesPerTick units of work in all, so one big round
/// cannot make one big frame; what does not fit waits in order for the next tick. A flow line is one unit; a move between holders or a
/// reinforcement is eight, because the engine takes about ten times as long over it (measured: 0.145 ms a line against 1.2 ms a reinforcement).
/// The world holds the balances; this component holds none, so it has nothing to restore after a load. If the world refuses a request the
/// ledger and combat disagree, which is a fault, not something to carry on past.
/// </summary>
public sealed class CombatStockBridge(IReadOnlyList<Group> groups, ISquadRoster roster, int linesPerTick, int depotPercent, string id = "stock") : IGameComponent
{
    /// <summary>Units of work a flow line, a move and a reinforcement cost against the budget of a tick.</summary>
    public const int FlowLineCost = 1, MoveCost = 8, ReinforcementCost = 8;

    private readonly record struct Reinforcement(int Depot, int Squad, int Count);

    private readonly Dictionary<string, Group> _groups = groups.ToDictionary(g => g.Id, StringComparer.Ordinal);
    private readonly List<StockLine> _pending = new();
    private readonly List<StockMove> _moves = new();
    private readonly List<Reinforcement> _reinforcements = new();
    private readonly HashSet<string> _outstanding = new(StringComparer.Ordinal);
    private IEventPort? _events;
    private int _batch;

    public string Id { get; } = id;

    /// <summary>
    /// True when nothing is waiting to be sent or answered, so a checkpoint loses nothing, the supply rule may look at the depots, and no round is asked
    /// for while members that combat already counts are still on their way to their squads (the squads could not take the losses of members they do not have yet).
    /// </summary>
    public bool Settled => _pending.Count == 0 && _moves.Count == 0 && _reinforcements.Count == 0 && _outstanding.Count == 0;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<SquadsRaised>(_ => Issue());
        context.Events.Connect<AmmoSpent>(OnAmmoSpent);
        context.Events.Connect<Casualty>(OnCasualty);
        context.Events.Connect<Standing>(OnStanding);
        context.Events.Connect<Resupplied>(OnResupplied);
        context.Events.Connect<Reinforced>(OnReinforced);
        context.Events.Connect<PlatformsLost>(OnPlatformsLost);
        context.Events.Connect<FuelBurned>(OnFuelBurned);
        context.Events.Connect<Refuelled>(OnRefuelled);
        context.Events.Connect<CommandResolved>(OnResolved);
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        long budget = linesPerTick;
        var sentAny = false;
        if (_pending.Count != 0)
        {
            var merged = Merged();
            var sent = merged.GetRange(0, (int)Math.Min(budget / FlowLineCost, merged.Count));
            Send(events, context.Tick, "stock", StockFlowEffect.EffectName, StockPayload.Write(sent));
            budget -= sent.Count * FlowLineCost;
            sentAny = true;
            // What did not fit keeps its place and goes in a later tick.
            _pending.Clear();
            _pending.AddRange(merged.Skip(sent.Count));
        }
        if (_moves.Count != 0)
        {
            var merged = MergedMoves();
            // Something is always sent when nothing else was, however small the budget, so slow work still finishes.
            var count = (int)Math.Min(Math.Max(budget / MoveCost, sentAny ? 0 : 1), merged.Count);
            if (count > 0)
            {
                var sent = merged.GetRange(0, count);
                Send(events, context.Tick, "supply", StockSupplyEffect.EffectName, StockMovePayload.Write(sent));
                budget -= count * MoveCost;
                sentAny = true;
            }
            _moves.Clear();
            _moves.AddRange(merged.Skip(count));
        }
        while (_reinforcements.Count != 0 && (budget >= ReinforcementCost || !sentAny))
        {
            var next = _reinforcements[0];
            _reinforcements.RemoveAt(0);
            Send(events, context.Tick, "reinforce", StockReinforceEffect.EffectName,
                string.Create(CultureInfo.InvariantCulture, $"{{\"depot\":{next.Depot},\"squad\":{next.Squad},\"count\":{next.Count}}}"));
            budget -= ReinforcementCost;
            sentAny = true;
        }
    }

    private void Send(IEventPort events, long tick, string prefix, string effect, string payload)
    {
        var requestId = prefix + "-" + (++_batch).ToString(CultureInfo.InvariantCulture);
        _outstanding.Add("effect:" + requestId);
        events.Emit(new EffectRequested(requestId, "sim." + effect, payload, tick));
    }

    /// <summary>The pending lines with same-kind lines on one holder resource summed, in the order each first appeared, take-everything lines last.</summary>
    private List<StockLine> Merged()
    {
        var merged = new List<StockLine>(_pending.Count);
        var at = new Dictionary<(string Kind, int Squad, string Resource), int>();
        foreach (var line in _pending)
        {
            var key = (line.Flow.Name, line.Squad, line.Resource);
            if (at.TryGetValue(key, out var index)) merged[index] = merged[index] with { Amount = checked(merged[index].Amount + line.Amount) };
            else { at[key] = merged.Count; merged.Add(line); }
        }
        // A group wiped out in a round still fired in that round, so whatever it held is taken only after everything else of the request.
        return [.. merged.Where(l => !l.Flow.Everything), .. merged.Where(l => l.Flow.Everything)];
    }

    private List<StockMove> MergedMoves()
    {
        var merged = new List<StockMove>(_moves.Count);
        var at = new Dictionary<(int From, int To, string Resource), int>();
        foreach (var move in _moves)
        {
            var key = (move.From, move.To, move.Resource);
            if (at.TryGetValue(key, out var index)) merged[index] = merged[index] with { Amount = checked(merged[index].Amount + move.Amount) };
            else { at[key] = merged.Count; merged.Add(move); }
        }
        return merged;
    }

    private void Issue()
    {
        var reserves = new SortedDictionary<int, SortedDictionary<string, long>>();
        foreach (var group in _groups.Values)
        {
            if (!roster.TrySquad(group.Id, out var squad, out var strength)) continue;
            _pending.Add(new StockLine(StockFlows.Issued, squad, StockResources.People, strength));
            var holdings = StockContent.Holdings(group);
            foreach (var (resource, held) in holdings)
                if (held > 0) _pending.Add(new StockLine(StockFlows.Issued, squad, resource, checked((int)held)));
            if (depotPercent <= 0 || !roster.TryDepot(group.Id, out var depot)) continue;
            if (!reserves.TryGetValue(depot, out var reserve)) reserves[depot] = reserve = new SortedDictionary<string, long>(StringComparer.Ordinal);
            reserve[StockResources.People] = reserve.GetValueOrDefault(StockResources.People) + strength;
            // A depot keeps people, rounds and fuel in reserve; platform units are not replaced from it.
            foreach (var (resource, held) in holdings) if (resource != StockResources.Platforms) reserve[resource] = reserve.GetValueOrDefault(resource) + held;
        }
        foreach (var (depot, reserve) in reserves)
            foreach (var (resource, total) in reserve)
                if (total * depotPercent / 100 is var amount and > 0) _pending.Add(new StockLine(StockFlows.Issued, depot, resource, checked((int)amount)));
    }

    private void OnAmmoSpent(AmmoSpent spent)
    {
        if (!roster.TrySquad(spent.Group, out var squad, out _)) return;
        var battery = _groups[spent.Group].Batteries.Single(b => b.Id == spent.Battery);
        _pending.Add(new StockLine(StockFlows.Consumed, squad, StockResources.Ammo(battery.Ammo!.Id), checked((int)spent.Rounds)));
    }

    private void OnCasualty(Casualty casualty)
    {
        if (roster.TrySquad(casualty.Group, out var squad, out _))
            _pending.Add(new StockLine(StockFlows.Destroyed, squad, StockResources.People, checked((int)casualty.Lost)));
    }

    private void OnStanding(Standing standing)
    {
        if (!roster.TrySquad(standing.Group, out var squad, out _)) return;
        var ammunition = StockContent.HeldResources(_groups[standing.Group]);
        switch (standing.State)
        {
            case "destroyed":
                foreach (var resource in ammunition) _pending.Add(new StockLine(StockFlows.Abandoned, squad, resource, 0));
                break;
            case "routed":
                _pending.Add(new StockLine(StockFlows.Withdrawn, squad, StockResources.People, 0));
                foreach (var resource in ammunition) _pending.Add(new StockLine(StockFlows.Withdrawn, squad, resource, 0));
                break;
        }
    }

    private void OnPlatformsLost(PlatformsLost lost)
    {
        if (!roster.TrySquad(lost.Group, out var squad, out _)) return;
        _pending.Add(new StockLine(StockFlows.Destroyed, squad, StockResources.Platforms, checked((int)lost.Lost)));
        // The fuel the platforms carried went down with them.
        if (lost.FuelLostMilli > 0 && _groups[lost.Group].FuelType is { } fuel)
            _pending.Add(new StockLine(StockFlows.Destroyed, squad, StockResources.Fuel(fuel), checked((int)lost.FuelLostMilli)));
    }

    private void OnFuelBurned(FuelBurned burned)
    {
        if (roster.TrySquad(burned.Group, out var squad, out _) && _groups[burned.Group].FuelType is { } fuel)
            _pending.Add(new StockLine(StockFlows.Consumed, squad, StockResources.Fuel(fuel), checked((int)burned.BurnedMilli)));
    }

    private void OnRefuelled(Refuelled refuelled)
    {
        if (refuelled.AcceptedMilli <= 0 || !roster.TrySquad(refuelled.Group, out var squad, out _) || !roster.TryDepot(refuelled.Group, out var depot)) return;
        if (_groups[refuelled.Group].FuelType is { } fuel)
            _moves.Add(new StockMove(depot, squad, StockResources.Fuel(fuel), checked((int)refuelled.AcceptedMilli)));
    }

    private void OnResupplied(Resupplied supplied)
    {
        if (supplied.Accepted <= 0 || !roster.TrySquad(supplied.Group, out var squad, out _) || !roster.TryDepot(supplied.Group, out var depot)) return;
        var battery = _groups[supplied.Group].Batteries.Single(b => b.Id == supplied.Battery);
        _moves.Add(new StockMove(depot, squad, StockResources.Ammo(battery.Ammo!.Id), checked((int)supplied.Accepted)));
    }

    private void OnReinforced(Reinforced reinforced)
    {
        if (reinforced.Accepted <= 0 || !roster.TrySquad(reinforced.Group, out var squad, out _) || !roster.TryDepot(reinforced.Group, out var depot)) return;
        _reinforcements.Add(new Reinforcement(depot, squad, checked((int)reinforced.Accepted)));
    }

    private void OnResolved(CommandResolved resolved)
    {
        if (resolved.Code == "QUEUED" || !_outstanding.Remove(resolved.CommandId)) return;
        if (!resolved.Applied) throw new InvalidOperationException("the world refused stock flows " + resolved.CommandId + " (" + resolved.Code + "): the stock ledger and combat disagree");
    }
}
