namespace GameRuntime;

/// <summary>
/// Where events the world publishes inside itself are parked for the host. The world drops an event nobody handles, so
/// the director bridge module handles the declared ones and writes them here; <see cref="SimComponent"/> drains it each tick.
/// </summary>
public sealed class SimEventOutbox
{
    private readonly List<(string Kind, string PayloadJson)> _items = new();
    internal void Add(string kind, string payloadJson) => _items.Add((kind, payloadJson));
    internal IReadOnlyList<(string Kind, string PayloadJson)> Drain()
    {
        var copy = _items.ToArray();
        _items.Clear();
        return copy;
    }
}
