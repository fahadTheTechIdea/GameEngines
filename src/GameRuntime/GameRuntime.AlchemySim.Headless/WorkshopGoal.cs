using System.Text.Json;

namespace GameRuntime.AlchemySim.Headless;

/// <summary>
/// The game's ending: the workshop is won when the sim reports the goal resource produced. It learns that only from the engine's own
/// stock events (forwarded as observations), never from the order that asked for it.
/// </summary>
public sealed class WorkshopGoal(string resource, string id = "goal") : IGameComponent
{
    public const string EndingId = "produced";

    private IEventPort? _events;
    private bool _reached;
    private bool _ended;

    public string Id { get; } = id;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<ObservationPosted>(OnObservation);
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        if (!_reached || _ended) return;
        _ended = true;
        events.Emit(new SessionEnded(context.Tick, EndingId));
    }

    private void OnObservation(ObservationPosted posted)
    {
        if (posted.Kind != "sim.event.stock-changed") return;
        using var document = JsonDocument.Parse(posted.PayloadJson);
        var root = document.RootElement;
        if (root.GetProperty("resource").GetString() == resource && root.GetProperty("kind").GetString() == "Produced") _reached = true;
    }
}
