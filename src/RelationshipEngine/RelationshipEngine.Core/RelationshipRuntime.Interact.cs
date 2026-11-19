using EngineCommon;
using ScenarioEngine.Contracts;

namespace RelationshipEngine;

public sealed partial class RelationshipRuntime
{
    /// <summary>The facts a rule reads, built for the two actors and the host's facts; only those the rule names are built.</summary>
    private Dictionary<string, FactValue> FactsFor(IReadOnlyList<string> ids, ActorState actor, ActorState target, IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        var facts = new Dictionary<string, FactValue>(ids.Count, StringComparer.Ordinal);
        foreach (var id in ids) facts[id] = Resolve(id, actor, target, hostFacts);
        return facts;
    }

    /// <summary>The value of one fact for a pair of actors. The ids are the ones <see cref="RelationshipFacts"/> declares; any other is the host's.</summary>
    public FactValue Resolve(string id, ActorState actor, ActorState target, IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        if (id.StartsWith(RelationshipFacts.Actor, StringComparison.Ordinal)) return ActorFact(id[RelationshipFacts.Actor.Length..], actor, id);
        if (id.StartsWith(RelationshipFacts.Target, StringComparison.Ordinal)) return ActorFact(id[RelationshipFacts.Target.Length..], target, id);
        if (id.StartsWith(RelationshipFacts.Edge, StringComparison.Ordinal)) return EdgeFact(id[RelationshipFacts.Edge.Length..], actor, target, id);
        if (id.StartsWith(RelationshipFacts.Reverse, StringComparison.Ordinal)) return EdgeFact(id[RelationshipFacts.Reverse.Length..], target, actor, id);
        return HostFact(hostFacts, id);
    }

    private FactValue ActorFact(string name, ActorState who, string id)
    {
        if (name == "mood") return FactValue.From(MoodValue(who));
        if (name == "mood_state") return FactValue.From(Set.Mood.States[who.MoodState].Id);
        if (name == "activity") return FactValue.From(who.Activity);
        if (name == "community") return FactValue.From(who.Community);
        if (name == "community_size") return FactValue.From((long)CommunitySize(who));
        if (name == "standing") return FactValue.From(who.Standing);
        if (name.StartsWith("need.", StringComparison.Ordinal) && Set.NeedById.TryGetValue(name[5..], out var need)) return FactValue.From(NeedLevel(who, need.Index));
        if (name.StartsWith("trait.", StringComparison.Ordinal) && Set.TraitById.ContainsKey(name[6..])) return FactValue.From(who.Traits.Contains(name[6..], StringComparer.Ordinal));
        throw new EngineRefusedException(RelationshipCodes.FactMissing, "there is no fact '" + id + "'");
    }

    private FactValue EdgeFact(string name, ActorState from, ActorState to, string id)
    {
        var edge = from.Edges.GetValueOrDefault(to.Id);
        if (name == "known") return FactValue.From(edge is not null);
        if (name == "same_community") return FactValue.From(from.Community == to.Community);
        if (name.StartsWith("state.", StringComparison.Ordinal) && Set.LadderById.TryGetValue(name[6..], out var ladder))
            return FactValue.From(ladder.Steps[edge is null ? DecayMath.PlainStep(ladder.Steps, Set.Axes[ladder.Axis].Baseline) : edge.Ladder[ladder.Index]].Id);
        if (name.StartsWith("bond.", StringComparison.Ordinal) && Set.BondById.TryGetValue(name[5..], out var bond)) return FactValue.From(edge is not null && edge.Bonds.Contains(bond.Index));
        if (name.StartsWith("memory.", StringComparison.Ordinal) && Set.MemoryById.TryGetValue(name[7..], out var memory))
            return FactValue.From(edge is null ? 0L : edge.Memories.Count(m => m.Memory == memory.Index && Step - m.Start < memory.Duration));
        if (Set.AxisById.TryGetValue(name, out var axis)) return FactValue.From(edge is null ? axis.Baseline : OpinionOf(edge, axis.Index));
        throw new EngineRefusedException(RelationshipCodes.FactMissing, "there is no fact '" + id + "'");
    }

    // ---- what an actor can do --------------------------------------------------------------------------------------------------------

    /// <summary>Why an interaction cannot be done now, or null when it can: the cooldown, then the precondition.</summary>
    private string? WhyNot(InteractionDef def, ActorState actor, ActorState target, IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        var edge = actor.Edges.GetValueOrDefault(target.Id);
        var cooldown = edge?.Cooldowns.FirstOrDefault(c => c.Interaction == def.Index);
        if (cooldown is not null && cooldown.ReadyAt > Step) return RelationshipCodes.Cooldown;
        if (def.Requires is not null && !Conditions.Evaluate(def.Requires, FactsFor(def.RequiresFacts, actor, target, hostFacts))) return RelationshipCodes.Precondition;
        return null;
    }

    private long DesireOf(InteractionDef def, ActorState actor, ActorState target, IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        long total = 0;
        foreach (var rule in def.Desire)
            if (Conditions.Evaluate(rule.When, FactsFor(rule.Facts, actor, target, hostFacts))) total += rule.Weight;
        return total;
    }

    /// <summary>Every interaction as the actor sees it for the target: whether it can be done now, why not, and how much the actor wants to (the sum of the weights of its desire rules). In document order.</summary>
    public IReadOnlyList<InteractionOffer> Offers(string actorId, string targetId, IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        ArgumentNullException.ThrowIfNull(hostFacts);
        var actor = Actor(actorId);
        var target = Actor(targetId);
        if (actor == target) throw new EngineRefusedException(RelationshipCodes.Self, "an actor does not choose an interaction toward itself");
        var offers = new List<InteractionOffer>();
        foreach (var def in Set.Interactions)
        {
            var why = WhyNot(def, actor, target, hostFacts);
            offers.Add(new InteractionOffer(def.Id, why is null, why ?? "", DesireOf(def, actor, target, hostFacts)));
        }
        return offers;
    }

    // ---- performing ------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Performs an interaction: checks the request, the cooldown of the pair and the precondition (each refused with its own code and nothing changed), lets the target accept or refuse by its accept rules plus a seeded
    /// noise, applies the effects of the outcome, lets the witnesses remember, and starts the pair's cooldown. A refusal by the target is an outcome, not an error.
    /// </summary>
    public InteractionResult Perform(InteractionRequest request, IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(hostFacts);
        var def = Set.InteractionById.TryGetValue(request.Interaction, out var found) ? found : throw new EngineRefusedException(RelationshipCodes.UnknownInteraction, "there is no interaction '" + request.Interaction + "'");
        var actor = Actor(request.Actor);
        var target = Actor(request.Target);
        if (actor == target) throw new EngineRefusedException(RelationshipCodes.Self, "an actor does not do '" + def.Id + "' to itself");
        var gift = CheckGift(def, request.Gift);
        var witnesses = CheckWitnesses(def, request, actor, target);
        var why = WhyNot(def, actor, target, hostFacts);
        if (why == RelationshipCodes.Cooldown) throw new EngineRefusedException(RelationshipCodes.Cooldown, "'" + def.Id + "' from '" + actor.Id + "' to '" + target.Id + "' is not ready until step " + actor.Edges[target.Id].Cooldowns.First(c => c.Interaction == def.Index).ReadyAt);
        if (why == RelationshipCodes.Precondition) throw new EngineRefusedException(RelationshipCodes.Precondition, "the precondition of '" + def.Id + "' does not hold: " + ConditionExplanation.Describe(def.Requires!, FactsFor(def.RequiresFacts, actor, target, hostFacts)));

        var changes = new RelationshipChanges();
        long score = 0;
        foreach (var rule in def.Accept)
            if (Conditions.Evaluate(rule.When, FactsFor(rule.Facts, actor, target, hostFacts))) score += rule.Weight;
        long noise = 0;
        if (def.Noise > 0) noise = DeterministicRoll.Below(Set.Seed, "accept", Rolls++, 2L * def.Noise + 1) - def.Noise;
        var accepted = score + noise >= def.AcceptAt;
        var tastePermille = gift is null ? 1000 : Set.Tastes[GiftTaste(target, gift) + 2];
        foreach (var effect in accepted ? def.Effects : def.RefusedEffects) Apply(effect, actor, target, tastePermille, changes);
        if (accepted && def.Witness is not null)
            foreach (var witness in witnesses)
                AddMemory(witness, actor, def.Witness.Memory, def.Witness.ScalePermille, "", HopsOf(def.Witness.Memory), changes);
        if (def.Cooldown > 0)
        {
            var edge = EdgeFor(actor, target, changes);
            edge.Cooldowns.RemoveAll(c => c.Interaction == def.Index);
            edge.Cooldowns.Add(new CooldownEntry(def.Index, Step + def.Cooldown));
            edge.Touched = Step;
        }
        return new InteractionResult(def.Id, actor.Id, target.Id, accepted, score, def.AcceptAt, noise, changes);
    }

    private GiftDef? CheckGift(InteractionDef def, string gift)
    {
        if (def.Gift && gift.Length == 0) throw new EngineRefusedException(RelationshipCodes.GiftRequired, "'" + def.Id + "' is a gift and needs a gift item");
        if (!def.Gift && gift.Length > 0) throw new EngineRefusedException(RelationshipCodes.GiftUnexpected, "'" + def.Id + "' is not a gift");
        if (!def.Gift) return null;
        return Set.GiftById.TryGetValue(gift, out var found) ? found : throw new EngineRefusedException(RelationshipCodes.UnknownGift, "there is no gift '" + gift + "'");
    }

    private List<ActorState> CheckWitnesses(InteractionDef def, InteractionRequest request, ActorState actor, ActorState target)
    {
        var witnesses = new List<ActorState>();
        var list = request.Witnesses ?? Array.Empty<string>();
        if (list.Count > 0 && def.Witness is null) throw new EngineRefusedException(RelationshipCodes.Witness, "'" + def.Id + "' has no witnesses");
        if (def.Witness is not null && list.Count > def.Witness.Max) throw new EngineRefusedException(RelationshipCodes.Witness, "'" + def.Id + "' has at most " + def.Witness.Max + " witnesses, not " + list.Count);
        foreach (var id in list)
        {
            var witness = _actors.TryGetValue(id, out var found) ? found : throw new EngineRefusedException(RelationshipCodes.Witness, "witness '" + id + "' is not an actor");
            if (witness == actor || witness == target) throw new EngineRefusedException(RelationshipCodes.Witness, "witness '" + id + "' is a party to the interaction");
            if (witnesses.Contains(witness)) throw new EngineRefusedException(RelationshipCodes.Witness, "witness '" + id + "' is listed twice");
            witnesses.Add(witness);
        }
        return witnesses;
    }

    /// <summary>The taste level of an actor for a gift: the sum of its tastes for the gift's tags, from -2 (hate) to 2 (love).</summary>
    public int GiftTaste(ActorState receiver, GiftDef gift)
    {
        var sum = 0;
        foreach (var tag in gift.Tags)
            if (receiver.Tastes.TryGetValue(tag, out var level)) sum += level;
        return Math.Clamp(sum, -2, 2);
    }

    private int HopsOf(int memory) => Set.Memories[memory].Spread?.Hops ?? 0;

    private void Apply(EffectDef effect, ActorState actor, ActorState target, int tastePermille, RelationshipChanges changes)
    {
        var scale = effect.ByTaste ? tastePermille : 1000;
        switch (effect.Kind)
        {
            case EffectKind.Axis:
                if (effect.TargetEdge) MoveAxis(target, actor, effect.Index, effect.Amount, scale, changes); else MoveAxis(actor, target, effect.Index, effect.Amount, scale, changes);
                break;
            case EffectKind.Memory:
                if (effect.TargetEdge) AddMemory(target, actor, effect.Index, scale, "", HopsOf(effect.Index), changes); else AddMemory(actor, target, effect.Index, scale, "", HopsOf(effect.Index), changes);
                break;
            case EffectKind.Moodlet:
                AddMoodlet(effect.TargetEdge ? target : actor, effect.Index, scale, effect.TargetEdge ? actor.Id : target.Id, changes);
                break;
            case EffectKind.Need:
                MoveNeedBy(effect.TargetEdge ? target : actor, effect.Index, DecayMath.Scale(effect.Amount, scale), changes);
                break;
            case EffectKind.Bond:
                if (effect.Amount == 1) SetBond(actor, target, effect.Index, changes);
                else
                {
                    var edge = actor.Edges.GetValueOrDefault(target.Id);
                    if (edge is not null) ClearBond(edge, effect.Index, changes);
                }
                break;
        }
    }
}
