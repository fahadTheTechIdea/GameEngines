using System.Text.Json;

namespace GameRuntime.GodotHost;

/// <summary>
/// IEventPort for Godot. Components emit during their Update; the component node drains the queue when that Update
/// returns, delivering to connected handlers in publication order and raising one Godot signal per event. The same
/// delivery order as the reference host, using Godot's signal for everything that leaves the layer.
/// </summary>
public sealed class GodotEventPort : IEventPort
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly Queue<(Type Type, object Value)> _queue = new();
    private const int Budget = 10_000;

    public void Emit<T>(in T evt) where T : struct => _queue.Enqueue((typeof(T), evt));

    public void Connect<T>(Action<T> handler) where T : struct
    {
        if (!_handlers.TryGetValue(typeof(T), out var list)) _handlers[typeof(T)] = list = new List<Delegate>();
        list.Add(handler);
    }

    /// <summary>Delivers queued events in publication order; each is also handed to <paramref name="signal"/> (kind, flat JSON).</summary>
    public void Drain(Action<string, string> signal)
    {
        var delivered = 0;
        while (_queue.Count != 0)
        {
            if (++delivered > Budget) { _queue.Clear(); throw new InvalidOperationException("event budget exceeded in one drain: " + Budget); }
            var (type, value) = _queue.Dequeue();
            if (_handlers.TryGetValue(type, out var list))
                foreach (var handler in list.ToArray()) handler.DynamicInvoke(value);
            signal(type.Name, JsonSerializer.Serialize(value, type));
        }
    }
}
