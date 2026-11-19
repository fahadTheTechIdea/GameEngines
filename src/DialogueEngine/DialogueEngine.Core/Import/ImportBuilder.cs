using System.Text.Json;
using System.Text.Json.Nodes;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>A conversation of the converted document: its id and the node (or <c>@alias</c>) it starts at.</summary>
internal sealed record ConversationSpec(string Id, string Start);

/// <summary>
/// What every converter shares: the nodes, text, speakers, variables and external facts it has made, the checking of the finished document, and its writing. A converter says what the source means;
/// this owns what a dialogue document is, so no converter writes document JSON of its own and each result is accepted by the loader or refused with <see cref="DialogueCodes.ImportInvalid"/>.
/// All nodes live in one pool with ids that are new in the document; a conversation is the part of the pool its start leads to (a node several conversations lead to is written into each).
/// </summary>
internal sealed class ImportBuilder(ImportOptions options)
{
    private readonly List<JsonObject> _nodes = new();
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, string> _messages = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _speakers = new(StringComparer.Ordinal);
    private readonly List<JsonObject> _variables = new();
    private readonly SortedDictionary<string, ImportFunction> _externals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _aliases = new(StringComparer.Ordinal);
    private int _counter;

    public ImportOptions Options { get; } = options;
    public ImportReport Report { get; } = new();

    public EngineRefusedException Syntax(string where, string message) => new(DialogueCodes.ImportSyntax, where + ": " + message);

    public EngineRefusedException Unsupported(string where, string message) => new(DialogueCodes.ImportUnsupported, where + ": " + message);

    public EngineRefusedException Function(string where, string message) => new(DialogueCodes.ImportFunction, where + ": " + message);

    /// <summary>A node id made of <paramref name="stem"/> and a number that is new in this document.</summary>
    public string NewId(string stem) => stem + "-" + (++_counter);

    public void Add(JsonObject node)
    {
        var id = node["id"]!.GetValue<string>();
        if (!_ids.Add(id)) throw new InvalidOperationException("the converter made node '" + id + "' twice");
        _nodes.Add(node);
    }

    /// <summary>Records the text of a line or answer under a new key of the given stem and returns the key.</summary>
    public string Message(string stem, string text)
    {
        if (text.Length > 1024) throw Unsupported(stem, "a text longer than 1024 characters (" + text.Length + ") cannot be a locale message");
        var key = stem + "." + (++_counter);
        _messages[key] = text;
        return key;
    }

    public void Speaker(string id) => _speakers.Add(id);

    public void Variable(JsonObject variable) => _variables.Add(variable);

    /// <summary>
    /// Declares the external fact an allowed function is read from. A fact two functions share must be declared the same way by both, or the conversion is refused: the host answers each fact once.
    /// </summary>
    public string External(ImportFunction function, string where)
    {
        if (_externals.TryGetValue(function.Fact, out var known) && (known.Kind != function.Kind || known.Minimum != function.Minimum || known.Maximum != function.Maximum))
            throw Function(where, "the fact '" + function.Fact + "' is declared twice with different types or ranges");
        _externals[function.Fact] = function;
        return "ext." + function.Fact;
    }

    /// <summary>The allowed function with this exact signature, or null.</summary>
    public ImportFunction? Allowed(string signature) => Options.Functions.FirstOrDefault(f => f.Signature == signature);

    /// <summary>Names a target the converter does not know yet (a jump forward): <c>@name</c> in a next field is replaced by the target set here.</summary>
    public void Alias(string name, string target) => _aliases[name] = target;

    private string Resolve(string target, string where)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (target.StartsWith('@'))
        {
            if (!seen.Add(target)) throw Syntax(where, "jumps lead back to themselves without any line, answer or command in between: " + string.Join(" > ", seen));
            target = _aliases.TryGetValue(target[1..], out var next) ? next : throw Syntax(where, "there is no target '" + target[1..] + "'");
        }
        return target;
    }

    /// <summary>The nodes a node leads to, by the fields that name them. A variant lists its options as plain ids, a choice or a branch as objects with a next.</summary>
    private static List<string> TargetsOf(JsonObject node)
    {
        var targets = new List<string>();
        foreach (var field in new[] { "next", "otherwise", "fallback", "exhausted" })
            if (node[field] is JsonValue value) targets.Add(value.GetValue<string>());
        foreach (var list in new[] { "options", "cases", "outcomes" })
            if (node[list] is JsonArray array)
                foreach (var item in array)
                {
                    if (item is JsonObject holder) { if (holder["next"] is JsonValue next) targets.Add(next.GetValue<string>()); }
                    else if (item is JsonValue plain) targets.Add(plain.GetValue<string>());
                }
        return targets;
    }

    private static void MapTargets(JsonObject node, Func<string, string> map)
    {
        foreach (var field in new[] { "next", "otherwise", "fallback", "exhausted" })
            if (node[field] is JsonValue value) node[field] = map(value.GetValue<string>());
        foreach (var list in new[] { "options", "cases", "outcomes" })
            if (node[list] is JsonArray array)
                for (var i = 0; i < array.Count; i++)
                {
                    if (array[i] is JsonObject holder) { if (holder["next"] is JsonValue next) holder["next"] = map(next.GetValue<string>()); }
                    else if (array[i] is JsonValue plain) array[i] = map(plain.GetValue<string>());
                }
    }

    /// <summary>
    /// Finishes the document: resolves jumps, gives each conversation the nodes its start leads to, drops nodes no conversation leads to, writes the document and the strings, and has the loader and
    /// the locale checks accept both.
    /// </summary>
    public ImportResult Build(IReadOnlyList<ConversationSpec> conversations, IReadOnlyList<ImportEntry> entries)
    {
        foreach (var node in _nodes) MapTargets(node, target => Resolve(target, "node " + node["id"]));
        var byId = _nodes.ToDictionary(n => n["id"]!.GetValue<string>(), StringComparer.Ordinal);
        var known = new HashSet<string>(conversations.Select(c => c.Id), StringComparer.Ordinal);
        var reachedEver = new HashSet<string>(StringComparer.Ordinal);
        var written = new JsonArray();
        foreach (var conversation in conversations)
        {
            var start = Resolve(conversation.Start, "start of " + conversation.Id);
            if (!byId.ContainsKey(start)) throw Syntax("start", "the start leads nowhere");
            var reached = new List<string> { start };
            var seen = new HashSet<string>(StringComparer.Ordinal) { start };
            for (var at = 0; at < reached.Count; at++)
            {
                var node = byId[reached[at]];
                foreach (var target in TargetsOf(node))
                {
                    if (!byId.ContainsKey(target)) throw Syntax("node", "leads to '" + target + "', which was never made");
                    if (seen.Add(target)) reached.Add(target);
                }
                if (node["kind"]?.GetValue<string>() == "call" && !known.Contains(node["conversation"]!.GetValue<string>()))
                    throw Syntax("node", "calls the conversation '" + node["conversation"] + "', which the document does not have");
            }
            var nodes = new JsonArray();
            foreach (var node in _nodes.Where(n => seen.Contains(n["id"]!.GetValue<string>()))) nodes.Add(node.DeepClone());
            reachedEver.UnionWith(seen);
            written.Add(new JsonObject { ["id"] = conversation.Id, ["start"] = start, ["nodes"] = nodes });
        }
        var lost = _nodes.Count(n => !reachedEver.Contains(n["id"]!.GetValue<string>()));
        if (lost > 0) Report.Add(ImportStatus.Dropped, "unreachable content", lost + " node(s) that nothing leads to after a jump, stop or choice were left out");

        var speakers = new JsonArray();
        foreach (var speaker in _speakers) speakers.Add(new JsonObject { ["id"] = speaker, ["portraits"] = new JsonArray(ImportOptions.Portrait) });
        var variables = new JsonArray();
        foreach (var variable in _variables) variables.Add(variable);
        var externals = new JsonArray();
        foreach (var function in _externals.Values)
        {
            var fact = new JsonObject { ["id"] = function.Fact, ["type"] = FactTypes.Name(function.Kind) };
            if (function.Kind == FactKind.Integer) { fact["min"] = function.Minimum; fact["max"] = function.Maximum; }
            externals.Add(fact);
        }
        var document = new JsonObject
        {
            ["format"] = DialogueLoader.Format,
            ["id"] = Options.DocumentId,
            ["variables"] = variables,
            ["externalFacts"] = externals,
            ["speakers"] = speakers,
            ["quests"] = new JsonArray(),
            ["conversations"] = written,
        };
        var text = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        var messages = new JsonObject();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var conversation in written.OfType<JsonObject>())
            foreach (var node in conversation["nodes"]!.AsArray().OfType<JsonObject>())
            {
                if (node["line"] is JsonValue line) used.Add(line.GetValue<string>());
                if (node["options"] is JsonArray answers) foreach (var answer in answers.OfType<JsonObject>()) used.Add(answer["line"]!.GetValue<string>());
            }
        foreach (var (key, value) in _messages.Where(m => used.Contains(m.Key))) messages[key] = value;
        var strings = new JsonObject { ["format"] = LocaleTable.Format, ["locale"] = Options.Locale, ["messages"] = messages }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        try
        {
            var set = DialogueLoader.Load(text, new Declared(_speakers));
            LocaleCoverage.Check(set, LocaleTable.Load(strings));
        }
        catch (EngineRefusedException refusal)
        {
            throw new EngineRefusedException(DialogueCodes.ImportInvalid, "the converted document is not accepted (" + refusal.Code + "): " + refusal.Message);
        }
        return new ImportResult(text, strings, _speakers.ToList(), Report) { Entries = entries, Functions = _externals.Values.ToList() };
    }

    private sealed class Declared(IReadOnlySet<string> ids) : ISpeakerCatalog
    {
        public bool HasSpeaker(string id) => ids.Contains(id);
    }

    /// <summary>A text for a message: braces that are not placeholders are written doubled, the way the locale tables read them.</summary>
    public static string EscapeBraces(string text) => text.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal);
}
