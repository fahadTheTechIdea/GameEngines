using Beep.SimEngine;
using Beep.SimEngine.Hosting;

namespace GameRuntime;

/// <summary>
/// Turns the mission's "sim.*" requests into director commands. The handler issues a one-shot ticket and queues the
/// command with the target seat; the sim applies it on its next tick, so effects always land on the tick after the
/// request. The component's own Update reports how each command was answered.
/// </summary>
public sealed class SimEffectDispatcher : IGameComponent
{
    private readonly SimGame _game;
    private readonly DirectorTickets _tickets;
    private readonly int _seat;
    private readonly Dictionary<int, string> _awaiting = new();
    private readonly List<CommandResult> _results = new();
    private IEventPort? _events;
    private long _cursor;

    public SimEffectDispatcher(SimGame game, DirectorTickets tickets, int seat = 0, string id = "sim-effects")
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _seat = seat;
        Id = id;
    }

    public string Id { get; }

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<EffectRequested>(Request);
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        _results.Clear();
        _game.Seat(_seat).ReadResults(ref _cursor, _results);
        foreach (var result in _results)
        {
            if (!_awaiting.Remove(result.Sequence, out var key)) continue;
            events.Emit(new CommandResolved(key, "sim", result.Accepted, result.Accepted ? "APPLIED" : result.Refusal.Id));
        }
    }

    private void Request(EffectRequested request)
    {
        const string prefix = "sim.";
        if (!request.Kind.StartsWith(prefix, StringComparison.Ordinal)) return;
        var events = _events ?? throw new InvalidOperationException("request before Init");
        var effect = request.Kind.Substring(prefix.Length);
        var key = "effect:" + request.RequestId;
        _tickets.Issue(key, effect, request.PayloadJson);
        var sequence = _game.Seat(_seat).Submit(new DirectorEffectCommand { Key = key, Effect = effect, Payload = request.PayloadJson });
        _awaiting[sequence] = key;
        events.Emit(new CommandResolved(key, "sim", true, "QUEUED"));
    }
}
