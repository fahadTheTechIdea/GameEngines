using Beep.SimEngine.Standard;
using EngineCommon;
using RelationshipEngine;

namespace GameRuntime;

/// <summary>
/// The people of the character factory as the actors of the relationships. The factory owns who a character is (name, stats, skills and its trait ids, drawn from a seed); the relationship document owns what
/// a trait id does to moods, needs and opinions; this class joins them without copying either: a character becomes an actor whose id is made from its archetype, seed and index, and whose trait ids are the
/// character's own. <see cref="CheckTraits"/> refuses a relationship document that names a trait the game does not have, so a misspelt trait is found when the game is assembled.
/// </summary>
public static class RelationshipCharacters
{
    public const string UnknownTrait = "REL_TRAIT_UNKNOWN";

    /// <summary>The actor id of a character: stable for the same archetype, seed and index, and made only of the characters an id allows.</summary>
    public static string ActorId(ICharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return character.Archetype + "." + character.Seed + "." + character.Index;
    }

    /// <summary>
    /// Refuses a relationship document that has effects for a trait id that is not in <paramref name="vocabulary"/>, the trait ids the game declares (the factory's trait definitions, or a catalog template's trait
    /// weights). A trait the document has no effects for is fine: not every trait matters to relationships.
    /// </summary>
    public static void CheckTraits(RelationshipSet set, IEnumerable<string> vocabulary)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(vocabulary);
        var known = vocabulary.ToHashSet(StringComparer.Ordinal);
        foreach (var trait in set.Traits)
            if (!known.Contains(trait.Id)) throw new EngineRefusedException(UnknownTrait, "the relationship document has effects for the trait '" + trait.Id + "', which the game does not declare");
    }

    /// <summary>Adds each character as an actor with its own traits; returns the actor ids in the order given. A character already an actor is refused (<c>REL_DUPLICATE_ACTOR</c>) and nothing after it is added.</summary>
    public static IReadOnlyList<string> AddActors(RelationshipRuntime runtime, IEnumerable<ICharacter> characters)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(characters);
        var ids = new List<string>();
        foreach (var character in characters)
        {
            var id = ActorId(character);
            runtime.AddActor(id, character.Traits.ToList());
            ids.Add(id);
        }
        return ids;
    }
}
