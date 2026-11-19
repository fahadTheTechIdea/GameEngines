namespace GameRuntime;

/// <summary>Every event the calendar component raised on a session, as text and as one hash (see <see cref="EventTranscript"/>): a run, a resumed run and a replay can be compared by one value.</summary>
public sealed class CalendarTranscript : EventTranscript
{
    public CalendarTranscript(RuntimeSession session) : base(session)
    {
        Listen<CalendarApplied>();
        Listen<CalendarRefused>();
        Listen<CalendarTimeAdvanced>();
        Listen<CalendarDayChanged>();
        Listen<CalendarDayPartChanged>();
        Listen<CalendarSeasonChanged>();
        Listen<CalendarHolidayStarted>();
        Listen<CalendarScheduleBlockChanged>();
        Listen<CalendarWhereAnswered>();
    }
}
