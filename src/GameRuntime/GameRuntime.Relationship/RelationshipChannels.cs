namespace GameRuntime;

/// <summary>
/// The relationship engine channels: a <see cref="ChannelRegistry"/> that declares the relationship events (<see cref="RelationshipEventCatalog"/>), connects the relationship request events
/// (<see cref="RelationshipRequestPort"/>) and refuses an unserved request as a <see cref="RelationshipRefused"/> carrying <see cref="UnknownChannel"/>. Nothing else is here: the registry behaviour is
/// described on <see cref="ChannelRegistry"/>, the one registry every request-serving engine shares. A refusal waiting for the registry Update blocks <see cref="RelationshipCheckpoint.Capture"/>.
/// </summary>
public sealed class RelationshipChannels : ChannelRegistry
{
    /// <summary>The refusal for a request on a channel no component serves.</summary>
    public const string UnknownChannel = "REL_CHANNEL_UNKNOWN";

    /// <summary>The refusal at Init for a second component on a channel that is already served.</summary>
    public const string DuplicateChannel = "REL_CHANNEL_DUPLICATE";

    /// <summary>The component id the registry has in its session.</summary>
    public const string ComponentId = "relationship-channels";

    private static readonly ChannelProtocol Protocol = new(
        2, ComponentId, UnknownChannel, DuplicateChannel,
        RelationshipEventCatalog.Declare,
        (events, handler) => RelationshipRequestPort.Connect(events, (channel, request) => handler(channel, request.RequestId)),
        (events, requestId, tick, channel) => events.Emit(new RelationshipRefused(ComponentId, channel, requestId, tick, UnknownChannel)));

    public RelationshipChannels() : base(Protocol) { }
}
