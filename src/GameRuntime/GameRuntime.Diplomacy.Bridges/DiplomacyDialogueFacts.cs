using DialogueEngine;
using DiplomacyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// What a negotiation reads of the diplomacy, as external facts: a realm's order and standing and, for the other realm across the table, the pair's regard, war, coalition, knowledge and treaties, under the <c>ext.</c>
/// prefix a dialogue document uses (an envoy who is cold because <c>ext.diplomacy.aurel.brass.regard</c> is low). The dialogue component is given <see cref="Facts"/> as its fact source; a dialogue document declares the
/// ones it reads as external facts with the same ids (without the prefix), and <see cref="Check"/> verifies the declarations against the diplomacy document when the game is assembled.
/// </summary>
public sealed class DiplomacyDialogueFacts
{
    public const string Mismatch = "DIP_DIALOGUE_MISMATCH";

    private readonly DiplomacyComponent _component;

    public DiplomacyDialogueFacts(DiplomacyComponent component, string realm, string other, string prefix = "ext.")
    {
        ArgumentNullException.ThrowIfNull(component);
        _component = component;
        Realm = realm;
        Other = other;
        Prefix = prefix;
    }

    public string Realm { get; private set; }
    public string Other { get; private set; }
    public string Prefix { get; }

    /// <summary>Points the facts at another pair of realms, for the next conversation (the envoy went to another court).</summary>
    public void Between(string realm, string other)
    {
        Realm = realm;
        Other = other;
    }

    /// <summary>The facts of the pair now. Both must be realms, or the call is refused with <c>DIP_UNKNOWN_REALM</c>.</summary>
    public IReadOnlyDictionary<string, FactValue> Facts() => _component.Facts(Prefix, Realm, Other);

    /// <summary>Refuses a dialogue document that declares an external fact in the diplomacy namespace (<c>diplomacy.</c>) that the diplomacy document does not offer, or offers with another type, or with a range the declaration does not cover.</summary>
    public static void Check(DialogueSet dialogue, DipSet diplomacy)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(diplomacy);
        var offered = DipFacts.Offered(diplomacy);
        foreach (var external in dialogue.Externals.Values.Where(e => e.Id.StartsWith("diplomacy.", StringComparison.Ordinal)))
        {
            if (!offered.TryGetValue(external.Id, out var given)) throw new EngineRefusedException(Mismatch, "the dialogue document reads '" + external.Id + "', which the diplomacy document does not offer");
            if (given.Kind != external.Kind) throw new EngineRefusedException(Mismatch, "'" + external.Id + "' is " + given.Kind + " in the diplomacy document and " + external.Kind + " in the dialogue document");
            if (given.Kind == FactKind.Integer && (given.Minimum < external.Minimum || given.Maximum > external.Maximum))
                throw new EngineRefusedException(Mismatch, "'" + external.Id + "' ranges " + given.Minimum + " to " + given.Maximum + " in the diplomacy document, outside the " + external.Minimum + " to " + external.Maximum + " the dialogue document declares");
        }
    }
}
