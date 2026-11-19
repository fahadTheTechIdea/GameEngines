using EngineCommon;

namespace RelationshipEngine;

/// <summary>One side of a triad as the holder sees it: whom, and the opinion held on the balance axis.</summary>
public readonly record struct StrongOpinion(string To, long Opinion);

/// <summary>
/// One triad for an explanation: the holder's opinion of <paramref name="Via"/>, Via's of the subject, the sign each has (+1 likes, -1 dislikes, 0 neither), the sign the holder's opinion of the subject should have for
/// the triad to balance (their product), the sign it has (without the pressure memory itself), whether the triad is balanced (all three signs there and their product positive), and whether the pressure rules consider it (inside both top-k lists, and neither side neutral).
/// </summary>
public sealed record TriadReading(string Via, long HolderVia, long ViaSubject, int SignHolderVia, int SignViaSubject, int Wanted, int Has, bool Balanced, bool Counts);

public sealed partial class RelationshipRuntime
{
    private sealed record Push(ActorState Holder, ActorState Subject, ActorState Via, long HolderVia, long ViaSubject, int Wanted);

    private static int SignOf(long opinion, BalanceDef rules) => opinion >= rules.Like ? 1 : opinion <= rules.Dislike ? -1 : 0;

    /// <summary>The opinion of the holder's edge on an axis without the memories of one kind: what the actor itself thinks, before the pressure of the triads.</summary>
    internal long OpinionWithout(EdgeState edge, int axis, int memory)
    {
        var def = Set.Axes[axis];
        var total = edge.Base[axis];
        foreach (var instance in edge.Memories)
            if (instance.Memory != memory) total += MemoryValue(instance, axis);
        return Math.Clamp(total, def.Min, def.Max);
    }

    private static readonly Comparison<StrongOpinion> ByStrength = (a, b) =>
    {
        var byStrength = Math.Abs(b.Opinion).CompareTo(Math.Abs(a.Opinion));
        return byStrength != 0 ? byStrength : string.CompareOrdinal(a.To, b.To);
    };

    private static readonly Comparison<Push> ByPull = (a, b) =>
    {
        var byPull = Math.Abs(b.HolderVia * b.ViaSubject).CompareTo(Math.Abs(a.HolderVia * a.ViaSubject));
        if (byPull != 0) return byPull;
        var bySubject = string.CompareOrdinal(a.Subject.Id, b.Subject.Id);
        return bySubject != 0 ? bySubject : string.CompareOrdinal(a.Via.Id, b.Via.Id);
    };

    /// <summary>The strongest opinions an actor holds on the balance axis (those that like or dislike), at most <c>TopK</c>, strongest first, then by id.</summary>
    internal List<StrongOpinion> StrongOpinions(ActorState actor, BalanceDef rules)
    {
        var found = new List<StrongOpinion>();
        foreach (var edge in actor.Edges.Values)
        {
            var opinion = OpinionOf(edge, rules.Axis);
            if (SignOf(opinion, rules) != 0) found.Add(new StrongOpinion(edge.To, opinion));
        }
        found.Sort(ByStrength);
        if (found.Count > rules.TopK) found.RemoveRange(rules.TopK, found.Count - rules.TopK);
        return found;
    }

    /// <summary>
    /// Heider's balance, applied once a day. A triad (holder A, via B, subject C) is balanced when the sign of A about C is the product of A about B and B about C (a friend of my friend is my friend, an enemy of my
    /// friend my enemy, an enemy of my enemy my friend). All three opinions must exist as edges and A's about B and B's about C must be strong (see <see cref="StrongOpinions"/>). Where A's own opinion of C (without
    /// the pressure memory) does not already have the wanted sign, the pressure memory is given to A about C, scaled by the document's strength, with B as its origin; the memory's own magnitude, duration and
    /// stacking bound how far it can move the opinion. Each actor takes at most <c>MaxPerActor</c> pushes a day, strongest first (the product of the two opinions), at most one per subject, each with the seeded chance (a push the chance turns away does not use the allowance, and the same subject is not tried again through a weaker friend). Every
    /// push is planned from the state at the start of the pass and applied afterwards, so the order of the actors does not matter. Only a new or changed push is reported; a renewal is not news.
    /// </summary>
    private void LiveBalance(RelationshipChanges changes)
    {
        var rules = Set.Social.Balance;
        if (!rules.Enabled || _actors.Count < 3) return;
        var strong = new Dictionary<string, List<StrongOpinion>>(_actors.Count, StringComparer.Ordinal);
        foreach (var actor in _actors.Values) strong[actor.Id] = StrongOpinions(actor, rules);
        var plan = new List<Push>();
        foreach (var holder in _actors.Values)
        {
            var candidates = new List<Push>();
            foreach (var first in strong[holder.Id])
            {
                var via = _actors[first.To];
                foreach (var second in strong[via.Id])
                {
                    if (second.To == holder.Id || !holder.Edges.TryGetValue(second.To, out var own)) continue;
                    var wanted = SignOf(first.Opinion, rules) * SignOf(second.Opinion, rules);
                    if (SignOf(OpinionWithout(own, rules.Axis, rules.Memory), rules) == wanted) continue;
                    candidates.Add(new Push(holder, _actors[second.To], via, first.Opinion, second.Opinion, wanted));
                }
            }
            if (candidates.Count == 0) continue;
            candidates.Sort(ByPull);
            var considered = new HashSet<string>(StringComparer.Ordinal);
            var pushed = 0;
            foreach (var push in candidates)
            {
                if (pushed == rules.MaxPerActor) break;
                if (!considered.Add(push.Subject.Id)) continue;
                if (rules.ChancePermille < 1000 && DeterministicRoll.Below(Set.Seed, "balance", Rolls++, 1000) >= rules.ChancePermille) continue;
                plan.Add(push);
                pushed++;
            }
        }
        foreach (var push in plan) ApplyPush(push, rules, changes);
    }

    private void ApplyPush(Push push, BalanceDef rules, RelationshipChanges changes)
    {
        var scale = push.Wanted * rules.StrengthPermille;
        var edge = push.Holder.Edges[push.Subject.Id];
        var memory = Set.Memories[rules.Memory];
        var before = edge.Memories.FirstOrDefault(m => m.Memory == rules.Memory && Step - m.Start < memory.Duration);
        var renewal = before is not null && before.Origin == push.Via.Id && before.Scale == scale;
        var made = new RelationshipChanges();
        if (!AddMemory(push.Holder, push.Subject, rules.Memory, scale, push.Via.Id, 0, made)) return;
        // The pressure memory is not announced as a memory (it would be one event a day for every pressured pair); what is announced is the triad behind it, and only when it is new or has changed.
        changes.States.AddRange(made.States);
        changes.Bonds.AddRange(made.Bonds);
        changes.Evictions.AddRange(made.Evictions);
        if (!renewal) changes.Balance.Add(new BalanceShifted(push.Holder.Id, push.Subject.Id, push.Via.Id, push.HolderVia, push.ViaSubject, scale));
    }

    /// <summary>Every triad through which <paramref name="holderId"/>'s opinion of <paramref name="subjectId"/> could be pressed, for an explanation: all actors the holder and the subject both have edges with.</summary>
    public IReadOnlyList<TriadReading> Triads(string holderId, string subjectId)
    {
        var rules = Set.Social.Balance;
        var holder = Actor(holderId);
        var subject = Actor(subjectId);
        if (holder == subject) throw new EngineRefusedException(RelationshipCodes.Self, "a triad needs three different actors");
        var readings = new List<TriadReading>();
        if (!holder.Edges.TryGetValue(subject.Id, out var own)) return readings;
        var has = SignOf(OpinionWithout(own, rules.Axis, rules.Memory), rules);
        var holderStrong = StrongOpinions(holder, rules);
        foreach (var edge in holder.Edges.Values)
        {
            if (edge.To == subject.Id || !_actors.TryGetValue(edge.To, out var via) || !via.Edges.TryGetValue(subject.Id, out var onward)) continue;
            var first = OpinionOf(edge, rules.Axis);
            var second = OpinionOf(onward, rules.Axis);
            var signFirst = SignOf(first, rules);
            var signSecond = SignOf(second, rules);
            var wanted = signFirst * signSecond;
            var counts = wanted != 0 && holderStrong.Any(s => s.To == via.Id) && StrongOpinions(via, rules).Any(s => s.To == subject.Id);
            readings.Add(new TriadReading(via.Id, first, second, signFirst, signSecond, wanted, has, wanted != 0 && wanted == has, counts));
        }
        return readings;
    }
}
