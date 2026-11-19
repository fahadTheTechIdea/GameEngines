using System.Text.Json;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace CalendarEngine;

/// <summary>The reader of one document. It is single use: it holds the declarations as it finds them, in the order the document needs them.</summary>
internal sealed partial class CalendarReader
{
    /// <summary>The week modes a document may name: a closed set, so an unknown word is refused at one place.</summary>
    private static readonly IReadOnlyDictionary<string, WeekMode> WeekModes = new Dictionary<string, WeekMode>(StringComparer.Ordinal) { ["continuous"] = WeekMode.Continuous, ["restart"] = WeekMode.RestartEachMonth };

    private readonly string _text;
    private readonly StrictJson _json = new(CalendarCodes.ForDocument);
    private readonly Conditions _conditions;

    private TimeDef _time = new(1, 1, 1);
    private CalendarSystem _system = null!;
    private readonly Dictionary<string, int> _weekdayIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _monthIndex = new(StringComparer.Ordinal);
    private IReadOnlyList<SeasonDef> _seasons = Array.Empty<SeasonDef>();
    private readonly Dictionary<string, int> _seasonIndex = new(StringComparer.Ordinal);
    private IReadOnlyList<MoonDef> _moons = Array.Empty<MoonDef>();
    private IReadOnlyList<HolidayDef> _holidays = Array.Empty<HolidayDef>();
    private readonly Dictionary<string, int> _holidayIndex = new(StringComparer.Ordinal);

    public CalendarReader(string text)
    {
        _text = text;
        _conditions = new Conditions(_json);
    }

    public CalendarSet Read()
    {
        using var document = _json.Parse(_text, "the document");
        var root = _json.Object(document.RootElement, "the document",
            ["format", "id", "time", "epoch", "weekMode", "weekdays", "months", "leap", "eras", "dayParts", "seasons", "twilightMinutes", "moons", "holidays", "formats", "locations", "activities", "interrupts", "facts", "schedules"]);
        if (_json.Long(root, "format", "the document", long.MinValue, long.MaxValue) != CalendarLoader.Format) throw _json.Refuse(CalendarCodes.Format, "this reader reads format " + CalendarLoader.Format + " only");
        var id = _json.Id(root, "id", "the document");
        _time = ReadTime(root.GetProperty("time"));
        _system = ReadSystem(root);
        var dayParts = ReadDayParts(_json.Array(root, "dayParts", "the document", 1, 24));
        var twilight = _json.Int(root, "twilightMinutes", "the document", 0, _time.MinutesPerDay / 4);
        _seasons = ReadSeasons(_json.Array(root, "seasons", "the document", 1, 16), twilight);
        _moons = ReadMoons(_json.Array(root, "moons", "the document", 0, 8));
        _holidays = ReadHolidays(_json.Array(root, "holidays", "the document", 0, 256));
        var formats = ReadFormats(_json.Array(root, "formats", "the document", 0, 32));
        var locations = ReadPlaces(_json.Array(root, "locations", "the document", 0, 1024), "locations");
        var activities = ReadPlaces(_json.Array(root, "activities", "the document", 0, 1024), "activities");
        var interrupts = ReadInterrupts(_json.Array(root, "interrupts", "the document", 0, 64), locations, activities);
        var hostFacts = ReadHostFacts(_json.Array(root, "facts", "the document", 0, 64));
        var schedules = ReadSchedules(_json.Array(root, "schedules", "the document", 0, 1024), locations, activities, hostFacts);
        return new CalendarSet(id, Digest.Of(_text), _system, dayParts, _seasons, twilight, _moons, _holidays, formats, locations, activities, interrupts, hostFacts, schedules);
    }

    // ---- clock and date text -----------------------------------------------------------------------------------------------------------

    /// <summary>A time of day written H:MM or HH:MM, read against the day this document declares. With <paramref name="allowEnd"/> the end of the day (the hour after the last, minute 0) is accepted.</summary>
    private int ReadClock(JsonElement owner, string field, string where, bool allowEnd)
    {
        var text = _json.Text(owner, field, where, 8);
        var at = text.IndexOf(':', StringComparison.Ordinal);
        if (at < 1 || at > 2 || text.Length - at - 1 != 2 || !text[..at].All(char.IsAsciiDigit) || !text[(at + 1)..].All(char.IsAsciiDigit))
            throw _json.Refuse(CalendarCodes.Clock, where + "." + field + " '" + text + "' must be a time of day written H:MM");
        var hour = int.Parse(text[..at], System.Globalization.CultureInfo.InvariantCulture);
        var minute = int.Parse(text[(at + 1)..], System.Globalization.CultureInfo.InvariantCulture);
        if (minute >= _time.MinutesPerHour) throw _json.Refuse(CalendarCodes.Clock, where + "." + field + " '" + text + "' has a minute beyond the " + _time.MinutesPerHour + " minutes of an hour");
        if (hour == _time.HoursPerDay && minute == 0 && allowEnd) return _time.MinutesPerDay;
        if (hour >= _time.HoursPerDay) throw _json.Refuse(CalendarCodes.Clock, where + "." + field + " '" + text + "' has an hour beyond the " + _time.HoursPerDay + " hours of a day");
        return hour * _time.MinutesPerHour + minute;
    }

    private CalendarDate ReadDate(JsonElement element, string where)
    {
        _json.Object(element, where, ["year", "month", "day"]);
        var year = _json.Long(element, "year", where, CalendarSystem.MinYear, CalendarSystem.MaxYear);
        var monthId = _json.Id(element, "month", where);
        if (!_monthIndex.TryGetValue(monthId, out var month)) throw _json.Refuse(CalendarCodes.Reference, where + ".month '" + monthId + "' is not a month of the calendar");
        var date = new CalendarDate(year, month, _json.Int(element, "day", where, 1, 1000));
        return _system.IsValid(date) ? date : throw _json.Refuse(CalendarCodes.Date, where + " is not a day of the calendar (year " + year + ", " + monthId + " " + date.Day + ")");
    }

    // ---- time and the date system ------------------------------------------------------------------------------------------------------

    private TimeDef ReadTime(JsonElement element)
    {
        _json.Object(element, "time", ["hoursPerDay", "minutesPerHour", "ticksPerMinute"]);
        var time = new TimeDef(_json.Int(element, "hoursPerDay", "time", 1, 48), _json.Int(element, "minutesPerHour", "time", 1, 120), _json.Int(element, "ticksPerMinute", "time", 1, 1000));
        return time.TicksPerDay <= 1_000_000 ? time : throw _json.Refuse(CalendarCodes.Range, "time: a day of " + time.TicksPerDay + " ticks is beyond 1,000,000");
    }

    private CalendarSystem ReadSystem(JsonElement root)
    {
        var weekMode = _json.Text(root, "weekMode", "the document", 16);
        if (!WeekModes.TryGetValue(weekMode, out var mode)) throw _json.Refuse(CalendarCodes.Format, "weekMode '" + weekMode + "' must be continuous or restart");
        var weekdays = new List<WeekdayDef>();
        foreach (var element in _json.Array(root, "weekdays", "the document", 1, 32).EnumerateArray())
        {
            var where = "weekdays[" + weekdays.Count + "]";
            _json.Object(element, where, ["id", "key"]);
            var id = _json.Id(element, "id", where);
            if (!_weekdayIndex.TryAdd(id, weekdays.Count)) throw _json.Refuse(CalendarCodes.Duplicate, "weekday '" + id + "' is declared twice");
            weekdays.Add(new WeekdayDef(weekdays.Count, id, _json.Id(element, "key", where)));
        }
        var months = new List<MonthDef>();
        foreach (var element in _json.Array(root, "months", "the document", 1, 64).EnumerateArray())
        {
            var where = "months[" + months.Count + "]";
            _json.Object(element, where, ["id", "key", "days", "leapDays", "intercalary"]);
            var id = _json.Id(element, "id", where);
            if (!_monthIndex.TryAdd(id, months.Count)) throw _json.Refuse(CalendarCodes.Duplicate, "month '" + id + "' is declared twice");
            var days = _json.Int(element, "days", where, 0, 1000);
            var leapDays = _json.Int(element, "leapDays", where, 0, 1000);
            if (days + leapDays == 0) throw _json.Refuse(CalendarCodes.Range, where + " has no days in any year");
            if (months.Count == 0 && days == 0) throw _json.Refuse(CalendarCodes.Range, where + ": the first month must have days in every year");
            months.Add(new MonthDef(months.Count, id, _json.Id(element, "key", where), days, leapDays, _json.Bool(element, "intercalary", where)));
        }
        if (months.Sum(m => m.Days) < 1 || months.Sum(m => m.Days) > 10_000) throw _json.Refuse(CalendarCodes.Range, "a common year of " + months.Sum(m => m.Days) + " days is beyond 1 to 10,000");
        if (mode == WeekMode.RestartEachMonth && months.All(m => m.Intercalary)) throw _json.Refuse(CalendarCodes.Weeks, "every month is intercalary, so no day belongs to a week");

        var rules = new List<LeapRule>();
        foreach (var element in _json.Array(root, "leap", "the document", 0, 16).EnumerateArray())
        {
            var where = "leap[" + rules.Count + "]";
            _json.Object(element, where, ["every", "offset", "leap"]);
            rules.Add(new LeapRule(_json.Int(element, "every", where, 1, LeapRules.MaxEvery), _json.Int(element, "offset", where, -LeapRules.MaxEvery, LeapRules.MaxEvery), _json.Bool(element, "leap", where)));
        }
        var extra = months.Sum(m => m.LeapDays);
        if (extra > 0 && !rules.Any(r => r.Leap)) throw _json.Refuse(CalendarCodes.Leap, "months have leap days but no leap rule ever makes a year leap");
        if (extra == 0 && rules.Count > 0) throw _json.Refuse(CalendarCodes.Leap, "leap rules are given but no month has leap days, so they would change nothing");
        LeapRules leap;
        try { leap = new LeapRules(rules); }
        catch (EngineRefusedException refusal) { throw _json.Refuse(refusal.Code, "leap: " + refusal.Message); }

        var eras = ReadEras(_json.Array(root, "eras", "the document", 0, 32));

        var epoch = root.GetProperty("epoch");
        _json.Object(epoch, "epoch", ["year"], "weekday");
        var epochYear = _json.Long(epoch, "year", "epoch", CalendarSystem.MinYear, CalendarSystem.MaxYear);
        var epochWeekday = 0;
        if (mode == WeekMode.Continuous)
        {
            if (!_json.Has(epoch, "weekday")) throw _json.Refuse(CalendarCodes.MissingField, "epoch needs 'weekday' (the weekday of the first day of the epoch year) in a calendar whose weeks run on");
            var id = _json.Id(epoch, "weekday", "epoch");
            if (!_weekdayIndex.TryGetValue(id, out epochWeekday)) throw _json.Refuse(CalendarCodes.Reference, "epoch.weekday '" + id + "' is not a weekday of the calendar");
        }
        else if (_json.Has(epoch, "weekday")) throw _json.Refuse(CalendarCodes.Weeks, "epoch.weekday means nothing when weeks restart each month");

        return new CalendarSystem(_time, epochYear, epochWeekday, mode, weekdays, months, leap, eras);
    }

    private IReadOnlyList<EraDef> ReadEras(JsonElement array)
    {
        var eras = new List<EraDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "eras[" + eras.Count + "]";
            _json.Object(element, where, ["id", "key", "from", "direction"]);
            var id = _json.Id(element, "id", where);
            if (!seen.Add(id)) throw _json.Refuse(CalendarCodes.Duplicate, "era '" + id + "' is declared twice");
            var from = _json.Long(element, "from", where, -1_000_000, CalendarSystem.MaxYear);
            var direction = _json.Text(element, "direction", where, 8);
            if (direction is not ("up" or "down")) throw _json.Refuse(CalendarCodes.Format, where + ".direction must be up or down");
            if (eras.Count > 0 && from <= eras[^1].From) throw _json.Refuse(CalendarCodes.Order, where + " starts at " + from + ", not after the era before it (" + eras[^1].From + ")");
            eras.Add(new EraDef(eras.Count, id, _json.Id(element, "key", where), from, direction == "down"));
        }
        if (eras.Count > 0)
        {
            if (eras[0].From > CalendarSystem.MinYear) throw _json.Refuse(CalendarCodes.Order, "the first era must start at or before year " + CalendarSystem.MinYear + " so every year has an era");
            if (eras[^1].Descending) throw _json.Refuse(CalendarCodes.Order, "the last era cannot count down: there is no era after it to count down to");
        }
        return eras;
    }
}
