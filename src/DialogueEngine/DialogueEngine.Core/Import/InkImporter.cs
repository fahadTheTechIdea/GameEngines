using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DialogueEngine;

/// <summary>
/// Converts the compiled JSON of an Ink story (what inklecate writes; the runtime format of https://github.com/inkle/ink/blob/master/Documentation/ink_JSON_runtime_format.md, read 2026-10-28)
/// into a dialogue document and its source-locale strings. The converter walks the story the way the Ink runtime does, with a symbolic evaluation stack, and turns what a dialogue document can
/// say into nodes: text, tags, glue, diverts to knots, stitches, gathers and choice branches, conditional diverts and conditional choices, global and temporary variables of whole numbers, text and
/// true/false, the sequence, cycle, once and shuffle forms, visit counts, tunnels (a conversation that returns), threads that only offer choices, lists (one true/false variable per member) and
/// external functions that the caller allows. Everything else is refused with <see cref="DialogueCodes.ImportUnsupported"/> naming the construct. The table is <see cref="ImportCapabilities.InkJson"/>.
/// </summary>
/// <remarks>
/// The published format document has no example of how sequences, conditional text, lists or glue are compiled; those shapes are written from the format's command descriptions and from the
/// behaviour of the compiler as far as known, and no real compiler output was available (inklecate is not installed), so they are tested on hand-written JSON only.
/// </remarks>
public static class InkImporter
{
    /// <summary>The runtime format versions the subset has been written against (the spec's current is 21).</summary>
    public const int FirstVersion = 19;
    public const int LastVersion = 21;

    public static ImportResult Import(string json, ImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(options);
        return new InkConversation(json, options).Run();
    }
}

internal sealed partial class InkConversation(string json, ImportOptions options)
{
    /// <summary>A container of the story: its items in order and its named content. A knot, stitch, choice branch or gather is a named one.</summary>
    private sealed class Box(Box? parent, string? name, int index)
    {
        public Box? Parent { get; } = parent;
        public string? Name { get; set; } = name;

        /// <summary>Where this container sits among its parent's items; -1 for named content, which does not flow back into the parent.</summary>
        public int Index { get; } = index;
        public List<JsonElement> Items { get; } = new();
        public Dictionary<int, Box> Children { get; } = new();
        public Dictionary<string, Box> Named { get; } = new(StringComparer.Ordinal);

        /// <summary>The number variable that counts entries into this container, when a <c>CNT?</c> reads it.</summary>
        public string? CountVariable { get; set; }
    }

    /// <summary>A place in the story: an item of a container. Entering a container is its item 0.</summary>
    private readonly record struct Pos(Box Box, int Index);

    private enum Mode { Main, Tunnel }

    private abstract record Flow;
    private sealed record Say(string Text, List<string> Tags, SortedDictionary<string, string> Args, bool Glue) : Flow { public bool Glued { get; set; } = Glue; }
    private sealed record Do(JsonObject Effect) : Flow;
    private sealed record When(JsonNode Condition, Pos Target) : Flow;
    private sealed record Go(Pos Target) : Flow;
    private sealed record Tunnel(Pos Target) : Flow;
    /// <summary>Glue with no output before it in its run of content: it joins the line said just before the place was reached, which only the diverting content knows.</summary>
    private sealed record LeadGlue : Flow;
    private sealed record Leave : Flow;
    private sealed record Stop : Flow;
    private sealed record Pick(List<Offer> Offers) : Flow;
    private sealed record Offer(string Text, int Flags, Box Branch, JsonNode? Condition);
    private sealed record Sequence(string Mode, int Branches, List<Pos> Targets, Pos Join) : Flow;

    private readonly ImportBuilder _b = new(options);
    private readonly Dictionary<(Pos, Mode, bool), string> _entries = new();
    private readonly Dictionary<(Pos, Mode), List<Flow>> _flows = new();
    private readonly Dictionary<Pos, string> _tunnels = new();
    private readonly List<ConversationSpec> _specs = new();
    private readonly Dictionary<string, VarInfo> _globals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, VarInfo> _temps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, long>> _lists = new(StringComparer.Ordinal);
    private readonly HashSet<string> _variableIds = new(StringComparer.Ordinal);
    private string? _end;
    private string? _back;
    private int _aliases;
    private int _taken;
    private int _counters;

    public ImportResult Run()
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 }); }
        catch (JsonException exception) { throw _b.Syntax("story", "not JSON: " + exception.Message); }
        using (document)
        {
            var top = document.RootElement;
            if (top.ValueKind != JsonValueKind.Object || !top.TryGetProperty("inkVersion", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number)
                || !top.TryGetProperty("root", out var rootElement) || rootElement.ValueKind != JsonValueKind.Array)
                throw _b.Syntax("story", "a compiled Ink story is an object with inkVersion and root");
            if (number is < InkImporter.FirstVersion or > InkImporter.LastVersion)
                throw _b.Unsupported("story", "inkVersion " + number + " (the subset is written against " + InkImporter.FirstVersion + " to " + InkImporter.LastVersion + ")");
            foreach (var property in top.EnumerateObject())
                if (property.Name is not ("inkVersion" or "root" or "listDefs")) throw _b.Syntax("story", "unknown top-level field '" + property.Name + "'");
            if (top.TryGetProperty("listDefs", out var lists)) ReadListDefinitions(lists);

            var root = Read(rootElement, null, null, -1);
            MarkCounted(root);
            if (root.Named.TryGetValue("global decl", out var globals)) ReadGlobals(globals);
            var start = new Pos(root, 0);
            if (options.Start is not null) start = new Pos(Find(root, options.Start), 0);
            _b.Report.Add(ImportStatus.Converted, "story", "starts at " + (options.Start ?? "the story's own start"));
            var entry = EntryAt(start, Mode.Main);
            _specs.Insert(0, new ConversationSpec(ImportOptions.Conversation, entry));
            return _b.Build(_specs, [new ImportEntry(options.Start ?? "start", ImportOptions.Conversation)]);
        }
    }

    private Box Find(Box root, string path)
    {
        var at = root;
        foreach (var part in path.Split('.'))
            at = at.Named.TryGetValue(part, out var next) ? next : throw _b.Syntax("start", "the story has no '" + path + "'");
        return at;
    }

    // ---- reading the containers ---------------------------------------------------------------------------------------------------------

    private Box Read(JsonElement array, Box? parent, string? name, int index, int depth = 0)
    {
        if (depth > 32) throw _b.Unsupported("story", "containers nested deeper than 32");
        var box = new Box(parent, name, index);
        var count = array.GetArrayLength();
        var position = 0;
        foreach (var item in array.EnumerateArray())
        {
            position++;
            var last = position == count;
            if (last && item.ValueKind == JsonValueKind.Object && !IsInstruction(item))
            {
                foreach (var property in item.EnumerateObject())
                {
                    if (property.Name == "#f") continue;
                    if (property.Name == "#n") { box.Name ??= property.Value.GetString(); continue; }
                    if (property.Value.ValueKind != JsonValueKind.Array) throw _b.Unsupported("story", "named content '" + property.Name + "' that is not a container");
                    box.Named[property.Name] = Read(property.Value, box, property.Name, -1, depth + 1);
                }
                continue;
            }
            if (last && item.ValueKind == JsonValueKind.Null) continue;
            if (item.ValueKind == JsonValueKind.Array) box.Children[box.Items.Count] = Read(item, box, null, box.Items.Count, depth + 1);
            box.Items.Add(item);
        }
        foreach (var (_, child) in box.Children)
            if (child.Name is not null) box.Named.TryAdd(child.Name, child);
        return box;
    }

    /// <summary>Whether an object at the end of a container is a runtime instruction (a divert or choice point) and not the dictionary of named content.</summary>
    private static bool IsInstruction(JsonElement item) =>
        item.EnumerateObject().Any(p => p.Name is "->" or "*" or "^->" or "temp=" or "VAR=" or "VAR?" or "CNT?" or "f()" or "->t->" or "x()" or "^var" or "#" or "<-" or "list");

    // ---- paths --------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The place a path names. A path that ends in a container is that container's start; one that ends in a number that is not a container is that item of its container (the rejoin point of a
    /// sequence or a conditional). Relative paths start with a dot and count <c>^</c> up from the object they are written in, as the runtime does.
    /// </summary>
    private Pos ResolvePos(string path, Box holder)
    {
        var parts = path.Split('.');
        Box at;
        var onObject = false;
        var from = 0;
        if (path.StartsWith('.'))
        {
            at = holder;
            onObject = true;
            from = 1;
        }
        else at = Root(holder);
        for (var i = from; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part == "^")
            {
                if (onObject) onObject = false;
                else at = at.Parent ?? throw _b.Syntax("path " + path, "goes above the story");
                continue;
            }
            if (onObject) throw _b.Unsupported("path " + path, "a path into the object it is written in");
            if (int.TryParse(part, out var index))
            {
                if (at.Children.TryGetValue(index, out var child)) { at = child; continue; }
                if (i == parts.Length - 1 && index >= 0 && index < at.Items.Count) return new Pos(at, index);
                throw _b.Syntax("path " + path, "points outside its container (item " + index + ")");
            }
            at = at.Named.TryGetValue(part, out var named) ? named : throw _b.Syntax("path " + path, "there is no content named '" + part + "'");
        }
        return new Pos(at, 0);
    }

    private Box ResolveBox(string path, Box holder)
    {
        var pos = ResolvePos(path, holder);
        if (pos.Index != 0) throw _b.Unsupported("path " + path, "points into the middle of a container, not at a container");
        return pos.Box;
    }

    private static Box Root(Box box)
    {
        while (box.Parent is not null) box = box.Parent;
        return box;
    }

    /// <summary>A container that ends by returning through the temporary <c>$r</c> (<c>{"->":"$r","var":true}</c>) is text the story shows in place, not a place to go to.</summary>
    private static bool IsInline(Box box)
    {
        for (var i = box.Items.Count - 1; i >= 0; i--)
        {
            var item = box.Items[i];
            if (item.ValueKind == JsonValueKind.Null) continue;
            return item.ValueKind == JsonValueKind.Object && item.TryGetProperty("->", out var to) && to.ValueKind == JsonValueKind.String && to.GetString() == "$r"
                && item.TryGetProperty("var", out var isVar) && isVar.ValueKind == JsonValueKind.True;
        }
        return false;
    }

    private string InlineText(Box box)
    {
        var text = new StringBuilder();
        foreach (var item in box.Items)
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString()!.StartsWith('^')) text.Append(Text(item.GetString()!));
            else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("->", out var to) && to.GetString() == "$r") break;
            else throw _b.Unsupported("story", "content other than plain text inside the text of a choice");
        }
        return text.ToString();
    }

    /// <summary>The text of a text item: after the leading caret everything is text (a literal caret is written doubled, so it already reads right).</summary>
    private static string Text(string item) => item[1..];

    /// <summary>Containers that a <c>CNT?</c> reads get a number variable, which the entry into them adds one to.</summary>
    private void MarkCounted(Box box)
    {
        for (var i = 0; i < box.Items.Count; i++)
        {
            var item = box.Items[i];
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("CNT?", out var path) && path.ValueKind == JsonValueKind.String)
            {
                var target = ResolveBox(path.GetString()!, box);
                if (target.CountVariable is null)
                {
                    target.CountVariable = "seen-" + (++_counters);
                    _b.Variable(new JsonObject { ["id"] = target.CountVariable, ["type"] = "int", ["initial"] = 0, ["min"] = 0, ["max"] = 1_000_000_000 });
                    _b.Report.Add(ImportStatus.Approximated, "visit counts (CNT?)", "a number variable of the document that is added to when the container is entered at its start");
                }
            }
            if (box.Children.TryGetValue(i, out var child)) MarkCounted(child);
        }
        foreach (var (_, child) in box.Named) if (child.Index < 0) MarkCounted(child);
    }
}
