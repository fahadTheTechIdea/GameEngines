using AIEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;

namespace GameRuntime;

/// <summary>
/// An agent that knows the weather and the danger around it, as a percept it perceives: knowledge, not truth. The agent does not read the world; it knows the weather, how far it can see and move, how dangerous the
/// hazards going in its region are and how many are ill only when told, and acts on what it was last told. The bridge posts a percept to the agent when what it would perceive changed since it was last told, and
/// once at the start. The agent decides what to do (seek shelter, stay in, flee); the world's own effects are not replaced by it.
/// </summary>
/// <remarks>
/// The AI document must declare the percept kind with exactly these attributes: <c>weather</c> (string), <c>season</c> (string), <c>visibility</c> and <c>mobility</c> (int covering 0 to 1000), <c>danger</c> (int covering
/// 0 to 1000: the strongest hazard going in the region) and <c>sickness</c> (int covering 0 to 1000: the share of the region exposed or infectious with the disease that has most). The constructor checks that against both
/// documents and refuses otherwise. Add the bridge to the session after the world component and before the AI component.
/// </remarks>
public sealed class WorldAiPercepts : IGameComponent
{
    public const string BadKind = "WE_AI_PERCEPT_KIND";

    private readonly WorldComponent _world;
    private readonly string _aiComponent;
    private readonly string _kind;
    private readonly string _perceptId;
    private readonly string _agent;
    private readonly int _region;
    private readonly int _visibility;
    private readonly int _mobility;
    private string _told = "";
    private IEventPort? _events;
    private long _posted;

    /// <param name="region">The region the agent is in (an id of the world document).</param>
    /// <param name="visibilityModifier">The world's modifier that says how far one sees (an id of the world document).</param>
    /// <param name="mobilityModifier">The world's modifier that says how freely one moves.</param>
    public WorldAiPercepts(string id, WorldComponent world, string region, string visibilityModifier, string mobilityModifier, string aiComponent, AiSet aiSet, string kind, string perceptId, string agent)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(aiSet);
        Id = id;
        _world = world;
        if (!world.Runtime.Set.RegionById.TryGetValue(region, out var regionDef)) throw new EngineRefusedException(BadKind, "the world document has no region '" + region + "'");
        if (!world.Runtime.Set.ModifierById.TryGetValue(visibilityModifier, out var visibility)) throw new EngineRefusedException(BadKind, "the world document has no modifier '" + visibilityModifier + "'");
        if (!world.Runtime.Set.ModifierById.TryGetValue(mobilityModifier, out var mobility)) throw new EngineRefusedException(BadKind, "the world document has no modifier '" + mobilityModifier + "'");
        _region = regionDef.Index;
        _visibility = visibility.Index;
        _mobility = mobility.Index;
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
        if (def.Owned) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' is owned, but an agent's knowledge of the weather belongs to no one else");
        var expected = new List<(string Id, FactKind Kind, long Low, long High)> { ("weather", FactKind.String, 0, 0), ("season", FactKind.String, 0, 0), ("visibility", FactKind.Integer, 0, 1000), ("mobility", FactKind.Integer, 0, 1000), ("danger", FactKind.Integer, 0, 1000), ("sickness", FactKind.Integer, 0, 1000) };
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
        var runtime = _world.Runtime;
        if (runtime.Season < 0) return;   // the world has not lived a step: there is nothing to perceive yet
        var set = runtime.Set;
        var danger = runtime.ActiveHazards.Where(h => h.Region == _region).Select(runtime.Intensity).DefaultIfEmpty(0).Max();
        var state = runtime.Regions[_region];
        var sickness = 0L;
        foreach (var disease in set.Diseases)
        {
            var living = state.Living(disease.Index);
            if (living > 0) sickness = Math.Max(sickness, (state.Exposed[disease.Index] + state.Infectious[disease.Index]) * 1000 / living);
        }
        var attrs = AiValues.Encode(new List<KeyValuePair<string, FactValue>>
        {
            new("weather", FactValue.From(set.Weathers[state.Weather].Id)), new("season", FactValue.From(set.Seasons[runtime.Season].Id)),
            new("visibility", FactValue.From((long)runtime.Modifier(_region, _visibility))), new("mobility", FactValue.From((long)runtime.Modifier(_region, _mobility))),
            new("danger", FactValue.From((long)danger)), new("sickness", FactValue.From(sickness)),
        });
        if (attrs == _told) return;
        _told = attrs;
        events.Emit(new AiPerceptPosted(_aiComponent, Id + ":" + context.Tick, _agent, _kind, _perceptId, "", attrs));
        _posted++;
    }
}
