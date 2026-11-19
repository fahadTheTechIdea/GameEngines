using ScenarioEngine.Contracts;

namespace RelationshipEngine;

/// <summary>
/// A loaded relationship document: every declaration plus the digest of the exact text it was read from. It is immutable and holds no actor; the runtime holds those. Every query is a pure function of it.
/// </summary>
public sealed class RelationshipSet
{
    public RelationshipSet(string id, string digest, ulong seed, int stepsPerDay, LimitsDef limits, IReadOnlyList<AxisDef> axes, IReadOnlyList<NeedDef> needs, IReadOnlyList<SourceDef> sources,
        MoodDef mood, IReadOnlyList<MoodletDef> moodlets, IReadOnlyList<MemoryDef> memories, IReadOnlyList<TraitDef> traits, IReadOnlyList<LadderDef> ladders, IReadOnlyList<BondDef> bonds,
        IReadOnlyList<GiftDef> gifts, IReadOnlyList<int> tastes, GossipDef gossip, SocialDef social, IReadOnlyList<InteractionDef> interactions, IReadOnlyDictionary<string, FactDefinition> hostFacts)
    {
        Id = id; Digest = digest; Seed = seed; StepsPerDay = stepsPerDay; Limits = limits; Axes = axes; Needs = needs; Sources = sources; Mood = mood; Moodlets = moodlets; Memories = memories;
        Traits = traits; Ladders = ladders; Bonds = bonds; Gifts = gifts; Tastes = tastes; Gossip = gossip; Social = social; Interactions = interactions; HostFacts = hostFacts;
        AxisById = axes.ToDictionary(a => a.Id, StringComparer.Ordinal);
        NeedById = needs.ToDictionary(n => n.Id, StringComparer.Ordinal);
        MoodletById = moodlets.ToDictionary(m => m.Id, StringComparer.Ordinal);
        MemoryById = memories.ToDictionary(m => m.Id, StringComparer.Ordinal);
        TraitById = traits.ToDictionary(t => t.Id, StringComparer.Ordinal);
        LadderById = ladders.ToDictionary(l => l.Id, StringComparer.Ordinal);
        BondById = bonds.ToDictionary(b => b.Id, StringComparer.Ordinal);
        GiftById = gifts.ToDictionary(g => g.Id, StringComparer.Ordinal);
        InteractionById = interactions.ToDictionary(i => i.Id, StringComparer.Ordinal);
        var byActivity = new SortedDictionary<string, List<SourceDef>>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (!byActivity.TryGetValue(source.Activity, out var list)) byActivity[source.Activity] = list = new List<SourceDef>();
            list.Add(source);
        }
        SourcesByActivity = byActivity.ToDictionary(p => p.Key, p => (IReadOnlyList<SourceDef>)p.Value, StringComparer.Ordinal);
        Facts = RelationshipFacts.Definitions(axes, needs, traits, ladders, bonds, memories, mood, limits, hostFacts);
    }

    public string Id { get; }

    /// <summary>The SHA-256 of the exact text of the document.</summary>
    public string Digest { get; }

    public ulong Seed { get; }
    public int StepsPerDay { get; }
    public LimitsDef Limits { get; }
    public IReadOnlyList<AxisDef> Axes { get; }
    public IReadOnlyList<NeedDef> Needs { get; }
    public IReadOnlyList<SourceDef> Sources { get; }
    public MoodDef Mood { get; }
    public IReadOnlyList<MoodletDef> Moodlets { get; }
    public IReadOnlyList<MemoryDef> Memories { get; }
    public IReadOnlyList<TraitDef> Traits { get; }
    public IReadOnlyList<LadderDef> Ladders { get; }
    public IReadOnlyList<BondDef> Bonds { get; }
    public IReadOnlyList<GiftDef> Gifts { get; }

    /// <summary>The permille a taste level scales a gift's effects by: index 0 hate, 1 dislike, 2 neutral, 3 like, 4 love.</summary>
    public IReadOnlyList<int> Tastes { get; }

    public GossipDef Gossip { get; }

    /// <summary>What is derived from the whole network of opinions at each day boundary: communities, standing, balance pressure.</summary>
    public SocialDef Social { get; }
    public IReadOnlyList<InteractionDef> Interactions { get; }

    /// <summary>The facts the host supplies, with their types (the engine's own facts are in <see cref="Facts"/>).</summary>
    public IReadOnlyDictionary<string, FactDefinition> HostFacts { get; }

    /// <summary>Every fact a condition of this document may read: the engine's own and the host's.</summary>
    public IReadOnlyDictionary<string, FactDefinition> Facts { get; }

    public IReadOnlyDictionary<string, AxisDef> AxisById { get; }
    public IReadOnlyDictionary<string, NeedDef> NeedById { get; }
    public IReadOnlyDictionary<string, MoodletDef> MoodletById { get; }
    public IReadOnlyDictionary<string, MemoryDef> MemoryById { get; }
    public IReadOnlyDictionary<string, TraitDef> TraitById { get; }
    public IReadOnlyDictionary<string, LadderDef> LadderById { get; }
    public IReadOnlyDictionary<string, BondDef> BondById { get; }
    public IReadOnlyDictionary<string, GiftDef> GiftById { get; }
    public IReadOnlyDictionary<string, InteractionDef> InteractionById { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<SourceDef>> SourcesByActivity { get; }
}
