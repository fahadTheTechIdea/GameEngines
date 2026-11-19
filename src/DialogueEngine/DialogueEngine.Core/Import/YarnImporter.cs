using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DialogueEngine;

/// <summary>
/// Converts the text of a Yarn Spinner script (<c>.yarn</c>) into a dialogue document and its source-locale strings. What is converted, approximated and refused is the table
/// <see cref="ImportCapabilities.YarnSpinner"/>; anything outside it is refused with <see cref="DialogueCodes.ImportUnsupported"/> naming the line (a function that is not allowed gets
/// <see cref="DialogueCodes.ImportFunction"/>), and a script that is not Yarn gets <see cref="DialogueCodes.ImportSyntax"/>. Sources: https://docs.yarnspinner.dev/ (nodes, lines, options, jump,
/// detour, once, enum, if, set, declare, functions, commands; read 2026-10-28).
/// </summary>
public static class YarnImporter
{
    public static ImportResult Import(string yarn, ImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(yarn);
        ArgumentNullException.ThrowIfNull(options);
        return new YarnConversation(yarn, options).Run();
    }
}

internal sealed partial class YarnConversation(string text, ImportOptions options)
{
    private enum Mode { Main, Detour }

    private readonly record struct Src(int No, int Indent, string Text);

    private abstract record Stmt(int No)
    {
        /// <summary>The rolls the statement's conditions need, emitted just before it (a roll in a condition is made before the branch that reads it).</summary>
        public List<Stmt> Rolls { get; set; } = [];
    }
    private sealed record LineStmt(int No, string? Speaker, string Text, List<string> Tags, SortedDictionary<string, string> Args) : Stmt(No) { public string? Key { get; set; } }
    private sealed record EffectStmt(int No, JsonObject Effect) : Stmt(No);
    private sealed record JumpStmt(int No, string Title) : Stmt(No);
    private sealed record DetourStmt(int No, string Title) : Stmt(No);
    private sealed record ReturnStmt(int No) : Stmt(No);
    private sealed record StopStmt(int No) : Stmt(No);
    /// <summary>Sets a whole-number variable to a seeded roll from Lo to Hi, each value equally likely (dice, random_range).</summary>
    private sealed record RollIntStmt(int No, string Variable, long Lo, long Hi) : Stmt(No);
    /// <summary>Sets a true/false variable to true with the probability Num in Den, from a seeded roll (a random comparison in a condition).</summary>
    private sealed record RollBoolStmt(int No, string Variable, int Num, int Den) : Stmt(No);
    private sealed record IfStmt(int No, List<(JsonNode? When, List<Stmt> Body)> Branches) : Stmt(No);
    private sealed record OnceStmt(int No, JsonNode? When, string Variable, List<Stmt> Body, List<Stmt>? Else) : Stmt(No);
    private sealed record OptionStmt(int No, string Text, List<string> Tags, JsonNode? When, List<Stmt> Body, string? OnceVariable) { public string? Key { get; set; } }
    private sealed record OptionsStmt(int No, List<OptionStmt> Options) : Stmt(No);
    private sealed record YarnNode(string Title, int No, List<Src> Body);

    /// <summary>A variable of the script: its document type, its first value, and the enum it was declared with (null for none).</summary>
    private sealed record Declared(string Kind, object Initial, string? Enum);

    private readonly ImportBuilder _b = new(options);
    private readonly Dictionary<string, YarnNode> _nodes = new(StringComparer.Ordinal);
    private readonly List<string> _order = new();
    private readonly Dictionary<string, Declared> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _enums = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _visited = new(StringComparer.Ordinal);
    private readonly Dictionary<(Mode, string), string> _entries = new();
    private readonly Dictionary<string, List<Stmt>> _parsed = new(StringComparer.Ordinal);
    private int _onces;
    private int _rolls;
    private readonly List<Stmt> _pendingRolls = new();
    private string? _endNode;
    private string? _backNode;

    private static readonly Regex Command = new(@"^<<\s*([A-Za-z_][A-Za-z0-9_]*)\s*(.*?)\s*>>$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex Speaker = new(@"^([A-Za-z_][A-Za-z0-9_]*):\s+(\S.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex Title = new(@"^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TrailingTag = new(@"\s#(\S+)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex OptionCondition = new(@"\s*<<\s*(once\s+if|once|if)\b\s*(.*?)\s*>>\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    public ImportResult Run()
    {
        ReadNodes();
        if (_nodes.Count == 0) throw _b.Syntax("script", "no node was found (a node is title: Name, ---, its lines, ===)");
        foreach (var node in _nodes.Values) DeclareEnums(node);
        foreach (var node in _nodes.Values) Declare(node);

        var starts = Starts();
        // Which (mode, title) pairs the starts lead to. A jump continues in main mode (it clears Yarn's return stack, so what follows ends the dialogue), a detour in detour mode.
        var needed = new List<(Mode, string)>();
        var seen = new HashSet<(Mode, string)>();
        var queue = new Queue<(Mode, string)>();
        foreach (var start in starts) if (seen.Add((Mode.Main, start))) queue.Enqueue((Mode.Main, start));
        while (queue.Count > 0)
        {
            var (mode, title) = queue.Dequeue();
            needed.Add((mode, title));
            if (!_parsed.TryGetValue(title, out var statements)) _parsed[title] = statements = new Cursor(this, _nodes[title].Body).Parse();
            foreach (var (target, via) in Targets(statements))
            {
                if (!_nodes.ContainsKey(target)) throw _b.Syntax("node '" + title + "'", (via == Mode.Main ? "jumps" : "detours") + " to '" + target + "', which no node is titled");
                if (seen.Add((via, target))) queue.Enqueue((via, target));
            }
        }
        var reachedTitles = needed.Select(n => n.Item2).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var left = _order.Count(t => !reachedTitles.Contains(t));
        if (left > 0) _b.Report.Add(ImportStatus.Dropped, "nodes not reachable from the start", left + " node(s) that no jump from '" + starts[0] + "' leads to were left out");

        foreach (var title in _order.Where(reachedTitles.Contains))
            foreach (var mode in new[] { Mode.Main, Mode.Detour }.Where(m => seen.Contains((m, title))))
            {
                var entry = EmitTitle(title, mode);
                _b.Alias(AliasOf(mode, title), entry);
            }
        _b.Report.Add(ImportStatus.Converted, "nodes", "start at '" + string.Join("', '", starts) + "'");

        var conversations = new List<ConversationSpec>();
        var entries = new List<ImportEntry>();
        foreach (var start in starts)
        {
            var id = starts.Count == 1 ? ImportOptions.Conversation : ImportOptions.ConversationFor(start);
            conversations.Add(new ConversationSpec(id, "@" + AliasOf(Mode.Main, start)));
            entries.Add(new ImportEntry(start, id));
        }
        foreach (var title in _order.Where(t => seen.Contains((Mode.Detour, t)))) conversations.Add(new ConversationSpec(DetourConversation(title), "@" + AliasOf(Mode.Detour, title)));
        if (starts.Count > 1) _b.Report.Add(ImportStatus.Converted, "start nodes", starts.Count + " conversations, one per start node; a node entered through any start counts for all of them");
        return _b.Build(conversations, entries);
    }

    private static string AliasOf(Mode mode, string title) => (mode == Mode.Main ? "m:" : "d:") + title;

    private static string DetourConversation(string title) => "detour-" + title;

    /// <summary>The start nodes: the caller's list, every node nothing leads to, the one start the caller named, or the node titled Start (else the first).</summary>
    private List<string> Starts()
    {
        var chosen = new List<string>();
        if (options.Starts is { Count: > 0 } named) chosen.AddRange(named.Select(s => s.ToLowerInvariant()));
        else if (options.AllStarts)
        {
            var led = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in _order)
            {
                if (!_parsed.TryGetValue(node, out var statements)) _parsed[node] = statements = new Cursor(this, _nodes[node].Body).Parse();
                foreach (var (target, _) in Targets(statements)) if (target != node) led.Add(target);
            }
            chosen.AddRange(_order.Where(t => !led.Contains(t)));
            if (chosen.Count == 0) chosen.AddRange(_order);
        }
        else chosen.Add((options.Start ?? (_nodes.ContainsKey("start") ? "start" : _order[0])).ToLowerInvariant());
        foreach (var start in chosen)
            if (!_nodes.ContainsKey(start)) throw _b.Syntax("start", "there is no node titled '" + start + "'");
        if (chosen.Distinct(StringComparer.Ordinal).Count() != chosen.Count) throw _b.Syntax("start", "a start node is named twice");
        return chosen;
    }

    private string End()
    {
        if (_endNode is not null) return _endNode;
        _endNode = _b.NewId("end");
        _b.Add(new JsonObject { ["id"] = _endNode, ["kind"] = "end", ["result"] = "done" });
        return _endNode;
    }

    private string Back()
    {
        if (_backNode is not null) return _backNode;
        _backNode = _b.NewId("back");
        _b.Add(new JsonObject { ["id"] = _backNode, ["kind"] = "return", ["result"] = "back" });
        return _backNode;
    }

    private string Finish(Mode mode) => mode == Mode.Main ? End() : Back();

    /// <summary>Every node a block can go to, and in which mode: a jump continues in main mode, a detour in detour mode.</summary>
    private static IEnumerable<(string Title, Mode Mode)> Targets(IEnumerable<Stmt> statements)
    {
        foreach (var statement in statements)
            switch (statement)
            {
                case JumpStmt jump: yield return (jump.Title, Mode.Main); break;
                case DetourStmt detour: yield return (detour.Title, Mode.Detour); break;
                case IfStmt branch: foreach (var target in branch.Branches.SelectMany(b => Targets(b.Body))) yield return target; break;
                case OnceStmt once: foreach (var target in Targets(once.Body).Concat(once.Else is null ? [] : Targets(once.Else))) yield return target; break;
                case OptionsStmt group: foreach (var target in group.Options.SelectMany(o => Targets(o.Body))) yield return target; break;
            }
    }

    // ---- the file -----------------------------------------------------------------------------------------------------------------------

    private void ReadNodes()
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var i = 0;
        while (i < lines.Length)
        {
            if (lines[i].Trim().Length == 0 || lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal)) { i++; continue; }
            string? title = null;
            var titleLine = i + 1;
            for (; i < lines.Length && lines[i].Trim() != "---"; i++)
            {
                var header = lines[i].Trim();
                if (header.Length == 0 || header.StartsWith("//", StringComparison.Ordinal)) continue;
                if (header == "===") throw _b.Syntax("line " + (i + 1), "a node ends before its body starts (---)");
                var colon = header.IndexOf(':');
                if (colon <= 0) throw _b.Syntax("line " + (i + 1), "a header is key: value, not '" + header + "'");
                var key = header[..colon].Trim();
                if (key == "title") { titleLine = i + 1; title = header[(colon + 1)..].Trim(); }
                else _b.Report.Add(ImportStatus.Dropped, "headers other than title", "header '" + key + "' has no meaning in a dialogue document");
            }
            if (i >= lines.Length) throw _b.Syntax("line " + lines.Length, "a node header is not followed by ---");
            if (title is null) throw _b.Syntax("line " + (i + 1), "a node has no title header");
            if (!Title.IsMatch(title)) throw _b.Syntax("line " + titleLine, "the title '" + title + "' must start with a letter and use letters, digits and underscores");
            i++;
            var body = new List<Src>();
            for (; i < lines.Length && lines[i].Trim() != "==="; i++)
            {
                var raw = lines[i];
                var trimmed = raw.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                var indent = 0;
                foreach (var c in raw) { if (c == ' ') indent++; else if (c == '\t') indent += 4; else break; }
                body.Add(new Src(i + 1, indent, trimmed));
            }
            if (i >= lines.Length) throw _b.Syntax("line " + lines.Length, "node '" + title + "' is never closed with ===");
            i++;
            var id = title.ToLowerInvariant();
            if (!_nodes.TryAdd(id, new YarnNode(id, titleLine, body))) throw _b.Syntax("line " + titleLine, "two nodes are titled '" + title + "' (titles are compared without case)");
            _order.Add(id);
        }
    }

    // ---- declarations -------------------------------------------------------------------------------------------------------------------

    private static readonly Regex DeclareLine = new(@"^<<\s*declare\s+\$([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+?)(?:\s+as\s+(\w+))?\s*>>$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    /// <summary>
    /// <c>&lt;&lt;enum Name&gt;&gt;</c> with <c>&lt;&lt;case X&gt;&gt;</c> lines up to <c>&lt;&lt;endenum&gt;&gt;</c>, wherever in the script it is written: a variable declared with one of its cases is a text variable that may hold
    /// only the cases.
    /// </summary>
    private void DeclareEnums(YarnNode node)
    {
        List<string>? open = null;
        string? name = null;
        foreach (var line in node.Body)
        {
            var command = Command.Match(line.Text);
            if (!command.Success) continue;
            var word = command.Groups[1].Value;
            var rest = command.Groups[2].Value;
            if (word == "enum")
            {
                if (open is not null) throw _b.Syntax("line " + line.No, "an enum is declared inside another enum");
                if (!Title.IsMatch(rest)) throw _b.Syntax("line " + line.No, "an enum is <<enum Name>>");
                if (_enums.ContainsKey(rest)) throw _b.Syntax("line " + line.No, "the enum " + rest + " is declared twice");
                name = rest;
                open = new List<string>();
            }
            else if (word == "case")
            {
                if (open is null) throw _b.Syntax("line " + line.No, "<<case>> outside an enum");
                if (!Title.IsMatch(rest)) throw _b.Unsupported("line " + line.No, "an enum case is a plain name (found '" + rest + "'; a case with a value is not converted)");
                if (open.Contains(rest)) throw _b.Syntax("line " + line.No, "the case " + rest + " is written twice");
                open.Add(rest);
            }
            else if (word == "endenum")
            {
                if (open is null) throw _b.Syntax("line " + line.No, "<<endenum>> without <<enum>>");
                if (open.Count == 0) throw _b.Syntax("line " + line.No, "the enum " + name + " has no case");
                _enums[name!] = open;
                _b.Report.Add(ImportStatus.Converted, "enum", "the enum " + name + " became a text variable that holds only its cases");
                open = null;
            }
        }
        if (open is not null) throw _b.Syntax("line " + node.No, "the enum " + name + " is never closed with <<endenum>>");
    }

    private void Declare(YarnNode node)
    {
        foreach (var line in node.Body.Where(l => Regex.IsMatch(l.Text, @"^<<\s*declare\b", RegexOptions.CultureInvariant)))
        {
            var match = DeclareLine.Match(line.Text);
            if (!match.Success) throw _b.Syntax("line " + line.No, "a declaration is <<declare $name = value>>");
            var name = match.Groups[1].Value.ToLowerInvariant();
            var literal = match.Groups[2].Value;
            var annotation = match.Groups[3].Success ? match.Groups[3].Value : null;
            if (_variables.ContainsKey(name)) throw _b.Syntax("line " + line.No, "variable $" + match.Groups[1].Value + " is declared twice (names are compared without case)");
            JsonObject variable;
            var enumCase = Regex.Match(literal, @"^(\w+)\.(\w+)$", RegexOptions.CultureInvariant);
            if (enumCase.Success && _enums.TryGetValue(enumCase.Groups[1].Value, out var cases))
            {
                if (annotation is not null && annotation != enumCase.Groups[1].Value) throw _b.Syntax("line " + line.No, "an enum value cannot be '" + annotation + "'");
                if (!cases.Contains(enumCase.Groups[2].Value)) throw _b.Syntax("line " + line.No, "the enum " + enumCase.Groups[1].Value + " has no case " + enumCase.Groups[2].Value);
                _variables[name] = new Declared("string", enumCase.Groups[2].Value, enumCase.Groups[1].Value);
                variable = new JsonObject { ["id"] = name, ["type"] = "string", ["initial"] = enumCase.Groups[2].Value, ["allowed"] = new JsonArray(cases.Select(c => (JsonNode)c).ToArray()) };
            }
            else if (literal is "true" or "false")
            {
                if (annotation is not (null or "bool" or "boolean")) throw _b.Syntax("line " + line.No, "a true/false value cannot be '" + annotation + "'");
                _variables[name] = new Declared("bool", literal == "true", null);
                variable = new JsonObject { ["id"] = name, ["type"] = "bool", ["initial"] = literal == "true" };
            }
            else if (long.TryParse(literal, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
            {
                if (annotation is not (null or "number")) throw _b.Syntax("line " + line.No, "a number cannot be '" + annotation + "'");
                _variables[name] = new Declared("int", number, null);
                variable = new JsonObject { ["id"] = name, ["type"] = "int", ["initial"] = number, ["min"] = -1_000_000_000, ["max"] = 1_000_000_000 };
            }
            else if (literal.Length >= 2 && literal[0] == '"' && literal[^1] == '"' && !literal[1..^1].Contains('"', StringComparison.Ordinal))
            {
                if (annotation is not (null or "string")) throw _b.Syntax("line " + line.No, "a text cannot be '" + annotation + "'");
                _variables[name] = new Declared("string", literal[1..^1], null);
                variable = new JsonObject { ["id"] = name, ["type"] = "string", ["initial"] = literal[1..^1] };
            }
            else throw _b.Unsupported("line " + line.No, "declared value '" + literal + "' is not a whole number, true/false, an enum case or a plain text (decimals and expressions are not converted)");
            _b.Variable(variable);
            _b.Report.Add(ImportStatus.Converted, "declare", "$" + match.Groups[1].Value + " as " + variable["type"]);
        }
    }

    // ---- emitting -----------------------------------------------------------------------------------------------------------------------

    /// <summary>The first node of a title in a mode, made once. A title entered by a node that counts visits (<c>visited()</c>) starts with the add that counts this one.</summary>
    private string EmitTitle(string title, Mode mode)
    {
        if (_entries.TryGetValue((mode, title), out var known)) return known;
        var block = _parsed[title];
        if (_visited.Contains(title))
        {
            block = new List<Stmt>(block);
            block.Insert(0, new EffectStmt(0, new JsonObject { ["add"] = VisitVariable(title), ["by"] = 1 }));
        }
        var entry = Emit(block, Finish(mode), title, mode);
        _entries[(mode, title)] = entry;
        return entry;
    }

    private static string VisitVariable(string title) => "visited-" + title.Replace('_', '-');

    /// <summary>Emits a block backwards so each node knows what follows it, and returns the node (or <c>@jump</c>) the block starts at.</summary>
    private string Emit(List<Stmt> block, string after, string title, Mode mode)
    {
        var cut = block.FindIndex(s => s is JumpStmt or StopStmt or ReturnStmt);
        if (cut >= 0 && cut < block.Count - 1)
        {
            _b.Report.Add(ImportStatus.Dropped, "statements after a jump or stop", (block.Count - 1 - cut) + " statement(s) after line " + block[cut].No + " can never run");
            block = block.GetRange(0, cut + 1);
        }
        JsonObject? open = null;
        for (var i = block.Count - 1; i >= 0; i--)
        {
            var statement = block[i];
            if (statement is EffectStmt effect)
            {
                if (open is not null && ((JsonArray)open["effects"]!).Count < 16)
                {
                    ((JsonArray)open["effects"]!).Insert(0, effect.Effect);
                    continue;
                }
                var id = _b.NewId(title + "-set");
                open = new JsonObject { ["id"] = id, ["kind"] = "effects", ["effects"] = new JsonArray(effect.Effect), ["next"] = after };
                _b.Add(open);
                after = id;
                continue;
            }
            open = null;
            after = EmitOne(statement, after, title, mode);
        }
        return after;
    }

    private string EmitOne(Stmt statement, string after, string title, Mode mode)
    {
        switch (statement)
        {
            case JumpStmt jump:
                _b.Report.Add(ImportStatus.Converted, "jump", "a jump is an edge of the one conversation");
                return "@" + AliasOf(Mode.Main, jump.Title);
            case DetourStmt detour:
            {
                _b.Report.Add(ImportStatus.Converted, "detour", "a call of the conversation made from the detoured node, which comes back to the next statement");
                var id = _b.NewId(title + "-detour");
                _b.Add(new JsonObject { ["id"] = id, ["kind"] = "call", ["conversation"] = DetourConversation(detour.Title), ["next"] = after });
                return id;
            }
            case ReturnStmt:
            {
                _b.Report.Add(ImportStatus.Converted, "return", mode == Mode.Main ? "outside a detour it stops the dialogue" : "ends the detour and goes back to where it was called");
                return Finish(mode);
            }
            case RollIntStmt roll:
                return EmitRollInt(roll, after);
            case RollBoolStmt roll:
                return EmitRollBool(roll, after);
            case StopStmt:
            {
                _b.Report.Add(ImportStatus.Converted, "stop", "ends the conversation");
                var id = _b.NewId(title + "-stop");
                _b.Add(new JsonObject { ["id"] = id, ["kind"] = "end", ["result"] = "done" });
                return id;
            }
            case LineStmt line:
            {
                var speaker = line.Speaker ?? ImportOptions.Narrator;
                _b.Speaker(speaker);
                var id = _b.NewId(title);
                var node = new JsonObject
                {
                    ["id"] = id, ["kind"] = "line", ["speaker"] = speaker, ["portrait"] = ImportOptions.Portrait,
                    ["line"] = line.Key ??= _b.Message("yarn." + title, line.Text), ["next"] = after,
                };
                if (line.Args.Count > 0)
                {
                    var args = new JsonObject();
                    foreach (var (name, fact) in line.Args) args[name] = fact;
                    node["args"] = args;
                }
                if (line.Tags.Count > 0) node["tags"] = new JsonArray(line.Tags.Select(t => (JsonNode)t).ToArray());
                _b.Add(node);
                _b.Report.Add(ImportStatus.Converted, "line", "a line is a line node; its text is in the strings table");
                return id;
            }
            case IfStmt branch:
            {
                if (branch.Branches.Count(b => b.When is not null) > 16) throw _b.Unsupported("line " + branch.No, "more than 16 if/elseif branches");
                var cases = new JsonArray();
                var otherwise = after;
                foreach (var (when, body) in branch.Branches)
                {
                    var entry = Emit(body, after, title, mode);
                    if (when is null) otherwise = entry;
                    else cases.Add(new JsonObject { ["when"] = when.DeepClone(), ["next"] = entry });
                }
                var id = _b.NewId(title + "-if");
                _b.Add(new JsonObject { ["id"] = id, ["kind"] = "branch", ["cases"] = cases, ["otherwise"] = otherwise });
                _b.Report.Add(ImportStatus.Converted, "if / elseif / else", "a branch node");
                return id;
            }
            case OnceStmt once:
            {
                var body = Emit(once.Body, after, title, mode);
                var otherwise = once.Else is null ? after : Emit(once.Else, after, title, mode);
                var mark = _b.NewId(title + "-once");
                _b.Add(new JsonObject { ["id"] = mark, ["kind"] = "effects", ["effects"] = new JsonArray(new JsonObject { ["set"] = once.Variable, ["value"] = true }), ["next"] = body });
                var fresh = new JsonObject { ["fact"] = "var." + once.Variable, ["op"] = "eq", ["value"] = false };
                JsonNode when = once.When is null ? fresh : new JsonObject { ["all"] = new JsonArray(fresh, once.When.DeepClone()) };
                var id = _b.NewId(title + "-onceif");
                _b.Add(new JsonObject { ["id"] = id, ["kind"] = "branch", ["cases"] = new JsonArray(new JsonObject { ["when"] = when, ["next"] = mark }), ["otherwise"] = otherwise });
                _b.Report.Add(ImportStatus.Converted, "once", "a branch on a true/false variable of the document that is set when the content first runs");
                return id;
            }
            case OptionsStmt group:
            {
                if (group.Options.Count > 16) throw _b.Unsupported("line " + group.No, "more than 16 options in one group");
                var options = new JsonArray();
                var n = 0;
                foreach (var option in group.Options)
                {
                    var entry = Emit(option.Body, after, title, mode);
                    var answer = new JsonObject { ["id"] = "o" + (++n), ["line"] = option.Key ??= _b.Message("yarn." + title + ".option", option.Text) };
                    JsonNode? when = option.When?.DeepClone();
                    if (option.OnceVariable is not null)
                    {
                        var fresh = new JsonObject { ["fact"] = "var." + option.OnceVariable, ["op"] = "eq", ["value"] = false };
                        when = when is null ? fresh : new JsonObject { ["all"] = new JsonArray(fresh, when) };
                        answer["effects"] = new JsonArray(new JsonObject { ["set"] = option.OnceVariable, ["value"] = true });
                        _b.Report.Add(ImportStatus.Converted, "once (option)", "an answer that can be taken once: it is hidden after it was taken");
                    }
                    if (when is not null)
                    {
                        answer["when"] = when;
                        answer["unavailable"] = "hide";
                    }
                    if (option.Tags.Count > 0) answer["tags"] = new JsonArray(option.Tags.Select(t => (JsonNode)t).ToArray());
                    answer["next"] = entry;
                    options.Add(answer);
                }
                var id = _b.NewId(title + "-choice");
                var choice = new JsonObject { ["id"] = id, ["kind"] = "choice", ["pick"] = "player", ["options"] = options };
                if (group.Options.Any(o => o.When is not null || o.OnceVariable is not null))
                {
                    choice["fallback"] = after;
                    _b.Report.Add(ImportStatus.Approximated, "option condition", "a false condition hides the option (Yarn marks it unavailable); when none is left the conversation goes on after the group");
                }
                _b.Add(choice);
                _b.Report.Add(ImportStatus.Converted, "options", "a choice node; an option's body follows it, then the flow continues after the group");
                return id;
            }
        }
        throw new InvalidOperationException("unknown statement " + statement.GetType().Name);
    }
}
