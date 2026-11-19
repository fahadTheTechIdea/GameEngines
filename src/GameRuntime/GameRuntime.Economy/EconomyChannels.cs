namespace GameRuntime;

/// <summary>
/// The economy engine channels: a <see cref="ChannelRegistry"/> that declares the economy events (<see cref="EconomyEventCatalog"/>), connects the economy request events
/// (<see cref="EconomyRequestPort"/>) and refuses an unserved request as an <see cref="EconomyRefused"/> carrying <see cref="UnknownChannel"/>. Nothing else is here: the registry behaviour is
/// described on <see cref="ChannelRegistry"/>, the one registry every request-serving engine shares. A refusal waiting for the registry Update blocks <see cref="EconomyCheckpoint.Capture"/>.
/// </summary>
public sealed class EconomyChannels : ChannelRegistry
{
    /// <summary>The refusal for a request on a channel no component serves.</summary>
    public const string UnknownChannel = "ECON_CHANNEL_UNKNOWN";

    /// <summary>The refusal at Init for a second component on a channel that is already served.</summary>
    public const string DuplicateChannel = "ECON_CHANNEL_DUPLICATE";

    /// <summary>The component id the registry has in its session.</summary>
    public const string ComponentId = "economy-channels";

    private static readonly ChannelProtocol Protocol = new(
        3, ComponentId, UnknownChannel, DuplicateChannel,
        EconomyEventCatalog.Declare,
        (events, handler) => EconomyRequestPort.Connect(events, (channel, request) => handler(channel, request.RequestId)),
        (events, requestId, tick, channel) => events.Emit(new EconomyRefused(ComponentId, channel, requestId, tick, UnknownChannel)));

    public EconomyChannels() : base(Protocol) { }
}
