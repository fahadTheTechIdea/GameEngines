using EngineCommon;

namespace RelationshipEngine;

public sealed partial class RelationshipRuntime
{
    private const int ScaleLimit = 10_000;

    private static int ClampScale(long scale) => (int)Math.Clamp(scale, -ScaleLimit, ScaleLimit);

    // ---- edges ------------------------------------------------------------------------------------------------------------------------

    /// <summary>The edge of <paramref name="from"/> toward <paramref name="to"/>, made at the axis baselines when it does not exist. When the actor holds as many edges as the document allows, the least significant one goes first.</summary>
    internal EdgeState EdgeFor(ActorState from, ActorState to, RelationshipChanges changes)
    {
        if (from.Edges.TryGetValue(to.Id, out var edge)) return edge;
        if (from.Edges.Count >= Set.Limits.MaxEdgesPerActor)
        {
            var weakest = from.Edges.Values.OrderBy(e => e.Bonds.Count > 0 ? 1 : 0).ThenBy(Significance).ThenBy(e => e.Touched).ThenBy(e => e.To, StringComparer.Ordinal).First();
            foreach (var bond in weakest.Bonds.ToList()) ClearBond(weakest, bond, changes);   // a symmetric bond does not outlive the edge that held it, on either side
            from.Edges.Remove(weakest.To);
            changes.Evictions.Add(new Evicted("edge", from.Id, weakest.To));
        }
        edge = new EdgeState(from.Id, to.Id, Set.Axes.Count, Set.Ladders.Count) { Touched = Step };
        foreach (var axis in Set.Axes) edge.Base[axis.Index] = axis.Baseline;
        foreach (var ladder in Set.Ladders) edge.Ladder[ladder.Index] = DecayMath.PlainStep(ladder.Steps, Set.Axes[ladder.Axis].Baseline);
        from.Edges.Add(to.Id, edge);
        return edge;
    }

    /// <summary>How much an edge holds: the distance of its base values from their baselines plus the strength of its memories. The smallest is forgotten first.</summary>
    private long Significance(EdgeState edge)
    {
        long total = 0;
        foreach (var axis in Set.Axes) total += Math.Abs(edge.Base[axis.Index] - axis.Baseline);
        foreach (var memory in edge.Memories) total += MemoryStrength(memory);
        return total;
    }

    /// <summary>Moves the base value of an axis by a delta (scaled by the holder's traits for a gain or a loss), inside the axis, and re-reads what depends on it.</summary>
    internal void MoveAxis(ActorState holder, ActorState other, int axis, long delta, int scale, RelationshipChanges changes)
    {
        var edge = EdgeFor(holder, other, changes);
        var def = Set.Axes[axis];
        var scaled = DecayMath.Scale(delta, scale);
        var sign = scaled >= 0 ? Sign.Gain : Sign.Loss;
        long permille = 1000;
        foreach (var trait in holder.Traits)
        {
            if (!Set.TraitById.TryGetValue(trait, out var traitDef)) continue;
            foreach (var axisScale in traitDef.AxisScales)
                if (axisScale.Axis == axis && axisScale.Sign == sign) permille = Math.Min(100_000, permille * axisScale.Permille / 1000);
        }
        scaled = scaled * permille / 1000;
        edge.Base[axis] = Math.Clamp(edge.Base[axis] + scaled, def.Min, def.Max);
        edge.Touched = Step;
        CheckBonds(edge, changes);
        ReadLadders(edge, changes);
    }

    /// <summary>Re-reads every ladder of an edge (with its hysteresis) and says so for each that changed.</summary>
    internal void ReadLadders(EdgeState edge, RelationshipChanges changes)
    {
        foreach (var ladder in Set.Ladders)
        {
            var state = DecayMath.Resolve(ladder.Steps, edge.Ladder[ladder.Index], OpinionOf(edge, ladder.Axis), ladder.Hysteresis);
            if (state == edge.Ladder[ladder.Index]) continue;
            changes.States.Add(new StateChanged(edge.From, edge.To, ladder.Index, edge.Ladder[ladder.Index], state));
            edge.Ladder[ladder.Index] = state;
        }
    }

    // ---- bonds ------------------------------------------------------------------------------------------------------------------------

    /// <summary>Sets a bond on the edge (and on the way back when it is symmetric). An exclusive bond already held toward another actor is cleared first.</summary>
    internal void SetBond(ActorState holder, ActorState other, int bond, RelationshipChanges changes)
    {
        var def = Set.Bonds[bond];
        if (def.Exclusive)
            foreach (var edge in holder.Edges.Values.ToList())
                if (edge.To != other.Id && edge.Bonds.Contains(bond)) ClearBond(edge, bond, changes);
        var own = EdgeFor(holder, other, changes);
        if (own.Bonds.Add(bond)) changes.Bonds.Add(new BondChanged(holder.Id, other.Id, bond, true));
        own.Touched = Step;
        if (!def.Symmetric) return;
        if (def.Exclusive)
            foreach (var edge in other.Edges.Values.ToList())
                if (edge.To != holder.Id && edge.Bonds.Contains(bond)) ClearBond(edge, bond, changes);
        var back = EdgeFor(other, holder, changes);
        if (back.Bonds.Add(bond)) changes.Bonds.Add(new BondChanged(other.Id, holder.Id, bond, true));
        back.Touched = Step;
    }

    /// <summary>Clears a bond from an edge and, when it is symmetric, from the way back.</summary>
    internal void ClearBond(EdgeState edge, int bond, RelationshipChanges changes)
    {
        if (edge.Bonds.Remove(bond)) changes.Bonds.Add(new BondChanged(edge.From, edge.To, bond, false));
        if (!Set.Bonds[bond].Symmetric) return;
        if (_actors.TryGetValue(edge.To, out var other) && other.Edges.TryGetValue(edge.From, out var back) && back.Bonds.Remove(bond))
            changes.Bonds.Add(new BondChanged(back.From, back.To, bond, false));
    }

    /// <summary>Breaks the bonds whose axis has fallen below the line the bond holds while above.</summary>
    internal void CheckBonds(EdgeState edge, RelationshipChanges changes)
    {
        if (edge.Bonds.Count == 0) return;
        foreach (var bond in edge.Bonds.ToList())
        {
            var def = Set.Bonds[bond];
            if (def.BreakAxis >= 0 && OpinionOf(edge, def.BreakAxis) < def.BreakBelow) ClearBond(edge, bond, changes);
        }
    }

    // ---- memories ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Gives the holder a memory of the subject, by the memory's stacking rule. Returns false when the rule ignored it. When the edge then holds more memories than the document allows, the weakest goes
    /// (the smallest strength now, then the oldest, then the lowest memory index) and the call says so.
    /// </summary>
    internal bool AddMemory(ActorState holder, ActorState subject, int memory, int scale, string origin, int hops, RelationshipChanges changes)
    {
        var def = Set.Memories[memory];
        var edge = EdgeFor(holder, subject, changes);
        edge.Memories.RemoveAll(m => Step - m.Start >= Set.Memories[m.Memory].Duration);
        var fresh = new MemoryInstance(memory, Step, scale, hops, origin, false);
        var same = edge.Memories.Where(m => m.Memory == memory).ToList();
        switch (def.Stack.Mode)
        {
            case StackMode.Refresh:
                if (same.Count > 0) Restart(same[0], fresh); else edge.Memories.Add(fresh);
                break;
            case StackMode.Stack:
                if (same.Count < def.Stack.Max) edge.Memories.Add(fresh);
                else Restart(same.OrderBy(m => m.Start).First(), fresh);
                break;
            case StackMode.Replace:
                if (same.Count == 0) edge.Memories.Add(fresh);
                else if (MemoryStrength(fresh) >= MemoryStrength(same[0])) Restart(same[0], fresh);
                else return false;
                break;
            case StackMode.Unique:
                if (same.Count > 0) return false;
                edge.Memories.Add(fresh);
                break;
        }
        while (edge.Memories.Count > Set.Limits.MaxMemoriesPerEdge)
        {
            var weakest = edge.Memories.Select((m, i) => (Memory: m, Index: i)).OrderBy(p => MemoryStrength(p.Memory)).ThenBy(p => p.Memory.Start).ThenBy(p => p.Memory.Memory).ThenBy(p => p.Index).First().Memory;
            edge.Memories.Remove(weakest);
            changes.Evictions.Add(new Evicted("memory", holder.Id, subject.Id + ":" + Set.Memories[weakest.Memory].Id));
        }
        edge.Touched = Step;
        changes.Memories.Add(new MemoryAdded(holder.Id, subject.Id, memory, origin));
        CheckBonds(edge, changes);
        ReadLadders(edge, changes);
        return true;
    }

    private static void Restart(MemoryInstance old, MemoryInstance fresh)
    {
        old.Start = fresh.Start; old.Scale = fresh.Scale; old.Hops = fresh.Hops; old.Origin = fresh.Origin; old.Told = false;
    }

    // ---- moodlets ---------------------------------------------------------------------------------------------------------------------

    /// <summary>The permille an actor's traits scale a moodlet by, for the moodlet's tags.</summary>
    internal int TraitMoodScale(ActorState actor, MoodletDef def)
    {
        long permille = 1000;
        foreach (var trait in actor.Traits)
        {
            if (!Set.TraitById.TryGetValue(trait, out var traitDef)) continue;
            foreach (var scale in traitDef.MoodScales)
                if (def.Tags.Contains(scale.Tag)) permille = Math.Min(100_000, permille * scale.Permille / 1000);
        }
        return (int)permille;
    }

    /// <summary>Gives an actor a mood modifier by its stacking rule; returns false when the rule ignored it. Beyond the document's limit the weakest goes and the call says so.</summary>
    internal bool AddMoodlet(ActorState actor, int moodlet, int scale, string source, RelationshipChanges changes)
    {
        var def = Set.Moodlets[moodlet];
        actor.Moodlets.RemoveAll(m => Step - m.Start >= Set.Moodlets[m.Moodlet].Duration);
        var fresh = new MoodletInstance(moodlet, Step, ClampScale((long)scale * TraitMoodScale(actor, def) / 1000), source);
        var same = actor.Moodlets.Where(m => m.Moodlet == moodlet).ToList();
        switch (def.Stack.Mode)
        {
            case StackMode.Refresh:
                if (same.Count > 0) Restart(same[0], fresh); else actor.Moodlets.Add(fresh);
                break;
            case StackMode.Stack:
                if (same.Count < def.Stack.Max) actor.Moodlets.Add(fresh);
                else Restart(same.OrderBy(m => m.Start).First(), fresh);
                break;
            case StackMode.Replace:
                if (same.Count == 0) actor.Moodlets.Add(fresh);
                else if (Math.Abs(MoodletValue(fresh)) >= Math.Abs(MoodletValue(same[0]))) Restart(same[0], fresh);
                else return false;
                break;
            case StackMode.Unique:
                if (same.Count > 0) return false;
                actor.Moodlets.Add(fresh);
                break;
        }
        while (actor.Moodlets.Count > Set.Limits.MaxMoodlets)
        {
            var weakest = actor.Moodlets.Select((m, i) => (Moodlet: m, Index: i)).OrderBy(p => Math.Abs(MoodletValue(p.Moodlet))).ThenBy(p => p.Moodlet.Start).ThenBy(p => p.Moodlet.Moodlet).ThenBy(p => p.Index).First().Moodlet;
            actor.Moodlets.Remove(weakest);
            changes.Evictions.Add(new Evicted("moodlet", actor.Id, Set.Moodlets[weakest.Moodlet].Id));
        }
        changes.Moodlets.Add(new MoodletAdded(actor.Id, moodlet, source));
        ReadMood(actor, changes);
        return true;
    }

    private static void Restart(MoodletInstance old, MoodletInstance fresh)
    {
        old.Start = fresh.Start; old.Scale = fresh.Scale;
    }

    /// <summary>Changes a need's level by a delta (in need units), inside 0 and the max, and says when it crosses its critical line.</summary>
    internal void MoveNeedBy(ActorState actor, int index, long delta, RelationshipChanges changes)
    {
        var need = Set.Needs[index];
        actor.Need[index] = Math.Clamp(actor.Need[index] + delta * Set.StepsPerDay, 0, need.Max * Set.StepsPerDay);
        var level = actor.Need[index] / Set.StepsPerDay;
        if (!actor.Critical[index] && level <= need.Critical) { actor.Critical[index] = true; changes.Needs.Add(new NeedCritical(actor.Id, index, level, true)); }
        else if (actor.Critical[index] && level >= need.Rearm) { actor.Critical[index] = false; changes.Needs.Add(new NeedCritical(actor.Id, index, level, false)); }
        ReadMood(actor, changes);
    }
}
