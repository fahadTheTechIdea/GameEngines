namespace RelationshipEngine;

/// <summary>A timed mood modifier an actor has: which one, when it began, how strongly (permille, after traits and taste) and who caused it (an actor id, or empty).</summary>
public sealed class MoodletInstance
{
    public MoodletInstance(int moodlet, long start, int scale, string source) { Moodlet = moodlet; Start = start; Scale = scale; Source = source; }
    public int Moodlet { get; }
    public long Start { get; set; }
    public int Scale { get; set; }
    public string Source { get; }
}

/// <summary>An opinion modifier on an edge: which memory, when it began, how strongly, how many more times it may be told, who told it (empty when the holder saw it), and whether it has been told yet.</summary>
public sealed class MemoryInstance
{
    public MemoryInstance(int memory, long start, int scale, int hops, string origin, bool told) { Memory = memory; Start = start; Scale = scale; Hops = hops; Origin = origin; Told = told; }
    public int Memory { get; }
    public long Start { get; set; }
    public int Scale { get; set; }
    public int Hops { get; set; }
    public string Origin { get; set; }
    public bool Told { get; set; }
}

/// <summary>An interaction that cannot be used again on this edge before <see cref="ReadyAt"/>.</summary>
public sealed record CooldownEntry(int Interaction, long ReadyAt);

/// <summary>One actor's opinion of another: base values per axis, memories, a step per ladder, bonds and cooldowns.</summary>
public sealed class EdgeState
{
    public EdgeState(string from, string to, int axes, int ladders)
    {
        From = from; To = to; Base = new long[axes]; Ladder = new int[ladders];
    }

    public string From { get; }
    public string To { get; }
    public long[] Base { get; }
    public List<MemoryInstance> Memories { get; } = new();
    public int[] Ladder { get; }
    public SortedSet<int> Bonds { get; } = new();
    public List<CooldownEntry> Cooldowns { get; } = new();
    public long Touched { get; set; }
}

/// <summary>One actor: its traits and tastes, what it is doing, its needs (stored times the steps per day), its mood modifiers and mood state, and its opinions of others.</summary>
public sealed class ActorState
{
    public ActorState(string id, IReadOnlyList<string> traits, IReadOnlyDictionary<string, int> tastes, int needs)
    {
        Id = id; Traits = traits; Tastes = tastes; Need = new long[needs]; Critical = new bool[needs]; NeedDecay = new int[needs]; Community = id;
    }

    public string Id { get; }
    public IReadOnlyList<string> Traits { get; }
    public IReadOnlyDictionary<string, int> Tastes { get; }
    public string Activity { get; set; } = "";

    /// <summary>The community this actor belongs to, named by the lowest id among its members (so always an actor that exists and belongs to it); the actor itself until the first day boundary finds it company. Derived at each day boundary, and saved.</summary>
    public string Community { get; set; }

    /// <summary>The actor standing in the village: parts per thousand of the average (1000 is an average actor), derived at each day boundary from who trusts whom, and saved.</summary>
    public long Standing { get; set; } = RelationshipRuntime.NeutralStanding;
    public long[] Need { get; }
    public bool[] Critical { get; }

    /// <summary>The permille this actor's traits scale each need's decay by; derived from the traits, not saved.</summary>
    public int[] NeedDecay { get; }

    public List<MoodletInstance> Moodlets { get; } = new();
    public int MoodState { get; set; }
    public long Joined { get; set; }
    public SortedDictionary<string, EdgeState> Edges { get; } = new(StringComparer.Ordinal);
}

public readonly record struct MoodChanged(string Actor, int From, int To, long Mood);
public readonly record struct NeedCritical(string Actor, int Need, long Level, bool Entered);
public readonly record struct StateChanged(string Actor, string Target, int Ladder, int From, int To);
public readonly record struct BondChanged(string Actor, string Target, int Bond, bool Set);
public readonly record struct MemoryAdded(string Holder, string Subject, int Memory, string Origin);
public readonly record struct MoodletAdded(string Actor, int Moodlet, string Source);

/// <summary>Something removed to make room: a <c>moodlet</c>, a <c>memory</c> or an <c>edge</c>, of which actor, and which one.</summary>
public readonly record struct Evicted(string Kind, string Actor, string What);

/// <summary>Everything that changed during a call, in the order it happened.</summary>
public sealed class RelationshipChanges
{
    public List<MoodChanged> Moods { get; } = new();
    public List<NeedCritical> Needs { get; } = new();
    public List<StateChanged> States { get; } = new();
    public List<BondChanged> Bonds { get; } = new();
    public List<MemoryAdded> Memories { get; } = new();
    public List<MoodletAdded> Moodlets { get; } = new();
    public List<Evicted> Evictions { get; } = new();
    public List<CommunityChanged> Communities { get; } = new();
    public List<StandingChanged> Standing { get; } = new();
    public List<BalanceShifted> Balance { get; } = new();

    public bool Any => Moods.Count + Needs.Count + States.Count + Bonds.Count + Memories.Count + Moodlets.Count + Evictions.Count + Communities.Count + Standing.Count + Balance.Count > 0;
}

/// <summary>What an interaction came to: whether the target accepted, the acceptance score against its threshold (with the noise that was added), and what changed.</summary>
public sealed record InteractionResult(string Interaction, string Actor, string Target, bool Accepted, long Score, long Threshold, long Noise, RelationshipChanges Changes);

/// <summary>A request to perform an interaction.</summary>
public sealed record InteractionRequest(string Interaction, string Actor, string Target, IReadOnlyList<string> Witnesses, string Gift);

/// <summary>An interaction as the actor sees it for a target: whether it can be done now and how much the actor wants to.</summary>
public sealed record InteractionOffer(string Interaction, bool Available, string Why, long Desire);

/// <summary>An actor's community changed at a day boundary (the names are the communities' ids, the lowest member).</summary>
public readonly record struct CommunityChanged(string Actor, string From, string To);

/// <summary>An actor's standing moved by at least the document's event delta (parts per thousand of the village average).</summary>
public readonly record struct StandingChanged(string Actor, long From, long To);

/// <summary>
/// Balance pressure was put on <paramref name="Holder"/>'s opinion of <paramref name="Subject"/> through <paramref name="Via"/>: <paramref name="HolderVia"/> is the holder's opinion of Via and
/// <paramref name="ViaSubject"/> Via's of the subject (the two sides of the triad), <paramref name="Scale"/> the permille the pressure memory was given at (its sign says which way it pushes).
/// </summary>
public readonly record struct BalanceShifted(string Holder, string Subject, string Via, long HolderVia, long ViaSubject, int Scale);
