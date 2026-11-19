using EngineCommon;

namespace RelationshipEngine;

/// <summary>
/// The words of a relationship document. A document holds localisation keys only; the text comes from the shared locale tables and chains (CLDR plural and ordinal categories, fallback chains, bidi isolates).
/// Every key here is a name, so a message gives no placeholder.
/// </summary>
public static class RelationshipNames
{
    /// <summary>Every localisation key the document uses, each once, in ordinal order.</summary>
    public static IReadOnlyList<string> RequiredKeys(RelationshipSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var axis in set.Axes) keys.Add(axis.Key);
        foreach (var need in set.Needs) keys.Add(need.Key);
        foreach (var state in set.Mood.States) keys.Add(state.Key);
        foreach (var moodlet in set.Moodlets) keys.Add(moodlet.Key);
        foreach (var memory in set.Memories) keys.Add(memory.Key);
        foreach (var ladder in set.Ladders) foreach (var step in ladder.Steps) keys.Add(step.Key);
        foreach (var bond in set.Bonds) keys.Add(bond.Key);
        foreach (var gift in set.Gifts) keys.Add(gift.Key);
        foreach (var interaction in set.Interactions) keys.Add(interaction.Key);
        return keys.ToList();
    }

    private static string Name(LocaleChain chain, string key) => chain.Render(key, Array.Empty<MessageArg>()).Text;

    public static string Axis(RelationshipSet set, LocaleChain chain, int axis) => Name(chain, set.Axes[axis].Key);
    public static string Need(RelationshipSet set, LocaleChain chain, int need) => Name(chain, set.Needs[need].Key);
    public static string MoodState(RelationshipSet set, LocaleChain chain, int state) => Name(chain, set.Mood.States[state].Key);
    public static string Moodlet(RelationshipSet set, LocaleChain chain, int moodlet) => Name(chain, set.Moodlets[moodlet].Key);
    public static string Memory(RelationshipSet set, LocaleChain chain, int memory) => Name(chain, set.Memories[memory].Key);
    public static string LadderStep(RelationshipSet set, LocaleChain chain, int ladder, int step) => Name(chain, set.Ladders[ladder].Steps[step].Key);
    public static string Bond(RelationshipSet set, LocaleChain chain, int bond) => Name(chain, set.Bonds[bond].Key);
    public static string Interaction(RelationshipSet set, LocaleChain chain, int interaction) => Name(chain, set.Interactions[interaction].Key);
    public static string Gift(RelationshipSet set, LocaleChain chain, int gift) => Name(chain, set.Gifts[gift].Key);

    /// <summary>How much of the document a locale table covers: keys the document uses and the table lacks, messages the document does not use, and messages that use a placeholder (names give none).</summary>
    public static CoverageReport Coverage(RelationshipSet set, LocaleTable table)
    {
        ArgumentNullException.ThrowIfNull(set);
        var keys = RequiredKeys(set).ToDictionary(k => k, _ => (IReadOnlyList<string>)[], StringComparer.Ordinal);
        return CoverageRules.Report(keys, table, _ => "a name does not give");
    }
}
