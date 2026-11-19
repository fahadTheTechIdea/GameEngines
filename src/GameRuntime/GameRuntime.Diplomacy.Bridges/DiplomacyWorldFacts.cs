using DiplomacyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;

namespace GameRuntime;

/// <summary>
/// What the world-events engine reads of the diplomacy, as host facts: whether a realm is at war, how many wars there are, how many treaties a realm holds, and whether a realm is out of order. A world document declares
/// the ones it reads (a raid is only likelier when a war is on: a hazard whose condition reads <c>diplomacy.at_war.aurel</c>), under the <c>diplomacy.</c> prefix, and <see cref="Check"/> verifies the declarations against
/// the diplomacy document when the game is assembled. The world component is given <see cref="HostFacts"/> as its host facts; the world never calls the diplomacy and the diplomacy never learns the world exists.
/// </summary>
public static class DiplomacyWorldFacts
{
    public const string Mismatch = "DIP_WORLD_MISMATCH";
    public const string Prefix = "diplomacy.";

    /// <summary>Every fact offered, with its type and range: <c>diplomacy.wars</c>, and for each realm <c>diplomacy.at_war.&lt;realm&gt;</c>, <c>diplomacy.treaties.&lt;realm&gt;</c> and <c>diplomacy.unrest.&lt;realm&gt;</c>.</summary>
    public static IReadOnlyDictionary<string, FactDefinition> Offered(DipSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal) { [Prefix + "wars"] = new(FactKind.Integer, 0, set.Limits.MaxWars) };
        foreach (var realm in set.Realms)
        {
            facts[Prefix + "at_war." + realm.Id] = new FactDefinition(FactKind.Boolean);
            facts[Prefix + "treaties." + realm.Id] = new FactDefinition(FactKind.Integer, 0, set.Limits.MaxTreaties);
            facts[Prefix + "unrest." + realm.Id] = new FactDefinition(FactKind.Boolean);
        }
        return facts;
    }

    /// <summary>The offered facts as they are now.</summary>
    public static IReadOnlyDictionary<string, FactValue> Read(DipRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal) { [Prefix + "wars"] = FactValue.From((long)runtime.Wars.Count) };
        foreach (var realm in runtime.Set.Realms)
        {
            var bit = 1UL << realm.Index;
            facts[Prefix + "at_war." + realm.Id] = FactValue.From(runtime.Wars.Any(w => (w.AttackerSide & bit) != 0 || (w.DefenderSide & bit) != 0));
            facts[Prefix + "treaties." + realm.Id] = FactValue.From((long)runtime.Treaties.Count(t => t.First == realm.Index || t.Second == realm.Index));
            facts[Prefix + "unrest." + realm.Id] = FactValue.From(runtime.Realms[realm.Index].Band != (int)StabilityBand.Stable);
        }
        return facts;
    }

    /// <summary>
    /// The host facts of the world component when the diplomacy and the game share the hosting: the diplomacy's facts together with the game's own. A fact both give is refused when the facts are read, because the world
    /// would have two answers.
    /// </summary>
    public static Func<IReadOnlyDictionary<string, FactValue>> HostFacts(DipRuntime runtime, Func<IReadOnlyDictionary<string, FactValue>> game)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(game);
        return () =>
        {
            var facts = new Dictionary<string, FactValue>(Read(runtime), StringComparer.Ordinal);
            foreach (var (id, value) in game())
                if (!facts.TryAdd(id, value)) throw new EngineRefusedException(Mismatch, "fact '" + id + "' is given by both the diplomacy and the game");
            return facts;
        };
    }

    /// <summary>Refuses a world document that declares a host fact in the diplomacy namespace that the diplomacy does not offer, or offers with another type, or with a range the declaration does not cover.</summary>
    public static void Check(WorldSet world, DipSet diplomacy)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(diplomacy);
        var offered = Offered(diplomacy);
        foreach (var (id, wanted) in world.HostFacts.Where(f => f.Key.StartsWith(Prefix, StringComparison.Ordinal)))
        {
            if (!offered.TryGetValue(id, out var given)) throw new EngineRefusedException(Mismatch, "the world document reads '" + id + "', which the diplomacy does not offer");
            if (given.Kind != wanted.Kind) throw new EngineRefusedException(Mismatch, "'" + id + "' is " + given.Kind + " in the diplomacy and " + wanted.Kind + " in the world document");
            if (wanted.Kind == FactKind.Integer && (given.Minimum < wanted.Minimum || given.Maximum > wanted.Maximum))
                throw new EngineRefusedException(Mismatch, "'" + id + "' ranges " + given.Minimum + " to " + given.Maximum + " in the diplomacy, outside the " + wanted.Minimum + " to " + wanted.Maximum + " the world document declares");
        }
    }
}
