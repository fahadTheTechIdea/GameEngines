using System.Globalization;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>A run of steps with one season: from this step, <c>Count</c> steps of <c>Season</c>.</summary>
public readonly record struct SeasonRun(long From, string Season, int Count);

/// <summary>One line of a journal: the host facts as they became at a tick, or the step the component lived to at a tick with the season of each step it went through, or a request as it was applied at a tick.</summary>
public sealed record WorldJournalEntry(long Tick, string Channel, IReadOnlyDictionary<string, FactValue>? Facts, long? Step, IReadOnlyList<SeasonRun>? Seasons, string? Kind, string? RequestId, string? A, string? B, string? C, long Number);

/// <summary>
/// What a world-events component was given, tick by tick: every request, refused ones too, as it was applied, the step its clock gave with the season of every step it crossed, and the host facts whenever they changed.
/// Replaying a journal from a checkpoint (<see cref="WorldReplay"/>) reproduces the session exactly, because the component is a function of its state, its requests, its steps with their seasons and its facts, and of
/// nothing else. A journal is text, one line per entry: <c>F|tick|id=K:value;...</c> for facts, <c>S|tick|step|from:season*count;...</c> for a step and <c>R|tick|channel|kind|requestId|a|b|c|number</c> for a request;
/// the escapes are those of <see cref="JournalText"/>.
/// </summary>
public sealed class WorldJournal
{
    public const string BadJournal = "WE_JOURNAL";

    private readonly List<string> _lines = new();

    public IReadOnlyList<string> Lines => _lines;

    public string ToText() => string.Join('\n', _lines);

    internal void WriteRequest(long tick, string channel, WorldPending request) =>
        _lines.Add(string.Join('|', "R", tick.ToString(CultureInfo.InvariantCulture), JournalText.Escape(channel), JournalText.Escape(request.Kind), JournalText.Escape(request.RequestId), JournalText.Escape(request.A),
            JournalText.Escape(request.B), JournalText.Escape(request.C), request.Number.ToString(CultureInfo.InvariantCulture)));

    internal void WriteFacts(long tick, IReadOnlyDictionary<string, FactValue> facts, ref string last)
    {
        var text = JournalText.CanonicalFacts(facts);
        if (text == last) return;
        last = text;
        _lines.Add("F|" + tick.ToString(CultureInfo.InvariantCulture) + "|" + text);
    }

    internal void WriteStep(long tick, long step, IReadOnlyList<SeasonRun> seasons, ref long last)
    {
        if (step == last) return;
        last = step;
        _lines.Add("S|" + tick.ToString(CultureInfo.InvariantCulture) + "|" + step.ToString(CultureInfo.InvariantCulture) + "|" +
            string.Join(';', seasons.Select(r => r.From.ToString(CultureInfo.InvariantCulture) + ":" + JournalText.Escape(r.Season) + "*" + r.Count.ToString(CultureInfo.InvariantCulture))));
    }

    private static EngineRefusedException Bad(int line, string why) => new(BadJournal, "journal line " + (line + 1) + ": " + why);

    private static IReadOnlyList<SeasonRun> ParseRuns(string text, int line)
    {
        var runs = new List<SeasonRun>();
        if (text.Length == 0) return runs;
        foreach (var part in text.Split(';'))
        {
            var colon = part.IndexOf(':', StringComparison.Ordinal);
            var star = part.LastIndexOf('*');
            if (colon < 1 || star < colon + 2 || !long.TryParse(part[..colon], NumberStyles.None, CultureInfo.InvariantCulture, out var from) || !int.TryParse(part[(star + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1)
                throw Bad(line, "'" + part + "' is not from:season*count");
            runs.Add(new SeasonRun(from, JournalText.Unescape(part[(colon + 1)..star]), count));
        }
        return runs;
    }

    /// <summary>Reads a journal strictly: every line one of the three shapes, ticks never going backwards, numbers valid.</summary>
    public static IReadOnlyList<WorldJournalEntry> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var entries = new List<WorldJournalEntry>();
        if (text.Length == 0) return entries;
        var lines = text.Split('\n');
        long previous = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var parts = lines[i].Split('|');
            if (parts.Length < 3 || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var tick)) throw Bad(i, "not a journal entry");
            if (tick < previous) throw Bad(i, "tick " + tick + " is before the entry above it");
            previous = tick;
            if (parts[0] == "F" && parts.Length == 3) entries.Add(new WorldJournalEntry(tick, "", JournalText.ParseFacts(parts[2], i, Bad), null, null, null, null, null, null, null, 0));
            else if (parts[0] == "S" && parts.Length == 4 && long.TryParse(parts[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var step))
                entries.Add(new WorldJournalEntry(tick, "", null, step, ParseRuns(parts[3], i), null, null, null, null, null, 0));
            else if (parts[0] == "R" && parts.Length == 9)
            {
                if (!long.TryParse(parts[8], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) throw Bad(i, "the number is not a whole number");
                entries.Add(new WorldJournalEntry(tick, JournalText.Unescape(parts[2]), null, null, null, JournalText.Unescape(parts[3]), JournalText.Unescape(parts[4]), JournalText.Unescape(parts[5]), JournalText.Unescape(parts[6]), JournalText.Unescape(parts[7]), number));
            }
            else throw Bad(i, "not a journal entry");
        }
        return entries;
    }
}
