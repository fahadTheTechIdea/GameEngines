namespace GameRuntime;

/// <summary>
/// The tutorial engine channels: a <see cref="ChannelRegistry"/> that declares the tutorial events (<see cref="TutorialEventCatalog"/>), connects the tutorial request event
/// (<see cref="TutorialRequestPort"/>) and refuses an unserved request as a <see cref="TutorialRefused"/> carrying <see cref="UnknownChannel"/>. Nothing else is here: the registry behaviour is
/// described on <see cref="ChannelRegistry"/>, the one registry every request-serving engine shares. A refusal waiting for the registry Update blocks <see cref="TutorialCheckpoint.Capture"/>.
/// </summary>
public sealed class TutorialChannels : ChannelRegistry
{
    /// <summary>The refusal for a request on a channel no component serves.</summary>
    public const string UnknownChannel = "TUT_CHANNEL_UNKNOWN";

    /// <summary>The refusal at Init for a second component on a channel that is already served.</summary>
    public const string DuplicateChannel = "TUT_CHANNEL_DUPLICATE";

    /// <summary>The component id the registry has in its session.</summary>
    public const string ComponentId = "tutorial-channels";

    private static readonly ChannelProtocol Protocol = new(
        4, ComponentId, UnknownChannel, DuplicateChannel,
        TutorialEventCatalog.Declare,
        (events, handler) => TutorialRequestPort.Connect(events, (channel, request) => handler(channel, request.RequestId)),
        (events, requestId, tick, channel) => events.Emit(new TutorialRefused(ComponentId, channel, requestId, tick, UnknownChannel)));

    public TutorialChannels() : base(Protocol) { }
}
