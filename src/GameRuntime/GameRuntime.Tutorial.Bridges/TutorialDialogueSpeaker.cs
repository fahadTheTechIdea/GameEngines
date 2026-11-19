using DialogueEngine;
using EngineCommon;
using TutorialEngine;

namespace GameRuntime;

/// <summary>One rule: when the step becomes active, the dialogue engine speaks the conversation; when that conversation ends (with the result, when one is named), the player did the event.</summary>
public sealed record TutorialSpokenStep(string Step, string Conversation, string Event, string Result = "");

/// <summary>
/// A tutorial step spoken by the dialogue engine. When a step with a rule becomes active the bridge asks the dialogue component to start the rule's conversation (an event, so deferred and answered in that
/// component's next Update, refused there if it cannot start); when that conversation ends the bridge tells the tutorial the rule's event happened (an observation, so the step whose completion reads that event
/// completes in the tutorial's next Update). The tutorial never reads the dialogue state and the dialogue never reads the tutorial's: they meet through these events only. Only a conversation this bridge started
/// counts as finished, so the same conversation spoken for another reason does not complete the step.
/// </summary>
public sealed class TutorialDialogueSpeaker : IGameComponent
{
    public const string BadRule = "TUT_SPEAKER_RULE";

    private readonly Dictionary<string, TutorialSpokenStep> _byStep;
    private readonly Dictionary<string, TutorialSpokenStep> _open = new(StringComparer.Ordinal);
    private readonly string _tutorialChannel;
    private readonly string _dialogueChannel;
    private IEventPort? _events;
    private long _started;
    private long _finished;

    /// <param name="tutorial">The tutorial document: every rule names a step and an event it has.</param>
    /// <param name="dialogue">The dialogue document: every rule names a conversation it has.</param>
    /// <param name="tutorialChannel">The channel of the tutorial component whose step changes are listened to and which is told what happened.</param>
    /// <param name="dialogueChannel">The channel of the dialogue component that speaks.</param>
    public TutorialDialogueSpeaker(string id, IEnumerable<TutorialSpokenStep> rules, TutorialSet tutorial, DialogueSet dialogue, string tutorialChannel = "", string dialogueChannel = "")
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(tutorial);
        ArgumentNullException.ThrowIfNull(dialogue);
        Id = id;
        _byStep = new Dictionary<string, TutorialSpokenStep>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            if (!tutorial.StepById.ContainsKey(rule.Step)) throw new EngineRefusedException(BadRule, "the tutorial document has no step '" + rule.Step + "'");
            if (!tutorial.EventById.ContainsKey(rule.Event)) throw new EngineRefusedException(BadRule, "the tutorial document has no event '" + rule.Event + "'");
            if (!dialogue.Conversations.ContainsKey(rule.Conversation)) throw new EngineRefusedException(BadRule, "the dialogue document has no conversation '" + rule.Conversation + "'");
            if (!_byStep.TryAdd(rule.Step, rule)) throw new EngineRefusedException(BadRule, "step '" + rule.Step + "' has two rules");
        }
        _tutorialChannel = tutorialChannel;
        _dialogueChannel = dialogueChannel;
    }

    public string Id { get; }

    /// <summary>How many conversations have been asked for.</summary>
    public long Started => _started;

    /// <summary>How many of them ended and were told to the tutorial.</summary>
    public long Finished => _finished;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<TutorialStepChanged>(OnStep);
        context.Events.Connect<DialogueEnded>(OnEnded);
    }

    private void OnStep(TutorialStepChanged e)
    {
        if (e.Channel != _tutorialChannel || e.To != "active" || !_byStep.TryGetValue(e.Step, out var rule)) return;
        _open[rule.Conversation] = rule;
        _events!.Emit(new DialogueStartRequested(Id + ":" + e.Tick + ":" + e.Step, rule.Conversation, _dialogueChannel));
        _started++;
    }

    private void OnEnded(DialogueEnded e)
    {
        if (e.Channel != _dialogueChannel || !_open.TryGetValue(e.Conversation, out var rule)) return;
        if (rule.Result.Length > 0 && rule.Result != e.Result) return;
        _open.Remove(e.Conversation);
        _events!.Emit(new TutorialRequested(Id + ":" + e.Tick + ":" + e.Conversation, TutorialRequestNames.NameOf(TutorialRequestKind.Observe), rule.Event, 1, _tutorialChannel));
        _finished++;
    }

    public void Update(in UpdateContext context)
    {
        // Nothing to do on a tick of its own: it acts on the tutorial and dialogue events.
    }
}
