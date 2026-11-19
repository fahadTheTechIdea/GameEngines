using System.Globalization;
using CalendarEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

/// <summary>
/// The calendar authoring tool. <c>check</c> reads a document the way a game does and says whether it is accepted, or which rule refused it and where; <c>explain</c> says why a schedule puts somebody
/// where it does at a date and time; <c>convert</c> turns an instant into a date and a date into an instant; <c>month-view</c> prints a month; <c>coverage</c> says how much of a document each locale table
/// covers. Exit codes: 0 done (for <c>coverage</c>, every table complete), 1 refused or incomplete (the code or the lists are printed), 2 wrong command line or a file problem.
/// </summary>
public static partial class CalendarCli
{
    public const string Usage =
        "usage: CalendarEngine.Cli check <calendar.json>\n" +
        "       CalendarEngine.Cli explain <calendar.json> <schedule> <year-month-day> <H:MM> [--interrupt <id>] [--fact <id>=<true|false|number|text>]...\n" +
        "       CalendarEngine.Cli convert <calendar.json> instant <ticks>\n" +
        "       CalendarEngine.Cli convert <calendar.json> date <year-month-day> [<H:MM>]\n" +
        "       CalendarEngine.Cli month-view <calendar.json> <year> <month> [--table <locale.json>]\n" +
        "       CalendarEngine.Cli coverage <calendar.json> <locale.json> [<locale.json> ...]\n" +
        "exit: 0 done | 1 refused or incomplete (the code or the lists are printed) | 2 usage or file problem";

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count < 2) return Wrong(error, Usage);
        try
        {
            switch (args[0])
            {
                case "check" when args.Count == 2: return Check(File.ReadAllText(args[1]), output);
                case "explain" when args.Count >= 5: return Explain(File.ReadAllText(args[1]), args, output, error);
                case "convert" when args.Count is 4 or 5: return Convert(File.ReadAllText(args[1]), args, output, error);
                case "month-view" when args.Count is 4 or 6: return MonthView(File.ReadAllText(args[1]), args, output, error);
                case "coverage" when args.Count >= 3: return Coverage(File.ReadAllText(args[1]), args.Skip(2).Select(File.ReadAllText).ToList(), output);
            }
        }
        catch (IOException exception) { return Wrong(error, "file problem: " + exception.Message); }
        catch (UnauthorizedAccessException exception) { return Wrong(error, "file problem: " + exception.Message); }
        return Wrong(error, Usage);
    }

    private static int Wrong(TextWriter error, string message)
    {
        error.WriteLine(message);
        return 2;
    }

    private static int Refused(TextWriter output, EngineRefusedException refusal)
    {
        output.WriteLine("refused " + refusal.Code + ": " + refusal.Message);
        return 1;
    }

    private static int Check(string document, TextWriter output)
    {
        CalendarSet set;
        try { set = CalendarLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        var system = set.System;
        output.WriteLine($"ok {set.Id} digest {set.Digest} months {system.Months.Count} weekdays {system.Weekdays.Count} common-year-days {system.CommonYearDays} leap-extra-days {system.LeapExtraDays} " +
            $"day {set.Time.HoursPerDay}h x {set.Time.MinutesPerHour}min x {set.Time.TicksPerMinute} ticks seasons {set.Seasons.Count} day-parts {set.DayParts.Count} moons {set.Moons.Count} holidays {set.Holidays.Count} " +
            $"schedules {set.Schedules.Count} interrupts {set.Interrupts.Count} host-facts {set.HostFacts.Count} text-keys {CalendarNames.RequiredKeys(set).Count}");
        foreach (var schedule in set.Schedules)
            output.WriteLine($"schedule {schedule.Id} routines {schedule.Routines.Count} ({string.Join(", ", schedule.Routines.OrderByDescending(r => r.Priority).Select(r => r.Id + " " + r.Priority))})");
        return 0;
    }

    /// <summary>A date written <c>year-month-day</c>, read from the right so a negative year keeps its minus sign.</summary>
    private static CalendarDate ParseDate(CalendarSet set, string text)
    {
        var lastDash = text.LastIndexOf('-');
        var monthDash = lastDash > 0 ? text.LastIndexOf('-', lastDash - 1) : -1;
        if (monthDash < 1 || !long.TryParse(text[..monthDash], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var year) || !int.TryParse(text[(lastDash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var day))
            throw new EngineRefusedException(CalendarCodes.DateInvalid, "'" + text + "' is not a date written year-month-day");
        var monthId = text[(monthDash + 1)..lastDash];
        var month = set.System.Months.FirstOrDefault(m => m.Id == monthId);
        if (month is null) throw new EngineRefusedException(CalendarCodes.DateInvalid, "'" + monthId + "' is not a month of the calendar");
        return new CalendarDate(year, month.Index, day);
    }

    private static (int Hour, int Minute) ParseClock(string text)
    {
        var parts = text.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hour) || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minute))
            throw new EngineRefusedException(CalendarCodes.DateInvalid, "'" + text + "' is not a time written H:MM");
        return (hour, minute);
    }

    private static FactValue ParseFact(string text)
    {
        if (text == "true") return FactValue.From(true);
        if (text == "false") return FactValue.From(false);
        return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? FactValue.From(number) : FactValue.From(text);
    }

    private static int Explain(string document, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            var set = CalendarLoader.Load(document);
            var (hour, minute) = ParseClock(args[4]);
            var instant = set.System.InstantOf(ParseDate(set, args[3]), hour, minute);
            string? interrupt = null;
            var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
            for (var i = 5; i < args.Count; i += 2)
            {
                if (i + 1 >= args.Count) return Wrong(error, Usage);
                if (args[i] == "--interrupt") interrupt = args[i + 1];
                else if (args[i] == "--fact")
                {
                    var at = args[i + 1].IndexOf('=', StringComparison.Ordinal);
                    if (at < 1) return Wrong(error, "--fact needs id=value\n" + Usage);
                    facts[args[i + 1][..at]] = ParseFact(args[i + 1][(at + 1)..]);
                }
                else return Wrong(error, Usage);
            }
            output.WriteLine(CalendarExplain.WhereIs(set, instant, args[2], interrupt, facts));
            return 0;
        }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
    }

    private static int Convert(string document, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            var set = CalendarLoader.Load(document);
            if (args[2] == "instant" && args.Count == 4)
            {
                if (!long.TryParse(args[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var instant)) return Wrong(error, "'" + args[3] + "' is not a whole number of ticks");
                var moment = set.System.MomentOf(instant);
                output.WriteLine($"instant {instant} = {Describe(set, moment)}");
                return 0;
            }
            if (args[2] == "date")
            {
                var (hour, minute) = args.Count == 5 ? ParseClock(args[4]) : (0, 0);
                var instant = set.System.InstantOf(ParseDate(set, args[3]), hour, minute);
                output.WriteLine($"{args[3]} {hour}:{minute:00} = instant {instant}");
                return 0;
            }
        }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        return Wrong(error, Usage);
    }

    private static string Describe(CalendarSet set, in Moment moment)
    {
        var day = moment.Day;
        var era = set.System.EraOf(day.Year);
        return $"{CalendarExplain.DateText(set, day)} {CalendarExplain.Clock(set, moment.MinuteOfDay)} weekday {(day.Weekday < 0 ? "none" : set.System.Weekdays[day.Weekday].Id)} day-of-year {day.DayOfYear}/{day.DaysInYear} " +
            $"season {set.Seasons[set.SeasonOf(day)].Id} day-part {set.DayParts[set.DayPartOf(moment.MinuteOfDay)].Id}" + (era is null ? "" : $" era {era.Value.Era.Id} year-of-era {era.Value.YearOfEra}");
    }
}
