using EngineCommon;
using ScenarioEngine.Contracts;

namespace AIEngine;

public sealed partial class AiRuntime
{
    /// <summary>
    /// Tells an agent about one thing, or updates what it knows: every attribute its kind declares, exactly, each of the declared type and inside its range.
    /// An owned kind needs an owner id and any other must have none. The agent holds at most the capacity of the kind.
    /// </summary>
    public void Perceive(string agent, string kind, string id, string owner, IReadOnlyDictionary<string, FactValue> attrs, long tick)
    {
        var state = Agent(agent);
        CheckTick(tick);
        if (!Set.TryPercept(kind, out var def)) throw new EngineRefusedException(AiCodes.Percept, "there is no percept kind '" + kind + "'");
        if (id.Length == 0 || id.Length > 128) throw new EngineRefusedException(AiCodes.Percept, "a percept id is 1 to 128 characters");
        if (def.Owned ? owner.Length == 0 || owner.Length > 128 : owner.Length != 0)
            throw new EngineRefusedException(AiCodes.Percept, "kind '" + kind + "' " + (def.Owned ? "needs an owner" : "has no owner"));
        if (attrs.Count != def.Attrs.Count) throw new EngineRefusedException(AiCodes.Percept, "kind '" + kind + "' has " + def.Attrs.Count + " attributes, not " + attrs.Count);
        var values = new Val[def.Attrs.Count];
        foreach (var attr in def.Attrs)
        {
            if (!attrs.TryGetValue(attr.Id, out var value)) throw new EngineRefusedException(AiCodes.Percept, "kind '" + kind + "' needs attribute '" + attr.Id + "'");
            if (!attr.Accepts(value)) throw new EngineRefusedException(AiCodes.Percept, "attribute '" + attr.Id + "' of '" + kind + "' is not a valid " + attr.Kind);
            values[attr.Index] = Val.From(value);
        }
        var known = state.Knowledge[def.Index];
        if (known.TryGetValue(id, out var existing))
        {
            if (existing.Owner != owner) throw new EngineRefusedException(AiCodes.Percept, "'" + id + "' is already known with another owner");
            System.Array.Copy(values, existing.Values, values.Length);
            existing.Seen = tick;
            return;
        }
        if (known.Count >= def.Capacity) throw new EngineRefusedException(AiCodes.Capacity, "agent '" + agent + "' already knows " + def.Capacity + " things of kind '" + kind + "'");
        if (def.Owned && known.CountOwnedBy(owner) >= def.OwnerCapacity) throw new EngineRefusedException(AiCodes.Capacity, "agent '" + agent + "' already knows " + def.OwnerCapacity + " things of kind '" + kind + "' held by '" + owner + "'");
        known.Add(new Percept(def, id, owner, tick, values));
    }

    /// <summary>Tells an agent a thing is gone. Not knowing it already is not a mistake.</summary>
    public bool Lose(string agent, string kind, string id)
    {
        var state = Agent(agent);
        if (!Set.TryPercept(kind, out var def)) throw new EngineRefusedException(AiCodes.Percept, "there is no percept kind '" + kind + "'");
        return state.Knowledge[def.Index].Remove(id);
    }

    /// <summary>Writes one of the keys the host owns. A key the agent owns is refused: it is the agent memory.</summary>
    public void SetKey(string agent, string key, FactValue value)
    {
        var state = Agent(agent);
        if (!Set.TryKey(key, out var def)) throw new EngineRefusedException(AiCodes.Reference, "there is no key '" + key + "'");
        if (!def.HostWrites) throw new EngineRefusedException(AiCodes.KeyWrite, "key '" + key + "' belongs to the agent");
        if (!def.Value.Accepts(value)) throw new EngineRefusedException(AiCodes.ValueType, "value for key '" + key + "' is not a valid " + def.Value.Kind);
        state.Keys[def.Index] = Val.From(value);
    }

    public FactValue Key(string agent, string key)
    {
        var state = Agent(agent);
        if (!Set.TryKey(key, out var def)) throw new EngineRefusedException(AiCodes.Reference, "there is no key '" + key + "'");
        return state.Keys[def.Index].ToFact(def.Value.Kind);
    }

    /// <summary>What an agent knows of one kind, in id order.</summary>
    public IReadOnlyList<PerceptInfo> Known(string agent, string kind)
    {
        var state = Agent(agent);
        if (!Set.TryPercept(kind, out var def)) throw new EngineRefusedException(AiCodes.Percept, "there is no percept kind '" + kind + "'");
        return state.Knowledge[def.Index].Values.Select(p => new PerceptInfo(kind, p.Id, p.Owner, p.Seen,
            def.Attrs.ToDictionary(a => a.Id, a => p.Values[a.Index].ToFact(a.Kind), StringComparer.Ordinal))).ToList();
    }
}