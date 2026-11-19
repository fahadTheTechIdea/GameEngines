using RelationshipEngine;

namespace GameRuntime;

public sealed partial class RelationshipComponent
{
    /// <summary>What the changes of a call or a step become as events, in a fixed order: moods, needs, states, bonds, memories, moodlets, evictions, communities, standings, balance pushes. Each is built now (the names are read now) and raised later.</summary>
    private List<Action<IEventPort>> EventsOf(long tick, RelationshipChanges changes)
    {
        var set = Runtime.Set;
        var events = new List<Action<IEventPort>>(changes.Moods.Count + changes.Needs.Count + changes.States.Count + changes.Bonds.Count + changes.Memories.Count + changes.Moodlets.Count + changes.Evictions.Count + changes.Communities.Count + changes.Standing.Count + changes.Balance.Count);
        foreach (var c in changes.Moods)
        {
            var e = new RelationshipMoodChanged(Id, Channel, tick, c.Actor, set.Mood.States[c.From].Id, set.Mood.States[c.To].Id, c.Mood);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Needs)
        {
            var e = new RelationshipNeedCritical(Id, Channel, tick, c.Actor, set.Needs[c.Need].Id, c.Level, c.Entered);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.States)
        {
            var ladder = set.Ladders[c.Ladder];
            var e = new RelationshipStateChanged(Id, Channel, tick, c.Actor, c.Target, ladder.Id, ladder.Steps[c.From].Id, ladder.Steps[c.To].Id);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Bonds)
        {
            var e = new RelationshipBondChanged(Id, Channel, tick, c.Actor, c.Target, set.Bonds[c.Bond].Id, c.Set);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Memories)
        {
            var e = new RelationshipMemoryAdded(Id, Channel, tick, c.Holder, c.Subject, set.Memories[c.Memory].Id, c.Origin);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Moodlets)
        {
            var e = new RelationshipMoodletAdded(Id, Channel, tick, c.Actor, set.Moodlets[c.Moodlet].Id, c.Source);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Evictions)
        {
            var e = new RelationshipEvicted(Id, Channel, tick, c.Kind, c.Actor, c.What);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Communities)
        {
            var e = new RelationshipCommunityChanged(Id, Channel, tick, c.Actor, c.From, c.To);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Standing)
        {
            var e = new RelationshipStandingChanged(Id, Channel, tick, c.Actor, c.From, c.To);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Balance)
        {
            var e = new RelationshipBalanceShifted(Id, Channel, tick, c.Holder, c.Subject, c.Via, c.HolderVia, c.ViaSubject, c.Scale);
            events.Add(port => port.Emit(e));
        }
        return events;
    }

    /// <summary>The changes of a request are raised now, with the answer, in the Update that answered it.</summary>
    private void Raise(IEventPort events, long tick, RelationshipChanges changes)
    {
        foreach (var raise in EventsOf(tick, changes)) raise(events);
    }

    /// <summary>The changes of the steps are owed: raised at most <c>maxEvents</c> per Update, in order, oldest first.</summary>
    private void Owe(long tick, RelationshipChanges changes)
    {
        foreach (var raise in EventsOf(tick, changes)) _owed.Enqueue(raise);
    }
}
