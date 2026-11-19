using DiplomacyEngine;
using EngineCommon;
using RelationshipEngine;

namespace GameRuntime;

/// <summary>What a treaty or a war does between the leaders of the realms it joins: set or break a bond (a marriage), move an axis, or leave a memory.</summary>
public enum LeaderEffectKind { Bond, Axis, Remember }

/// <summary>
/// One rule: when the <paramref name="Trigger"/> happens, ask the relationship layer for this between the two realms' leaders. A trigger is <c>treaty:&lt;type&gt;:&lt;change&gt;</c> (the change being a treaty change name: signed,
/// expired, cancelled, broken, ended) or <c>war:&lt;change&gt;</c> (a war change name: declared, ended...). For a treaty the first realm's leader holds and the second's is the other; for a war the attacker's and the defender's.
/// Bond sets (<paramref name="Set"/>) or breaks the bond named by <paramref name="What"/>; Axis moves the axis <paramref name="What"/> by <paramref name="Amount"/>; Remember leaves the memory <paramref name="What"/> at
/// <paramref name="Amount"/> per mille. <paramref name="Both"/> asks it in both directions.
/// </summary>
public sealed record LeaderRule(string Trigger, LeaderEffectKind Kind, string What, int Amount = 1000, bool Set = true, bool Both = true);

/// <summary>
/// What the realms did, as it happens to their leaders. A treaty signed, broken or ended, or a war begun or ended, becomes a request to the relationship component for the leaders of the realms it concerns (a
/// marriage is a bond between the two rulers; a betrayal is a memory the betrayed ruler keeps), as an event, never a call into it. Rules are checked when the game is assembled: the types and changes must be the
/// diplomacy document's, and the bond, axis or memory the relationship document's.
/// </summary>
public sealed class DiplomacyLeaderOutcomes : IGameComponent
{
    public const string BadRule = "DIP_LEADER_RULE";

    private readonly Dictionary<string, List<LeaderRule>> _rules = new(StringComparer.Ordinal);
    private readonly DipSet _set;
    private readonly string _diplomacyChannel;
    private readonly string _relationshipChannel;
    private IEventPort? _events;
    private long _requested;

    public DiplomacyLeaderOutcomes(string id, IEnumerable<LeaderRule> rules, DipSet diplomacy, RelationshipSet relationships, string diplomacyChannel = "", string relationshipChannel = "")
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(diplomacy);
        ArgumentNullException.ThrowIfNull(relationships);
        Id = id;
        _set = diplomacy;
        foreach (var rule in rules)
        {
            var parts = rule.Trigger.Split(':');
            var valid = parts is ["treaty", var type, var change] ? diplomacy.TypeById.ContainsKey(type) && EnumNames.Parse<TreatyChange>(change) is not null
                : parts is ["war", var warChange] && EnumNames.Parse<WarChange>(warChange) is not null;
            if (!valid) throw new EngineRefusedException(BadRule, "trigger '" + rule.Trigger + "' must be treaty:<type>:<change> or war:<change> of the diplomacy document");
            switch (rule.Kind)
            {
                case LeaderEffectKind.Bond when !relationships.BondById.ContainsKey(rule.What): throw new EngineRefusedException(BadRule, "the relationship document has no bond '" + rule.What + "'");
                case LeaderEffectKind.Axis when !relationships.AxisById.ContainsKey(rule.What): throw new EngineRefusedException(BadRule, "the relationship document has no axis '" + rule.What + "'");
                case LeaderEffectKind.Remember when !relationships.MemoryById.ContainsKey(rule.What): throw new EngineRefusedException(BadRule, "the relationship document has no memory '" + rule.What + "'");
            }
            if (!_rules.TryGetValue(rule.Trigger, out var list)) _rules[rule.Trigger] = list = new List<LeaderRule>();
            list.Add(rule);
        }
        _diplomacyChannel = diplomacyChannel;
        _relationshipChannel = relationshipChannel;
    }

    public string Id { get; }

    /// <summary>How many requests have been asked of the relationship layer.</summary>
    public long Requested => _requested;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<DiplomacyTreatyChanged>(e => Happened(e.Channel, "treaty:" + e.Type + ":" + e.Change, e.First, e.Second, e.Step));
        context.Events.Connect<DiplomacyWarChanged>(e => Happened(e.Channel, "war:" + e.Change, e.Attacker, e.Defender, e.Step));
    }

    private void Happened(string channel, string trigger, string first, string second, long step)
    {
        if (channel != _diplomacyChannel || !_rules.TryGetValue(trigger, out var rules)) return;
        var a = _set.RealmById[first].Leader;
        var b = _set.RealmById[second].Leader;
        if (a.Length == 0 || b.Length == 0) return;
        foreach (var rule in rules)
        {
            Ask(rule, a, b, step);
            if (rule.Both) Ask(rule, b, a, step);
        }
    }

    private void Ask(LeaderRule rule, string holder, string other, long step)
    {
        var id = Id + ":" + step + ":" + _requested;
        switch (rule.Kind)
        {
            case LeaderEffectKind.Bond: _events!.Emit(new RelationshipBondRequested(id, holder, other, rule.What, rule.Set, _relationshipChannel)); break;
            case LeaderEffectKind.Axis: _events!.Emit(new RelationshipAxisRequested(id, holder, other, rule.What, rule.Amount, _relationshipChannel)); break;
            case LeaderEffectKind.Remember: _events!.Emit(new RelationshipRememberRequested(id, holder, other, rule.What, rule.Amount, _relationshipChannel)); break;
        }
        _requested++;
    }

    public void Update(in UpdateContext context)
    {
        // Nothing to do on a tick of its own: it acts on the diplomacy's events.
    }
}
