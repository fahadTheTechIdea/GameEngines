using System.Globalization;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>One line of a journal: the host facts as they became at a tick, or the step the component lived to at a tick, or a request as it was applied at a tick.</summary>
public sealed record TutorialJournalEntry(long Tick, string Channel, IReadOnlyDictionary<string, FactValue>? Facts, long? Step, string? Kind, string? RequestId, string? Text, long Number);

/// <summary>
/// What a tutorial component was given, tick by tick: every request, refused ones too, as it was applied, the step its clock gave, and the host facts whenever they changed. Replaying a journal from a
/// checkpoint (<see cref="TutorialReplay"/>) reproduces the session exactly, because the component is a function of its state, its requests, its steps and its facts, and of nothing else. A journal is
/// text, one line per entry: <c>F|tick|id=K:value;...</c> for facts, <c>S|tick|step</c> for a step and <c>R|tick|channel|kind|requestId|text|number</c> for a request; the escapes are those of <see cref="JournalText"/>.
/// </summary>
public sealed class TutorialJournal
{
    public const string BadJournal = "TUT_JOURNAL";

    private readonly List<string> _lines = new();

    public IReadOnlyList<string> Lines => _lines;

    public string ToText() => string.Join('\n', _lines);

    internal void WriteRequest(long tick, string channel, TutorialPending request) =>
        _lines.Add(string.Join('|', "R", tick.ToString(CultureInfo.InvariantCulture), JournalText.Escape(channel), JournalText.Escape(request.Kind), JournalText.Escape(request.RequestId), JournalText.Escape(request.Text),
            request.Number.ToString(CultureInfo.InvariantCulture)));

    internal void WriteFacts(long tick, IReadOnlyDictionary<string, FactValue> facts, ref string last)
    {
        var text = JournalText.CanonicalFacts(facts);
        if (text == last) return;
        last = text;
        _lines.Add("F|" + tick.ToString(CultureInfo.InvariantCulture) + "|" + text);
    }

    internal void WriteStep(long tick, long step, ref long last)
    {
        if (step == last) return;
        last = step;
        _lines.Add("S|" + tick.ToString(CultureInfo.InvariantCulture) + "|" + step.ToString(CultureInfo.InvariantCulture));
    }

    private static EngineRefusedException Bad(int line, string why) => new(BadJournal, "journal line " + (line + 1) + ": " + why);

    /// <summary>Reads a journal strictly: every line one of the three shapes, ticks never going backwards, numbers valid.</summary>
    public static IReadOnlyList<TutorialJournalEntry> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var entries = new List<TutorialJournalEntry>();
        if (text.Length == 0) return entries;
        var lines = text.Split('\n');
        long previous = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var parts = lines[i].Split('|');
            if (parts.Length < 3 || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var tick)) throw Bad(i, "not a journal entry");
            if (tick < previous) throw Bad(i, "tick " + tick + " is before the entry above it");
            previous = tick;
            if (parts[0] == "F" && parts.Length == 3) entries.Add(new TutorialJournalEntry(tick, "", JournalText.ParseFacts(parts[2], i, Bad), null, null, null, null, 0));
            else if (parts[0] == "S" && parts.Length == 3 && long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var step)) entries.Add(new TutorialJournalEntry(tick, "", null, step, null, null, null, 0));
            else if (parts[0] == "R" && parts.Length == 7)
            {
                if (!long.TryParse(parts[6], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) throw Bad(i, "the number is not a whole number");
                entries.Add(new TutorialJournalEntry(tick, JournalText.Unescape(parts[2]), null, null, JournalText.Unescape(parts[3]), JournalText.Unescape(parts[4]), JournalText.Unescape(parts[5]), number));
            }
            else throw Bad(i, "not a journal entry");
        }
        return entries;
    }
}
