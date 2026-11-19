namespace GameRuntime;

/// <summary>
/// The dialogue engine's channels: a <see cref="ChannelRegistry"/> that declares the dialogue events (<see cref="DialogueEventCatalog"/>), connects the dialogue request events
/// (<see cref="DialogueRequestPort"/>) and refuses an unserved request as a <see cref="DialogueRefused"/> carrying <see cref="UnknownChannel"/>. Nothing else is here: the registry's behaviour, and the
/// reasons a game cannot forget to install it, are described on <see cref="ChannelRegistry"/>. A refusal waiting for the registry's Update blocks <see cref="DialogueCheckpoint.Capture"/>.
/// </summary>
public sealed class DialogueChannels : ChannelRegistry
{
    /// <summary>The refusal for a request on a channel no component serves.</summary>
    public const string UnknownChannel = "DLG_CHANNEL_UNKNOWN";

    /// <summary>The refusal at Init for a second component on a channel that is already served.</summary>
    public const string DuplicateChannel = "DLG_CHANNEL_DUPLICATE";

    /// <summary>The component id the registry has in its session.</summary>
    public const string ComponentId = "dialogue-channels";

    private static readonly ChannelProtocol Protocol = new(
        0, ComponentId, UnknownChannel, DuplicateChannel,
        DialogueEventCatalog.Declare,
        (events, handler) => DialogueRequestPort.Connect(events, (channel, request) => handler(channel, request.RequestId)),
        (events, requestId, tick, channel) => events.Emit(new DialogueRefused(requestId, tick, UnknownChannel, channel)));

    /// <summary>A registry for one session. It does nothing until <see cref="ChannelRegistry.Install"/> adds it; a component built with a registry that was never installed on its session fails its first Update.</summary>
    public DialogueChannels() : base(Protocol) { }
}
