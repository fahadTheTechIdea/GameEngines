using System.Globalization;
using System.Text.Json;
using AIEngine;
using ScenarioEngine.Contracts;

namespace GameRuntime.Field.Headless;

/// <summary>
/// The game logic of the field game, as a component that acts only inside its own Update and only on what events told it. At the first tick
/// it asks the game layer for the two armies' squads (director effects, applied by the world on its next tick). Once both are raised it hands each side
/// to an AI agent (the <c>commander</c> behaviour of the field document) and tells it what its side has learned of the groups; the agents decide which standing
/// group fires at which and when a round falls due, and this component turns their intents into one round of fire between the combat groups. Each member the combat
/// engine reports lost it reports to the world as casualties of the squad that group stands for. The fight ends when one side has no group standing, every request is
/// answered and the stock layer has recorded everything combat reported. It holds no targeting rule and no schedule.
/// </summary>
public sealed class FieldCommander(FieldForces forces, FieldOptions options, FieldCommanderState state, Func<bool> stockSettled, AiComponent ai, Func<bool>? authorised = null, string id = "commander") : IGameComponent
{
    public const string AgentPrefix = "commander-";

    private sealed class Seen(int side, long health, string standing)
    {
        public int Side { get; } = side;
        public long Health { get; set; } = health;
        public string Standing { get; set; } = standing;
        public bool Fighting => Standing is "active" or "suppressed";
    }

    private readonly SortedDictionary<string, Seen> _groups = new(StringComparer.Ordinal);
    private readonly HashSet<string> _outstanding = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _told = new(StringComparer.Ordinal);
    private readonly List<AiIntentRaised> _fires = new();
    private IEventPort? _events;
    private bool _roundOpen;
    private bool _agents;
    private int _request;
    private long _now;

    public string Id { get; } = id;
    public FieldCommanderState State => state;

    /// <summary>True when nothing waits for an answer and no round is owed, so a checkpoint loses nothing. A round is owed while an agent has seen a beat go by and not yet ordered it.</summary>
    public bool Settled => _outstanding.Count == 0 && !RoundOwed;

    private bool RoundOwed => _agents && ai.Runtime.Agents.Any(agent => ai.Runtime.Key(agent, "due").Boolean);

    public string Fingerprint() =>
        string.Create(CultureInfo.InvariantCulture, $"field round={state.Round} requested={state.Requested} spawned={state.Spawned} over={state.Over} outstanding={_outstanding.Count} head={Head(forces.SideA)}/{Head(forces.SideB)}");

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<ForcesRestored>(OnForces);
        context.Events.Connect<Engagement>(OnEngagement);
        context.Events.Connect<EngagementRefused>(refused => Answered(refused.RequestId, "refused " + refused.RequestId + " " + refused.Code));
        context.Events.Connect<Standing>(OnStanding);
        context.Events.Connect<Casualty>(OnCasualty);
        context.Events.Connect<ObservationPosted>(OnObservation);
        context.Events.Connect<CommandResolved>(OnResolved);
        context.Events.Connect<AiIntentRaised>(raised => { if (raised.Source == ai.Id) _fires.Add(raised); });
        context.Events.Connect<AiStepDone>(done => { if (done.Source == ai.Id) OrderRound(done.Tick); });
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        _now = context.Tick;
        if (state.Over) return;
        if (!state.Requested)
        {
            state.Requested = true;
            Spawn(events, context.Tick, "spawn-a", forces.FormationA, forces.SquadsA, 10, 10);
            Spawn(events, context.Tick, "spawn-b", forces.FormationB, forces.SquadsB, 10, 150);
            return;
        }
        if (!state.Spawned) return;
        if (!_agents) RaiseAgents(events);
        if (_roundOpen && _outstanding.Count == 0)
        {
            _roundOpen = false;
            events.Emit(new FieldProgress(context.Tick, "round", string.Create(CultureInfo.InvariantCulture,
                $"{state.Round} headcount a={Head(forces.SideA)}/{Total(forces.SideA)} b={Head(forces.SideB)}/{Total(forces.SideB)} groups-standing a={forces.SideA.Count(Fighting)} b={forces.SideB.Count(Fighting)}")));
        }
        var ready = false;
        if (_outstanding.Count == 0 && stockSettled())
        {
            var aOut = !forces.SideA.Any(Fighting);
            var bOut = !forces.SideB.Any(Fighting);
            // When a side is out, the agents order nothing whatever they were last told: the side that is out has no group left to fire, and the other has no foe left to fire at,
            // and both are told so (Describe) before the agents think.
            if (aOut || bOut)
            {
                state.Over = true;
                events.Emit(new SessionEnded(context.Tick, aOut && bOut ? "mutual" : aOut ? "side-2-wins" : "side-1-wins"));
                return;
            }
            // Whoever hosts the game may hold the squads back (a briefing not yet accepted); until it lets them, no agent is ready to order.
            ready = authorised?.Invoke() ?? true;
        }
        foreach (var agent in AgentIds) Tell(events, agent, "ready", ready ? "true" : "false");
    }

    /// <summary>One commander agent for each side.</summary>
    public const int AgentCount = 2;

    private static IEnumerable<string> AgentIds => [AgentPrefix + "1", AgentPrefix + "2"];

    private bool Fighting(string group) => _groups.TryGetValue(group, out var seen) && seen.Fighting;

    private int Head(IReadOnlyList<string> side) => side.Sum(g => state.SquadOf.TryGetValue(g, out var squad) && state.Headcount.TryGetValue(squad, out var left) ? left : 0);

    private int Total(IReadOnlyList<string> side) => (side == forces.SideA ? state.StrengthA * forces.SquadsA : state.StrengthB * forces.SquadsB);

    // ---- the agents ---------------------------------------------------------------------------------------------------------------------

    private string NextRequest() => "field-" + (_request++).ToString(CultureInfo.InvariantCulture);

    private void Tell(IEventPort events, string agent, string key, string value)
    {
        if (_told.TryGetValue(agent + "/key/" + key, out var told) && told == value) return;
        _told[agent + "/key/" + key] = value;
        events.Emit(new AiKeyPosted(ai.Id, NextRequest(), agent, key, value));
    }

    /// <summary>Gives each side an agent and tells it everything its side knows of the groups. The side a group is on decides, for that agent, whether the group is hostile.</summary>
    private void RaiseAgents(IEventPort events)
    {
        _agents = true;
        foreach (var agent in AgentIds)
        {
            events.Emit(new AiAgentAddRequested(ai.Id, NextRequest(), agent, "commander"));
            Tell(events, agent, "distance", options.DistanceMetres.ToString(CultureInfo.InvariantCulture));
            Tell(events, agent, "terrain", AiValues.Encode(FactValue.From(options.Terrain)));
        }
        foreach (var (group, seen) in _groups) Describe(events, group, seen);
    }

    private void Describe(IEventPort events, string group, Seen seen)
    {
        foreach (var agent in AgentIds)
        {
            var side = int.Parse(agent[AgentPrefix.Length..], CultureInfo.InvariantCulture);
            var attrs = AiValues.Encode(new Dictionary<string, FactValue> { ["hostile"] = FactValue.From(seen.Side != side), ["standing"] = FactValue.From(seen.Fighting) });
            if (_told.TryGetValue(agent + "/group/" + group, out var told) && told == attrs) continue;
            _told[agent + "/group/" + group] = attrs;
            events.Emit(new AiPerceptPosted(ai.Id, NextRequest(), agent, "group", group, "", attrs));
        }
    }

    /// <summary>The agents have thought this tick: every shot they raised is one round of fire, all at once, in the order the sides and their groups came.</summary>
    private void OrderRound(long tick)
    {
        var shots = _fires.ToList();
        _fires.Clear();
        if (shots.Count == 0) return;
        var orders = shots.Select(shot =>
        {
            var args = AiValues.Decode(FieldAi.Set.Intents["fire"], shot.Args, AIEngine.AiCodes.ValueType);
            return string.Create(CultureInfo.InvariantCulture, $"{args["attacker"].Text}>{args["defender"].Text}|{args["metres"].Integer}|{args["cover"].Text}|{args["terrain"].Text}");
        });
        state.Round++;
        var requestId = "round-" + state.Round.ToString(CultureInfo.InvariantCulture);
        _outstanding.Add(requestId);
        _roundOpen = true;
        _events!.Emit(new RoundRequested(requestId, string.Join(';', orders), options.RoundSeconds));
    }

    private void Spawn(IEventPort events, long tick, string requestId, string formation, int squads, int x, int y)
    {
        var payload = string.Create(CultureInfo.InvariantCulture,
            $"{{\"formation\":\"{formation}\",\"squads\":{squads},\"company\":0,\"seed\":{options.Seed},\"layout\":{{\"x\":{x},\"y\":{y},\"columns\":25,\"spacing\":2}}}}");
        _outstanding.Add("effect:" + requestId);
        events.Emit(new EffectRequested(requestId, "sim.characters.spawn-formation", payload, tick));
    }

    private void Answered(string requestId, string note)
    {
        _outstanding.Remove(requestId);
        _events?.Emit(new FieldProgress(_now, "note", note));
    }

    private void OnForces(ForcesRestored restored)
    {
        foreach (var text in restored.Groups.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = text.Split(',');
            _groups[fields[0]] = new Seen(int.Parse(fields[1], CultureInfo.InvariantCulture), long.Parse(fields[2], CultureInfo.InvariantCulture), fields[6]);
        }
    }

    private void OnEngagement(Engagement engagement)
    {
        // A round answers with one engagement per order under one request id: the request is finished the first time.
        _outstanding.Remove(engagement.RequestId);
        if (_groups.TryGetValue(engagement.Attacker, out var attacker)) attacker.Health = engagement.AttackerHealth;
        if (_groups.TryGetValue(engagement.Defender, out var defender)) defender.Health = engagement.DefenderHealth;
    }

    private void OnStanding(Standing standing)
    {
        if (!_groups.TryGetValue(standing.Group, out var group)) return;
        group.Standing = standing.State;
        if (_agents) Describe(_events!, standing.Group, group);
    }

    private void OnCasualty(Casualty casualty)
    {
        if (!state.SquadOf.TryGetValue(casualty.Group, out var squad)) return;
        var requestId = $"loss-{casualty.RequestId}-{casualty.Group}";
        var key = "effect:" + requestId;
        if (!_outstanding.Add(key)) return;
        _events!.Emit(new EffectRequested(requestId, "sim.characters.casualties", string.Create(CultureInfo.InvariantCulture, $"{{\"squad\":{squad},\"count\":{casualty.Lost}}}"), casualty.Tick));
    }

    private void OnObservation(ObservationPosted posted)
    {
        if (posted.Kind != "sim.event.formation-spawned" && posted.Kind != "sim.event.squad-casualties" && posted.Kind != "sim.event.squad-reinforced") return;
        using var document = JsonDocument.Parse(posted.PayloadJson);
        var root = document.RootElement;
        if (posted.Kind == "sim.event.squad-casualties" || posted.Kind == "sim.event.squad-reinforced")
        {
            state.Headcount[root.GetProperty("squad").GetInt32()] = root.GetProperty("remaining").GetInt32();
            return;
        }
        var first = root.GetProperty("first").GetInt32();
        var squads = root.GetProperty("squads").GetInt32();
        var strength = root.GetProperty("members").GetInt32() / squads;
        var firstSide = state.SquadOf.Count == 0;
        var side = firstSide ? forces.SideA : forces.SideB;
        if (firstSide) state.StrengthA = strength; else state.StrengthB = strength;
        if (side.Count != squads) throw new InvalidOperationException($"the world raised {squads} squads for a side that has {side.Count} groups");
        for (var i = 0; i < squads; i++)
        {
            state.SquadOf[side[i]] = first + i;
            state.Headcount[first + i] = strength;
        }
        if (state.SquadOf.Count == forces.SideA.Count + forces.SideB.Count)
        {
            state.Spawned = true;
            _events!.Emit(new SquadsRaised(posted.Tick));
            _events!.Emit(new FieldProgress(posted.Tick, "raised", string.Create(CultureInfo.InvariantCulture,
                $"a={forces.SquadsA} squads of {forces.FormationA} ({Head(forces.SideA)} people), b={forces.SquadsB} squads of {forces.FormationB} ({Head(forces.SideB)} people)")));
        }
    }

    private void OnResolved(CommandResolved resolved)
    {
        if (resolved.Code == "QUEUED") return;
        _outstanding.Remove(resolved.CommandId);
        if (resolved.Applied) return;
        _events!.Emit(new FieldProgress(_now, "refused", resolved.CommandId + " " + resolved.Code));
        if (resolved.CommandId.StartsWith("effect:spawn-", StringComparison.Ordinal))
        {
            state.Over = true;
            _events.Emit(new SessionEnded(_now, "refused-" + resolved.Code));
        }
    }
}