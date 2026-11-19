namespace GameRuntime;

/// <summary>
/// The world-events engine channels: a <see cref="ChannelRegistry"/> that declares the world events (<see cref="WorldEventCatalog"/>), connects the world request event (<see cref="WorldRequestPort"/>) and refuses an
/// unserved request as a <see cref="WorldRefused"/> carrying <see cref="UnknownChannel"/>. Nothing else is here: the registry behaviour is described on <see cref="ChannelRegistry"/>, the one registry every
/// request-serving engine shares. A refusal waiting for the registry Update blocks <see cref="WorldCheckpoint.Capture"/>.
/// </summary>
public sealed class WorldChannels : ChannelRegistry
{
    /// <summary>The refusal for a request on a channel no component serves.</summary>
    public const string UnknownChannel = "WE_CHANNEL_UNKNOWN";

    /// <summary>The refusal at Init for a second component on a channel that is already served.</summary>
    public const string DuplicateChannel = "WE_CHANNEL_DUPLICATE";

    /// <summary>The component id the registry has in its session.</summary>
    public const string ComponentId = "world-channels";

    private static readonly ChannelProtocol Protocol = new(
        5, ComponentId, UnknownChannel, DuplicateChannel,
        WorldEventCatalog.Declare,
        (events, handler) => WorldRequestPort.Connect(events, (channel, request) => handler(channel, request.RequestId)),
        (events, requestId, tick, channel) => events.Emit(new WorldRefused(ComponentId, channel, requestId, tick, UnknownChannel)));

    public WorldChannels() : base(Protocol) { }
}
