using EngineCommon;
using ScenarioEngine.Contracts;

namespace RelationshipEngine;

public sealed partial class RelationshipRuntime
{
    /// <summary>
    /// Lives the steps after <see cref="Step"/> up to <paramref name="toStep"/>, one at a time, at most <see cref="MaxStepsPerAdvance"/> of them; a caller that is further behind asks again (<see cref="Owed"/> says how
    /// many remain). Each step moves every actor's needs, drops expired moodlets and re-reads the mood; the day's work (drift toward baselines, expiry, bonds, ladders, forgetting, communities, standing, balance pressure, gossip)
    /// is done in the first step of the day (<see cref="DaySchedule.Atomic"/>) or one stage per step in its first <see cref="DayStages"/> steps (<see cref="DaySchedule.Spread"/>). <paramref name="hostFacts"/> are the facts the document's decay modifiers read; a fact they need and the host did not give is refused before anything moves.
    /// </summary>
    public RelationshipChanges Advance(long toStep, IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        ArgumentNullException.ThrowIfNull(hostFacts);
        if (toStep < Step) throw new EngineRefusedException(RelationshipCodes.StepOrder, "step " + toStep + " is before the current step " + Step);
        var changes = new RelationshipChanges();
        var steps = Math.Min(toStep - Step, MaxStepsPerAdvance);
        if (steps == 0) return changes;
        var multipliers = DecayMultipliers(hostFacts);
        for (var i = 0; i < steps; i++)
        {
            Step++;
            LiveStep(multipliers, changes);
            LiveDayWork(changes);
        }
        return changes;
    }

    /// <summary>How many steps remain between the current step and <paramref name="toStep"/>.</summary>
    public long Owed(long toStep) => Math.Max(0, toStep - Step);

    /// <summary>Per need, the permille the decay is scaled by this step: the product of the modifiers whose condition holds. Evaluated once for everyone, because the conditions read host facts only.</summary>
    private int[] DecayMultipliers(IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        var result = new int[Set.Needs.Count];
        for (var n = 0; n < result.Length; n++)
        {
            long permille = 1000;
            foreach (var modifier in Set.Needs[n].DecayModifiers)
            {
                var facts = new Dictionary<string, FactValue>(modifier.Facts.Count, StringComparer.Ordinal);
                foreach (var id in modifier.Facts) facts[id] = HostFact(hostFacts, id);
                if (Conditions.Evaluate(modifier.When, facts)) permille = Math.Min(100_000, permille * modifier.Permille / 1000);
            }
            result[n] = (int)permille;
        }
        return result;
    }

    /// <summary>A host fact, checked against its declaration: present, of its type and inside its range.</summary>
    internal FactValue HostFact(IReadOnlyDictionary<string, FactValue> hostFacts, string id)
    {
        if (!Set.HostFacts.TryGetValue(id, out var definition)) throw new EngineRefusedException(RelationshipCodes.FactMissing, "fact '" + id + "' is not declared by the document");
        if (!hostFacts.TryGetValue(id, out var value)) throw new EngineRefusedException(RelationshipCodes.FactMissing, "the host did not give the fact '" + id + "'");
        if (value.Kind != definition.Kind) throw new EngineRefusedException(RelationshipCodes.FactInvalid, "fact '" + id + "' is " + value.Kind + ", declared " + definition.Kind);
        if (definition.Kind == FactKind.Integer && (value.Integer < definition.Minimum || value.Integer > definition.Maximum))
            throw new EngineRefusedException(RelationshipCodes.FactInvalid, "fact '" + id + "' is " + value.Integer + ", outside " + definition.Minimum + " to " + definition.Maximum);
        return value;
    }

    private void LiveStep(int[] multipliers, RelationshipChanges changes)
    {
        foreach (var actor in _actors.Values)
        {
            for (var n = 0; n < Set.Needs.Count; n++) MoveNeed(actor, n, multipliers[n], changes);
            for (var m = actor.Moodlets.Count - 1; m >= 0; m--)
                if (Step - actor.Moodlets[m].Start >= Set.Moodlets[actor.Moodlets[m].Moodlet].Duration) actor.Moodlets.RemoveAt(m);
            ReadMood(actor, changes);
        }
    }

    private void MoveNeed(ActorState actor, int index, int hostPermille, RelationshipChanges changes)
    {
        var need = Set.Needs[index];
        var rate = need.DecayPerDay * actor.NeedDecay[index] * hostPermille / 1_000_000;
        long gain = 0;
        if (actor.Activity.Length > 0 && Set.SourcesByActivity.TryGetValue(actor.Activity, out var sources))
            foreach (var source in sources)
                if (source.Need == index) gain += source.PerDay;
        actor.Need[index] = Math.Clamp(actor.Need[index] + gain - rate, 0, need.Max * Set.StepsPerDay);
        var level = actor.Need[index] / Set.StepsPerDay;
        if (!actor.Critical[index] && level <= need.Critical) { actor.Critical[index] = true; changes.Needs.Add(new NeedCritical(actor.Id, index, level, true)); }
        else if (actor.Critical[index] && level >= need.Rearm) { actor.Critical[index] = false; changes.Needs.Add(new NeedCritical(actor.Id, index, level, false)); }
    }

    /// <summary>Re-reads the mood state of an actor (with the hysteresis) and says so when it changed.</summary>
    internal void ReadMood(ActorState actor, RelationshipChanges changes)
    {
        var mood = MoodValue(actor);
        var state = DecayMath.Resolve(Set.Mood.States, actor.MoodState, mood, Set.Mood.Hysteresis);
        if (state == actor.MoodState) return;
        changes.Moods.Add(new MoodChanged(actor.Id, actor.MoodState, state, mood));
        actor.MoodState = state;
    }
}
