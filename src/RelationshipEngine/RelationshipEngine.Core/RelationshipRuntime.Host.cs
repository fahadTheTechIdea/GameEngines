using EngineCommon;

namespace RelationshipEngine;

public sealed partial class RelationshipRuntime
{
    // What the host (a quest, a dialogue outcome, a story event) can do without an interaction: each by the document's own names, each refused with its own code when the name is not the document's.

    private static void CheckScale(int scale)
    {
        if (scale is < -ScaleLimit or > ScaleLimit) throw new EngineRefusedException(RelationshipCodes.Range, "a scale is -" + ScaleLimit + " to " + ScaleLimit + " permille, not " + scale);
    }

    /// <summary>Gives an actor a mood modifier by id (scaled by its traits like any other).</summary>
    public RelationshipChanges Grant(string actorId, string moodletId, int scalePermille, string source = "")
    {
        CheckScale(scalePermille);
        var actor = Actor(actorId);
        var moodlet = Set.MoodletById.TryGetValue(moodletId, out var found) ? found : throw new EngineRefusedException(RelationshipCodes.UnknownMoodlet, "there is no moodlet '" + moodletId + "'");
        if (source.Length > 0) { CheckId(source, "moodlet source"); Actor(source); }
        var changes = new RelationshipChanges();
        AddMoodlet(actor, moodlet.Index, scalePermille, source, changes);
        return changes;
    }

    /// <summary>Gives the holder a memory of the subject by id.</summary>
    public RelationshipChanges Remember(string holderId, string subjectId, string memoryId, int scalePermille)
    {
        CheckScale(scalePermille);
        var holder = Actor(holderId);
        var subject = Actor(subjectId);
        var memory = Set.MemoryById.TryGetValue(memoryId, out var found) ? found : throw new EngineRefusedException(RelationshipCodes.UnknownMemory, "there is no memory '" + memoryId + "'");
        if (holder == subject) throw new EngineRefusedException(RelationshipCodes.Self, "an actor does not remember itself in an opinion");
        var changes = new RelationshipChanges();
        AddMemory(holder, subject, memory.Index, scalePermille, "", HopsOf(memory.Index), changes);
        return changes;
    }

    /// <summary>Changes a need's level by a delta, inside 0 and the need's max.</summary>
    public RelationshipChanges AdjustNeed(string actorId, string needId, long delta)
    {
        var actor = Actor(actorId);
        var need = Set.NeedById.TryGetValue(needId, out var found) ? found : throw new EngineRefusedException(RelationshipCodes.UnknownNeed, "there is no need '" + needId + "'");
        if (delta < -RelationshipReader.Bound || delta > RelationshipReader.Bound) throw new EngineRefusedException(RelationshipCodes.Range, "a need delta is at most " + RelationshipReader.Bound);
        var changes = new RelationshipChanges();
        MoveNeedBy(actor, need.Index, delta, changes);
        return changes;
    }

    /// <summary>Moves the base value of an axis of the holder's opinion of the other (traits scale a gain or a loss).</summary>
    public RelationshipChanges AdjustAxis(string holderId, string otherId, string axisId, long delta)
    {
        var holder = Actor(holderId);
        var other = Actor(otherId);
        var axis = Set.AxisById.TryGetValue(axisId, out var found) ? found : throw new EngineRefusedException(RelationshipCodes.UnknownAxis, "there is no axis '" + axisId + "'");
        if (holder == other) throw new EngineRefusedException(RelationshipCodes.Self, "an actor has no opinion of itself");
        if (delta < -RelationshipReader.Bound || delta > RelationshipReader.Bound) throw new EngineRefusedException(RelationshipCodes.Range, "an axis delta is at most " + RelationshipReader.Bound);
        var changes = new RelationshipChanges();
        MoveAxis(holder, other, axis.Index, delta, 1000, changes);
        return changes;
    }

    /// <summary>Sets or clears a bond between two actors.</summary>
    public RelationshipChanges SetBond(string holderId, string otherId, string bondId, bool set)
    {
        var holder = Actor(holderId);
        var other = Actor(otherId);
        var bond = Set.BondById.TryGetValue(bondId, out var found) ? found : throw new EngineRefusedException(RelationshipCodes.UnknownBond, "there is no bond '" + bondId + "'");
        if (holder == other) throw new EngineRefusedException(RelationshipCodes.Self, "an actor has no bond with itself");
        var changes = new RelationshipChanges();
        if (set) SetBond(holder, other, bond.Index, changes);
        else if (holder.Edges.TryGetValue(other.Id, out var edge)) ClearBond(edge, bond.Index, changes);
        return changes;
    }
}
