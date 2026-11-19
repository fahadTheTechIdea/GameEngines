using System.Globalization;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>One line of a journal: the host facts as they became at a tick, or the step the component lived to at a tick, or a request as it was applied at a tick.</summary>
public sealed record EconomyJournalEntry(long Tick, string Channel, IReadOnlyDictionary<string, FactValue>? Facts, long? Step, string? Kind, string? RequestId, string? A, string? B, string? C, string? D, string? E, long N, long M, long P, long Q);

/// <summary>
/// What an economy component was given, tick by tick: every request, refused ones too, as it was applied, the step its clock gave, and the host facts whenever they changed. Replaying a journal from a
/// checkpoint (<see cref="EconomyReplay"/>) reproduces the session exactly, because the component is a function of its state, its requests, its steps and its facts, and of nothing else. A journal is
/// text, one line per entry: <c>F|tick|id=K:value;...</c> for facts, <c>S|tick|step</c> for a step and <c>R|tick|channel|kind|requestId|a|b|c|d|e|n|m|p|q</c> for a request; the escapes are those of <see cref="JournalText"/>.
/// </summary>
public sealed class EconomyJournal
{
    public const string BadJournal = "ECON_JOURNAL";

    private static readonly IReadOnlyDictionary<EconomyRequestKind, string> Names = new Dictionary<EconomyRequestKind, string>
    {
        [EconomyRequestKind.Trade] = "trade", [EconomyRequestKind.Shock] = "shock", [EconomyRequestKind.Settle] = "settle", [EconomyRequestKind.Growth] = "growth",
        [EconomyRequestKind.Fund] = "fund", [EconomyRequestKind.Lend] = "lend", [EconomyRequestKind.Quote] = "quote", [EconomyRequestKind.Price] = "price", [EconomyRequestKind.Unlock] = "unlock",
    };

    private readonly List<string> _lines = new();

    public IReadOnlyList<string> Lines => _lines;

    public string ToText() => string.Join('\n', _lines);

    private static string Num(long value) => value.ToString(CultureInfo.InvariantCulture);

    internal void WriteRequest(long tick, string channel, EconomyPending request) =>
        _lines.Add(string.Join('|', "R", Num(tick), JournalText.Escape(channel), Names[request.Kind], JournalText.Escape(request.RequestId), JournalText.Escape(request.A), JournalText.Escape(request.B),
            JournalText.Escape(request.C), JournalText.Escape(request.D), JournalText.Escape(request.E), Num(request.N), Num(request.M), Num(request.P), Num(request.Q)));

    internal void WriteFacts(long tick, IReadOnlyDictionary<string, FactValue> facts, ref string last)
    {
        var text = JournalText.CanonicalFacts(facts);
        if (text == last) return;
        last = text;
        _lines.Add("F|" + Num(tick) + "|" + text);
    }

    internal void WriteStep(long tick, long step, ref long last)
    {
        if (step == last) return;
        last = step;
        _lines.Add("S|" + Num(tick) + "|" + Num(step));
    }

    internal static EconomyRequestKind? KindOf(string name)
    {
        foreach (var (kind, text) in Names)
            if (text == name) return kind;
        return null;
    }

    internal static string NameOf(EconomyRequestKind kind) => Names[kind];

    private static EngineRefusedException Bad(int line, string why) => new(BadJournal, "journal line " + (line + 1) + ": " + why);

    /// <summary>Reads a journal strictly: every line one of the three shapes, ticks never going backwards, kinds and numbers valid.</summary>
    public static IReadOnlyList<EconomyJournalEntry> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var entries = new List<EconomyJournalEntry>();
        if (text.Length == 0) return entries;
        var lines = text.Split('\n');
        long previous = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var parts = lines[i].Split('|');
            if (parts.Length < 3 || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var tick)) throw Bad(i, "not a journal entry");
            if (tick < previous) throw Bad(i, "tick " + tick + " is before the entry above it");
            previous = tick;
            if (parts[0] == "F" && parts.Length == 3) entries.Add(new EconomyJournalEntry(tick, "", JournalText.ParseFacts(parts[2], i, Bad), null, null, null, null, null, null, null, null, 0, 0, 0, 0));
            else if (parts[0] == "S" && parts.Length == 3 && long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var step))
                entries.Add(new EconomyJournalEntry(tick, "", null, step, null, null, null, null, null, null, null, 0, 0, 0, 0));
            else if (parts[0] == "R" && parts.Length == 14)
            {
                var kind = KindOf(parts[3]);
                if (kind is null) throw Bad(i, "unknown request kind '" + parts[3] + "'");
                var numbers = new long[4];
                for (var k = 0; k < 4; k++)
                    if (!long.TryParse(parts[10 + k], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out numbers[k])) throw Bad(i, "a number is not a whole number");
                entries.Add(new EconomyJournalEntry(tick, JournalText.Unescape(parts[2]), null, null, NameOf(kind.Value), JournalText.Unescape(parts[4]), JournalText.Unescape(parts[5]), JournalText.Unescape(parts[6]),
                    JournalText.Unescape(parts[7]), JournalText.Unescape(parts[8]), JournalText.Unescape(parts[9]), numbers[0], numbers[1], numbers[2], numbers[3]));
            }
            else throw Bad(i, "not a journal entry");
        }
        return entries;
    }
}
