using System.Globalization;
using System.Text;
using ScenarioEngine.Contracts;

namespace CalendarEngine;

/// <summary>
/// Says why an actor is where it is at an instant: the date, each routine of its schedule with whether it applied and why not, which block answered, and whether an interrupt won. It reads through the same
/// <see cref="CalendarContext"/> a game does, so the explanation cannot disagree with the answer. The text is the same for the same document, instant and facts.
/// </summary>
public static class CalendarExplain
{
    public static string Clock(CalendarSet set, int minuteOfDay) =>
        (minuteOfDay / set.Time.MinutesPerHour).ToString("00", CultureInfo.InvariantCulture) + ":" + (minuteOfDay % set.Time.MinutesPerHour).ToString("00", CultureInfo.InvariantCulture);

    public static string DateText(CalendarSet set, in DayInfo day) =>
        day.Year.ToString(CultureInfo.InvariantCulture) + "-" + set.System.Months[day.Month].Id + "-" + day.Day.ToString(CultureInfo.InvariantCulture);

    public static string WhereIs(CalendarSet set, long instant, string schedule, string? interrupt, IReadOnlyDictionary<string, FactValue>? hostFacts)
    {
        var context = CalendarContext.At(set, instant, hostFacts);
        var moment = context.Moment;
        var day = moment.Day;
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"instant {instant} is {DateText(set, day)} {Clock(set, moment.MinuteOfDay)}, weekday {(day.Weekday < 0 ? "none" : set.System.Weekdays[day.Weekday].Id)}, season {set.Seasons[context.Season].Id}, day part {set.DayParts[set.DayPartOf(moment.MinuteOfDay)].Id}");
        text.Append(", holidays ").Append(context.Holidays.Count == 0 ? "none" : string.Join(", ", context.Holidays.Select(h => set.Holidays[h].Id))).Append('\n');
        var def = context.Schedule(schedule);
        text.Append("schedule ").Append(def.Id).Append('\n');
        foreach (var routine in def.Routines.OrderByDescending(r => r.Priority))
        {
            var why = routine.Selector is null ? null : context.WhyNot(routine.Selector);
            text.Append(CultureInfo.InvariantCulture, $"  routine {routine.Id} (priority {routine.Priority}): ");
            if (why is not null) { text.Append("does not apply, ").Append(why).Append('\n'); continue; }
            var block = CalendarContext.BlockAt(routine, moment.MinuteOfDay);
            text.Append(block is null ? "applies, but has no block at this time, so the next routine is asked" : "applies, block " + block.Id + " covers " + Clock(set, block.From) + " to " + Clock(set, block.To)).Append('\n');
        }
        var answer = context.WhereIs(schedule, interrupt);
        if (interrupt is not null)
        {
            var kind = set.InterruptById[interrupt];
            var routineAnswer = context.WhereIs(schedule);
            text.Append(CultureInfo.InvariantCulture, $"  interrupt {kind.Id} (priority {kind.Priority}): ").Append(answer.Source == AnswerSource.Interrupt ? "wins over" : "loses to").Append(CultureInfo.InvariantCulture, $" routine {routineAnswer.Routine} (priority {routineAnswer.Priority})\n");
        }
        text.Append(CultureInfo.InvariantCulture, $"result: location {answer.Location}, activity {answer.Activity}, from {(answer.Source == AnswerSource.Interrupt ? "interrupt " + answer.Routine : "routine " + answer.Routine + " block " + answer.Block)}");
        return text.ToString();
    }
}
