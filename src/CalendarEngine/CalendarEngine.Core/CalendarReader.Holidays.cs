using System.Text.Json;
using EngineCommon;

namespace CalendarEngine;

internal sealed partial class CalendarReader
{
    /// <summary>The recurrence frequencies a document may name: a closed set, so an unknown word is refused at one place.</summary>
    private static readonly IReadOnlyDictionary<string, RecurrenceFreq> Frequencies = new Dictionary<string, RecurrenceFreq>(StringComparer.Ordinal)
    {
        ["daily"] = RecurrenceFreq.Daily, ["weekly"] = RecurrenceFreq.Weekly, ["monthly"] = RecurrenceFreq.Monthly, ["yearly"] = RecurrenceFreq.Yearly,
    };

    /// <summary>A recurrence rule as the document writes it (the RFC 5545 parts, in JSON). It is checked by <see cref="Recurrence.Create"/>.</summary>
    private Recurrence ReadRule(JsonElement element, string where)
    {
        _json.Object(element, where, ["freq", "start"], "interval", "until", "bymonth", "bymonthday", "byday", "byseason");
        var freqText = _json.Text(element, "freq", where, 8);
        if (!Frequencies.TryGetValue(freqText, out var freq)) throw _json.Refuse(CalendarCodes.Recurrence, where + ".freq '" + freqText + "' must be daily, weekly, monthly or yearly");
        var interval = _json.Has(element, "interval") ? _json.Int(element, "interval", where, 1, 1000) : 1;
        var start = ReadDate(element.GetProperty("start"), where + ".start");
        CalendarDate? until = _json.Has(element, "until") ? ReadDate(element.GetProperty("until"), where + ".until") : null;
        var byMonth = _json.Has(element, "bymonth") ? IdList(element, "bymonth", where, _monthIndex, "month") : [];
        var bySeason = _json.Has(element, "byseason") ? IdList(element, "byseason", where, _seasonIndex, "season") : [];
        var byMonthDay = new List<int>();
        if (_json.Has(element, "bymonthday"))
            foreach (var day in _json.Array(element, "bymonthday", where, 1, 64).EnumerateArray())
            {
                if (day.ValueKind != JsonValueKind.Number || !day.TryGetInt32(out var number)) throw _json.Refuse(CalendarCodes.Format, where + ".bymonthday must hold whole numbers");
                byMonthDay.Add(number);
            }
        var byDay = new List<ByDay>();
        if (_json.Has(element, "byday"))
        {
            foreach (var entry in _json.Array(element, "byday", where, 1, 32).EnumerateArray())
            {
                _json.Object(entry, where + ".byday", ["weekday"], "nth");
                var id = _json.Id(entry, "weekday", where + ".byday");
                if (!_weekdayIndex.TryGetValue(id, out var weekday)) throw _json.Refuse(CalendarCodes.Reference, where + ".byday weekday '" + id + "' is not a weekday of the calendar");
                byDay.Add(new ByDay(weekday, _json.Has(entry, "nth") ? _json.Int(entry, "nth", where + ".byday", -60, 60) : 0));
            }
        }
        try { return Recurrence.Create(_system, freq, interval, start, until, byMonth, byMonthDay.ToArray(), byDay.ToArray(), bySeason, _seasons.Count); }
        catch (EngineRefusedException refusal) { throw _json.Refuse(refusal.Code, where + ": " + refusal.Message); }
    }

    private int[] IdList(JsonElement owner, string field, string where, Dictionary<string, int> index, string what)
    {
        var result = new List<int>();
        foreach (var entry in _json.Array(owner, field, where, 1, 64).EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String) throw _json.Refuse(CalendarCodes.Format, where + "." + field + " must hold ids");
            var id = _json.CheckId(entry.GetString()!, where + "." + field);
            if (!index.TryGetValue(id, out var found)) throw _json.Refuse(CalendarCodes.Reference, where + "." + field + " '" + id + "' is not a " + what + " of the calendar");
            if (result.Contains(found)) throw _json.Refuse(CalendarCodes.Duplicate, where + "." + field + " lists '" + id + "' twice");
            result.Add(found);
        }
        return result.ToArray();
    }

    private IReadOnlyList<HolidayDef> ReadHolidays(JsonElement array)
    {
        var holidays = new List<HolidayDef>();
        foreach (var element in array.EnumerateArray())
        {
            var where = "holidays[" + holidays.Count + "]";
            _json.Object(element, where, ["id", "key", "days", "tags", "rule"]);
            var id = _json.Id(element, "id", where);
            if (!_holidayIndex.TryAdd(id, holidays.Count)) throw _json.Refuse(CalendarCodes.Duplicate, "holiday '" + id + "' is declared twice");
            var tags = new List<string>();
            foreach (var tag in _json.Array(element, "tags", where, 0, 8).EnumerateArray())
            {
                if (tag.ValueKind != JsonValueKind.String) throw _json.Refuse(CalendarCodes.Format, where + ".tags must hold ids");
                var name = _json.CheckId(tag.GetString()!, where + ".tags");
                if (tags.Contains(name)) throw _json.Refuse(CalendarCodes.Duplicate, where + ".tags lists '" + name + "' twice");
                tags.Add(name);
            }
            holidays.Add(new HolidayDef(holidays.Count, id, _json.Id(element, "key", where), ReadRule(element.GetProperty("rule"), where + ".rule"), _json.Int(element, "days", where, 1, 31), tags));
        }
        return holidays;
    }

    private IReadOnlyList<FormatDef> ReadFormats(JsonElement array)
    {
        var formats = new List<FormatDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "formats[" + formats.Count + "]";
            _json.Object(element, where, ["id", "key"]);
            var id = _json.Id(element, "id", where);
            if (!seen.Add(id)) throw _json.Refuse(CalendarCodes.Duplicate, "format '" + id + "' is declared twice");
            formats.Add(new FormatDef(id, _json.Id(element, "key", where)));
        }
        return formats;
    }
}
