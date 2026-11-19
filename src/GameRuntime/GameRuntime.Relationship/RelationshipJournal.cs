using System.Globalization;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>One line of a journal: the host facts as they became at a tick, or the step the component lived to at a tick, or a request as it was applied at a tick.</summary>
public sealed record RelationshipJournalEntry(long Tick, string Channel, IReadOnlyDictionary<string, FactValue>? Facts, long? Step, string? Kind, string? RequestId, string? A, string? B, string? C, string? D, string? E, long N, long M);

/// <summary>
/// What a relationship component was given, tick by tick: every request, refused ones too, as it was applied, the step its clock gave, and the host facts whenever they changed. Replaying a journal from a
/// checkpoint (<see cref="RelationshipReplay"/>) reproduces the session exactly, because the component is a function of its state, its requests, its steps and its facts, and of nothing else. A journal is
/// text, one line per entry: <c>F|tick|id=K:value;...</c> for facts, <c>S|tick|step</c> for a step and <c>R|tick|channel|kind|requestId|a|b|c|d|e|n|m</c> for a request; the escapes are those of <see cref="JournalText"/>.
/// </summary>
public sealed class RelationshipJournal
{
    public const string BadJournal = "REL_JOURNAL";

    private readonly List<string> _lines = new();

    public IReadOnlyList<string> Lines => _lines;

    public string ToText() => string.Join('\n', _lines);

    internal void WriteRequest(long tick, string channel, RelationshipPending request) =>
        _lines.Add(string.Join('|', "R", tick.ToString(CultureInfo.InvariantCulture), JournalText.Escape(channel), KindName(request.Kind), JournalText.Escape(request.RequestId), JournalText.Escape(request.A), JournalText.Escape(request.B),
            JournalText.Escape(request.C), JournalText.Escape(request.D), JournalText.Escape(request.E), request.N.ToString(CultureInfo.InvariantCulture), request.M.ToString(CultureInfo.InvariantCulture)));

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

    private static string KindName(RelationshipRequestKind kind)
    {
        switch (kind)
        {
            case RelationshipRequestKind.ActorAdd: return "actor-add";
            case RelationshipRequestKind.ActorRemove: return "actor-remove";
            case RelationshipRequestKind.Activity: return "activity";
            case RelationshipRequestKind.Interact: return "interact";
            case RelationshipRequestKind.Grant: return "grant";
            case RelationshipRequestKind.Remember: return "remember";
            case RelationshipRequestKind.Need: return "need";
            case RelationshipRequestKind.Axis: return "axis";
            case RelationshipRequestKind.Bond: return "bond";
            case RelationshipRequestKind.Mood: return "mood";
            case RelationshipRequestKind.Opinion: return "opinion";
        }
        throw new InvalidOperationException("unknown request kind " + kind);
    }

    internal static RelationshipRequestKind? KindOf(string name) => Enum.GetValues<RelationshipRequestKind>().Cast<RelationshipRequestKind?>().FirstOrDefault(k => KindName(k!.Value) == name);

    private static EngineRefusedException Bad(int line, string why) => new(BadJournal, "journal line " + (line + 1) + ": " + why);

    /// <summary>Reads a journal strictly: every line one of the three shapes, ticks never going backwards, kinds and numbers valid.</summary>
    public static IReadOnlyList<RelationshipJournalEntry> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var entries = new List<RelationshipJournalEntry>();
        if (text.Length == 0) return entries;
        var lines = text.Split('\n');
        long previous = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var parts = lines[i].Split('|');
            if (parts.Length < 3 || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var tick)) throw Bad(i, "not a journal entry");
            if (tick < previous) throw Bad(i, "tick " + tick + " is before the entry above it");
            previous = tick;
            if (parts[0] == "F" && parts.Length == 3) entries.Add(new RelationshipJournalEntry(tick, "", JournalText.ParseFacts(parts[2], i, Bad), null, null, null, null, null, null, null, null, 0, 0));
            else if (parts[0] == "S" && parts.Length == 3 && long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var step)) entries.Add(new RelationshipJournalEntry(tick, "", null, step, null, null, null, null, null, null, null, 0, 0));
            else if (parts[0] == "R" && parts.Length == 12)
            {
                var named = KindOf(parts[3]);
                if (named is null) throw Bad(i, "unknown request kind '" + parts[3] + "'");
                var kind = named.Value;
                if (!long.TryParse(parts[10], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) || !long.TryParse(parts[11], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var m)) throw Bad(i, "a number is not a whole number");
                entries.Add(new RelationshipJournalEntry(tick, JournalText.Unescape(parts[2]), null, null, KindName(kind), JournalText.Unescape(parts[4]), JournalText.Unescape(parts[5]), JournalText.Unescape(parts[6]),
                    JournalText.Unescape(parts[7]), JournalText.Unescape(parts[8]), JournalText.Unescape(parts[9]), n, m));
            }
            else throw Bad(i, "not a journal entry");
        }
        return entries;
    }
}
