using EngineCommon;

namespace RelationshipEngine;

public sealed partial class RelationshipRuntime
{
    private sealed record Telling(string Listener, string Subject, int Memory, int Scale, string Teller, int Hops);

    /// <summary>
    /// One night of gossip. Each actor, in id order, tells its newest not yet told spreading memories (at most the document's tellings) to the actors it trusts most (at most the listeners, those at or above the
    /// trust line, most trusted first, then by id); each telling succeeds by the memory's seeded chance and gives the listener the memory it spreads as, at a fraction of the strength and one hop less. Every
    /// instance is told once. The tellings are all planned from the state at the start of the night and applied afterwards, so how far news travels in a night does not depend on who is asked first.
    /// </summary>
    private void Gossip(RelationshipChanges changes)
    {
        var rules = Set.Gossip;
        if (rules.MaxTellings == 0 || rules.MaxListeners == 0) return;
        var plan = new List<Telling>();
        foreach (var teller in _actors.Values)
        {
            var news = new List<(MemoryInstance Instance, string Subject)>();
            foreach (var edge in teller.Edges.Values)
                foreach (var memory in edge.Memories)
                    if (!memory.Told && memory.Hops > 0 && Set.Memories[memory.Memory].Spread is not null && Step - memory.Start < Set.Memories[memory.Memory].Duration) news.Add((memory, edge.To));
            if (news.Count == 0) continue;
            var told = news.OrderByDescending(n => n.Instance.Start).ThenBy(n => n.Subject, StringComparer.Ordinal).ThenBy(n => n.Instance.Memory).Take(rules.MaxTellings).ToList();
            var listeners = teller.Edges.Values.Where(e => OpinionOf(e, rules.TrustAxis) >= rules.MinTrust)
                .OrderByDescending(e => OpinionOf(e, rules.TrustAxis)).ThenBy(e => e.To, StringComparer.Ordinal).Take(rules.MaxListeners + 1).Select(e => e.To).ToList();
            foreach (var (instance, subject) in told)
            {
                instance.Told = true;
                var spread = Set.Memories[instance.Memory].Spread!;
                var heard = 0;
                foreach (var listener in listeners)
                {
                    if (listener == subject) continue;
                    if (heard == rules.MaxListeners) break;
                    heard++;
                    if (DeterministicRoll.Below(Set.Seed, "gossip", Rolls++, 1000) >= spread.Chance) continue;
                    plan.Add(new Telling(listener, subject, spread.Memory, ClampScale((long)instance.Scale * spread.Permille / 1000), teller.Id, instance.Hops - 1));
                }
            }
        }
        foreach (var telling in plan)
            AddMemory(_actors[telling.Listener], _actors[telling.Subject], telling.Memory, telling.Scale, telling.Teller, telling.Hops, changes);
    }
}
