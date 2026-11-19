using AIEngine;
using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// Tells AI agents how they feel, as a percept they perceive: knowledge, not truth. An agent does not read the relationship state; it knows its own hunger and mood only when something tells it, and what it knows
/// is the percept it was last told (with the tick it was told at, so the AI document can let it go stale). The percept carries the <b>urgency</b> of each need (0 to 1000, from the document's urgency curve: what an
/// agent chooses by) and the mood and its state. Which agents are told is the host's decision (<c>perceivers</c>); the bridge posts a percept to an agent when what it would perceive changed since it was last told,
/// and once at the start. An agent that is not named is never told and acts on whatever it knew before.
/// </summary>
/// <remarks>
/// The AI document must declare the percept kind with exactly these attributes: <c>mood</c> (int covering the mood range), <c>mood_state</c> (string), and <c>urgency_&lt;need&gt;</c> (int covering 0 to 1000) for each need of the
/// relationship document. The constructor checks that against both documents and refuses otherwise. Add the bridge to the session after the relationship component and before the AI component. The agent id is the actor id.
/// </remarks>
public sealed class RelationshipAiBridge : IGameComponent
{
    public const string BadKind = "RELAI_PERCEPT_KIND";

    private readonly RelationshipComponent _relationships;
    private readonly string _aiComponent;
    private readonly string _kind;
    private readonly string _perceptId;
    private readonly Func<IReadOnlyList<string>> _perceivers;
    private readonly Dictionary<string, string> _told = new(StringComparer.Ordinal);
    private IEventPort? _events;
    private long _posted;

    /// <param name="aiComponent">The id of the AI component whose agents are told.</param>
    /// <param name="aiSet">The AI document, to check the percept kind against.</param>
    /// <param name="kind">The percept kind that carries the state of the agent.</param>
    /// <param name="perceptId">The id the percept has in the agent's knowledge.</param>
    /// <param name="perceivers">The agents (actor ids) that can perceive their own state now, in the order they are told.</param>
    public RelationshipAiBridge(string id, RelationshipComponent relationships, string aiComponent, AiSet aiSet, string kind, string perceptId, Func<IReadOnlyList<string>> perceivers)
    {
        ArgumentNullException.ThrowIfNull(aiSet);
        Id = id;
        ArgumentNullException.ThrowIfNull(relationships);
        _relationships = relationships;
        _aiComponent = aiComponent;
        _kind = kind;
        _perceptId = perceptId;
        ArgumentNullException.ThrowIfNull(perceivers);
        _perceivers = perceivers;
        Check(aiSet, relationships.Runtime.Set, kind);
    }

    public string Id { get; }

    /// <summary>How many percepts have been posted.</summary>
    public long Posted => _posted;

    /// <summary>The attribute id of a need's urgency in the percept.</summary>
    public static string UrgencyAttribute(NeedDef need) => "urgency_" + need.Id.Replace('.', '_').Replace('-', '_');

    private static void Check(AiSet ai, RelationshipSet set, string kind)
    {
        if (!ai.TryPercept(kind, out var def)) throw new EngineRefusedException(BadKind, "the AI document has no percept kind '" + kind + "'");
        if (def.Owned) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' is owned, but an agent's feeling about itself belongs to no one else");
        var expected = new List<(string Id, FactKind Kind, long Low, long High)> { ("mood", FactKind.Integer, set.Mood.Min, set.Mood.Max), ("mood_state", FactKind.String, 0, 0) };
        foreach (var need in set.Needs) expected.Add((UrgencyAttribute(need), FactKind.Integer, 0, 1000));
        if (def.Attrs.Count != expected.Count) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' must declare exactly " + string.Join(", ", expected.Select(e => e.Id)));
        foreach (var (attr, type, low, high) in expected)
        {
            if (!def.TryAttr(attr, out var declared) || declared.Kind != type) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' must declare '" + attr + "' as " + type);
            if (type == FactKind.Integer && (declared.Minimum > low || declared.Maximum < high)) throw new EngineRefusedException(BadKind, "attribute '" + attr + "' of '" + kind + "' must allow " + low + " to " + high);
        }
    }

    /// <summary>Tells the perceivers again at the next Update, because who perceives has changed or the AI component was reset.</summary>
    public void Notify() => _told.Clear();

    public void Init(in ComponentContext context) => _events = context.Events;

    public void Update(in UpdateContext context)
    {
        if (_events is null) throw new InvalidOperationException("Update before Init");
        var events = _events;
        var runtime = _relationships.Runtime;
        var set = runtime.Set;
        foreach (var agent in _perceivers())
        {
            if (!runtime.Actors.TryGetValue(agent, out var actor)) continue;   // not an actor (any more): there is nothing for it to feel
            var pairs = new List<KeyValuePair<string, FactValue>>
            {
                new("mood", FactValue.From(runtime.MoodValue(actor))),
                new("mood_state", FactValue.From(set.Mood.States[actor.MoodState].Id)),
            };
            foreach (var need in set.Needs) pairs.Add(new KeyValuePair<string, FactValue>(UrgencyAttribute(need), FactValue.From(runtime.Urgency(actor, need.Index))));
            var attrs = AiValues.Encode(pairs);
            if (_told.TryGetValue(agent, out var last) && last == attrs) continue;
            _told[agent] = attrs;
            events.Emit(new AiPerceptPosted(_aiComponent, Id + ":" + context.Tick + ":" + agent, agent, _kind, _perceptId, "", attrs));
            _posted++;
        }
    }
}
