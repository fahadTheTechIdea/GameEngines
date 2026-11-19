using DialogueEngine;
using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// What a conversation between two actors reads of their relationship, as external facts: the mood, needs, traits and activity of the speaker (<c>actor.</c>) and the listener (<c>target.</c>), and the opinions
/// they hold of each other (<c>edge.</c> the speaker's about the listener, <c>reverse.</c> the listener's about the speaker), under the <c>ext.</c> prefix a dialogue document uses. The dialogue component is given
/// <see cref="Facts"/> as its fact source; a dialogue document declares the ones it reads as external facts with the same ids (<c>edge.state.friendship</c>), and <see cref="Check"/> verifies the declarations
/// against the relationship document when the game is assembled.
/// </summary>
public sealed class RelationshipDialogueFacts
{
    public const string Mismatch = "REL_DIALOGUE_MISMATCH";

    private readonly RelationshipComponent _component;

    public RelationshipDialogueFacts(RelationshipComponent component, string speaker, string listener, string prefix = "ext.")
    {
        ArgumentNullException.ThrowIfNull(component);
        _component = component;
        Speaker = speaker;
        Listener = listener;
        Prefix = prefix;
    }

    public string Speaker { get; private set; }
    public string Listener { get; private set; }
    public string Prefix { get; }

    /// <summary>Points the facts at another pair of actors, for the next conversation.</summary>
    public void Between(string speaker, string listener)
    {
        Speaker = speaker;
        Listener = listener;
    }

    /// <summary>The facts of the pair now. Both must be actors, or the call is refused with <c>REL_UNKNOWN_ACTOR</c>.</summary>
    public IReadOnlyDictionary<string, FactValue> Facts() => RelationshipFacts.Read(_component.Runtime, Speaker, Listener, Prefix);

    /// <summary>
    /// Refuses a dialogue document that declares an external fact in the relationship namespace (<c>actor.</c>, <c>target.</c>, <c>edge.</c>, <c>reverse.</c>) that the relationship document does not offer, or offers with
    /// another type, or with a range the declaration does not cover.
    /// </summary>
    public static void Check(DialogueSet dialogue, RelationshipSet relationships)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(relationships);
        foreach (var external in dialogue.Externals.Values.Where(e => RelationshipFacts.IsReserved(e.Id)))
        {
            if (!relationships.Facts.TryGetValue(external.Id, out var offered)) throw new EngineRefusedException(Mismatch, "the dialogue document reads '" + external.Id + "', which the relationship document does not offer");
            if (offered.Kind != external.Kind) throw new EngineRefusedException(Mismatch, "'" + external.Id + "' is " + offered.Kind + " in the relationship document and " + external.Kind + " in the dialogue document");
            if (offered.Kind == FactKind.Integer && (offered.Minimum < external.Minimum || offered.Maximum > external.Maximum))
                throw new EngineRefusedException(Mismatch, "'" + external.Id + "' ranges " + offered.Minimum + " to " + offered.Maximum + " in the relationship document, outside the " + external.Minimum + " to " + external.Maximum + " the dialogue document declares");
        }
    }
}
