namespace GameRuntime;

/// <summary>
/// The diplomacy engine channels: a <see cref="ChannelRegistry"/> that declares the diplomacy events (<see cref="DiplomacyEventCatalog"/>), connects the diplomacy request event (<see cref="DiplomacyRequestPort"/>) and refuses
/// an unserved request as a <see cref="DiplomacyRefused"/> carrying <see cref="UnknownChannel"/>. Nothing else is here: the registry behaviour is described on <see cref="ChannelRegistry"/>, the one registry every
/// request-serving engine shares. A refusal waiting for the registry Update blocks <see cref="DiplomacyCheckpoint.Capture"/>.
/// </summary>
public sealed class DiplomacyChannels : ChannelRegistry
{
    /// <summary>The refusal for a request on a channel no component serves.</summary>
    public const string UnknownChannel = "DIP_CHANNEL_UNKNOWN";

    /// <summary>The refusal at Init for a second component on a channel that is already served.</summary>
    public const string DuplicateChannel = "DIP_CHANNEL_DUPLICATE";

    /// <summary>The component id the registry has in its session.</summary>
    public const string ComponentId = "diplomacy-channels";

    private static readonly ChannelProtocol Protocol = new(
        6, ComponentId, UnknownChannel, DuplicateChannel,
        DiplomacyEventCatalog.Declare,
        (events, handler) => DiplomacyRequestPort.Connect(events, (channel, request) => handler(channel, request.RequestId)),
        (events, requestId, tick, channel) => events.Emit(new DiplomacyRefused(ComponentId, channel, requestId, tick, UnknownChannel)));

    public DiplomacyChannels() : base(Protocol) { }
}
