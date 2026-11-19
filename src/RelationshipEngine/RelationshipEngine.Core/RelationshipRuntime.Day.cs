using EngineCommon.Graph;

namespace RelationshipEngine;

public sealed partial class RelationshipRuntime
{
    /// <summary>
    /// How many steps of a day the derived work takes under <see cref="DaySchedule.Spread"/>: upkeep (drift, expiry, bonds, ladders, forgetting), communities, standing, balance pressure, gossip. Stage <c>k</c> is lived in the
    /// step whose number is <c>k</c> past the start of a day (<c>Step % StepsPerDay == k</c>), so where the pass has got to is a function of <see cref="Step"/> and the document, never a field of the state: a save taken
    /// between two stages holds exactly what a save holds, and a runtime restored from it lives the same next stage.
    /// </summary>
    public const int DayStages = 5;

    private readonly Action<RelationshipChanges>[] _dayStages;

    private Action<RelationshipChanges>[] BuildDayStages() => [LiveUpkeep, changes => LiveCommunities(changes), changes => LiveStanding(changes, null), LiveBalance, Gossip];

    /// <summary>
    /// The day's derived work, for the step just lived. <see cref="DaySchedule.Atomic"/>: all of it in the first step of the day. <see cref="DaySchedule.Spread"/>: the stage the step's place in the day names (none after the
    /// last stage), each a whole stage planned from the state at its own step, so a stage never sees half of another. Under Spread a stage reads the opinions as they stand at its own step, not at the first step of the day:
    /// that is the one difference from Atomic (documented in docs/RELATIONSHIP_ENGINE.md section 12), and it is the same for every run and every restore.
    /// </summary>
    private void LiveDayWork(RelationshipChanges changes)
    {
        var offset = Step % Set.StepsPerDay;
        if (Set.Social.Schedule == DaySchedule.Atomic)
        {
            if (offset == 0) LiveDayAtomic(changes);
            return;
        }
        if (offset < DayStages) _dayStages[offset](changes);
    }

    private void LiveDayAtomic(RelationshipChanges changes)
    {
        LiveUpkeep(changes);
        // One graph serves both passes when they read the same opinions, as they always did in one step.
        var graph = LiveCommunities(changes);
        LiveStanding(changes, graph);
        LiveBalance(changes);
        Gossip(changes);
    }

    private void LiveUpkeep(RelationshipChanges changes)
    {
        foreach (var actor in _actors.Values)
        {
            var forgotten = new List<string>();
            foreach (var edge in actor.Edges.Values)
            {
                DriftEdge(edge);
                edge.Memories.RemoveAll(m => Step - m.Start >= Set.Memories[m.Memory].Duration);
                edge.Cooldowns.RemoveAll(c => c.ReadyAt <= Step);
                CheckBonds(edge, changes);
                ReadLadders(edge, changes);
                if (IsEmpty(edge)) forgotten.Add(edge.To);
            }
            foreach (var target in forgotten) actor.Edges.Remove(target);
        }
    }

    private void DriftEdge(EdgeState edge)
    {
        foreach (var axis in Set.Axes)
        {
            var value = edge.Base[axis.Index];
            if (value == axis.Baseline || axis.DriftPerDay == 0) continue;
            edge.Base[axis.Index] = value > axis.Baseline ? Math.Max(axis.Baseline, value - axis.DriftPerDay) : Math.Min(axis.Baseline, value + axis.DriftPerDay);
        }
    }

    private bool IsEmpty(EdgeState edge)
    {
        if (edge.Memories.Count > 0 || edge.Bonds.Count > 0 || edge.Cooldowns.Count > 0) return false;
        foreach (var axis in Set.Axes)
            if (edge.Base[axis.Index] != axis.Baseline) return false;
        return true;
    }
}
