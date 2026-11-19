using AIEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using TutorialEngine;

namespace GameRuntime;

/// <summary>
/// A guide agent that knows where the player is, as a percept it perceives: knowledge, not truth. The agent does not read the tutorial; it knows the step being taught, how strong the hint is, how far the player is
/// and what phase the tutorial is in only when told, and acts on what it was last told. The bridge posts a percept to the guide when what it would perceive changed since it was last told, and once at the start.
/// The agent decides what the guide does (stays quiet, points, encourages, congratulates); the tutorial's own hints are the document's and are not replaced by it.
/// </summary>
/// <remarks>
/// The AI document must declare the percept kind with exactly these attributes: <c>step</c> (string: the step being taught, or empty), <c>hint_level</c> (int covering 0 to the most hints a step has),
/// <c>progress</c> (int covering 0 to 1000) and <c>phase</c> (string). The constructor checks that against both documents and refuses otherwise. Add the bridge to the session after the tutorial component and before
/// the AI component. The agent id is the guide's.
/// </remarks>
public sealed class TutorialAiBridge : IGameComponent
{
    public const string BadKind = "TUTAI_PERCEPT_KIND";

    private readonly TutorialComponent _tutorial;
    private readonly string _aiComponent;
    private readonly string _kind;
    private readonly string _perceptId;
    private readonly string _guide;
    private string _told = "";
    private IEventPort? _events;
    private long _posted;

    /// <param name="aiComponent">The id of the AI component whose agent is told.</param>
    /// <param name="aiSet">The AI document, to check the percept kind against.</param>
    /// <param name="kind">The percept kind that carries the state of the tutorial.</param>
    /// <param name="perceptId">The id the percept has in the agent's knowledge.</param>
    /// <param name="guide">The agent that guides.</param>
    public TutorialAiBridge(string id, TutorialComponent tutorial, string aiComponent, AiSet aiSet, string kind, string perceptId, string guide)
    {
        ArgumentNullException.ThrowIfNull(aiSet);
        Id = id;
        ArgumentNullException.ThrowIfNull(tutorial);
        _tutorial = tutorial;
        _aiComponent = aiComponent;
        _kind = kind;
        _perceptId = perceptId;
        _guide = guide;
        Check(aiSet, tutorial.Runtime.Set, kind);
    }

    public string Id { get; }

    /// <summary>How many percepts have been posted.</summary>
    public long Posted => _posted;

    private static void Check(AiSet ai, TutorialSet set, string kind)
    {
        if (!ai.TryPercept(kind, out var def)) throw new EngineRefusedException(BadKind, "the AI document has no percept kind '" + kind + "'");
        if (def.Owned) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' is owned, but a guide's knowledge of the tutorial belongs to no one else");
        var hints = set.Steps.Max(s => s.Hints.Count);
        var expected = new List<(string Id, FactKind Kind, long Low, long High)> { ("step", FactKind.String, 0, 0), ("hint_level", FactKind.Integer, 0, hints), ("progress", FactKind.Integer, 0, 1000), ("phase", FactKind.String, 0, 0) };
        if (def.Attrs.Count != expected.Count) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' must declare exactly " + string.Join(", ", expected.Select(e => e.Id)));
        foreach (var (attr, type, low, high) in expected)
        {
            if (!def.TryAttr(attr, out var declared) || declared.Kind != type) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' must declare '" + attr + "' as " + type);
            if (type == FactKind.Integer && (declared.Minimum > low || declared.Maximum < high)) throw new EngineRefusedException(BadKind, "attribute '" + attr + "' of '" + kind + "' must allow " + low + " to " + high);
        }
    }

    /// <summary>Tells the guide again at the next Update, because the AI component was reset.</summary>
    public void Notify() => _told = "";

    public void Init(in ComponentContext context) => _events = context.Events;

    public void Update(in UpdateContext context)
    {
        var events = _events;
        if (events is null) throw new InvalidOperationException("Update before Init");
        var runtime = _tutorial.Runtime;
        var active = runtime.ActiveSteps().ToList();
        var step = active.Count == 0 ? "" : runtime.Set.Steps[active[0]].Id;
        var level = active.Count == 0 ? 0 : runtime.Steps[active[0]].Hint;
        var attrs = AiValues.Encode(new List<KeyValuePair<string, FactValue>>
        {
            new("step", FactValue.From(step)), new("hint_level", FactValue.From(level)), new("progress", FactValue.From(runtime.ProgressPermille())), new("phase", FactValue.From(TutorialFacts.PhaseName(runtime.Phase))),
        });
        if (attrs == _told) return;
        _told = attrs;
        events.Emit(new AiPerceptPosted(_aiComponent, Id + ":" + context.Tick, _guide, _kind, _perceptId, "", attrs));
        _posted++;
    }
}
