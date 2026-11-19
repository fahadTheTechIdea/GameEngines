namespace GameRuntime;

/// <summary>
/// The calendar engine's channels: a <see cref="ChannelRegistry"/> that declares the calendar events (<see cref="CalendarEventCatalog"/>), connects the calendar request events
/// (<see cref="CalendarRequestPort"/>) and refuses an unserved request as a <see cref="CalendarRefused"/> carrying <see cref="UnknownChannel"/>. Nothing else is here: the registry's behaviour is
/// described on <see cref="ChannelRegistry"/>, the one registry dialogue and calendar share. A refusal waiting for the registry's Update blocks <see cref="CalendarCheckpoint.Capture"/>.
/// </summary>
public sealed class CalendarChannels : ChannelRegistry
{
    /// <summary>The refusal for a request on a channel no component serves.</summary>
    public const string UnknownChannel = "CAL_CHANNEL_UNKNOWN";

    /// <summary>The refusal at Init for a second component on a channel that is already served.</summary>
    public const string DuplicateChannel = "CAL_CHANNEL_DUPLICATE";

    /// <summary>The component id the registry has in its session.</summary>
    public const string ComponentId = "calendar-channels";

    private static readonly ChannelProtocol Protocol = new(
        1, ComponentId, UnknownChannel, DuplicateChannel,
        CalendarEventCatalog.Declare,
        (events, handler) => CalendarRequestPort.Connect(events, (channel, request) => handler(channel, request.RequestId)),
        (events, requestId, tick, channel) => events.Emit(new CalendarRefused(ComponentId, channel, requestId, tick, UnknownChannel)));

    public CalendarChannels() : base(Protocol) { }
}
