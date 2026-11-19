using EngineCommon;
using TutorialEngine;
using WorldEventsEngine;

namespace GameRuntime;

/// <summary>One rule: when the world issues this signal (in this region, or in any when none is named), the player has seen the event.</summary>
public sealed record WorldSeenRule(string Signal, string Event, string Region = "");

/// <summary>
/// Tutorial steps completed by what really happened in the world. When the world issues a signal that a rule names (a storm made landfall), the bridge tells the tutorial the rule's event happened (an observation, so
/// deferred and applied in the tutorial's next Update): a step that says "weather your first storm" completes when the storm really came. The world never learns that a tutorial exists; the tutorial never reads
/// the world. Rules are checked when the game is assembled: the signal must be one the world can issue and the event one the tutorial document has.
/// </summary>
public sealed class WorldTutorialObserver : IGameComponent
{
    public const string BadRule = "WE_TUTORIAL_RULE";

    private readonly List<WorldSeenRule> _rules;
    private readonly string _worldChannel;
    private readonly string _tutorialChannel;
    private IEventPort? _events;
    private long _observed;

    public WorldTutorialObserver(string id, IEnumerable<WorldSeenRule> rules, WorldSet world, TutorialSet tutorial, string worldChannel = "", string tutorialChannel = "")
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tutorial);
        Id = id;
        _rules = rules.ToList();
        foreach (var rule in _rules)
        {
            if (!world.Signals.Contains(rule.Signal)) throw new EngineRefusedException(BadRule, "the world document never issues the signal '" + rule.Signal + "'");
            if (rule.Region.Length > 0 && !world.RegionById.ContainsKey(rule.Region)) throw new EngineRefusedException(BadRule, "the world document has no region '" + rule.Region + "'");
            if (!tutorial.EventById.ContainsKey(rule.Event)) throw new EngineRefusedException(BadRule, "the tutorial document has no event '" + rule.Event + "'");
        }
        _worldChannel = worldChannel;
        _tutorialChannel = tutorialChannel;
    }

    public string Id { get; }

    /// <summary>How many observations have been sent.</summary>
    public long Observed => _observed;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<WorldEffectIssued>(OnEffect);
    }

    private void OnEffect(WorldEffectIssued e)
    {
        if (e.Channel != _worldChannel || e.Kind != Words.Name(EffectKind.Signal)) return;
        foreach (var rule in _rules.Where(r => r.Signal == e.Name && (r.Region.Length == 0 || r.Region == e.Region)))
        {
            _events!.Emit(new TutorialRequested(Id + ":" + e.Step + ":" + _observed, TutorialRequestNames.NameOf(TutorialRequestKind.Observe), rule.Event, 1, _tutorialChannel));
            _observed++;
        }
    }

    public void Update(in UpdateContext context)
    {
        // Nothing to do on a tick of its own: it acts on the world's effect events.
    }
}
