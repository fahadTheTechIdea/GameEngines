using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using TutorialEngine;

namespace GameRuntime;

/// <summary>
/// What a conversation reads of the player's tutorial, as external facts: the status of each step, the skills and tips, the phase and the journey, under the <c>ext.</c> prefix a dialogue document uses (a guide who
/// only explains the road tool when <c>ext.step.road.status</c> is <c>active</c>). The dialogue component is given <see cref="Facts"/> as its fact source; a dialogue document declares the ones it reads as external
/// facts with the same ids, and <see cref="Check"/> verifies the declarations against the tutorial document when the game is assembled.
/// </summary>
public sealed class TutorialDialogueFacts
{
    public const string Mismatch = "TUT_DIALOGUE_MISMATCH";

    private readonly TutorialComponent _component;

    public TutorialDialogueFacts(TutorialComponent component, string prefix = "ext.")
    {
        ArgumentNullException.ThrowIfNull(component);
        _component = component;
        Prefix = prefix;
    }

    public string Prefix { get; }

    /// <summary>The facts of the tutorial now, without the host's own and without the ones only a step's completion can read.</summary>
    public IReadOnlyDictionary<string, FactValue> Facts() => _component.Runtime.ReadFacts(Prefix);

    /// <summary>
    /// Refuses a dialogue document that declares an external fact in the tutorial namespace (<c>event.</c>, <c>step.</c>, <c>tip.</c>, <c>skill.</c>, <c>tutorial.</c>, <c>journey.</c>, <c>setting.</c>) that the tutorial
    /// document does not offer to a conversation, or offers with another type, or with a range the declaration does not cover.
    /// </summary>
    public static void Check(DialogueSet dialogue, TutorialSet tutorial)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(tutorial);
        foreach (var external in dialogue.Externals.Values.Where(e => TutorialFacts.IsReserved(e.Id)))
        {
            if (!tutorial.Facts.TryGetValue(external.Id, out var offered) || tutorial.Bindings[external.Id].Source == FactSource.EventSince)
                throw new EngineRefusedException(Mismatch, "the dialogue document reads '" + external.Id + "', which the tutorial document does not offer to a conversation");
            if (offered.Kind != external.Kind) throw new EngineRefusedException(Mismatch, "'" + external.Id + "' is " + offered.Kind + " in the tutorial document and " + external.Kind + " in the dialogue document");
            if (offered.Kind == FactKind.Integer && (offered.Minimum < external.Minimum || offered.Maximum > external.Maximum))
                throw new EngineRefusedException(Mismatch, "'" + external.Id + "' ranges " + offered.Minimum + " to " + offered.Maximum + " in the tutorial document, outside the " + external.Minimum + " to " + external.Maximum + " the dialogue document declares");
        }
    }
}
