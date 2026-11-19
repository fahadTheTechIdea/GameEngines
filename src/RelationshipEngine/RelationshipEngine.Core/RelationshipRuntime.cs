using System.Text.RegularExpressions;
using EngineCommon;

namespace RelationshipEngine;

/// <summary>
/// The state of one village of actors: their needs, moods and opinions of one another. It has no clock of its own. The caller gives it a step number (the game layer converts the calendar instant), and it moves
/// through the steps one at a time, at most <see cref="MaxStepsPerAdvance"/> in one call, so the work of one call is bounded however far behind the game is. Everything it holds is bounded by the document's limits;
/// when a limit is reached the stated item is evicted and the call says so. Nothing here reads a clock, a float or a random source other than the document's seeded roll.
/// </summary>
public sealed partial class RelationshipRuntime
{
    public const int MaxStepsPerAdvance = 480;
    public const int MaxTraitsPerActor = 16;
    public const int MaxTastes = 32;
    public const int MaxActivityLength = 128;

    private static readonly Regex IdText = new("^[a-z0-9._-]{1,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly SortedDictionary<string, ActorState> _actors = new(StringComparer.Ordinal);

    private RelationshipRuntime(RelationshipSet set, long step)
    {
        Set = set; Step = step; _dayStages = BuildDayStages();
    }

    public RelationshipSet Set { get; }

    /// <summary>The step the state is at. Steps before it have been lived through; the next <see cref="Advance"/> lives step <c>Step + 1</c>.</summary>
    public long Step { get; private set; }

    /// <summary>How many seeded rolls have been drawn; part of the saved state so a loaded game goes on with the same sequence.</summary>
    public long Rolls { get; private set; }

    public IReadOnlyDictionary<string, ActorState> Actors => _actors;

    public static RelationshipRuntime Create(RelationshipSet set, long startStep = 0)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (startStep < 0) throw new EngineRefusedException(RelationshipCodes.StepOrder, "the start step is not negative");
        return new RelationshipRuntime(set, startStep);
    }

    /// <summary>Rebuilds a runtime from checked save data; only the save reader calls it.</summary>
    internal static RelationshipRuntime Restore(RelationshipSet set, RelationshipImage image)
    {
        var runtime = new RelationshipRuntime(set, image.Step) { Rolls = image.Rolls };
        foreach (var saved in image.Actors) runtime.RestoreActor(saved);
        return runtime;
    }

    public int EdgeCount => _actors.Values.Sum(a => a.Edges.Count);

    // ---- actors ----------------------------------------------------------------------------------------------------------------------

    private string CheckId(string id, string what)
    {
        ArgumentNullException.ThrowIfNull(id);
        return IdText.IsMatch(id) ? id : throw new EngineRefusedException(RelationshipCodes.UnknownName, what + " '" + id + "' may only use a-z, 0-9, hyphen, dot and underscore, 1 to 128 characters");
    }

    public ActorState Actor(string id) => _actors.TryGetValue(id, out var actor) ? actor : throw new EngineRefusedException(RelationshipCodes.UnknownActor, "there is no actor '" + id + "'");

    /// <summary>
    /// Adds an actor with its trait ids (any ids; those the document has effects for matter) and its gift tastes (tag to -2 hate .. +2 love). Needs start at their initial levels, the mood state is the plain state of the
    /// mood, and nothing is announced.
    /// </summary>
    public ActorState AddActor(string id, IReadOnlyList<string> traits, IReadOnlyDictionary<string, int>? tastes = null)
    {
        CheckId(id, "actor id");
        ArgumentNullException.ThrowIfNull(traits);
        if (_actors.ContainsKey(id)) throw new EngineRefusedException(RelationshipCodes.DuplicateActor, "actor '" + id + "' already exists");
        if (_actors.Count >= Set.Limits.MaxActors) throw new EngineRefusedException(RelationshipCodes.Capacity, "the document allows " + Set.Limits.MaxActors + " actors");
        if (traits.Count > MaxTraitsPerActor) throw new EngineRefusedException(RelationshipCodes.Range, "an actor has at most " + MaxTraitsPerActor + " traits");
        var sorted = traits.Select(t => CheckId(t, "trait")).Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).ToList();
        if (sorted.Count != traits.Count) throw new EngineRefusedException(RelationshipCodes.Range, "an actor's traits are listed once each");
        var given = new SortedDictionary<string, int>(StringComparer.Ordinal);
        if (tastes is not null)
        {
            if (tastes.Count > MaxTastes) throw new EngineRefusedException(RelationshipCodes.Taste, "an actor has at most " + MaxTastes + " tastes");
            foreach (var (tag, level) in tastes)
            {
                CheckId(tag, "taste tag");
                if (level is < -2 or > 2) throw new EngineRefusedException(RelationshipCodes.Taste, "taste '" + tag + "' is " + level + ", not -2 (hate) to 2 (love)");
                given[tag] = level;
            }
        }
        var actor = new ActorState(id, sorted, given, Set.Needs.Count) { Joined = Step };
        for (var n = 0; n < Set.Needs.Count; n++)
        {
            actor.Need[n] = checked(Set.Needs[n].Initial * Set.StepsPerDay);
            actor.Critical[n] = Set.Needs[n].Initial <= Set.Needs[n].Critical;
        }
        DeriveNeedDecay(actor);
        actor.MoodState = DecayMath.PlainStep(Set.Mood.States, MoodValue(actor));
        _actors.Add(id, actor);
        _communitySizes = null;
        return actor;
    }

    private void DeriveNeedDecay(ActorState actor)
    {
        for (var n = 0; n < Set.Needs.Count; n++)
        {
            var permille = 1000;
            foreach (var trait in actor.Traits)
            {
                if (!Set.TraitById.TryGetValue(trait, out var def)) continue;
                foreach (var scale in def.NeedDecay)
                    if (scale.Need == n) permille = (int)Math.Min(100_000L, (long)permille * scale.Permille / 1000);
            }
            actor.NeedDecay[n] = permille;
        }
    }

    private void RestoreActor(ImageActor saved)
    {
        var actor = new ActorState(saved.Id, saved.Traits.ToList(), new SortedDictionary<string, int>(saved.Tastes.ToDictionary(t => t.Tag, t => t.Level, StringComparer.Ordinal), StringComparer.Ordinal), Set.Needs.Count) { Activity = saved.Activity, Community = saved.Community, Standing = saved.Standing, MoodState = saved.MoodState, Joined = saved.Joined };
        for (var n = 0; n < Set.Needs.Count; n++) { actor.Need[n] = saved.Needs[n].Level; actor.Critical[n] = saved.Needs[n].Critical; }
        DeriveNeedDecay(actor);
        foreach (var moodlet in saved.Moodlets) actor.Moodlets.Add(new MoodletInstance(moodlet.Moodlet, moodlet.Start, moodlet.Scale, moodlet.Source));
        foreach (var edge in saved.Edges)
        {
            var state = new EdgeState(saved.Id, edge.To, Set.Axes.Count, Set.Ladders.Count) { Touched = edge.Touched };
            for (var a = 0; a < edge.Base.Count; a++) state.Base[a] = edge.Base[a];
            for (var l = 0; l < edge.Ladder.Count; l++) state.Ladder[l] = edge.Ladder[l];
            foreach (var bond in edge.Bonds) state.Bonds.Add(bond);
            foreach (var memory in edge.Memories) state.Memories.Add(new MemoryInstance(memory.Memory, memory.Start, memory.Scale, memory.Hops, memory.Origin, memory.Told));
            foreach (var cooldown in edge.Cooldowns) state.Cooldowns.Add(new CooldownEntry(cooldown.Interaction, cooldown.ReadyAt));
            actor.Edges.Add(edge.To, state);
        }
        _actors.Add(saved.Id, actor);
    }

    /// <summary>Removes an actor and every opinion others hold of it. The members of a community it named take the next lowest member's id as the name.</summary>
    public void RemoveActor(string id)
    {
        var actor = Actor(id);
        _actors.Remove(actor.Id);
        foreach (var other in _actors.Values) other.Edges.Remove(id);
        var heirs = _actors.Values.Where(a => a.Community == id).ToList();
        if (heirs.Count > 0)
        {
            var name = heirs[0].Id;   // the actors are in ordinal id order, so the first is the lowest
            foreach (var heir in heirs) heir.Community = name;
        }
        _communitySizes = null;
    }

    /// <summary>Sets what an actor is doing now ("" for nothing); needs gain from the sources of that activity from the next step on.</summary>
    public void SetActivity(string actorId, string activity)
    {
        var actor = Actor(actorId);
        ArgumentNullException.ThrowIfNull(activity);
        if (activity.Length > 0 && (activity.Length > MaxActivityLength || !IdText.IsMatch(activity)))
            throw new EngineRefusedException(RelationshipCodes.UnknownName, "activity '" + activity + "' may only use a-z, 0-9, hyphen, dot and underscore, 1 to 128 characters");
        actor.Activity = activity;
    }

    // ---- needs and mood --------------------------------------------------------------------------------------------------------------

    /// <summary>The level of a need, 0 to its max.</summary>
    public long NeedLevel(ActorState actor, int need) => actor.Need[need] / Set.StepsPerDay;

    /// <summary>The urgency of a need, 0 to 1000, from its curve; the AI reads this to choose.</summary>
    public long Urgency(ActorState actor, int need) => DecayMath.Urgency(Set.Needs[need], NeedLevel(actor, need));

    /// <summary>The trait mood bias, summed over the actor's traits the document has effects for.</summary>
    public long TraitMoodBase(ActorState actor)
    {
        long total = 0;
        foreach (var trait in actor.Traits)
            if (Set.TraitById.TryGetValue(trait, out var def)) total += def.MoodBase;
        return total;
    }

    /// <summary>The current value of a moodlet instance.</summary>
    public long MoodletValue(MoodletInstance instance)
    {
        var def = Set.Moodlets[instance.Moodlet];
        return DecayMath.Value(DecayMath.Scale(def.Magnitude, instance.Scale), Step - instance.Start, def.Duration, def.Decay);
    }

    /// <summary>The contribution of one need to the mood.</summary>
    public long NeedMood(ActorState actor, NeedWeight weight) => (NeedLevel(actor, weight.Need) - weight.Neutral) * weight.Weight / Set.Needs[weight.Need].Max;

    /// <summary>The mood: base, trait bias, the needs the mood weighs and the moodlets that last, clamped to the mood's range.</summary>
    public long MoodValue(ActorState actor)
    {
        var mood = Set.Mood;
        var total = mood.Base + TraitMoodBase(actor);
        foreach (var weight in mood.NeedWeights) total += NeedMood(actor, weight);
        foreach (var moodlet in actor.Moodlets) total += MoodletValue(moodlet);
        return Math.Clamp(total, mood.Min, mood.Max);
    }

    public long Mood(string actorId) => MoodValue(Actor(actorId));

    public string MoodStateId(string actorId) => Set.Mood.States[Actor(actorId).MoodState].Id;

    // ---- opinions --------------------------------------------------------------------------------------------------------------------

    public EdgeState? Edge(string from, string to) => Actor(from).Edges.TryGetValue(to, out var edge) ? edge : null;

    /// <summary>The value of a memory instance on one axis now (0 when it does not touch the axis).</summary>
    public long MemoryValue(MemoryInstance instance, int axis)
    {
        var def = Set.Memories[instance.Memory];
        foreach (var effect in def.Effects)
            if (effect.Axis == axis) return DecayMath.Value(DecayMath.Scale(effect.Magnitude, instance.Scale), Step - instance.Start, def.Duration, def.Decay);
        return 0;
    }

    /// <summary>How strong a memory instance is now: the sum of the absolute value of its effects.</summary>
    public long MemoryStrength(MemoryInstance instance)
    {
        var def = Set.Memories[instance.Memory];
        long total = 0;
        foreach (var effect in def.Effects) total += Math.Abs(DecayMath.Value(DecayMath.Scale(effect.Magnitude, instance.Scale), Step - instance.Start, def.Duration, def.Decay));
        return total;
    }

    /// <summary>The opinion of <paramref name="from"/> about <paramref name="to"/> on an axis: the base value plus the memories that last, clamped to the axis. With no edge it is the axis baseline.</summary>
    public long Opinion(string from, string to, int axis)
    {
        Actor(to);
        var edge = Actor(from).Edges.GetValueOrDefault(to);
        return edge is null ? Set.Axes[axis].Baseline : OpinionOf(edge, axis);
    }

    internal long OpinionOf(EdgeState edge, int axis)
    {
        var def = Set.Axes[axis];
        var total = edge.Base[axis];
        foreach (var memory in edge.Memories) total += MemoryValue(memory, axis);
        return Math.Clamp(total, def.Min, def.Max);
    }

    public string LadderStateId(string from, string to, int ladder)
    {
        var edge = Actor(from).Edges.GetValueOrDefault(to);
        Actor(to);
        var def = Set.Ladders[ladder];
        return def.Steps[edge is null ? DecayMath.PlainStep(def.Steps, Set.Axes[def.Axis].Baseline) : edge.Ladder[ladder]].Id;
    }

    public bool HasBond(string from, string to, int bond)
    {
        Actor(to);
        return Actor(from).Edges.GetValueOrDefault(to)?.Bonds.Contains(bond) ?? false;
    }

    public int MemoryCount(string from, string to, int memory)
    {
        Actor(to);
        var edge = Actor(from).Edges.GetValueOrDefault(to);
        return edge is null ? 0 : edge.Memories.Count(m => m.Memory == memory && Step - m.Start < Set.Memories[memory].Duration);
    }

    /// <summary>The digest of the whole state: the same for the same state, whatever order things were done in.</summary>
    public string StateDigest() => RelationshipImage.Of(this).Digest;
}
