namespace GameRuntime;

/// <summary>The calendar component's events, declared once, by <see cref="CalendarChannels.Install"/> only, before the session's Init. A version is bumped when an event's fields change.</summary>
internal static class CalendarEventCatalog
{
    internal static void Declare(RuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DeclareEvent<CalendarScaleRequested>("calendar.scale-requested", 1);
        session.DeclareEvent<CalendarPauseRequested>("calendar.pause-requested", 1);
        session.DeclareEvent<CalendarSkipRequested>("calendar.skip-requested", 1);
        session.DeclareEvent<CalendarActorAddRequested>("calendar.actor-add-requested", 1);
        session.DeclareEvent<CalendarActorRemoveRequested>("calendar.actor-remove-requested", 1);
        session.DeclareEvent<CalendarInterruptRequested>("calendar.interrupt-requested", 1);
        session.DeclareEvent<CalendarInterruptClearRequested>("calendar.interrupt-clear-requested", 1);
        session.DeclareEvent<CalendarWhereRequested>("calendar.where-requested", 1);
        session.DeclareEvent<CalendarApplied>("calendar.applied", 1);
        session.DeclareEvent<CalendarRefused>("calendar.refused", 1);
        session.DeclareEvent<CalendarTimeAdvanced>("calendar.time-advanced", 1);
        session.DeclareEvent<CalendarDayChanged>("calendar.day-changed", 1);
        session.DeclareEvent<CalendarDayPartChanged>("calendar.day-part-changed", 1);
        session.DeclareEvent<CalendarSeasonChanged>("calendar.season-changed", 1);
        session.DeclareEvent<CalendarHolidayStarted>("calendar.holiday-started", 1);
        session.DeclareEvent<CalendarScheduleBlockChanged>("calendar.schedule-block-changed", 1);
        session.DeclareEvent<CalendarWhereAnswered>("calendar.where-answered", 1);
    }
}
