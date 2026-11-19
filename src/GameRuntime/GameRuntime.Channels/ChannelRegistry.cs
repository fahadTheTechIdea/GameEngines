using EngineCommon;

namespace GameRuntime;

/// <summary>
/// Which channels exist for one engine's requests, and the answer to a request for one that does not: the single registry every request-serving component uses. A game cannot forget it: the only way to
/// declare an engine's events on a session is <see cref="Install"/>, which also adds the registry as the session's last component, and the only way to build the engine's component is to hand it a
/// registry. So a request can only exist in a session whose registry is running (a component whose registry is not running fails its first Update), and every request ends with an answer: from the
/// component that serves its channel, or from the registry, in the same tick (it updates after every other component), as the engine's refusal event carrying the unknown-channel code. Two components on
/// one channel would both answer, so the second is refused at Init with the duplicate-channel code. A component whose registry was installed on another session (or never initialised) fails its first
/// Update, because its unserved requests could not be answered.
/// </summary>
/// <remarks>
/// The set of channels is fixed by how the game was assembled, so it is the same on every run and in every resumed session; nothing here is saved because nothing here changes while the game plays. The
/// channels are a sorted set and the refusals are written in arrival order, so the same requests give the same events. A refusal waiting for the registry's Update blocks a checkpoint (a checkpoint
/// asks <see cref="IsQuiet"/>), so no checkpoint is taken between a request and its answer. An engine adds a sealed subclass that names its <see cref="ChannelProtocol"/> and nothing else; the
/// source guard of the runtime projects fails when a second registry appears.
/// </remarks>
public abstract class ChannelRegistry : IGameComponent
{
    private readonly ChannelProtocol _protocol;
    private readonly SortedDictionary<string, string> _served = new(StringComparer.Ordinal);
    private readonly List<(string RequestId, string Channel)> _unserved = new();
    private IEventPort? _events;
    private long _refused;

    protected ChannelRegistry(ChannelProtocol protocol)
    {
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        if (protocol.Rank < 0) throw new ArgumentException("a registry rank is not negative", nameof(protocol));
    }

    public string Id => _protocol.ComponentId;

    /// <summary>Declares the engine's events on the session and adds this registry as its last component. It is the only way to declare them, so a session that can carry a request always has its registry running.</summary>
    public void Install(RuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _protocol.Declare(session);
        session.AddComponent(this, int.MaxValue - _protocol.Rank);
    }

    /// <summary>The channels served, in ordinal order, each with the id of the component that serves it.</summary>
    public IReadOnlyDictionary<string, string> Served => _served;

    /// <summary>True when no refusal is waiting for the next Update.</summary>
    public bool IsQuiet => _unserved.Count == 0;

    /// <summary>How many requests have been refused as unserved.</summary>
    public long Refused => _refused;

    public string Fingerprint() => "channels=" + string.Join(',', _served.Keys) + " refused=" + _refused + " pending=" + _unserved.Count;

    /// <summary>Called by the engine's component at its Init: the channel is now served by that component.</summary>
    public void Serve(string channel, string component)
    {
        if (!_served.TryAdd(channel, component))
            throw new EngineRefusedException(_protocol.DuplicateCode, "channel '" + channel + "' is already served by '" + _served[channel] + "', so '" + component + "' would answer the same requests twice");
    }

    /// <summary>True once this registry has been initialised by the session whose event port is given.</summary>
    public bool RunsOn(IEventPort events) => ReferenceEquals(_events, events);

    public void Init(in ComponentContext context)
    {
        if (_events is not null) throw new InvalidOperationException("the channel registry '" + Id + "' serves one session; this one already runs on another");
        _events = context.Events;
        _protocol.Connect(context.Events, (channel, requestId) =>
        {
            if (!_served.ContainsKey(channel)) _unserved.Add((requestId, channel));
        });
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        if (_unserved.Count == 0) return;
        var waiting = _unserved.ToArray();
        _unserved.Clear();
        foreach (var (requestId, channel) in waiting)
        {
            _protocol.Refuse(events, requestId, context.Tick, channel);
            _refused++;
        }
    }
}
