using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DialogueEngine;

internal sealed partial class YarnConversation
{
    /// <summary>What closes a block being read: nothing (the end of the node or the indentation of an option body), an <c>if</c> or a <c>once</c>.</summary>
    private enum Closer { None, If, Once }

    /// <summary>Reads the body of one node, line by line, into statements. Options take the lines indented under them; if/elseif/else/endif and once/else/endonce take what lies between.</summary>
    private sealed class Cursor(YarnConversation owner, List<Src> lines)
    {
        private int _i;

        public List<Stmt> Parse()
        {
            var statements = Sequence(int.MinValue, Closer.None);
            owner.RequireNoPendingRolls();
            if (_i < lines.Count) throw owner._b.Syntax("line " + lines[_i].No, "'" + lines[_i].Text + "' has nothing to close");
            return statements;
        }

        private static bool Closes(Closer closer, string word)
        {
            switch (closer)
            {
                case Closer.If: return word is "elseif" or "else" or "endif";
                case Closer.Once: return word is "else" or "endonce";
                case Closer.None: return false;
            }
            throw new InvalidOperationException("unknown closer " + closer);
        }

        private List<Stmt> Sequence(int floor, Closer closer)
        {
            var list = new List<Stmt>();
            while (_i < lines.Count)
            {
                var line = lines[_i];
                if (line.Indent < floor) break;
                var command = Command.Match(line.Text);
                var word = command.Success ? command.Groups[1].Value : "";
                if (command.Success && word is "elseif" or "else" or "endif" or "endonce")
                {
                    if (!Closes(closer, word)) throw owner._b.Syntax("line " + line.No, "<<" + word + ">> has no " + (word is "endonce" or "else" && closer == Closer.None ? "<<if>> or <<once>>" : word is "endonce" ? "<<once>>" : "<<if>>"));
                    break;
                }
                if (line.Text.StartsWith("->", StringComparison.Ordinal)) { var group = Options(line.Indent); list.AddRange(group.Rolls); list.Add(group); continue; }
                _i++;
                var statement = command.Success ? Cmd(line, word, command.Groups[2].Value) : Plain(line);
                if (statement is null) continue;
                list.AddRange(statement.Rolls);
                list.Add(statement);
            }
            return list;
        }

        private OptionsStmt Options(int indent)
        {
            var first = lines[_i].No;
            var options = new List<OptionStmt>();
            var rolls = new List<Stmt>();
            while (_i < lines.Count && lines[_i].Indent == indent && lines[_i].Text.StartsWith("->", StringComparison.Ordinal))
            {
                var line = lines[_i++];
                var raw = line.Text[2..].Trim();
                var tags = owner.Tags(ref raw, line.No);
                JsonNode? when = null;
                string? onceVariable = null;
                var condition = OptionCondition.Match(raw);
                if (condition.Success)
                {
                    var kind = Regex.Replace(condition.Groups[1].Value, @"\s+", " ");
                    if (kind != "once") { when = owner.Condition(condition.Groups[2].Value, line.No); rolls.AddRange(owner.TakeRolls()); }
                    else if (condition.Groups[2].Value.Length > 0) throw owner._b.Syntax("line " + line.No, "<<once>> takes no condition; <<once if ...>> does");
                    if (kind.StartsWith("once", StringComparison.Ordinal)) onceVariable = owner.NewOnce();
                    raw = raw[..condition.Index];
                }
                if (raw.Length == 0) throw owner._b.Syntax("line " + line.No, "an option has no text");
                var shown = owner.Interpolate(raw, line.No, allowArgs: false, out _);
                owner.NoteMarkup(raw);
                options.Add(new OptionStmt(line.No, shown, tags, when, Sequence(indent + 1, Closer.None), onceVariable));
            }
            return new OptionsStmt(first, options) { Rolls = rolls };
        }

        private Stmt? Plain(Src line)
        {
            var raw = line.Text;
            // A line may end with #tags and with <<once>> / <<once if ...>>, in either order.
            var tags = new List<string>();
            var once = false;
            string? onceWhen = null;
            while (true)
            {
                var more = owner.Tags(ref raw, line.No);
                tags.InsertRange(0, more.Where(t => !tags.Contains(t)));
                var tail = Regex.Match(raw, @"\s*<<\s*once(?:\s+if\s+(.+?))?\s*>>\s*$", RegexOptions.CultureInvariant | RegexOptions.Singleline);
                if (tail.Success && !once)
                {
                    once = true;
                    onceWhen = tail.Groups[1].Success ? tail.Groups[1].Value : null;
                    raw = raw[..tail.Index];
                    continue;
                }
                if (more.Count == 0) break;
            }
            if (raw.Contains("<<", StringComparison.Ordinal)) throw owner._b.Unsupported("line " + line.No, "a command inside a line of text");
            string? speaker = null;
            var named = Speaker.Match(raw);
            if (named.Success)
            {
                speaker = named.Groups[1].Value.ToLowerInvariant();
                raw = named.Groups[2].Value;
            }
            if (speaker is null) owner._b.Report.Add(ImportStatus.Approximated, "line without a speaker", "spoken by '" + ImportOptions.Narrator + "'");
            var shown = owner.Interpolate(raw, line.No, allowArgs: true, out var args);
            owner.NoteMarkup(raw);
            if (tags.Count > 8) throw owner._b.Unsupported("line " + line.No, "more than 8 tags on one line");
            var statement = new LineStmt(line.No, speaker, shown, tags, args);
            if (!once) return statement;
            var onceCondition = onceWhen is null ? null : owner.Condition(onceWhen, line.No);
            return new OnceStmt(line.No, onceCondition, owner.NewOnce(), [statement], null) { Rolls = owner.TakeRolls() };
        }

        private Stmt? Cmd(Src line, string name, string rest)
        {
            switch (name)
            {
                case "declare": return null;   // read before any statement, for the whole file
                case "enum": case "case": case "endenum": return null;   // read before any statement too (DeclareEnums)
                case "stop":
                    if (rest.Length > 0) throw owner._b.Syntax("line " + line.No, "<<stop>> takes nothing");
                    return new StopStmt(line.No);
                case "return":
                    if (rest.Length > 0) throw owner._b.Syntax("line " + line.No, "<<return>> takes nothing");
                    return new ReturnStmt(line.No);
                case "jump":
                    if (!Title.IsMatch(rest)) throw owner._b.Unsupported("line " + line.No, "a jump must name a node title (found '" + rest + "'; jumping to an expression is not converted)");
                    return new JumpStmt(line.No, rest.ToLowerInvariant());
                case "detour":
                    if (!Title.IsMatch(rest)) throw owner._b.Unsupported("line " + line.No, "a detour must name a node title (found '" + rest + "'; detouring to an expression is not converted)");
                    return new DetourStmt(line.No, rest.ToLowerInvariant());
                case "set": return owner.SetStatement(rest, line.No);
                case "if": return If(line, rest);
                case "once": return Once(line, rest);
                case "call":
                    throw owner._b.Function("line " + line.No, "<<call " + rest + ">> runs a function for what it does; an allowed function only answers a fact the host supplies, so it cannot be called for its effect");
            }
            return new EffectStmt(line.No, owner.CommandSignal(name, rest, line.No));
        }

        private Stmt If(Src opening, string condition)
        {
            var branches = new List<(JsonNode?, List<Stmt>)>();
            var when = owner.Condition(condition, opening.No);
            var rolls = owner.TakeRolls();
            while (true)
            {
                var body = Sequence(int.MinValue, Closer.If);
                branches.Add((when, body));
                if (_i >= lines.Count) throw owner._b.Syntax("line " + opening.No, "<<if>> is never closed with <<endif>>");
                var line = lines[_i++];
                var command = Command.Match(line.Text);
                var name = command.Success ? command.Groups[1].Value : "";
                if (name == "endif") break;
                if (name == "elseif") { when = owner.Condition(command.Groups[2].Value, line.No); rolls.AddRange(owner.TakeRolls()); continue; }
                if (name == "else")
                {
                    var rest = Sequence(int.MinValue, Closer.If);
                    branches.Add((null, rest));
                    if (_i >= lines.Count || Command.Match(lines[_i].Text) is not { Success: true } close || close.Groups[1].Value != "endif")
                        throw owner._b.Syntax("line " + (_i < lines.Count ? lines[_i].No : opening.No), "<<else>> must be followed by <<endif>>");
                    _i++;
                    break;
                }
                throw owner._b.Syntax("line " + line.No, "unexpected '" + line.Text + "' in an if");
            }
            return new IfStmt(opening.No, branches) { Rolls = rolls };
        }

        /// <summary><c>&lt;&lt;once&gt;&gt;</c> or <c>&lt;&lt;once if cond&gt;&gt;</c>, a body, an optional <c>&lt;&lt;else&gt;&gt;</c>, <c>&lt;&lt;endonce&gt;&gt;</c>.</summary>
        private Stmt Once(Src opening, string rest)
        {
            var rolls = new List<Stmt>();
            JsonNode? when = null;
            if (rest.Length > 0)
            {
                var m = Regex.Match(rest, @"^if\s+(.+)$", RegexOptions.CultureInvariant | RegexOptions.Singleline);
                if (!m.Success) throw owner._b.Syntax("line " + opening.No, "<<once>> is followed by nothing or by if and a condition");
                when = owner.Condition(m.Groups[1].Value, opening.No);
                rolls = owner.TakeRolls();
            }
            var variable = owner.NewOnce();
            var body = Sequence(int.MinValue, Closer.Once);
            if (_i >= lines.Count) throw owner._b.Syntax("line " + opening.No, "<<once>> is never closed with <<endonce>>");
            var close = lines[_i++];
            var name = Command.Match(close.Text) is { Success: true } c ? c.Groups[1].Value : "";
            List<Stmt>? otherwise = null;
            if (name == "else")
            {
                otherwise = Sequence(int.MinValue, Closer.Once);
                if (_i >= lines.Count || Command.Match(lines[_i].Text) is not { Success: true } end || end.Groups[1].Value != "endonce")
                    throw owner._b.Syntax("line " + (_i < lines.Count ? lines[_i].No : opening.No), "<<else>> must be followed by <<endonce>>");
                _i++;
            }
            else if (name != "endonce") throw owner._b.Syntax("line " + close.No, "unexpected '" + close.Text + "' in a once");
            return new OnceStmt(opening.No, when, variable, body, otherwise) { Rolls = rolls };
        }
    }

    /// <summary>A true/false variable of the document that remembers that a <c>once</c> has run.</summary>
    private string NewOnce()
    {
        var name = "once-" + (++_onces);
        _b.Variable(new JsonObject { ["id"] = name, ["type"] = "bool", ["initial"] = false });
        return name;
    }

    // ---- commands -----------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A command the game acts on: <c>&lt;&lt;name arg arg&gt;&gt;</c> becomes a signal of that name. An argument is a whole number, true/false, text in double quotes, a bare word (text, as Yarn Spinner passes it) or
    /// <c>{$variable}</c>, whose value is read when the signal is raised. Yarn Spinner converts each argument to the type the game's command function takes; here it keeps the type it was written with.
    /// </summary>
    private JsonObject CommandSignal(string name, string rest, int no)
    {
        var signal = new JsonObject { ["signal"] = name.ToLowerInvariant().Replace('_', '-') };
        if (rest.Length == 0)
        {
            _b.Report.Add(ImportStatus.Approximated, "command without arguments", "<<" + name + ">> became the signal '" + name.ToLowerInvariant().Replace('_', '-') + "'");
            return signal;
        }
        var args = new JsonArray();
        for (var i = 0; i < rest.Length;)
        {
            if (char.IsWhiteSpace(rest[i])) { i++; continue; }
            if (rest[i] == '"')
            {
                var close = rest.IndexOf('"', i + 1);
                if (close < 0) throw _b.Syntax("line " + no, "a quoted argument is never closed");
                args.Add(new JsonObject { ["text"] = rest[(i + 1)..close] });
                i = close + 1;
                continue;
            }
            if (rest[i] == '{')
            {
                var close = rest.IndexOf('}', i);
                if (close < 0) throw _b.Syntax("line " + no, "an opening brace is never closed");
                var inner = rest[(i + 1)..close].Trim();
                var variable = Regex.Match(inner, @"^\$([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
                if (!variable.Success) throw _b.Unsupported("line " + no, "the command argument {" + inner + "} is not a plain variable (expressions and function calls in arguments are not converted)");
                var variableName = variable.Groups[1].Value.ToLowerInvariant();
                if (!_variables.ContainsKey(variableName)) throw _b.Syntax("line " + no, "$" + variable.Groups[1].Value + " is not declared (<<declare $name = value>>)");
                args.Add(new JsonObject { ["var"] = variableName });
                i = close + 1;
                continue;
            }
            var end = i;
            while (end < rest.Length && !char.IsWhiteSpace(rest[end])) end++;
            var word = rest[i..end];
            i = end;
            if (word is "true" or "false") args.Add(new JsonObject { ["bool"] = word == "true" });
            else if (long.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) args.Add(new JsonObject { ["int"] = number });
            else if (Regex.IsMatch(word, @"^-?\d+\.\d+$", RegexOptions.CultureInvariant)) throw _b.Unsupported("line " + no, "the decimal argument " + word + " (only whole numbers are converted)");
            else if (word.StartsWith('$')) throw _b.Unsupported("line " + no, "the argument " + word + " written without braces; write {" + word + "} to pass a variable");
            else if (word.Length > 256) throw _b.Unsupported("line " + no, "an argument longer than 256 characters");
            else args.Add(new JsonObject { ["text"] = word });
        }
        if (args.Count > 8) throw _b.Unsupported("line " + no, "more than 8 command arguments");
        signal["args"] = args;
        _b.Report.Add(ImportStatus.Converted, "command with arguments", "<<" + name + " ...>> became a signal that carries its arguments");
        return signal;
    }

    // ---- text ---------------------------------------------------------------------------------------------------------------------------

    /// <summary>Takes the <c>#tags</c> off the end of a line. A <c>#line:</c> tag is the id Yarn Spinner's own localisation uses; it is dropped.</summary>
    private List<string> Tags(ref string raw, int no)
    {
        var tags = new List<string>();
        while (true)
        {
            var match = TrailingTag.Match(raw);
            if (!match.Success) break;
            raw = raw[..match.Index];
            var tag = match.Groups[1].Value;
            if (tag.StartsWith("line:", StringComparison.Ordinal)) { _b.Report.Add(ImportStatus.Dropped, "#line: tags", "Yarn Spinner's own line ids are not carried; keys are made new"); continue; }
            tag = tag.ToLowerInvariant();
            if (tag.Length > 128 || !tag.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '.' or '_'))
                throw _b.Unsupported("line " + no, "the tag '#" + tag + "' uses characters a dialogue tag cannot (a-z, 0-9, hyphen, dot, underscore)");
            if (!tags.Contains(tag)) tags.Insert(0, tag);
            _b.Report.Add(ImportStatus.Converted, "#tags", "kept as tags of the line");
        }
        if (tags.Count > 8) throw _b.Unsupported("line " + no, "more than 8 tags on one line");
        return tags;
    }

    private void NoteMarkup(string raw)
    {
        if (Regex.IsMatch(raw, @"\[/?\w+[^\]]*\]", RegexOptions.CultureInvariant)) _b.Report.Add(ImportStatus.Approximated, "markup", "[tag] markup is kept in the text for the game's text renderer");
    }

    /// <summary>
    /// The text of a line for the strings table: <c>{$name}</c> and <c>{allowed_function()}</c> become the placeholder <c>{name}</c> (a line only), other braces are doubled, a backslash makes the next
    /// character itself.
    /// </summary>
    private string Interpolate(string raw, int no, bool allowArgs, out SortedDictionary<string, string> args)
    {
        args = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var text = new StringBuilder();
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c == '\\' && i + 1 < raw.Length) { text.Append(ImportBuilder.EscapeBraces(raw[++i].ToString())); continue; }
            if (c == '{')
            {
                var close = raw.IndexOf('}', i);
                if (close < 0) throw _b.Syntax("line " + no, "an opening brace is never closed");
                var inner = raw[(i + 1)..close].Trim();
                var match = Regex.Match(inner, @"^\$([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
                if (match.Success)
                {
                    if (!allowArgs) throw _b.Unsupported("line " + no, "an option's text cannot show a variable (a dialogue answer takes no placeholder)");
                    var name = match.Groups[1].Value.ToLowerInvariant();
                    if (!_variables.ContainsKey(name)) throw _b.Syntax("line " + no, "$" + match.Groups[1].Value + " is not declared (<<declare $name = value>>)");
                    args[name] = "var." + name;
                    text.Append('{').Append(name).Append('}');
                    _b.Report.Add(ImportStatus.Converted, "{$variable} in text", "became a placeholder filled from the variable when the line is reached");
                    i = close;
                    continue;
                }
                var call = Regex.Match(inner, @"^([A-Za-z_][A-Za-z0-9_]*)\s*\(.*\)$", RegexOptions.CultureInvariant | RegexOptions.Singleline);
                if (!call.Success) throw _b.Unsupported("line " + no, "'{" + inner + "}' is not a plain variable or an allowed function call; expressions in text are not converted");
                if (!allowArgs) throw _b.Unsupported("line " + no, "an option's text cannot show a value (a dialogue answer takes no placeholder)");
                var operand = CallOperand(inner, no);
                if (operand.Roll is not null) throw _b.Unsupported("line " + no, "a random value cannot be shown in text");
                if (operand.Fact is null) throw _b.Unsupported("line " + no, "the true/false answer of visited(...) cannot be shown in text");
                var fact = operand.Fact;
                var placeholder = fact[(fact.IndexOf('.') + 1)..];
                args[placeholder] = fact;
                text.Append('{').Append(placeholder).Append('}');
                i = close;
                continue;
            }
            text.Append(ImportBuilder.EscapeBraces(c.ToString()));
        }
        var result = text.ToString().Trim();
        if (result.Length == 0) throw _b.Syntax("line " + no, "a line has no text");
        return result;
    }

    // ---- set ----------------------------------------------------------------------------------------------------------------------------

    private JsonObject Set(string rest, int no)
    {
        var match = Regex.Match(rest, @"^\$([A-Za-z_][A-Za-z0-9_]*)\s*(to|=|\+=|-=)\s*(.+)$", RegexOptions.CultureInvariant | RegexOptions.Singleline);
        if (!match.Success) throw _b.Syntax("line " + no, "a set is <<set $name to value>>");
        var name = match.Groups[1].Value.ToLowerInvariant();
        if (!_variables.TryGetValue(name, out var variable)) throw _b.Syntax("line " + no, "$" + match.Groups[1].Value + " is not declared (<<declare $name = value>>)");
        var op = match.Groups[2].Value;
        var expression = match.Groups[3].Value.Trim();
        _b.Report.Add(ImportStatus.Converted, "set", "became a set or add effect");
        if (op is "+=" or "-=")
        {
            var by = IntegerOf(expression, no, variable.Kind, name);
            return new JsonObject { ["add"] = name, ["by"] = op == "-=" ? -by : by };
        }
        // $x = $x + n and $x = $x - n are an add.
        var step = Regex.Match(expression, @"^\$" + Regex.Escape(match.Groups[1].Value) + @"\s*([+-])\s*(\d+)$", RegexOptions.CultureInvariant);
        if (step.Success)
        {
            if (variable.Kind != "int") throw _b.Syntax("line " + no, "$" + name + " is not a number");
            var by = long.Parse(step.Groups[2].Value, CultureInfo.InvariantCulture);
            return new JsonObject { ["add"] = name, ["by"] = step.Groups[1].Value == "-" ? -by : by };
        }
        if (variable.Enum is not null)
        {
            var enumValue = Regex.Match(expression, @"^(?:(\w+))?\.(\w+)$", RegexOptions.CultureInvariant);
            if (!enumValue.Success) throw _b.Unsupported("line " + no, "only a case of the enum " + variable.Enum + " can be set on $" + name + " (found '" + expression + "')");
            if (enumValue.Groups[1].Success && enumValue.Groups[1].Value != variable.Enum) throw _b.Syntax("line " + no, "$" + name + " holds the enum " + variable.Enum + ", not " + enumValue.Groups[1].Value);
            if (!_enums[variable.Enum].Contains(enumValue.Groups[2].Value)) throw _b.Syntax("line " + no, "the enum " + variable.Enum + " has no case " + enumValue.Groups[2].Value);
            return new JsonObject { ["set"] = name, ["value"] = enumValue.Groups[2].Value };
        }
        switch (variable.Kind)
        {
            case "int": return new JsonObject { ["set"] = name, ["value"] = IntegerOf(expression, no, variable.Kind, name) };
            case "bool":
                if (expression is not ("true" or "false")) throw _b.Unsupported("line " + no, "only true or false can be set on a true/false variable (found '" + expression + "')");
                return new JsonObject { ["set"] = name, ["value"] = expression == "true" };
            case "string":
                if (expression.Length < 2 || expression[0] != '"' || expression[^1] != '"' || expression[1..^1].Contains('"', StringComparison.Ordinal))
                    throw _b.Unsupported("line " + no, "only a plain quoted text can be set on a text variable (found '" + expression + "')");
                return new JsonObject { ["set"] = name, ["value"] = expression[1..^1] };
        }
        throw new InvalidOperationException("unknown variable kind " + variable.Kind);
    }

    private long IntegerOf(string expression, int no, string kind, string name)
    {
        if (kind != "int") throw _b.Syntax("line " + no, "$" + name + " is not a number");
        if (!long.TryParse(expression, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
            throw _b.Unsupported("line " + no, "only a whole number can be used here (found '" + expression + "'; arithmetic with other variables or functions is not converted)");
        return number;
    }
}
