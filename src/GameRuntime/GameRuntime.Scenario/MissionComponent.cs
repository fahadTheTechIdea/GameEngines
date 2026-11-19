using System.Collections.Immutable;
using System.Text.Json;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// ScenarioEngine as a game component. Handlers only record what arrived (observations, commands); the component's
/// Update hands the tick and the recorded input to the engine and turns the committed result back into events.
/// It never reads another component to find out whether something changed.
/// </summary>
public sealed class MissionComponent : IGameComponent
{
    private readonly IScenarioSimulation _simulation;
    private readonly List<ObservationPosted> _observations = new();
    private readonly HashSet<string> _awaitingResolution = new(StringComparer.Ordinal);
    private IEventPort? _events;
    private bool _endingReported;
    private readonly IReadOnlySet<string>? _observedKinds;

    /// <param name="observedKinds">The observation kinds this mission listens to; others are not its concern and are not forwarded. Null forwards all.</param>
    public MissionComponent(IScenarioSimulation simulation, IReadOnlySet<string>? observedKinds = null, string id = "mission")
    {
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        _observedKinds = observedKinds;
        Id = id;
    }

    public string Id { get; }

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<ObservationPosted>(posted => { if (_observedKinds is null || _observedKinds.Contains(posted.Kind)) _observations.Add(posted); });
        context.Events.Connect<MissionCommandRequested>(Admit);
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        var before = _simulation.Snapshot;
        if (context.Tick != before.Tick + 1)
            throw new InvalidOperationException($"mission engine is at tick {before.Tick} but the session is at {context.Tick}");

        var sequence = before.LastObservationSequence;
        var frame = ImmutableArray.CreateBuilder<WorldObservation>(_observations.Count);
        foreach (var posted in _observations)
        {
            using var document = JsonDocument.Parse(string.IsNullOrEmpty(posted.PayloadJson) ? "{}" : posted.PayloadJson);
            frame.Add(new WorldObservation(++sequence, posted.Kind, null, document.RootElement.Clone()));
        }
        _observations.Clear();

        var result = _simulation.Update(new TickInput(context.Tick, new WorldFrame(before.WorldGeneration, context.Tick, frame.ToImmutable())));
        if (!result.Committed) throw new InvalidOperationException("mission engine refused tick " + context.Tick + ": " + result.Code);

        foreach (var evt in result.Events)
            events.Emit(new MissionEvent(evt.Kind, evt.Payload.GetRawText(), evt.Tick, evt.Sequence));
        foreach (var request in result.WorldRequests)
            events.Emit(new EffectRequested(request.RequestId, request.Kind, request.Payload.GetRawText(), context.Tick));

        foreach (var commandId in _awaitingResolution.ToArray())
        {
            if (!result.Snapshot.Receipts.TryGetValue(commandId, out var receipt) || receipt.Status == CommandStatus.Queued) continue;
            _awaitingResolution.Remove(commandId);
            events.Emit(new CommandResolved(commandId, Id, receipt.Status == CommandStatus.Applied, receipt.Code));
        }

        if (!_endingReported && result.Snapshot.EndingId is { } ending)
        {
            _endingReported = true;
            events.Emit(new SessionEnded(context.Tick, ending));
        }
    }

    private void Admit(MissionCommandRequested request)
    {
        var events = _events ?? throw new InvalidOperationException("command before Init");
        using var document = JsonDocument.Parse(string.IsNullOrEmpty(request.PayloadJson) ? "{}" : request.PayloadJson);
        var snapshot = _simulation.Snapshot;
        var envelope = new CommandEnvelope(request.CommandId, request.ControllerId, snapshot.Tick + 1,
            snapshot.WorldGeneration, request.Operation, document.RootElement.Clone());
        var admission = _simulation.Enqueue(envelope);
        if (admission.Accepted) _awaitingResolution.Add(request.CommandId);
        else events.Emit(new CommandResolved(request.CommandId, Id, false, admission.Code));
    }
}
