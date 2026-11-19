using System.Globalization;
using System.Text;
using EngineCommon;

namespace RelationshipEngine;

public static partial class RelationshipExplain
{
    private static string Signed(long value) => (value >= 0 ? "+" : "") + value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Which community an actor is in, who else is in it, and how many of the actors it likes are its mates (read now, from the opinions as they stand, beside what the last day boundary stored).</summary>
    public static string Community(RelationshipRuntime runtime, string actorId)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var rules = runtime.Set.Social.Communities;
        var actor = runtime.Actor(actorId);
        var members = runtime.Actors.Values.Where(a => a.Community == actor.Community).Select(a => a.Id).ToList();
        var text = new StringBuilder();
        text.Append("community of ").Append(actor.Id).Append(" = ").Append(actor.Community).Append(" (").Append(members.Count).Append(rules.Enabled ? " members: " : " member, communities are off: ").Append(string.Join(", ", members)).Append(')');
        if (!rules.Enabled) return text.ToString();
        var axis = runtime.Set.Axes[rules.Axis];
        var liked = 0;
        var mates = 0;
        foreach (var edge in actor.Edges.Values)
        {
            var opinion = runtime.OpinionOf(edge, rules.Axis);
            var weight = opinion - rules.Above;
            if (weight <= 0) continue;
            liked++;
            var mate = runtime.Actors[edge.To].Community == actor.Community;
            if (mate) mates++;
            text.Append("\n  likes ").Append(edge.To).Append(": ").Append(axis.Id).Append(' ').Append(opinion).Append(" (weight ").Append(weight).Append(mate ? ", same community)" : ", " + runtime.Actors[edge.To].Community + ")");
        }
        text.Append("\n  ").Append(mates).Append(" of ").Append(liked).Append(" liked actors share the community (above ").Append(rules.Above).Append(" on ").Append(axis.Id).Append(')');
        return text.ToString();
    }

    /// <summary>
    /// The standing of an actor as the last day boundary stored it, and the same figure worked out from the opinions as they stand now, with who passes the most trust to the actor. The two differ when opinions have moved
    /// since the boundary; the second is what the next boundary will start from.
    /// </summary>
    public static string Standing(RelationshipRuntime runtime, string actorId)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var rules = runtime.Set.Social.Standing;
        var actor = runtime.Actor(actorId);
        var text = new StringBuilder();
        text.Append("standing of ").Append(actor.Id).Append(" = ").Append(actor.Standing.ToString(CultureInfo.InvariantCulture)).Append(" (parts per thousand of the village average; 1000 is average)");
        if (!rules.Enabled) return text.Append(", standing is off").ToString();
        var (nodes, index, graph, result) = runtime.StandingNow();
        var me = index[actor.Id];
        text.Append("\n  from the opinions now: ").Append(RelationshipRuntime.StandingOf(result.Scores[me], nodes.Length).ToString(CultureInfo.InvariantCulture)).Append(" (").Append(result.Explain()).Append(')');
        var inbound = result.Contributors(graph, me, 3);
        foreach (var contributor in inbound)
            text.Append("\n  trusted by ").Append(nodes[contributor.From].Id).Append(": ").Append(Signed(contributor.Amount)).Append(" of ").Append(EngineCommon.Graph.TrustIteration.Scale).Append(" (its standing ").Append(nodes[contributor.From].Standing).Append(')');
        if (inbound.Count == 0) text.Append("\n  nobody trusts ").Append(actor.Id).Append(" above ").Append(rules.Above).Append(" on ").Append(runtime.Set.Axes[rules.Axis].Id).Append(": only the pre-trust share is left");
        text.Append("\n  pre-trust ").Append(rules.PreTrustTrait.Length == 0 ? "to everyone equally" : "to actors with trait " + rules.PreTrustTrait + (actor.Traits.Contains(rules.PreTrustTrait) ? " (this actor has it)" : "")).Append(", weight ").Append(rules.AlphaPermille).Append(" permille, ").Append(rules.Iterations).Append(" iterations at most");
        return text.ToString();
    }

    /// <summary>Why the holder's opinion of the subject is pressed, or not: every triad through a third actor both have edges with, its signs, and the pressure memory the holder has now (with whom it came through).</summary>
    public static string Balance(RelationshipRuntime runtime, string holderId, string subjectId)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var set = runtime.Set;
        var rules = set.Social.Balance;
        var triads = runtime.Triads(holderId, subjectId);
        var axis = set.Axes[rules.Axis];
        var text = new StringBuilder();
        text.Append("balance of ").Append(holderId).Append(" about ").Append(subjectId).Append(" on ").Append(axis.Id).Append(rules.Enabled ? " (likes from " + rules.Like + ", dislikes up to " + rules.Dislike + ")" : " (balance pressure is off)");
        if (triads.Count == 0) text.Append("\n  no third actor both ").Append(holderId).Append(" and ").Append(subjectId).Append(" hold an opinion about, or ").Append(holderId).Append(" has no opinion of ").Append(subjectId);
        foreach (var triad in triads)
            text.Append("\n  via ").Append(triad.Via).Append(": ").Append(holderId).Append(" about ").Append(triad.Via).Append(' ').Append(triad.HolderVia).Append(" (").Append(Word(triad.SignHolderVia)).Append("), ")
                .Append(triad.Via).Append(" about ").Append(subjectId).Append(' ').Append(triad.ViaSubject).Append(" (").Append(Word(triad.SignViaSubject)).Append("): wants ").Append(Word(triad.Wanted)).Append(", has ").Append(Word(triad.Has))
                .Append(triad.Wanted == 0 ? ", no triad" : triad.Balanced ? ", balanced" : triad.Counts ? ", unbalanced, pressed" : ", unbalanced, not among the strongest");
        var edge = runtime.Edge(holderId, subjectId);
        if (edge is not null && rules.Memory >= 0)
            foreach (var memory in edge.Memories.Where(m => m.Memory == rules.Memory))
                text.Append("\n  pressure now: ").Append(Signed(runtime.MemoryValue(memory, rules.Axis))).Append(" through ").Append(memory.Origin).Append(" (scale ").Append(memory.Scale).Append(", age ").Append(runtime.Step - memory.Start).Append(" of ").Append(set.Memories[memory.Memory].Duration).Append(')');
        return text.ToString();
    }

    private static string Word(int sign) => sign > 0 ? "likes" : sign < 0 ? "dislikes" : "neither";
}
