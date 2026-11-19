using AIEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// The AI engine as one game component. A request event (add or remove an agent, tell an agent something, write a host key) only queues; in its own Update the
/// component applies the requests in the order they arrived, then lets the agents that are due think within a budget of work units, and raises what they
/// decided as intent events. The component reads no clock: the tick of the Update is the only time there is. What an intent means is for the component that owns the concern.
/// </summary>
public sealed partial class AiComponent : IGameComponent
{
    public const string NotQuiet = "AI_NOT_QUIET";

    private enum RequestKind { Add, Remove, Percept, Lose, Key }

    /// <summary>One queued request. The members a kind does not use are empty.</summary>
    private readonly record struct Pending(RequestKind Kind, string RequestId, string Agent, string A, string B, string C, string D);

    private readonly AiRuntime _runtime;
    private readonly long _budget;
    private readonly List<Pending> _pending = new();
    private IEventPort? _events;
    private long _lastTick;
    private long _steps;

    /// <param name="budget">Work units a tick may spend on thinking. It must cover the largest behaviour of the document, or no agent could always be served.</param>
    public AiComponent(AiRuntime runtime, long budget, string id = "ai")
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        if (budget < runtime.Set.MaxCost) throw new EngineRefusedException(AiCodes.Budget, "a budget of " + budget + " is below the largest behaviour bound " + runtime.Set.MaxCost);
        _budget = budget;
        Id = id;
    }

    public string Id { get; }

    /// <summary>The agents and everything they know and remember. The component is the only thing that changes it during a session.</summary>
    public AiRuntime Runtime => _runtime;

    /// <summary>True when no request waits for the next Update. A checkpoint is only consistent then.</summary>
    public bool IsQuiet => _pending.Count == 0;

    public string Fingerprint() => $"ai tick={_lastTick} state={AiSave.StateDigest(_runtime)[..16]} steps={_steps} pending={_pending.Count}";

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        // Every AI component hears every request; each keeps only the ones addressed to it.
        context.Events.Connect<AiAgentAddRequested>(r => Queue(r.Component, new Pending(RequestKind.Add, r.RequestId, r.Agent, r.Behavior, "", "", "")));
        context.Events.Connect<AiAgentRemoveRequested>(r => Queue(r.Component, new Pending(RequestKind.Remove, r.RequestId, r.Agent, "", "", "", "")));
        context.Events.Connect<AiPerceptPosted>(r => Queue(r.Component, new Pending(RequestKind.Percept, r.RequestId, r.Agent, r.Kind, r.Id, r.Owner, r.Attrs)));
        context.Events.Connect<AiPerceptLost>(r => Queue(r.Component, new Pending(RequestKind.Lose, r.RequestId, r.Agent, r.Kind, r.Id, "", "")));
        context.Events.Connect<AiKeyPosted>(r => Queue(r.Component, new Pending(RequestKind.Key, r.RequestId, r.Agent, r.Key, r.Value, "", "")));
    }

    private void Queue(string component, in Pending request)
    {
        if (component == Id) _pending.Add(request);
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        _lastTick = context.Tick;
        if (_pending.Count > 0)
        {
            var requests = _pending.ToArray();
            _pending.Clear();
            foreach (var request in requests) Apply(events, context.Tick, request);
        }
        if (_runtime.Agents.Count == 0) return;
        var report = _runtime.Step(context.Tick, _budget);
        _steps++;
        foreach (var intent in report.Intents) events.Emit(new AiIntentRaised(Id, intent.Tick, intent.Agent, intent.Node, intent.Name, AiValues.Encode(intent.Args)));
        if (report.Thought > 0)
            events.Emit(new AiStepDone(context.Tick, Id, report.Thought, report.Spent, report.Deferred, report.LongestWait, report.Intents.Count));
    }
}