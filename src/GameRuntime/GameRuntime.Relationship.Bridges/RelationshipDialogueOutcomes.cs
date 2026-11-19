using EngineCommon;

namespace GameRuntime;

/// <summary>What a dialogue outcome asks the relationships to do.</summary>
public enum DialogueOutcomeKind { Interact, Grant, Remember }

/// <summary>
/// One rule: when the <paramref name="Trigger"/> happens, ask for this. A trigger is <c>signal:&lt;name&gt;</c> for a signal a conversation raised, or <c>quest:&lt;id&gt;:&lt;status&gt;</c> for a quest that reached a status. The
/// ids in the request may be fixed or <c>{0}</c>, <c>{1}</c>, ... for the text arguments (<c>s:</c>) of the signal in order. Interact uses <paramref name="Subject"/> as the actor, <paramref name="Other"/> as the target and
/// <paramref name="What"/> as the interaction (with <paramref name="Gift"/>); Grant uses Subject as the actor and What as the moodlet; Remember uses Subject as the holder, Other as the subject and What as the memory.
/// </summary>
public sealed record DialogueOutcome(string Trigger, DialogueOutcomeKind Kind, string Subject, string Other, string What, string Gift = "", int ScalePermille = 1000);

/// <summary>
/// What a conversation did to the relationships: a signal it raised or a quest that ended becomes a request to the relationship component, as an event (so deferred, answered in that component's next Update and
/// refused there if the ids are wrong), never a call into it. A conversation writes down what happened (the player gave the flowers); whether the target accepted is the relationship engine's answer.
/// </summary>
public sealed class RelationshipDialogueOutcomes : IGameComponent
{
    public const string BadRule = "REL_DIALOGUE_RULE";

    private readonly Dictionary<string, DialogueOutcome> _rules;
    private readonly string _relationshipChannel;
    private readonly string _dialogueChannel;
    private IEventPort? _events;
    private long _requested;
    private long _unmatched;

    /// <param name="relationshipChannel">The channel of the relationship component the requests go to.</param>
    /// <param name="dialogueChannel">The channel of the dialogue component whose events are listened to.</param>
    public RelationshipDialogueOutcomes(string id, IEnumerable<DialogueOutcome> rules, string relationshipChannel = "", string dialogueChannel = "")
    {
        ArgumentNullException.ThrowIfNull(rules);
        Id = id;
        _rules = new Dictionary<string, DialogueOutcome>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            if (!DialogueTriggers.IsTrigger(rule.Trigger)) throw new EngineRefusedException(BadRule, "trigger '" + rule.Trigger + "' must be signal:<name> or quest:<id>:<status>");
            if (!_rules.TryAdd(rule.Trigger, rule)) throw new EngineRefusedException(BadRule, "trigger '" + rule.Trigger + "' has two rules");
        }
        _relationshipChannel = relationshipChannel;
        _dialogueChannel = dialogueChannel;
    }

    public string Id { get; }

    /// <summary>How many requests have been raised.</summary>
    public long Requested => _requested;

    /// <summary>How many triggers had no rule.</summary>
    public long Unmatched => _unmatched;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<DialogueSignalRaised>(s => Triggered(s.Channel, DialogueTriggers.ForSignal(s.Name), s.Tick, s.RequestId, s.Args));
        context.Events.Connect<DialogueQuestChanged>(q => Triggered(q.Channel, DialogueTriggers.ForQuest(q.Quest, q.Status), q.Tick, q.RequestId, ""));
    }

    private void Triggered(string channel, string trigger, long tick, string requestId, string args)
    {
        if (channel != _dialogueChannel) return;
        if (!_rules.TryGetValue(trigger, out var rule)) { _unmatched++; return; }
        var texts = DialogueTriggers.TextArguments(args);
        var id = Id + ":" + tick + ":" + requestId + ":" + trigger;
        var subject = DialogueTriggers.Fill(rule.Subject, texts);
        var other = DialogueTriggers.Fill(rule.Other, texts);
        switch (rule.Kind)
        {
            case DialogueOutcomeKind.Interact: _events!.Emit(new RelationshipInteractRequested(id, rule.What, subject, other, "", rule.Gift, _relationshipChannel)); break;
            case DialogueOutcomeKind.Grant: _events!.Emit(new RelationshipGrantRequested(id, subject, rule.What, rule.ScalePermille, other, _relationshipChannel)); break;
            case DialogueOutcomeKind.Remember: _events!.Emit(new RelationshipRememberRequested(id, subject, other, rule.What, rule.ScalePermille, _relationshipChannel)); break;
        }
        _requested++;
    }

    public void Update(in UpdateContext context)
    {
        // Nothing to do on a tick of its own: it acts on the dialogue events.
    }
}
