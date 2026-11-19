using DiplomacyEngine;
using EngineCommon;
using TutorialEngine;

namespace GameRuntime;

/// <summary>One rule: when the diplomacy issues this signal (for this realm, or for any when none is named), the player has seen the event.</summary>
public sealed record DiplomacySeenRule(string Signal, string Event, string Realm = "");

/// <summary>
/// Tutorial steps completed by what really happened between the realms. When the diplomacy issues a signal that a rule names (a first treaty was signed, a first war ended in peace), the bridge tells the tutorial the
/// rule's event happened (an observation, so deferred and applied in the tutorial's next Update): a step that says "make your first alliance" completes when the alliance was really made. The diplomacy never learns that a
/// tutorial exists; the tutorial never reads the diplomacy. Rules are checked when the game is assembled: the signal must be one the diplomacy can issue and the event one the tutorial document has.
/// </summary>
public sealed class DiplomacyTutorialObserver : IGameComponent
{
    public const string BadRule = "DIP_TUTORIAL_RULE";

    private readonly List<DiplomacySeenRule> _rules;
    private readonly string _diplomacyChannel;
    private readonly string _tutorialChannel;
    private IEventPort? _events;
    private long _observed;

    public DiplomacyTutorialObserver(string id, IEnumerable<DiplomacySeenRule> rules, DipSet diplomacy, TutorialSet tutorial, string diplomacyChannel = "", string tutorialChannel = "")
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(diplomacy);
        ArgumentNullException.ThrowIfNull(tutorial);
        Id = id;
        _rules = rules.ToList();
        foreach (var rule in _rules)
        {
            if (!diplomacy.Signals.Contains(rule.Signal)) throw new EngineRefusedException(BadRule, "the diplomacy document never issues the signal '" + rule.Signal + "'");
            if (rule.Realm.Length > 0 && !diplomacy.RealmById.ContainsKey(rule.Realm)) throw new EngineRefusedException(BadRule, "the diplomacy document has no realm '" + rule.Realm + "'");
            if (!tutorial.EventById.ContainsKey(rule.Event)) throw new EngineRefusedException(BadRule, "the tutorial document has no event '" + rule.Event + "'");
        }
        _diplomacyChannel = diplomacyChannel;
        _tutorialChannel = tutorialChannel;
    }

    public string Id { get; }

    /// <summary>How many observations have been sent.</summary>
    public long Observed => _observed;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<DiplomacyEffectIssued>(OnEffect);
    }

    private void OnEffect(DiplomacyEffectIssued e)
    {
        if (e.Channel != _diplomacyChannel || e.Kind != EnumNames.Name(DiplomacyEngine.EffectKind.Signal)) return;
        foreach (var rule in _rules.Where(r => r.Signal == e.Name && (r.Realm.Length == 0 || r.Realm == e.Realm)))
        {
            _events!.Emit(new TutorialRequested(Id + ":" + e.Step + ":" + _observed, TutorialRequestNames.NameOf(TutorialRequestKind.Observe), rule.Event, 1, _tutorialChannel));
            _observed++;
        }
    }

    public void Update(in UpdateContext context)
    {
        // Nothing to do on a tick of its own: it acts on the diplomacy's effect events.
    }
}
