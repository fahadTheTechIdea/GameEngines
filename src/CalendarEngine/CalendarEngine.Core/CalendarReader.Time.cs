using System.Text.Json;
using EngineCommon;

namespace CalendarEngine;

internal sealed partial class CalendarReader
{
    private IReadOnlyList<DayPartDef> ReadDayParts(JsonElement array)
    {
        var parts = new List<DayPartDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "dayParts[" + parts.Count + "]";
            _json.Object(element, where, ["id", "key", "from"]);
            var id = _json.Id(element, "id", where);
            if (!seen.Add(id)) throw _json.Refuse(CalendarCodes.Duplicate, "day part '" + id + "' is declared twice");
            var from = ReadClock(element, "from", where, false);
            if (parts.Count == 0 && from != 0) throw _json.Refuse(CalendarCodes.Order, where + ": the first day part must start at 0:00 so every minute is in a part");
            if (parts.Count > 0 && from <= parts[^1].FromMinute) throw _json.Refuse(CalendarCodes.Order, where + " starts at or before the part before it");
            parts.Add(new DayPartDef(parts.Count, id, _json.Id(element, "key", where), from));
        }
        return parts;
    }

    private IReadOnlyList<SeasonDef> ReadSeasons(JsonElement array, int twilight)
    {
        var seasons = new List<SeasonDef>();
        foreach (var element in array.EnumerateArray())
        {
            var where = "seasons[" + seasons.Count + "]";
            _json.Object(element, where, ["id", "key", "month", "day", "sunrise", "sunset"]);
            var id = _json.Id(element, "id", where);
            if (!_seasonIndex.TryAdd(id, seasons.Count)) throw _json.Refuse(CalendarCodes.Duplicate, "season '" + id + "' is declared twice");
            var monthId = _json.Id(element, "month", where);
            if (!_monthIndex.TryGetValue(monthId, out var month)) throw _json.Refuse(CalendarCodes.Reference, where + ".month '" + monthId + "' is not a month of the calendar");
            var day = _json.Int(element, "day", where, 1, 1000);
            if (day > _system.Months[month].Days) throw _json.Refuse(CalendarCodes.Date, where + ".day " + day + " is beyond the " + _system.Months[month].Days + " days " + monthId + " has in every year");
            if (seasons.Count > 0 && (month, day).CompareTo((seasons[^1].Month, seasons[^1].Day)) <= 0) throw _json.Refuse(CalendarCodes.Order, where + " does not start after the season before it");
            var sunrise = ReadClock(element, "sunrise", where, false);
            var sunset = ReadClock(element, "sunset", where, true);
            if (sunrise >= sunset) throw _json.Refuse(CalendarCodes.Order, where + ": sunrise must be before sunset");
            if (sunrise < twilight || sunset + twilight > _time.MinutesPerDay) throw _json.Refuse(CalendarCodes.Range, where + ": the twilight of " + twilight + " minutes would run past the start or the end of the day");
            seasons.Add(new SeasonDef(seasons.Count, id, _json.Id(element, "key", where), month, day, sunrise, sunset));
        }
        return seasons;
    }

    private IReadOnlyList<MoonDef> ReadMoons(JsonElement array)
    {
        var moons = new List<MoonDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "moons[" + moons.Count + "]";
            _json.Object(element, where, ["id", "key", "period", "newMoon", "phases"]);
            var id = _json.Id(element, "id", where);
            if (!seen.Add(id)) throw _json.Refuse(CalendarCodes.Duplicate, "moon '" + id + "' is declared twice");
            var period = element.GetProperty("period");
            _json.Object(period, where + ".period", ["num", "den"]);
            var newMoon = element.GetProperty("newMoon");
            _json.Object(newMoon, where + ".newMoon", ["num", "den"]);
            var keys = new List<string>();
            foreach (var key in _json.Array(element, "phases", where, 8, 8).EnumerateArray())
            {
                if (key.ValueKind != JsonValueKind.String) throw _json.Refuse(CalendarCodes.Format, where + ".phases must be eight localisation keys");
                keys.Add(_json.CheckId(key.GetString()!, where + ".phases"));
            }
            moons.Add(new MoonDef(moons.Count, id, _json.Id(element, "key", where),
                _json.Long(period, "num", where + ".period", 1, 1_000_000_000), _json.Long(period, "den", where + ".period", 1, 1_000_000_000),
                _json.Long(newMoon, "num", where + ".newMoon", -1_000_000_000_000, 1_000_000_000_000), _json.Long(newMoon, "den", where + ".newMoon", 1, 1_000_000_000), keys));
        }
        return moons;
    }
}
