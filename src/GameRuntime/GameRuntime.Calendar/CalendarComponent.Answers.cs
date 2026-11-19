using CalendarEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

public sealed partial class CalendarComponent
{
    private void Answer(IEventPort events, long tick, in CalendarPending request, IReadOnlyDictionary<string, FactValue> facts)
    {
        try
        {
            switch (request.Kind)
            {
                case CalendarRequestKind.Scale: Runtime.SetScale(request.N, request.M); Applied(events, tick, request); break;
                case CalendarRequestKind.Pause: Runtime.SetPaused(request.N != 0); Applied(events, tick, request); break;
                case CalendarRequestKind.Skip: Skip(events, tick, request); break;
                case CalendarRequestKind.ActorAdd: Runtime.AddActor(request.A, request.B); _dirty = true; Applied(events, tick, request); break;
                case CalendarRequestKind.ActorRemove: Runtime.RemoveActor(request.A); _dirty = true; Applied(events, tick, request); break;
                case CalendarRequestKind.Interrupt: Runtime.Interrupt(request.A, request.B, request.N); _dirty = true; Applied(events, tick, request); break;
                case CalendarRequestKind.InterruptClear: Runtime.ClearInterrupt(request.A); _dirty = true; Applied(events, tick, request); break;
                case CalendarRequestKind.Where: Where(events, tick, request, facts); break;
            }
        }
        catch (EngineRefusedException refused)
        {
            // A refusal is a normal answer to a bad request: it is published with its code and the calendar has already been left as it was.
            events.Emit(new CalendarRefused(Id, Channel, request.RequestId, tick, refused.Code));
        }
        _answered++;
    }

    private void Applied(IEventPort events, long tick, in CalendarPending request) => events.Emit(new CalendarApplied(Id, Channel, request.RequestId, tick));

    private void Skip(IEventPort events, long tick, in CalendarPending request)
    {
        var system = Runtime.Set.System;
        var time = Runtime.Set.Time;
        if (request.N < 0 || request.N > CalendarRuntime.MaxSkipDays) throw new EngineRefusedException(CalendarCodes.Skip, "a skip is 0 to " + CalendarRuntime.MaxSkipDays + " days ahead, not " + request.N);
        if (request.M < 0 || request.M >= time.HoursPerDay || request.K < 0 || request.K >= time.MinutesPerHour)
            throw new EngineRefusedException(CalendarCodes.DateInvalid, "the time " + request.M + ":" + request.K + " is not a time of this day");
        var today = system.MomentOf(Runtime.Instant).Day.DayNumber;
        var step = Runtime.SkipTo(system.InstantOf(today + request.N, (int)(request.M * time.MinutesPerHour + request.K)));
        _dirty = true;
        _context = null;
        Applied(events, tick, request);
        Publish(events, tick, step);
    }

    private void Where(IEventPort events, long tick, in CalendarPending request, IReadOnlyDictionary<string, FactValue> facts)
    {
        if (_context is null || _contextInstant != Runtime.Instant)
        {
            _context = Runtime.Context(facts);
            _contextInstant = Runtime.Instant;
        }
        var answer = Runtime.WhereIs(_context, request.A);
        events.Emit(new CalendarWhereAnswered(Id, Channel, request.RequestId, tick, request.A, answer.Schedule, answer.Location, answer.Activity, Origin(answer), answer.Routine, answer.Block, answer.Priority));
    }

    internal static string Origin(ScheduleAnswer answer)
    {
        switch (answer.Source)
        {
            case AnswerSource.Routine: return "routine";
            case AnswerSource.Interrupt: return "interrupt";
        }
        throw new InvalidOperationException("unknown answer source " + answer.Source);
    }

    /// <summary>Raises what a step crossed: the time that passed, the new day, the seasons and holidays that began, and the new part of the day.</summary>
    private void Publish(IEventPort events, long tick, TimeStep step)
    {
        var set = Runtime.Set;
        var system = set.System;
        var moment = system.MomentOf(step.To);
        events.Emit(new CalendarTimeAdvanced(Id, Channel, tick, step.From, step.To, step.DaysCrossed, step.Clamped, step.HolidaysOmitted));
        if (step.DaysCrossed > 0)
        {
            var day = moment.Day;
            events.Emit(new CalendarDayChanged(Id, Channel, tick, day.Year, system.Months[day.Month].Id, day.Day, day.Weekday < 0 ? "" : system.Weekdays[day.Weekday].Id, day.DayNumber, step.DaysCrossed));
        }
        foreach (var change in step.Seasons)
        {
            var day = system.DayOf(change.Day);
            events.Emit(new CalendarSeasonChanged(Id, Channel, tick, set.Seasons[change.From].Id, set.Seasons[change.To].Id, day.Year, system.Months[day.Month].Id, day.Day));
        }
        foreach (var start in step.Holidays)
        {
            var day = system.DayOf(start.Day);
            var holiday = set.Holidays[start.Holiday];
            events.Emit(new CalendarHolidayStarted(Id, Channel, tick, holiday.Id, string.Join(',', holiday.Tags), holiday.Days, day.Year, system.Months[day.Month].Id, day.Day));
        }
        if (step.DayPartChanged)
        {
            var season = set.SeasonOf(moment.Day);
            var light = set.DaylightAt(season, moment.MinuteOfDay);
            events.Emit(new CalendarDayPartChanged(Id, Channel, tick, set.DayParts[set.DayPartOf(moment.MinuteOfDay)].Id, moment.Hour, moment.Minute, set.Seasons[season].Id, light.IsDay, light.LightPermille,
                moment.Day.Year, system.Months[moment.Day.Month].Id, moment.Day.Day));
        }
    }

    /// <summary>Announces the actors whose block changed, when something that can change a block has happened: a new minute, new host facts, a request, or announcements still owed.</summary>
    private void Announce(IEventPort events, long tick, IReadOnlyDictionary<string, FactValue> facts)
    {
        var minute = LeapRules.FloorDiv(Runtime.Instant, Runtime.Set.Time.TicksPerMinute);
        var factsText = CalendarJournal.CanonicalFacts(facts);
        if (!_dirty && minute == _announcedMinute && factsText == _announcedFacts) return;
        _announcedMinute = minute;
        _announcedFacts = factsText;
        _dirty = false;
        try
        {
            var changes = Runtime.Refresh(Runtime.Context(facts), _maxAnnouncements);
            _announceFailure = "";
            foreach (var change in changes)
            {
                var a = change.Answer;
                events.Emit(new CalendarScheduleBlockChanged(Id, Channel, tick, change.Actor, a.Schedule, a.Location, a.Activity, Origin(a), a.Routine, a.Block, a.Priority));
            }
            _dirty = changes.Count >= _maxAnnouncements;   // more may be owed: the next Update goes on
        }
        catch (EngineRefusedException refused)
        {
            Report(events, tick, refused);
        }
    }

    /// <summary>A fact the schedules need was not given, or is of the wrong kind: the host's mistake. It is published once per kind of failure; nothing is announced until the facts or the minute change.</summary>
    private void Report(IEventPort events, long tick, EngineRefusedException refused)
    {
        if (refused.Code != _announceFailure) events.Emit(new CalendarRefused(Id, Channel, AnnounceRequestId, tick, refused.Code));
        _announceFailure = refused.Code;
    }
}
