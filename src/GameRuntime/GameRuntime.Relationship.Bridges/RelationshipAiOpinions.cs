using AIEngine;
using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// Tells AI agents what they know of the people around them, as percepts: knowledge, not truth. For each (agent, other) pair the host says the agent can perceive, the agent is told, under the other's actor id,
/// <b>its own opinions of the other</b> on every axis (<c>opinion_&lt;axis&gt;</c>) and the state of each ladder (<c>state_&lt;ladder&gt;</c>), whether it knows the other at all (<c>known</c>), and the two things a
/// village knows about everybody: the other's <b>standing</b> (<c>standing</c>, parts per thousand of the village average) and whether the other is in the agent's own community (<c>same_community</c>) and how big
/// that community is (<c>community_size</c>). An agent is never told what others think of it, how another feels, or what it has not seen: those are not things it could know. The AI document chooses what to do with
/// them (befriend the most respected neighbour, avoid a hostile circle) through its own utility nodes; this bridge decides nothing. The percept is posted again only when what the agent would perceive changed.
/// </summary>
/// <remarks>
/// The AI document must declare the percept kind with exactly these attributes, with ranges that cover the relationship document's (checked when the game is assembled). Add the bridge after the relationship component and
/// before the AI component. The agent id and the percept id are actor ids. An agent that stops perceiving someone keeps what it last knew until the kind's memory time lets it go.
/// </remarks>
public sealed class RelationshipAiOpinions : IGameComponent
{
    private readonly RelationshipComponent _relationships;
    private readonly string _aiComponent;
    private readonly string _kind;
    private readonly Func<IReadOnlyList<(string Agent, string Other)>> _perceived;
    private readonly Dictionary<(string Agent, string Other), string> _told = new();
    private IEventPort? _events;
    private long _posted;

    /// <param name="aiComponent">The id of the AI component whose agents are told.</param>
    /// <param name="aiSet">The AI document, to check the percept kind against.</param>
    /// <param name="kind">The percept kind that carries what an agent knows of one other.</param>
    /// <param name="perceived">The pairs (agent, other) that can perceive each other now, in the order they are told; the host decides who sees whom (the same room, the same street).</param>
    public RelationshipAiOpinions(string id, RelationshipComponent relationships, string aiComponent, AiSet aiSet, string kind, Func<IReadOnlyList<(string Agent, string Other)>> perceived)
    {
        ArgumentNullException.ThrowIfNull(aiSet);
        Id = id;
        ArgumentNullException.ThrowIfNull(relationships);
        _relationships = relationships;
        _aiComponent = aiComponent;
        _kind = kind;
        ArgumentNullException.ThrowIfNull(perceived);
        _perceived = perceived;
        Check(aiSet, relationships.Runtime.Set, kind);
    }

    public string Id { get; }

    /// <summary>How many percepts have been posted.</summary>
    public long Posted => _posted;

    /// <summary>The attribute id of an axis opinion in the percept.</summary>
    public static string OpinionAttribute(AxisDef axis) => "opinion_" + Name(axis.Id);

    /// <summary>The attribute id of a ladder state in the percept.</summary>
    public static string StateAttribute(LadderDef ladder) => "state_" + Name(ladder.Id);

    private static string Name(string id) => id.Replace('.', '_').Replace('-', '_');

    private static IReadOnlyList<(string Id, FactKind Kind, long Low, long High)> Expected(RelationshipSet set)
    {
        var expected = new List<(string Id, FactKind Kind, long Low, long High)>();
        foreach (var axis in set.Axes) expected.Add((OpinionAttribute(axis), FactKind.Integer, axis.Min, axis.Max));
        foreach (var ladder in set.Ladders) expected.Add((StateAttribute(ladder), FactKind.String, 0, 0));
        expected.Add(("known", FactKind.Boolean, 0, 0));
        expected.Add(("standing", FactKind.Integer, 0, RelationshipRuntime.StandingMax));
        expected.Add(("same_community", FactKind.Boolean, 0, 0));
        expected.Add(("community_size", FactKind.Integer, 1, set.Limits.MaxActors));
        return expected;
    }

    private static void Check(AiSet ai, RelationshipSet set, string kind)
    {
        if (!ai.TryPercept(kind, out var def)) throw new EngineRefusedException(RelationshipAiBridge.BadKind, "the AI document has no percept kind '" + kind + "'");
        if (def.Owned) throw new EngineRefusedException(RelationshipAiBridge.BadKind, "percept kind '" + kind + "' is owned, but what an agent knows of another actor belongs to no third party");
        var expected = Expected(set);
        if (def.Attrs.Count != expected.Count) throw new EngineRefusedException(RelationshipAiBridge.BadKind, "percept kind '" + kind + "' must declare exactly " + string.Join(", ", expected.Select(e => e.Id)));
        foreach (var (attr, type, low, high) in expected)
        {
            if (!def.TryAttr(attr, out var declared) || declared.Kind != type) throw new EngineRefusedException(RelationshipAiBridge.BadKind, "percept kind '" + kind + "' must declare '" + attr + "' as " + type);
            if (type == FactKind.Integer && (declared.Minimum > low || declared.Maximum < high)) throw new EngineRefusedException(RelationshipAiBridge.BadKind, "attribute '" + attr + "' of '" + kind + "' must allow " + low + " to " + high);
        }
    }

    /// <summary>Tells the agents again at the next Update, because who perceives whom has changed or the AI component was reset.</summary>
    public void Notify() => _told.Clear();

    public void Init(in ComponentContext context) => _events = context.Events;

    public void Update(in UpdateContext context)
    {
        if (_events is null) throw new InvalidOperationException("Update before Init");
        var events = _events;
        var runtime = _relationships.Runtime;
        foreach (var (agent, other) in _perceived())
        {
            // Not an actor (any more), or the agent itself: there is nothing to know about.
            if (agent == other || !runtime.Actors.TryGetValue(agent, out var holder) || !runtime.Actors.TryGetValue(other, out var seen)) continue;
            var attrs = AiValues.Encode(Perceive(runtime, holder, seen));
            if (_told.TryGetValue((agent, other), out var last) && last == attrs) continue;
            _told[(agent, other)] = attrs;
            events.Emit(new AiPerceptPosted(_aiComponent, Id + ":" + context.Tick + ":" + agent + ":" + other, agent, _kind, other, "", attrs));
            _posted++;
        }
    }

    /// <summary>What the holder would perceive of the other: its own opinions and what a village knows about everyone, and nothing the other thinks.</summary>
    private static List<KeyValuePair<string, FactValue>> Perceive(RelationshipRuntime runtime, ActorState holder, ActorState seen)
    {
        var set = runtime.Set;
        var edge = holder.Edges.GetValueOrDefault(seen.Id);
        var pairs = new List<KeyValuePair<string, FactValue>>();
        foreach (var axis in set.Axes) pairs.Add(new(OpinionAttribute(axis), FactValue.From(runtime.Opinion(holder.Id, seen.Id, axis.Index))));
        foreach (var ladder in set.Ladders) pairs.Add(new(StateAttribute(ladder), FactValue.From(runtime.LadderStateId(holder.Id, seen.Id, ladder.Index))));
        pairs.Add(new("known", FactValue.From(edge is not null)));
        pairs.Add(new("standing", FactValue.From(seen.Standing)));
        pairs.Add(new("same_community", FactValue.From(holder.Community == seen.Community)));
        pairs.Add(new("community_size", FactValue.From((long)runtime.CommunitySize(seen))));
        return pairs;
    }
}
