using Beep.SimEngine;
using Beep.SimEngine.Commands;
using Beep.SimEngine.Hosting;

namespace GameRuntime;

/// <summary>
/// Orders transformations in the sim on behalf of the game. An <see cref="OrderTransformationRequested"/> only queues; in its next Update
/// this component submits the engine's own enqueue command to the sim, and in the Update after the sim has answered it reports the
/// engine's verdict as a <see cref="TransformationOrderResolved"/>. It checks nothing itself: whether the recipe is known, the processor
/// can run it and the stock suffices is decided by the engine, so a refusal always carries the engine's code.
/// </summary>
public sealed class SimOrderComponent(SimGame game, int seat = 0, string id = "sim-orders") : IGameComponent
{
    private readonly List<OrderTransformationRequested> _requested = new();
    private readonly Dictionary<int, string> _awaiting = new();
    private readonly List<CommandResult> _results = new();
    private IEventPort? _events;
    private long _cursor;

    public string Id { get; } = id;

    /// <summary>True when no order waits to be submitted or answered; a checkpoint is only consistent then.</summary>
    public bool IsQuiet => _requested.Count == 0 && _awaiting.Count == 0;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<OrderTransformationRequested>(request => _requested.Add(request));
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        _results.Clear();
        game.Seat(seat).ReadResults(ref _cursor, _results);
        foreach (var result in _results)
            if (_awaiting.Remove(result.Sequence, out var requestId))
                events.Emit(new TransformationOrderResolved(requestId, result.Accepted, result.Accepted ? "APPLIED" : result.Refusal.Id, context.Tick));

        var orders = _requested.ToArray();
        _requested.Clear();
        foreach (var order in orders) Submit(events, context.Tick, order);
    }

    private void Submit(IEventPort events, long tick, OrderTransformationRequested order)
    {
        var processors = game.Seat(seat).Snapshot.Entities.Where(e => e.Def == order.Processor).ToArray();
        if (processors.Length != 1)
        {
            events.Emit(new TransformationOrderResolved(order.RequestId, false, SimRecipeCodes.UnknownProcessor, tick));
            return;
        }
        var sequence = game.Seat(seat).Submit(new EnqueueTransformationCommand
        {
            Transformer = processors[0].Id,
            Capability = new Code(order.Capability),
            Transformation = order.Transformation,
            Count = order.Count,
        });
        _awaiting[sequence] = order.RequestId;
    }
}
