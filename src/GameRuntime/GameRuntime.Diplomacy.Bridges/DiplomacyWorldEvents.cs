using DiplomacyEngine;
using EngineCommon;
using WorldEventsEngine;

namespace GameRuntime;

/// <summary>One rule: when the world issues this signal (in this region, or in any when none is named), this political event happens in the realm, at this scale (per mille).</summary>
public sealed record WorldPoliticsRule(string Signal, string Realm, string Event, int Scale, string Region = "");

/// <summary>
/// What the world does to a realm's order. When the world issues a signal that a rule names (a plague has broken out, a flood has started), the bridge tells the diplomacy that the political event happened in the realm
/// (a request, so deferred and applied in the diplomacy's next Update): the realm's factions that mind a disaster change their contentment, and stability follows. The diplomacy never reads the world's state and the world
/// never reads the diplomacy's: they meet through these events and the facts of <see cref="DiplomacyWorldFacts"/>. Rules are checked when the game is assembled against both documents: the signal must be one the world can
/// issue, the region one it has, the realm one the diplomacy has and the event one of the closed set.
/// </summary>
public sealed class DiplomacyWorldEvents : IGameComponent
{
    public const string BadRule = "DIP_WORLD_RULE";

    private readonly List<WorldPoliticsRule> _rules;
    private readonly string _worldChannel;
    private readonly string _diplomacyChannel;
    private IEventPort? _events;
    private long _asked;

    public DiplomacyWorldEvents(string id, IEnumerable<WorldPoliticsRule> rules, WorldSet world, DipSet diplomacy, string worldChannel = "", string diplomacyChannel = "")
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(diplomacy);
        Id = id;
        _rules = rules.ToList();
        foreach (var rule in _rules)
        {
            if (!world.Signals.Contains(rule.Signal)) throw new EngineRefusedException(BadRule, "the world document never issues the signal '" + rule.Signal + "'");
            if (rule.Region.Length > 0 && !world.RegionById.ContainsKey(rule.Region)) throw new EngineRefusedException(BadRule, "the world document has no region '" + rule.Region + "'");
            if (!diplomacy.RealmById.ContainsKey(rule.Realm)) throw new EngineRefusedException(BadRule, "the diplomacy document has no realm '" + rule.Realm + "'");
            if (EnumNames.Parse<PoliticalEvent>(rule.Event) is null) throw new EngineRefusedException(BadRule, "'" + rule.Event + "' is no political event; use " + EnumNames.Choices<PoliticalEvent>());
            if (rule.Scale is < 1 or > 1000) throw new EngineRefusedException(BadRule, "the scale of the rule for '" + rule.Signal + "' is 1 to 1000 per mille, not " + rule.Scale);
        }
        _worldChannel = worldChannel;
        _diplomacyChannel = diplomacyChannel;
    }

    public string Id { get; }

    /// <summary>How many political events have been asked of the diplomacy.</summary>
    public long Asked => _asked;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<WorldEffectIssued>(OnEffect);
    }

    private void OnEffect(WorldEffectIssued e)
    {
        if (e.Channel != _worldChannel || e.Kind != EnumNames.Name(WorldEventsEngine.EffectKind.Signal)) return;
        foreach (var rule in _rules.Where(r => r.Signal == e.Name && (r.Region.Length == 0 || r.Region == e.Region)))
        {
            _events!.Emit(new DiplomacyRequested(Id + ":" + e.Step + ":" + _asked, DiplomacyRequestNames.NameOf(DiplomacyRequestKind.Happen), rule.Realm, rule.Event, "", rule.Scale, 0, _diplomacyChannel));
            _asked++;
        }
    }

    public void Update(in UpdateContext context)
    {
        // Nothing to do on a tick of its own: it acts on the world's effect events.
    }
}
