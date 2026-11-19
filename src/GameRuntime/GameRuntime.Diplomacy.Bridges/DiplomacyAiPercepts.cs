using AIEngine;
using DiplomacyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// An envoy that knows what its realm knows of another, as a percept it perceives: knowledge, not truth. The agent does not read the diplomacy; it knows its realm's regard of the other realm, the other's standing in the
/// world (which is public), whether the realms are at war, how many treaties bind them, how well its realm knows the other's might and the band that might is believed to be in (wide when little is known: the engine's
/// estimate, not the true value), and its own realm's might, only when told, and acts on what it was last told. The bridge posts a percept to the agent when what it would perceive changed since it was last told, and once at
/// the start. The agent decides what to do (propose, threaten, wait); the diplomacy's own rules are not replaced by it.
/// </summary>
/// <remarks>
/// The AI document must declare the percept kind with exactly these attributes: <c>regard</c> and <c>trust</c> (int covering -1000 to 1000), <c>at_war</c> (bool), <c>treaties</c> (int covering 0 to 1000), <c>intel</c>
/// (string), <c>power_low</c>, <c>power_high</c> and <c>own_power</c> (int covering 0 to 1,000,000,000). The constructor checks that against both documents and refuses otherwise. Add the bridge to the session after the
/// diplomacy component and before the AI component.
/// </remarks>
public sealed class DiplomacyAiPercepts : IGameComponent
{
    public const string BadKind = "DIP_AI_PERCEPT_KIND";

    private readonly DiplomacyComponent _diplomacy;
    private readonly string _aiComponent;
    private readonly string _kind;
    private readonly string _perceptId;
    private readonly string _agent;
    private readonly string _realm;
    private readonly string _other;
    private string _told = "";
    private IEventPort? _events;
    private long _posted;

    public DiplomacyAiPercepts(string id, DiplomacyComponent diplomacy, string realm, string other, string aiComponent, AiSet aiSet, string kind, string perceptId, string agent)
    {
        ArgumentNullException.ThrowIfNull(diplomacy);
        ArgumentNullException.ThrowIfNull(aiSet);
        Id = id;
        _diplomacy = diplomacy;
        if (!diplomacy.Runtime.Set.RealmById.ContainsKey(realm)) throw new EngineRefusedException(BadKind, "the diplomacy document has no realm '" + realm + "'");
        if (!diplomacy.Runtime.Set.RealmById.ContainsKey(other)) throw new EngineRefusedException(BadKind, "the diplomacy document has no realm '" + other + "'");
        if (realm == other) throw new EngineRefusedException(BadKind, "an envoy perceives another realm than its own");
        _realm = realm;
        _other = other;
        _aiComponent = aiComponent;
        _kind = kind;
        _perceptId = perceptId;
        _agent = agent;
        Check(aiSet, kind);
    }

    public string Id { get; }

    /// <summary>How many percepts have been posted.</summary>
    public long Posted => _posted;

    private static void Check(AiSet ai, string kind)
    {
        if (!ai.TryPercept(kind, out var def)) throw new EngineRefusedException(BadKind, "the AI document has no percept kind '" + kind + "'");
        if (def.Owned) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' is owned, but an envoy's knowledge of a realm belongs to no one else");
        var expected = new List<(string Id, FactKind Kind, long Low, long High)>
        {
            ("regard", FactKind.Integer, -1000, 1000), ("trust", FactKind.Integer, -1000, 1000), ("at_war", FactKind.Boolean, 0, 0), ("treaties", FactKind.Integer, 0, 1000), ("intel", FactKind.String, 0, 0),
            ("power_low", FactKind.Integer, 0, 1_000_000_000), ("power_high", FactKind.Integer, 0, 1_000_000_000), ("own_power", FactKind.Integer, 0, 1_000_000_000),
        };
        if (def.Attrs.Count != expected.Count) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' must declare exactly " + string.Join(", ", expected.Select(e => e.Id)));
        foreach (var (attr, type, low, high) in expected)
        {
            if (!def.TryAttr(attr, out var declared) || declared.Kind != type) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' must declare '" + attr + "' as " + type);
            if (type == FactKind.Integer && (declared.Minimum > low || declared.Maximum < high)) throw new EngineRefusedException(BadKind, "attribute '" + attr + "' of '" + kind + "' must allow " + low + " to " + high);
        }
    }

    /// <summary>Tells the agent again at the next Update, because the AI component was reset.</summary>
    public void Notify() => _told = "";

    public void Init(in ComponentContext context) => _events = context.Events;

    public void Update(in UpdateContext context)
    {
        var events = _events;
        if (events is null) throw new InvalidOperationException("Update before Init");
        var runtime = _diplomacy.Runtime;
        if (runtime.Season < 0) return;   // the diplomacy has not lived a step: there is nothing to perceive yet
        var set = runtime.Set;
        var a = set.RealmById[_realm].Index;
        var b = set.RealmById[_other].Index;
        var estimate = runtime.Estimate(_realm, _other);
        var attrs = AiValues.Encode(new List<KeyValuePair<string, FactValue>>
        {
            new("regard", FactValue.From((long)runtime.RegardOf(a, b))), new("trust", FactValue.From(runtime.Realms[b].Trust / 1000)), new("at_war", FactValue.From(runtime.AtWar(a, b))),
            new("treaties", FactValue.From((long)runtime.Treaties.Count(t => (t.First == a && t.Second == b) || (t.First == b && t.Second == a)))), new("intel", FactValue.From(EnumNames.Name(estimate.Level))),
            new("power_low", FactValue.From(Math.Min(estimate.Low, DipFacts.Saturate))), new("power_high", FactValue.From(Math.Min(estimate.High, DipFacts.Saturate))), new("own_power", FactValue.From(Math.Min(runtime.Realms[a].Might, DipFacts.Saturate))),
        });
        if (attrs == _told) return;
        _told = attrs;
        events.Emit(new AiPerceptPosted(_aiComponent, Id + ":" + context.Tick, _agent, _kind, _perceptId, "", attrs));
        _posted++;
    }
}
