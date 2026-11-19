namespace GameRuntime.Headless;

/// <summary>
/// Puts the sim's in-world events into the mission layer's vocabulary. A layer may only register observation kinds under
/// its own id, so "sim.event.cash-changed" arrives at the mission as "gf.tycoon.cash-changed". It reacts to events and
/// holds no state of its own.
/// </summary>
public sealed class TycoonObservations : IGameComponent
{
    private static readonly IReadOnlyDictionary<string, string> Mapping = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["sim.event.cash-changed"] = TycoonMissionLayer.CashChanged,
        ["sim.event.objective-changed"] = TycoonMissionLayer.ObjectiveChanged,
    };

    private IEventPort? _events;

    public string Id => "tycoon-observations";

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<ObservationPosted>(Translate);
    }

    public void Update(in UpdateContext context) { }

    private void Translate(ObservationPosted posted)
    {
        if (Mapping.TryGetValue(posted.Kind, out var kind)) _events!.Emit(new ObservationPosted(kind, posted.PayloadJson, posted.Tick));
    }
}
