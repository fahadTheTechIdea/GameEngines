using DiplomacyEngine;
using EngineCommon;

namespace GameRuntime;

/// <summary>What a negotiation's outcome asks the diplomacy to do: put a treaty to a realm, answer an offer, declare a war, remember a deed, or grant a casus belli.</summary>
public enum DiplomacyOutcomeKind { Propose, Respond, Declare, Deed, Grant }

/// <summary>
/// One rule: when the <paramref name="Trigger"/> happens, ask for this. A trigger is <c>signal:&lt;name&gt;</c> for a signal a conversation raised, or <c>quest:&lt;id&gt;:&lt;status&gt;</c> for a quest that reached a status. The ids in
/// the request may be fixed or <c>{0}</c>, <c>{1}</c>, ... for the text arguments (<c>s:</c>) of the signal in order. Propose uses Subject as the proposer, Other as the target, What as the treaty type, Days as its term and Amount as
/// its tribute. Respond uses Id as the proposal and Accept. Declare uses Subject as the attacker, Other as the target and What as the casus belli ("" for none). Deed uses Subject as the doer, Other as the receiver, What as the deed
/// and Amount as its scale. Grant uses Subject as the holder, Other as the target, What as the casus belli and Days.
/// </summary>
public sealed record DiplomacyOutcome(string Trigger, DiplomacyOutcomeKind Kind, string Subject, string Other, string What, int Days = 0, long Amount = 0, long Id = 0, bool Accept = true);

/// <summary>
/// What a negotiation did to the diplomacy: a signal it raised or a quest that ended becomes a request to the diplomacy component, as an event (so deferred, answered in that component's next Update and refused there if the
/// ids are wrong), never a call into it. A conversation writes down what was said (the envoy offered an alliance); whether the other realm accepted is the diplomacy's answer, by its acceptance score.
/// </summary>
public sealed class DiplomacyDialogueOutcomes : IGameComponent
{
    public const string BadRule = "DIP_DIALOGUE_RULE";

    private readonly Dictionary<string, DiplomacyOutcome> _rules;
    private readonly string _diplomacyChannel;
    private readonly string _dialogueChannel;
    private IEventPort? _events;
    private long _requested;
    private long _unmatched;

    /// <param name="diplomacyChannel">The channel of the diplomacy component the requests go to.</param>
    /// <param name="dialogueChannel">The channel of the dialogue component whose events are listened to.</param>
    public DiplomacyDialogueOutcomes(string id, IEnumerable<DiplomacyOutcome> rules, string diplomacyChannel = "", string dialogueChannel = "")
    {
        ArgumentNullException.ThrowIfNull(rules);
        Id = id;
        _rules = new Dictionary<string, DiplomacyOutcome>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            if (!DialogueTriggers.IsTrigger(rule.Trigger)) throw new EngineRefusedException(BadRule, "trigger '" + rule.Trigger + "' must be signal:<name> or quest:<id>:<status>");
            if (!_rules.TryAdd(rule.Trigger, rule)) throw new EngineRefusedException(BadRule, "trigger '" + rule.Trigger + "' has two rules");
        }
        _diplomacyChannel = diplomacyChannel;
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
        var what = DialogueTriggers.Fill(rule.What, texts);
        switch (rule.Kind)
        {
            case DiplomacyOutcomeKind.Propose: _events!.Emit(new DiplomacyRequested(id, "propose", subject, other, what, rule.Days, rule.Amount, _diplomacyChannel)); break;
            case DiplomacyOutcomeKind.Respond: _events!.Emit(new DiplomacyRequested(id, "respond", "", "", "", rule.Id, rule.Accept ? 1 : 0, _diplomacyChannel)); break;
            case DiplomacyOutcomeKind.Declare: _events!.Emit(new DiplomacyRequested(id, "declare", subject, other, what, 0, 0, _diplomacyChannel)); break;
            case DiplomacyOutcomeKind.Deed: _events!.Emit(new DiplomacyRequested(id, "deed", subject, other, what, rule.Amount, 0, _diplomacyChannel)); break;
            case DiplomacyOutcomeKind.Grant: _events!.Emit(new DiplomacyRequested(id, "grant", subject, other, what, rule.Days, 0, _diplomacyChannel)); break;
        }
        _requested++;
    }

    public void Update(in UpdateContext context)
    {
        // Nothing to do on a tick of its own: it acts on the dialogue events.
    }
}
