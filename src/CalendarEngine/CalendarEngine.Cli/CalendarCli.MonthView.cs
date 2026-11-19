using System.Globalization;
using System.Text;
using CalendarEngine;
using EngineCommon;

public static partial class CalendarCli
{
    private static int MonthView(string document, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            var set = CalendarLoader.Load(document);
            if (!long.TryParse(args[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var year)) return Wrong(error, "'" + args[2] + "' is not a year");
            var month = set.System.Months.FirstOrDefault(m => m.Id == args[3]);
            if (month is null) return Wrong(error, "'" + args[3] + "' is not a month of the calendar");
            LocaleChain? chain = null;
            if (args.Count == 6)
            {
                if (args[4] != "--table") return Wrong(error, Usage);
                var table = LocaleTable.Load(File.ReadAllText(args[5]));
                chain = LocaleChain.Create(table.Locale, [table], table.Locale);
            }
            var length = set.System.DaysInMonth(year, month.Index);
            if (length == 0)
            {
                output.WriteLine($"{month.Id} {year} has no days in this year ({set.Id})");
                return 0;
            }
            var weeks = set.System.Weekdays.Count;
            var title = chain is null ? month.Id : CalendarNames.Month(set, chain, month.Index);
            output.WriteLine($"{title} {year} ({set.Id})");
            var holidayMarks = new SortedDictionary<int, string>();
            var firstDay = set.System.DayNumber(new CalendarDate(year, month.Index, 1));
            for (var day = 1; day <= length; day++)
            {
                var info = set.System.DayOf(firstDay + day - 1);
                var on = set.HolidaysOn(info);
                if (on.Count > 0) holidayMarks[day] = string.Join("+", on.Select(h => set.Holidays[h].Id));
            }
            var text = new StringBuilder();
            var first = set.System.DayOf(firstDay);
            if (first.Weekday < 0)
            {
                text.Append("days outside the weeks:");
                for (var day = 1; day <= length; day++) text.Append(' ').Append(day).Append(holidayMarks.ContainsKey(day) ? "*" : "");
                text.Append('\n');
            }
            else
            {
                for (var w = 0; w < weeks; w++)
                {
                    var name = chain is null ? set.System.Weekdays[w].Id : CalendarNames.Weekday(set, chain, w);
                    text.Append((name.Length > 3 ? name[..3] : name).PadLeft(4));
                }
                text.Append('\n');
                var column = first.Weekday;
                text.Append(new string(' ', 4 * column));
                for (var day = 1; day <= length; day++)
                {
                    text.Append((day + (holidayMarks.ContainsKey(day) ? "*" : "")).PadLeft(4));
                    column++;
                    if (column == weeks && day < length) { text.Append('\n'); column = 0; }
                }
                text.Append('\n');
            }
            foreach (var (day, ids) in holidayMarks) text.Append("* ").Append(day).Append(' ').Append(ids).Append('\n');
            output.Write(text.ToString());
            return 0;
        }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
    }

    private static int Coverage(string document, IReadOnlyList<string> tables, TextWriter output)
    {
        CalendarSet set;
        try { set = CalendarLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        var complete = true;
        foreach (var text in tables)
        {
            LocaleTable table;
            try { table = LocaleTable.Load(text); }
            catch (EngineRefusedException refusal) { return Refused(output, refusal); }
            var report = CalendarNames.Coverage(set, table);
            output.WriteLine($"{report.Locale}: {report.Translated}/{report.Total} ({report.Percent}%) missing {report.Missing.Count} unknown {report.Unknown.Count} problems {report.Problems.Count}");
            foreach (var key in report.Missing) output.WriteLine("  missing " + key);
            foreach (var key in report.Unknown) output.WriteLine("  unknown " + key);
            foreach (var problem in report.Problems) output.WriteLine("  problem " + problem.Key + ": " + problem.Why);
            complete &= report.IsComplete;
        }
        return complete ? 0 : 1;
    }
}
