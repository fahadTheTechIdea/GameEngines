using System.Globalization;
using System.Text;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace RelationshipEngine;

/// <summary>One line of an explanation: what contributed, how much it contributed now, and the detail a reader needs to check it.</summary>
public sealed record ExplainTerm(string Kind, string Id, long Value, string Detail);

/// <summary>An explanation of a number: its terms, their sum, and the number itself (the sum clamped to the range). The terms always add up to the number.</summary>
public sealed record Explanation(string Subject, IReadOnlyList<ExplainTerm> Terms, long Sum, long Value, string State)
{
    public string Text()
    {
        var text = new StringBuilder();
        text.Append(Subject).Append(" = ").Append(Value.ToString(CultureInfo.InvariantCulture)).Append(" (").Append(State).Append(')');
        foreach (var term in Terms)
            text.Append("\n  ").Append(term.Kind).Append(' ').Append(term.Id).Append(": ").Append(term.Value >= 0 ? "+" : "").Append(term.Value.ToString(CultureInfo.InvariantCulture)).Append(term.Detail.Length > 0 ? " (" + term.Detail + ")" : "");
        text.Append("\n  sum ").Append(Sum.ToString(CultureInfo.InvariantCulture));
        if (Sum != Value) text.Append(", clamped to ").Append(Value.ToString(CultureInfo.InvariantCulture));
        return text.ToString();
    }
}

/// <summary>Says why a mood, an opinion or an interaction is what it is, from the same arithmetic the runtime uses: a pure reading that changes nothing.</summary>
public static partial class RelationshipExplain
{
    public static Explanation Mood(RelationshipRuntime runtime, string actorId)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var set = runtime.Set;
        var actor = runtime.Actor(actorId);
        var terms = new List<ExplainTerm> { new("base", "mood", set.Mood.Base, "") };
        var bias = runtime.TraitMoodBase(actor);
        if (bias != 0) terms.Add(new ExplainTerm("traits", string.Join('+', actor.Traits.Where(t => set.TraitById.TryGetValue(t, out var d) && d.MoodBase != 0)), bias, ""));
        foreach (var weight in set.Mood.NeedWeights)
        {
            var need = set.Needs[weight.Need];
            terms.Add(new ExplainTerm("need", need.Id, runtime.NeedMood(actor, weight), "level " + runtime.NeedLevel(actor, weight.Need) + " of " + need.Max + ", neutral " + weight.Neutral + ", weight " + weight.Weight));
        }
        foreach (var moodlet in actor.Moodlets)
        {
            var def = set.Moodlets[moodlet.Moodlet];
            var value = runtime.MoodletValue(moodlet);
            terms.Add(new ExplainTerm("moodlet", def.Id, value, "age " + (runtime.Step - moodlet.Start) + " of " + def.Duration + ", scale " + moodlet.Scale + (moodlet.Source.Length > 0 ? ", from " + moodlet.Source : "")));
        }
        var sum = terms.Sum(t => t.Value);
        return new Explanation("mood of " + actor.Id, terms, sum, runtime.MoodValue(actor), set.Mood.States[actor.MoodState].Id);
    }

    public static Explanation Opinion(RelationshipRuntime runtime, string from, string to, string axisId)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var set = runtime.Set;
        var axis = set.AxisById.TryGetValue(axisId, out var found) ? found : throw new EngineRefusedException(RelationshipCodes.UnknownAxis, "there is no axis '" + axisId + "'");
        runtime.Actor(to);
        var edge = runtime.Edge(from, to);
        var terms = new List<ExplainTerm> { new("base", axis.Id, edge?.Base[axis.Index] ?? axis.Baseline, edge is null ? "no edge: the baseline" : "baseline " + axis.Baseline + ", drift " + axis.DriftPerDay + " a day") };
        if (edge is not null)
            foreach (var memory in edge.Memories)
            {
                var value = runtime.MemoryValue(memory, axis.Index);
                if (value == 0 && !set.Memories[memory.Memory].Effects.Any(e => e.Axis == axis.Index)) continue;
                var def = set.Memories[memory.Memory];
                terms.Add(new ExplainTerm("memory", def.Id, value, "age " + (runtime.Step - memory.Start) + " of " + def.Duration + ", scale " + memory.Scale + (memory.Origin.Length == 0 ? "" : memory.Memory == set.Social.Balance.Memory ? ", pressure of the triad through " + memory.Origin : ", told by " + memory.Origin)));
            }
        var sum = terms.Sum(t => t.Value);
        var state = string.Join(", ", set.Ladders.Where(l => l.Axis == axis.Index).Select(l => l.Id + " " + runtime.LadderStateId(from, to, l.Index)));
        return new Explanation("opinion of " + from + " about " + to + " on " + axis.Id, terms, sum, runtime.Opinion(from, to, axis.Index), state.Length == 0 ? "no ladder" : state);
    }

    /// <summary>Whether the actor can do an interaction to the target now and why, how much it wants to, and how the target would decide (each rule with its weight and whether it held).</summary>
    public static string Interaction(RelationshipRuntime runtime, string actorId, string targetId, string interactionId, IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var def = runtime.Set.InteractionById.TryGetValue(interactionId, out var found) ? found : throw new EngineRefusedException(RelationshipCodes.UnknownInteraction, "there is no interaction '" + interactionId + "'");
        var actor = runtime.Actor(actorId);
        var target = runtime.Actor(targetId);
        if (actor == target) throw new EngineRefusedException(RelationshipCodes.Self, "an actor does not choose an interaction toward itself");
        var text = new StringBuilder();
        text.Append(def.Id).Append(' ').Append(actorId).Append(" -> ").Append(targetId);
        var offer = runtime.Offers(actorId, targetId, hostFacts).First(o => o.Interaction == def.Id);
        text.Append(offer.Available ? ": available" : ": not available (" + offer.Why + ")");
        if (def.Requires is not null) text.Append("\n  requires ").Append(ConditionExplanation.Describe(def.Requires, Facts(runtime, def.RequiresFacts, actor, target, hostFacts)));
        text.Append("\n  desire ").Append(offer.Desire.ToString(CultureInfo.InvariantCulture));
        Rules(text, runtime, "desire", def.Desire, actor, target, hostFacts);
        text.Append("\n  accept needs ").Append(def.AcceptAt.ToString(CultureInfo.InvariantCulture)).Append(def.Noise > 0 ? " (noise -" + def.Noise + " to +" + def.Noise + ")" : "");
        Rules(text, runtime, "accept", def.Accept, actor, target, hostFacts);
        return text.ToString();
    }

    private static Dictionary<string, FactValue> Facts(RelationshipRuntime runtime, IReadOnlyList<string> ids, ActorState actor, ActorState target, IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        var facts = new Dictionary<string, FactValue>(ids.Count, StringComparer.Ordinal);
        foreach (var id in ids) facts[id] = runtime.Resolve(id, actor, target, hostFacts);
        return facts;
    }

    private static void Rules(StringBuilder text, RelationshipRuntime runtime, string kind, IReadOnlyList<RuleDef> rules, ActorState actor, ActorState target, IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        foreach (var rule in rules)
        {
            var facts = Facts(runtime, rule.Facts, actor, target, hostFacts);
            text.Append("\n    ").Append(kind).Append(' ').Append(rule.Weight >= 0 ? "+" : "").Append(rule.Weight.ToString(CultureInfo.InvariantCulture)).Append(" if ").Append(ConditionExplanation.Describe(rule.When, facts));
        }
    }
}
