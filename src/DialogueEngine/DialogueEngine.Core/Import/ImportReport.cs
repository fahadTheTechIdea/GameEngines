using System.Globalization;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>How a feature of the source format came out in the converted document.</summary>
public enum ImportStatus
{
    /// <summary>It became the dialogue-document feature that means the same.</summary>
    Converted,
    /// <summary>It became something close; the detail says what differs.</summary>
    Approximated,
    /// <summary>It was left out and the rest was converted; the detail says what.</summary>
    Dropped,
}

/// <summary>One feature of the source and what became of it, with how often it was met and the first place it was met.</summary>
public sealed record ImportNote(ImportStatus Status, string Feature, string Detail, int Count);

/// <summary>What a conversion did, feature by feature, in the order each was first met. A feature the converter refuses is not here: it stops the conversion with <see cref="DialogueCodes.ImportUnsupported"/>.</summary>
public sealed class ImportReport
{
    private readonly List<ImportNote> _notes = new();

    public IReadOnlyList<ImportNote> Notes => _notes;

    internal void Add(ImportStatus status, string feature, string detail)
    {
        var at = _notes.FindIndex(n => n.Status == status && n.Feature == feature);
        if (at < 0) _notes.Add(new ImportNote(status, feature, detail, 1));
        else _notes[at] = _notes[at] with { Count = _notes[at].Count + 1 };
    }
}

/// <summary>
/// A function of the source the caller allows, mapped to a fact the host supplies. A source format can call functions that only the game can answer (does the player carry a sword, how much gold is
/// in the bank); a dialogue document can read only declared facts, so each allowed call is one exact signature (<c>has_item("sword")</c>, <c>bank()</c>, arguments fixed text, whole numbers or
/// true/false) and the external fact its result is read from (<c>ext.&lt;Fact&gt;</c>, declared by the converter with this type). A call that is not on the list is refused with
/// <see cref="DialogueCodes.ImportFunction"/>; the converter never guesses what a function does.
/// </summary>
/// <param name="Signature">The canonical call text: name, then the arguments in parentheses separated by commas without spaces, text in double quotes.</param>
/// <param name="Fact">The id of the external fact (without <c>ext.</c>).</param>
/// <param name="Kind">The type of the answer.</param>
/// <param name="Minimum">For a whole number, the lowest answer the host gives.</param>
/// <param name="Maximum">For a whole number, the highest answer the host gives.</param>
public sealed record ImportFunction(string Signature, string Fact, FactKind Kind, long Minimum = 0, long Maximum = 0)
{
    /// <summary>The canonical signature of a call: <c>name(a,b)</c> with text in quotes, numbers in the invariant culture and <c>true</c>/<c>false</c>.</summary>
    public static string Canonical(string name, IEnumerable<object> arguments) =>
        name + "(" + string.Join(",", arguments.Select(a => a switch
        {
            string s => "\"" + s + "\"",
            long n => n.ToString(CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => null,
        } ?? throw new EngineRefusedException(DialogueCodes.ImportFunction, "a function argument must be text, a whole number or true/false"))) + ")";

    /// <summary>
    /// Reads <c>signature=fact:type</c> with <c>type</c> <c>bool</c>, <c>string</c> or <c>int:min..max</c>, for example <c>has_item("sword")=has-sword:bool</c> or <c>bank()=bank:int:0..100000</c>.
    /// </summary>
    public static ImportFunction Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        EngineRefusedException Bad(string why) => new(DialogueCodes.ImportFunction, "function list entry '" + text + "': " + why);
        var equals = text.LastIndexOf('=');
        if (equals <= 0) throw Bad("it is signature=fact:type");
        var signature = text[..equals].Trim();
        var open = signature.IndexOf('(');
        if (open <= 0 || !signature.EndsWith(')')) throw Bad("the signature is name(arguments)");
        var name = signature[..open].Trim();
        var arguments = new List<object>();
        var inner = signature[(open + 1)..^1];
        var quoted = false;
        var start = 0;
        var pieces = new List<string>();
        for (var i = 0; i <= inner.Length; i++)
        {
            if (i < inner.Length && inner[i] == '"') quoted = !quoted;
            if (i == inner.Length || (inner[i] == ',' && !quoted)) { pieces.Add(inner[start..i].Trim()); start = i + 1; }
        }
        if (quoted) throw Bad("a quote is never closed");
        if (inner.Trim().Length > 0)
            foreach (var piece in pieces)
            {
                if (piece.Length >= 2 && piece[0] == '"' && piece[^1] == '"' && !piece[1..^1].Contains('"')) arguments.Add(piece[1..^1]);
                else if (piece is "true" or "false") arguments.Add(piece == "true");
                else if (long.TryParse(piece, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) arguments.Add(number);
                else throw Bad("the argument '" + piece + "' is not text in quotes, a whole number or true/false");
            }
        var rest = text[(equals + 1)..].Trim().Split(':');
        if (rest.Length < 2 || rest[0].Length == 0) throw Bad("it is signature=fact:type");
        var fact = rest[0];
        foreach (var c in fact)
            if (!(c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c is '-' or '.' or '_')) throw Bad("the fact id '" + fact + "' may only use a-z, 0-9, hyphen, dot and underscore");
        switch (rest[1])
        {
            case "bool" when rest.Length == 2: return new ImportFunction(Canonical(name, arguments), fact, FactKind.Boolean);
            case "string" when rest.Length == 2: return new ImportFunction(Canonical(name, arguments), fact, FactKind.String);
            case "int" when rest.Length == 3:
            {
                var range = rest[2].Split("..");
                if (range.Length != 2 || !long.TryParse(range[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var low) || !long.TryParse(range[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var high) || low > high
                    || low < -1_000_000_000 || high > 1_000_000_000)
                    throw Bad("an int answer needs a range min..max inside -1000000000..1000000000");
                return new ImportFunction(Canonical(name, arguments), fact, FactKind.Integer, low, high);
            }
        }
        throw Bad("the type is bool, string or int:min..max");
    }
}

/// <summary>What the caller chooses for a conversion.</summary>
/// <param name="DocumentId">The id of the converted document.</param>
/// <param name="Locale">The locale the source text is in; the strings table is written for it.</param>
/// <param name="Start">The node the conversation starts at (Yarn: a title; Ink: a knot, or null for the story's own start). Yarn: null means a node titled Start, else the first node.</param>
public sealed record ImportOptions(string DocumentId, string Locale, string? Start = null)
{
    /// <summary>The id of the one conversation a single-start document holds.</summary>
    public const string Conversation = "talk";

    /// <summary>The speaker a line has when the source names none.</summary>
    public const string Narrator = "narrator";

    /// <summary>The one portrait every imported speaker has: the source formats carry none.</summary>
    public const string Portrait = "default";

    /// <summary>Yarn: several start nodes, each its own conversation (<c>talk-&lt;title&gt;</c>), so the game picks the entry the way Yarn Spinner's StartDialogue(node) does. Overrides <see cref="Start"/>.</summary>
    public IReadOnlyList<string>? Starts { get; init; }

    /// <summary>Yarn: every node that no other node jumps or detours to is a start of its own (all nodes when every node is led to by another).</summary>
    public bool AllStarts { get; init; }

    /// <summary>The functions of the source the caller allows; see <see cref="ImportFunction"/>.</summary>
    public IReadOnlyList<ImportFunction> Functions { get; init; } = [];

    /// <summary>The id of the conversation that starts at the named entry when the document has several.</summary>
    public static string ConversationFor(string start) => Conversation + "-" + start;
}

/// <summary>An entry of a converted document: a name from the source (a Yarn title) and the conversation that starts there.</summary>
public sealed record ImportEntry(string Name, string Conversation);

/// <summary>
/// A converted document with the text of its lines in the source locale, the speakers the game catalog has to provide and the feature-by-feature report. <see cref="Entries"/> lists the
/// conversations a game can start (one for a single start; one per start node otherwise) and <see cref="Functions"/> the external facts the host must supply for the allowed functions the script uses.
/// </summary>
public sealed record ImportResult(string Document, string Strings, IReadOnlyList<string> Speakers, ImportReport Report)
{
    public IReadOnlyList<ImportEntry> Entries { get; init; } = [new ImportEntry("start", ImportOptions.Conversation)];

    public IReadOnlyList<ImportFunction> Functions { get; init; } = [];
}

/// <summary>The names the dialogue document gives the types of facts.</summary>
internal static class FactTypes
{
    /// <summary>The document's name for a fact type: <c>bool</c>, <c>int</c> or <c>string</c>.</summary>
    public static string Name(FactKind kind)
    {
        switch (kind)
        {
            case FactKind.Boolean: return "bool";
            case FactKind.Integer: return "int";
            case FactKind.String: return "string";
        }
        throw new InvalidOperationException("unknown fact kind " + kind);
    }
}
