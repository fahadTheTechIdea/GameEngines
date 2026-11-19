using System.Text.Json;
using EngineCommon;

namespace RelationshipEngine;

internal sealed partial class RelationshipReader
{
    /// <summary>The most nodes times edges per node the graph library takes (its edge limit); the social passes build a graph of every opinion, so a document that can hold more is refused when they are on.</summary>
    public const long GraphEdgeCeiling = 2_000_000;

    private const int AlphaCeiling = 1000;

    private static readonly IReadOnlyDictionary<string, DaySchedule> Schedules = new Dictionary<string, DaySchedule>(StringComparer.Ordinal) { ["atomic"] = DaySchedule.Atomic, ["spread"] = DaySchedule.Spread };

    /// <summary>
    /// The <c>social</c> object: what is derived from the whole network of opinions each day. Every block is always present; <c>enabled: false</c> turns it off (its other fields are still checked, and the actors then
    /// keep their defaults: alone in their own community, standing 1000).
    /// </summary>
    private SocialDef ReadSocial(JsonElement element)
    {
        _json.Object(element, "social", ["communities", "standing", "balance", "schedule"]);
        var axisIndex = Index(_axes, a => a.Id);
        var communities = ReadCommunities(element.GetProperty("communities"), axisIndex);
        var standing = ReadStanding(element.GetProperty("standing"), axisIndex);
        var balance = ReadBalance(element.GetProperty("balance"), axisIndex);
        if ((communities.Enabled || standing.Enabled) && (long)_limits.MaxActors * _limits.MaxEdgesPerActor > GraphEdgeCeiling)
            throw _json.Refuse(RelationshipCodes.Social, "communities and standing build a graph of every opinion: maxActors x maxEdgesPerActor is " + (long)_limits.MaxActors * _limits.MaxEdgesPerActor + ", the graph takes at most " + GraphEdgeCeiling);
        var schedule = Choice(element, "schedule", "social", Schedules);
        if (schedule == DaySchedule.Spread && _stepsPerDay < RelationshipRuntime.DayStages)
            throw _json.Refuse(RelationshipCodes.Social, "social.schedule \"spread\" runs one stage in each of the first " + RelationshipRuntime.DayStages + " steps of the day, but time.stepsPerDay is " + _stepsPerDay);
        return new SocialDef(communities, standing, balance, schedule);
    }

    /// <summary>An axis line that opinions are measured against: strictly inside the axis, so some opinions can be above it and some below.</summary>
    private long Line(JsonElement owner, string field, string where, AxisDef axis, bool above)
    {
        var low = above ? axis.Min : axis.Min + 1;
        var high = above ? axis.Max - 1 : axis.Max;
        return _json.Long(owner, field, where, low, high);
    }

    private CommunityDef ReadCommunities(JsonElement element, IReadOnlyDictionary<string, int> axisIndex)
    {
        const string where = "social.communities";
        _json.Object(element, where, ["enabled", "axis", "above", "maxRounds"]);
        var axis = Ref(element, "axis", where, axisIndex, "axis");
        return new CommunityDef(_json.Bool(element, "enabled", where), axis, Line(element, "above", where, _axes[axis], true), _json.Int(element, "maxRounds", where, 1, 64));
    }

    private StandingDef ReadStanding(JsonElement element, IReadOnlyDictionary<string, int> axisIndex)
    {
        const string where = "social.standing";
        _json.Object(element, where, ["enabled", "axis", "above", "alphaPermille", "iterations", "preTrustTrait", "eventDelta"]);
        var axis = Ref(element, "axis", where, axisIndex, "axis");
        var trait = _json.Text(element, "preTrustTrait", where, 128);
        if (trait.Length > 0) _json.CheckId(trait, where + ".preTrustTrait");
        return new StandingDef(_json.Bool(element, "enabled", where), axis, Line(element, "above", where, _axes[axis], true), _json.Int(element, "alphaPermille", where, 1, AlphaCeiling),
            _json.Int(element, "iterations", where, 1, 64), trait, _json.Long(element, "eventDelta", where, 1, StandingMaxDelta));
    }

    private const long StandingMaxDelta = 1_000_000_000;

    private BalanceDef ReadBalance(JsonElement element, IReadOnlyDictionary<string, int> axisIndex)
    {
        const string where = "social.balance";
        _json.Object(element, where, ["enabled", "axis", "like", "dislike", "memory", "strengthPermille", "topK", "maxPerActor", "chancePermille"]);
        var enabled = _json.Bool(element, "enabled", where);
        var axis = Ref(element, "axis", where, axisIndex, "axis");
        var like = Line(element, "like", where, _axes[axis], false);
        var dislike = Line(element, "dislike", where, _axes[axis], true);
        if (dislike >= like) throw _json.Refuse(RelationshipCodes.Social, where + ".dislike " + dislike + " must be below like " + like);
        var memoryName = _json.Text(element, "memory", where, 128);
        var memory = -1;
        if (memoryName.Length > 0) memory = Ref(element, "memory", where, Index(_memories, m => m.Id), "memory");
        else if (enabled) throw _json.Refuse(RelationshipCodes.Social, where + ".memory must name the memory the pressure is given as");
        if (memory >= 0) CheckPressureMemory(_memories[memory], axis, where);
        return new BalanceDef(enabled, axis, like, dislike, memory, _json.Int(element, "strengthPermille", where, 1, 10_000), _json.Int(element, "topK", where, 1, 8), _json.Int(element, "maxPerActor", where, 1, 16),
            _json.Int(element, "chancePermille", where, 1, 1000));
    }

    /// <summary>The memory the pressure is given as must move the balance axis, and must not travel by gossip (the pressure is the holder's own, from its own view of a triad).</summary>
    private void CheckPressureMemory(MemoryDef memory, int axis, string where)
    {
        if (memory.Effects.All(e => e.Axis != axis)) throw _json.Refuse(RelationshipCodes.Social, where + ".memory '" + memory.Id + "' has no effect on axis '" + _axes[axis].Id + "'");
        if (memory.Spread is not null) throw _json.Refuse(RelationshipCodes.Social, where + ".memory '" + memory.Id + "' spreads by gossip; the pressure of a triad is the holder's own and must not");
    }
}
