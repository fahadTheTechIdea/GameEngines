using System.Text.Json;
using Beep.SimEngine;
using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;

namespace GameRuntime;

/// <summary>
/// SimEngine as a game component. The runtime owns time: this advances the world exactly one tick per Update with the
/// Update(tick) method and never uses the engine's own clock, pause or speed. What the world published this tick
/// (its notification feed) becomes ObservationPosted events; the component never polls other components.
/// </summary>
public sealed class SimComponent : IGameComponent
{
    private readonly SimGame _game;
    private readonly List<Notification> _feed = new();
    private readonly IReadOnlyList<SimProbe> _probes;
    private readonly SimEventOutbox? _outbox;
    private IEventPort? _events;
    private long _cursor;
    private bool _outcomeReported;

    public SimComponent(SimGame game, IReadOnlyList<SimProbe>? probes = null, SimEventOutbox? outbox = null, string id = "sim")
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _probes = probes ?? Array.Empty<SimProbe>();
        _outbox = outbox;
        Id = id;
    }

    public string Id { get; }

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        if (!_game.Initialized) _game.Initialize();
        _cursor = 0;
        foreach (var probe in _probes) probe.Last = probe.Read(_game);
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        if (context.Tick != _game.Tick + 1)
            throw new InvalidOperationException($"sim is at tick {_game.Tick} but the session is at {context.Tick}");
        if (_game.Outcome.IsNone)
        {
            _game.Update(context.Tick);
            if (_game.Fault != null) throw new InvalidOperationException("sim fault: " + _game.Fault);
        }

        _feed.Clear();
        _game.ReadAllNotifications(ref _cursor, _feed);
        foreach (var note in _feed)
        {
            var payload = JsonSerializer.Serialize(new
            {
                kind = note.Kind.Id,
                player = note.Player.Value,
                subject = note.Subject,
                amountRaw = note.Amount.Raw,
            });
            events.Emit(new ObservationPosted("sim.notification", payload, context.Tick));
        }

        if (_outbox is not null)
            foreach (var (kind, payload) in _outbox.Drain()) events.Emit(new ObservationPosted(kind, payload, context.Tick));

        foreach (var probe in _probes)
        {
            var now = probe.Read(_game);
            if (now == probe.Last) continue;
            events.Emit(new ObservationPosted(probe.Kind, probe.Payload(probe.Last, now), context.Tick));
            probe.Last = now;
        }

        if (!_outcomeReported && !_game.Outcome.IsNone)
        {
            _outcomeReported = true;
            events.Emit(new ObservationPosted("sim.ended", JsonSerializer.Serialize(new { outcome = _game.Outcome.Id }), context.Tick));
        }
    }
}
