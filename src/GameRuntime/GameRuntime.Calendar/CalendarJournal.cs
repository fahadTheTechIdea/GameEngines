using System.Globalization;
using System.Text;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>One line of a journal: the host facts as they became at a tick, or a request as it was applied at a tick.</summary>
public sealed record CalendarJournalEntry(long Tick, string Channel, IReadOnlyDictionary<string, FactValue>? Facts, string? Kind, string? RequestId, string? A, string? B, long N, long M, long K);

/// <summary>
/// What a calendar component was given, tick by tick: every request, refused ones too, as it was applied, and the host facts whenever they changed. Replaying a journal from a checkpoint
/// (<see cref="CalendarReplay"/>) reproduces the session exactly, because the component is a function of its state, its requests and its facts, and of nothing else. A journal is text, one line per entry:
/// <c>F|tick|id=K:value;...</c> for facts (K is B, I or S) and <c>R|tick|channel|kind|requestId|a|b|n|m|k</c> for a request; the characters % | ; = and a line end inside a text are written as %25 %7C %3B %3D %0A.
/// </summary>
public sealed class CalendarJournal
{
    public const string BadJournal = "CAL_JOURNAL";

    private readonly List<string> _lines = new();

    public IReadOnlyList<string> Lines => _lines;

    public string ToText() => string.Join('\n', _lines);

    internal void WriteRequest(long tick, string channel, CalendarPending request) =>
        _lines.Add(string.Join('|', "R", tick.ToString(CultureInfo.InvariantCulture), Escape(channel), KindName(request.Kind), Escape(request.RequestId), Escape(request.A), Escape(request.B),
            request.N.ToString(CultureInfo.InvariantCulture), request.M.ToString(CultureInfo.InvariantCulture), request.K.ToString(CultureInfo.InvariantCulture)));

    internal void WriteFacts(long tick, IReadOnlyDictionary<string, FactValue> facts, ref string last)
    {
        var text = CanonicalFacts(facts);
        if (text == last) return;
        last = text;
        _lines.Add("F|" + tick.ToString(CultureInfo.InvariantCulture) + "|" + text);
    }

    /// <summary>The facts in a fixed order, each as <c>id=K:value</c>, so the same facts always give the same text (see <see cref="JournalText"/>).</summary>
    public static string CanonicalFacts(IReadOnlyDictionary<string, FactValue> facts) => JournalText.CanonicalFacts(facts);

    private static string KindName(CalendarRequestKind kind)
    {
        switch (kind)
        {
            case CalendarRequestKind.Scale: return "scale";
            case CalendarRequestKind.Pause: return "pause";
            case CalendarRequestKind.Skip: return "skip";
            case CalendarRequestKind.ActorAdd: return "actor-add";
            case CalendarRequestKind.ActorRemove: return "actor-remove";
            case CalendarRequestKind.Interrupt: return "interrupt";
            case CalendarRequestKind.InterruptClear: return "interrupt-clear";
            case CalendarRequestKind.Where: return "where";
        }
        throw new InvalidOperationException("unknown request kind " + kind);
    }

    internal static CalendarRequestKind? KindOf(string name) => Enum.GetValues<CalendarRequestKind>().Cast<CalendarRequestKind?>().FirstOrDefault(k => KindName(k!.Value) == name);

    private static string Escape(string text) => JournalText.Escape(text);

    private static string Unescape(string text) => JournalText.Unescape(text);

    private static EngineRefusedException Bad(int line, string why) => new(BadJournal, "journal line " + (line + 1) + ": " + why);

    /// <summary>Reads a journal strictly: every line one of the two shapes, ticks never going backwards, kinds and numbers valid.</summary>
    public static IReadOnlyList<CalendarJournalEntry> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var entries = new List<CalendarJournalEntry>();
        if (text.Length == 0) return entries;
        var lines = text.Split('\n');
        long previous = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var parts = lines[i].Split('|');
            if (parts.Length < 3 || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var tick)) throw Bad(i, "not a journal entry");
            if (tick < previous) throw Bad(i, "tick " + tick + " is before the entry above it");
            previous = tick;
            if (parts[0] == "F" && parts.Length == 3) entries.Add(new CalendarJournalEntry(tick, "", JournalText.ParseFacts(parts[2], i, Bad), null, null, null, null, 0, 0, 0));
            else if (parts[0] == "R" && parts.Length == 10)
            {
                var named = KindOf(parts[3]);
                if (named is null) throw Bad(i, "unknown request kind '" + parts[3] + "'");
                var kind = named.Value;
                if (!long.TryParse(parts[7], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) || !long.TryParse(parts[8], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var m) || !long.TryParse(parts[9], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var k))
                    throw Bad(i, "a number is not a whole number");
                entries.Add(new CalendarJournalEntry(tick, Unescape(parts[2]), null, KindName(kind), Unescape(parts[4]), Unescape(parts[5]), Unescape(parts[6]), n, m, k));
            }
            else throw Bad(i, "not a journal entry");
        }
        return entries;
    }

}
