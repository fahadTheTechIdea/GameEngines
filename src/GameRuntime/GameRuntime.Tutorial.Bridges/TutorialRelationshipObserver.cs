using EngineCommon;
using TutorialEngine;

namespace GameRuntime;

/// <summary>What the relationships did that counts: a bond was set, or an interaction was accepted.</summary>
public enum TutorialRelationshipSignal { BondSet, InteractionAccepted }

/// <summary>One rule: this signal by this actor (empty for any) with this bond or interaction (empty for any) is the player doing the event.</summary>
public sealed record TutorialRelationshipRule(TutorialRelationshipSignal Signal, string Actor, string What, string Event);

/// <summary>
/// Steps completed by what really happened between people. When the relationship engine sets a bond or accepts an interaction that a rule names, the bridge tells the tutorial the rule's event happened. The
/// relationships never learn that a tutorial exists; "make a friend" completes when a friendship really formed, and a refused chat does not count.
/// </summary>
public sealed class TutorialRelationshipObserver : IGameComponent
{
    public const string BadRule = "TUT_RELATIONSHIP_RULE";

    private readonly List<TutorialRelationshipRule> _rules;
    private readonly string _relationshipChannel;
    private readonly string _tutorialChannel;
    private IEventPort? _events;
    private long _observed;

    public TutorialRelationshipObserver(string id, IEnumerable<TutorialRelationshipRule> rules, TutorialSet tutorial, string relationshipChannel = "", string tutorialChannel = "")
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(tutorial);
        Id = id;
        _rules = rules.ToList();
        foreach (var rule in _rules)
            if (!tutorial.EventById.ContainsKey(rule.Event)) throw new EngineRefusedException(BadRule, "the tutorial document has no event '" + rule.Event + "'");
        _relationshipChannel = relationshipChannel;
        _tutorialChannel = tutorialChannel;
    }

    public string Id { get; }

    /// <summary>How many observations have been sent.</summary>
    public long Observed => _observed;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<RelationshipBondChanged>(e => { if (e.Channel == _relationshipChannel && e.Set) Matched(TutorialRelationshipSignal.BondSet, e.Actor, e.Bond, e.Tick, e.Target); });
        context.Events.Connect<RelationshipInteractionPerformed>(e => { if (e.Channel == _relationshipChannel && e.Accepted) Matched(TutorialRelationshipSignal.InteractionAccepted, e.Actor, e.Interaction, e.Tick, e.RequestId); });
    }

    private void Matched(TutorialRelationshipSignal signal, string actor, string what, long tick, string detail)
    {
        var number = 0;
        foreach (var rule in _rules)
        {
            if (rule.Signal != signal || (rule.Actor.Length > 0 && rule.Actor != actor) || (rule.What.Length > 0 && rule.What != what)) continue;
            _events!.Emit(new TutorialRequested(Id + ":" + tick + ":" + signal + ":" + actor + ":" + detail + ":" + number++, TutorialRequestNames.NameOf(TutorialRequestKind.Observe), rule.Event, 1, _tutorialChannel));
            _observed++;
        }
    }

    public void Update(in UpdateContext context)
    {
        // Nothing to do on a tick of its own: it acts on the relationship events.
    }
}
