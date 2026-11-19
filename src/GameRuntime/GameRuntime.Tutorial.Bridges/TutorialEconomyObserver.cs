using EngineCommon;
using TutorialEngine;

namespace GameRuntime;

/// <summary>One rule: a trade by this trader (empty for any) of this good (empty for any) is the player doing the event.</summary>
public sealed record TutorialTradeRule(string Trader, string Good, string Event);

/// <summary>
/// Steps completed by what really happened in the economy. When the economy executes a trade that a rule names, the bridge tells the tutorial the rule's event happened (an observation, so deferred and applied in
/// the tutorial's next Update). The economy never learns that a tutorial exists; the tutorial never reads the economy: a step that says "make a trade" completes when the market really made one, and not when a
/// button was pressed.
/// </summary>
public sealed class TutorialEconomyObserver : IGameComponent
{
    public const string BadRule = "TUT_ECONOMY_RULE";

    private readonly List<TutorialTradeRule> _rules;
    private readonly string _economyChannel;
    private readonly string _tutorialChannel;
    private IEventPort? _events;
    private long _observed;

    public TutorialEconomyObserver(string id, IEnumerable<TutorialTradeRule> rules, TutorialSet tutorial, string economyChannel = "", string tutorialChannel = "")
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(tutorial);
        Id = id;
        _rules = rules.ToList();
        foreach (var rule in _rules)
            if (!tutorial.EventById.ContainsKey(rule.Event)) throw new EngineRefusedException(BadRule, "the tutorial document has no event '" + rule.Event + "'");
        _economyChannel = economyChannel;
        _tutorialChannel = tutorialChannel;
    }

    public string Id { get; }

    /// <summary>How many observations have been sent.</summary>
    public long Observed => _observed;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<EconomyTradeExecuted>(OnTrade);
    }

    private void OnTrade(EconomyTradeExecuted e)
    {
        if (e.Channel != _economyChannel) return;
        var number = 0;
        foreach (var rule in _rules)
        {
            if ((rule.Trader.Length > 0 && rule.Trader != e.Trader) || (rule.Good.Length > 0 && rule.Good != e.Good)) continue;
            _events!.Emit(new TutorialRequested(Id + ":" + e.Tick + ":" + e.RequestId + ":" + number++, TutorialRequestNames.NameOf(TutorialRequestKind.Observe), rule.Event, 1, _tutorialChannel));
            _observed++;
        }
    }

    public void Update(in UpdateContext context)
    {
        // Nothing to do on a tick of its own: it acts on the economy events.
    }
}
