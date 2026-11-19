namespace GameRuntime;

/// <summary>What a request asks for. One list, so a component that serves requests and the registry that refuses the unserved ones cannot disagree about which requests exist.</summary>
internal enum CalendarRequestKind { Scale, Pause, Skip, ActorAdd, ActorRemove, Interrupt, InterruptClear, Where }

/// <summary>One queued request. <see cref="A"/> and <see cref="B"/> are the texts the kind takes (actor, schedule or interrupt), <see cref="N"/>, <see cref="M"/> and <see cref="K"/> its numbers; the rest are empty.</summary>
internal readonly record struct CalendarPending(CalendarRequestKind Kind, string RequestId, string A = "", string B = "", long N = 0, long M = 0, long K = 0);

/// <summary>The one place every calendar request event is connected: the handler gets the request's channel and the request as a <see cref="CalendarPending"/>.</summary>
internal static class CalendarRequestPort
{
    public static void Connect(IEventPort events, Action<string, CalendarPending> handler)
    {
        events.Connect<CalendarScaleRequested>(r => handler(r.Channel, new CalendarPending(CalendarRequestKind.Scale, r.RequestId, N: r.Numerator, M: r.Denominator)));
        events.Connect<CalendarPauseRequested>(r => handler(r.Channel, new CalendarPending(CalendarRequestKind.Pause, r.RequestId, N: r.Paused ? 1 : 0)));
        events.Connect<CalendarSkipRequested>(r => handler(r.Channel, new CalendarPending(CalendarRequestKind.Skip, r.RequestId, N: r.Days, M: r.Hour, K: r.Minute)));
        events.Connect<CalendarActorAddRequested>(r => handler(r.Channel, new CalendarPending(CalendarRequestKind.ActorAdd, r.RequestId, r.Actor, r.Schedule)));
        events.Connect<CalendarActorRemoveRequested>(r => handler(r.Channel, new CalendarPending(CalendarRequestKind.ActorRemove, r.RequestId, r.Actor)));
        events.Connect<CalendarInterruptRequested>(r => handler(r.Channel, new CalendarPending(CalendarRequestKind.Interrupt, r.RequestId, r.Actor, r.Kind, N: r.DurationTicks)));
        events.Connect<CalendarInterruptClearRequested>(r => handler(r.Channel, new CalendarPending(CalendarRequestKind.InterruptClear, r.RequestId, r.Actor)));
        events.Connect<CalendarWhereRequested>(r => handler(r.Channel, new CalendarPending(CalendarRequestKind.Where, r.RequestId, r.Actor)));
    }
}
