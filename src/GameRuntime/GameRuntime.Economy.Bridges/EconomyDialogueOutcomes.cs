using EngineCommon;

namespace GameRuntime;

/// <summary>What a dialogue outcome asks the economy to do.</summary>
public enum EconomyOutcomeKind { Trade, Shock, Settle }

/// <summary>
/// One rule: when the <paramref name="Trigger"/> happens (see <see cref="DialogueTriggers"/>), ask for this. The ids may be fixed or <c>{0}</c>, <c>{1}</c>, ... for the text arguments (<c>s:</c>) of the signal.
/// Trade: <paramref name="A"/> is the trader, <paramref name="B"/> the market, <paramref name="C"/> the good, <paramref name="Quantity"/> the units (positive: the trader buys). Shock: A is the shock, B the market
/// (empty: every market), <paramref name="ScalePermille"/> how hard. Settle: A is the manual schedule.
/// </summary>
public sealed record EconomyOutcome(string Trigger, EconomyOutcomeKind Kind, string A, string B = "", string C = "", long Quantity = 0, int ScalePermille = 1000);

/// <summary>
/// What a conversation did to the markets: a signal it raised or a quest that ended becomes a request to the economy component, as an event (so deferred, answered in that component's next Update and refused
/// there if the ids are wrong or the stock is short), never a call into it. A conversation writes down what happened (the player agreed to buy the grain); whether the market could sell is the economy's answer,
/// which the dialogue's host reads back as the trade event one tick later.
/// </summary>
public sealed class EconomyDialogueOutcomes : IGameComponent
{
    public const string BadRule = "ECON_DIALOGUE_RULE";

    private readonly Dictionary<string, EconomyOutcome> _rules;
    private readonly string _economyChannel;
    private readonly string _dialogueChannel;
    private IEventPort? _events;
    private long _requested;
    private long _unmatched;

    /// <param name="economyChannel">The channel of the economy component the requests go to.</param>
    /// <param name="dialogueChannel">The channel of the dialogue component whose events are listened to.</param>
    public EconomyDialogueOutcomes(string id, IEnumerable<EconomyOutcome> rules, string economyChannel = "", string dialogueChannel = "")
    {
        ArgumentNullException.ThrowIfNull(rules);
        Id = id;
        _rules = new Dictionary<string, EconomyOutcome>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            if (!DialogueTriggers.IsTrigger(rule.Trigger)) throw new EngineRefusedException(BadRule, "trigger '" + rule.Trigger + "' must be signal:<name> or quest:<id>:<status>");
            if (!_rules.TryAdd(rule.Trigger, rule)) throw new EngineRefusedException(BadRule, "trigger '" + rule.Trigger + "' has two rules");
        }
        _economyChannel = economyChannel;
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
        var a = DialogueTriggers.Fill(rule.A, texts);
        var b = DialogueTriggers.Fill(rule.B, texts);
        var c = DialogueTriggers.Fill(rule.C, texts);
        switch (rule.Kind)
        {
            case EconomyOutcomeKind.Trade: _events!.Emit(new EconomyTradeRequested(id, a, b, c, rule.Quantity, _economyChannel)); break;
            case EconomyOutcomeKind.Shock: _events!.Emit(new EconomyShockRequested(id, a, b, rule.ScalePermille, _economyChannel)); break;
            case EconomyOutcomeKind.Settle: _events!.Emit(new EconomySettleRequested(id, a, _economyChannel)); break;
        }
        _requested++;
    }

    public void Update(in UpdateContext context)
    {
        // Nothing to do on a tick of its own: it acts on the dialogue events.
    }
}
