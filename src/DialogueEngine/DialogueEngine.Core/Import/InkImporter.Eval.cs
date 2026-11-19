using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ScenarioEngine.Contracts;

namespace DialogueEngine;

internal sealed partial class InkConversation
{
    /// <summary>A variable of the story and the variable (or, for a list, the variables) of the document that stand for it.</summary>
    private sealed class VarInfo(string name, string id, string kind)
    {
        public string Name { get; } = name;
        public string Id { get; } = id;

        /// <summary><c>int</c>, <c>bool</c>, <c>string</c> or <c>list</c>.</summary>
        public string Kind { get; } = kind;

        /// <summary>For a list: the document variable of each member, by qualified member name (<c>list.item</c>).</summary>
        public SortedDictionary<string, string> Members { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>What the symbolic evaluation stack holds: the expression the runtime would have computed, kept as a tree until it is used.</summary>
    private abstract record Ast;
    private sealed record Const(object Value) : Ast;
    private sealed record VarRef(VarInfo Variable) : Ast;
    private sealed record Seen(Box Target) : Ast;
    private sealed record Ext(string Fact, FactKind Kind) : Ast;
    private sealed record Members(List<string> Names) : Ast;
    private sealed record Visit : Ast;
    private sealed record Binary(string Operator, Ast Left, Ast Right) : Ast;
    private sealed record Unary(string Operator, Ast Operand) : Ast;
    private sealed record SeqIndex(string Mode, int Count) : Ast;
    private sealed record Marker : Ast;

    // ---- lists --------------------------------------------------------------------------------------------------------------------------

    private void ReadListDefinitions(JsonElement lists)
    {
        if (lists.ValueKind != JsonValueKind.Object) throw _b.Syntax("story", "listDefs must be an object");
        foreach (var list in lists.EnumerateObject())
        {
            if (list.Value.ValueKind != JsonValueKind.Object) throw _b.Syntax("story", "the list " + list.Name + " must be an object of member to number");
            var members = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var member in list.Value.EnumerateObject())
            {
                if (member.Value.ValueKind != JsonValueKind.Number || !member.Value.TryGetInt64(out var value)) throw _b.Syntax("story", "the list member " + list.Name + "." + member.Name + " must be a whole number");
                members[member.Name] = value;
            }
            _lists[list.Name] = members;
        }
    }

    // ---- variables ----------------------------------------------------------------------------------------------------------------------

    private const char TempSeparator = '\u0001';

    /// <summary>The temporaries a name can belong to are those of the knot or stitch the content is in. A knot is a named container of the story's root; a stitch is a container the author named inside a knot (the compiler's own c-N, g-N, b and s names are not).</summary>
    private static string ScopeOf(Box box)
    {
        for (var at = box; at is not null; at = at.Parent)
        {
            if (at.Name is null || at.Parent is null) continue;
            if (at.Parent.Parent is null) return at.Name;
            if (at.Parent.Parent.Parent is null && !CompilerName.IsMatch(at.Name)) return at.Parent.Name + "." + at.Name;
        }
        return "";
    }

    private static string Enclosing(string scope)
    {
        var dot = scope.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? "" : scope[..dot];
    }

    private static readonly System.Text.RegularExpressions.Regex CompilerName = new(@"^((c|g)-\d+|b|s\d*|\$r\d*|\d+)$", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private string NewVariableId(string stem, string name)
    {
        var id = stem + name.ToLowerInvariant();
        foreach (var c in id)
            if (!(c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c is '-' or '.' or '_')) throw _b.Unsupported("story", "the name '" + name + "' cannot be an id of a dialogue document");
        if (id.Length > 120) throw _b.Unsupported("story", "the name '" + name + "' is longer than an id may be");
        if (!_variableIds.Add(id)) throw _b.Syntax("story", "the names '" + name + "' and another differ only in case, which a dialogue document does not tell apart");
        return id;
    }

    private static string KindOf(object value) => value switch
    {
        long => "int",
        bool => "bool",
        string => "string",
        _ => null,
    } ?? throw new InvalidOperationException("a constant of type " + value.GetType().Name);

    /// <summary>Declares the variable a story variable stands for, once, with the value it first has.</summary>
    private VarInfo Declare(string name, Ast value, bool global, string scope = "")
    {
        var table = global ? _globals : _temps;
        var key = global ? name : scope + TempSeparator + name;
        if (table.TryGetValue(key, out var known)) return known;
        var stem = global ? "" : "t-" + (scope.Length == 0 ? "" : scope.ToLowerInvariant() + "-");
        VarInfo info;
        if (value is Const constant)
        {
            var kind = KindOf(constant.Value);
            info = new VarInfo(name, NewVariableId(stem, name), kind);
            var variable = new JsonObject { ["id"] = info.Id, ["type"] = kind };
            switch (constant.Value)
            {
                case long number:
                    if (number < -1_000_000_000 || number > 1_000_000_000) throw _b.Unsupported("story", "the whole number " + number + " is outside -1000000000 to 1000000000");
                    variable["initial"] = number; variable["min"] = -1_000_000_000; variable["max"] = 1_000_000_000;
                    break;
                case bool flag: variable["initial"] = flag; break;
                case string text:
                    if (text.Length > 256) throw _b.Unsupported("story", "a text longer than 256 characters in a variable");
                    variable["initial"] = text;
                    break;
            }
            _b.Variable(variable);
        }
        else if (value is Members list)
        {
            info = new VarInfo(name, NewVariableId(stem, name), "list");
            var origins = list.Names.Select(n => n[..n.IndexOf('.')]).Distinct(StringComparer.Ordinal).ToList();
            if (origins.Count != 1) throw _b.Unsupported("story", "the list variable " + name + " starts with members of " + (origins.Count == 0 ? "no list (an empty list has no origin in the compiled story)" : "more than one list"));
            foreach (var member in _lists[origins[0]].Keys.OrderBy(m => m, StringComparer.Ordinal))
            {
                var qualified = origins[0] + "." + member;
                var id = NewVariableId(info.Id + "-", member);
                info.Members[qualified] = id;
                _b.Variable(new JsonObject { ["id"] = id, ["type"] = "bool", ["initial"] = list.Names.Contains(qualified) });
            }
            _b.Report.Add(ImportStatus.Approximated, "lists (LIST)", "one true/false variable per member of the list; a list value is the set of members that are true");
        }
        else throw _b.Unsupported("story", "the variable " + name + " starts with a value that is not a whole number, text, true/false or a list (" + Describe(value) + ")");
        if (!global) _b.Report.Add(ImportStatus.Approximated, "temp variables", "one variable of the document for each knot or stitch that declares the name, set again each time its declaration runs (Ink drops a temporary when its knot ends)");
        table[key] = info;
        _b.Report.Add(ImportStatus.Converted, "variables", "a variable of the document with the type of its first value");
        return info;
    }

    /// <summary>The variable a name stands for where it is read: a temporary of the stitch or knot it is in (a stitch also sees its knot's), else a global.</summary>
    private VarInfo Lookup(string name, string scope)
    {
        for (var at = scope; ; at = Enclosing(at))
        {
            if (_temps.TryGetValue(at + TempSeparator + name, out var temp)) return temp;
            if (at.Length == 0) break;
        }
        if (_globals.TryGetValue(name, out var global)) return global;
        throw _b.Syntax("story", "the variable '" + name + "' is read before it is declared");
    }

    /// <summary>The <c>global decl</c> container: the first value of every VAR, run once before the story, in the same stack language as the rest.</summary>
    private void ReadGlobals(Box globals)
    {
        var scan = new Scan { Declaring = true };
        for (var i = 0; i < globals.Items.Count; i++)
        {
            var item = globals.Items[i];
            if (item.ValueKind == JsonValueKind.String)
            {
                var word = item.GetString()!;
                if (word == "ev") { scan.Evaluating = true; continue; }
                if (word == "/ev") { scan.Evaluating = false; continue; }
                if (word == "end" || word == "done") break;
            }
            if (!scan.Evaluating) throw _b.Unsupported("story", "content other than declarations in the global declarations");
            Evaluate(globals, ref i, scan);
        }
    }

    // ---- the stack language ---------------------------------------------------------------------------------------------------------------

    /// <summary>Runs one item of an evaluation block (the caller has seen <c>ev</c>); <paramref name="index"/> may move past items the instruction used.</summary>
    private void Evaluate(Box box, ref int index, Scan scan)
    {
        var item = box.Items[index];
        switch (item.ValueKind)
        {
            case JsonValueKind.Number:
                if (!item.TryGetInt64(out var number)) throw _b.Unsupported("story", "the decimal number " + item.GetRawText() + " (only whole numbers are converted)");
                scan.Stack.Add(new Const(number));
                return;
            case JsonValueKind.True: scan.Stack.Add(new Const(true)); return;
            case JsonValueKind.False: scan.Stack.Add(new Const(false)); return;
            case JsonValueKind.String:
                EvalWord(box, ref index, item.GetString()!, scan);
                return;
            case JsonValueKind.Object:
                Instruction(box, index, item, scan);
                return;
            case JsonValueKind.Array:
                if (box.Children.TryGetValue(index, out var label) && label.Items.Count == 0) return;   // [{"#n":"$r1"}], the return label of an inline text
                throw _b.Unsupported("story", "a container inside an evaluation");
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return;
        }
    }

    private Ast Pop(Scan scan, string what)
    {
        if (scan.Stack.Count == 0) throw _b.Syntax("story", what + " has nothing to work on (the evaluation stack is empty)");
        var top = scan.Stack[^1];
        scan.Stack.RemoveAt(scan.Stack.Count - 1);
        return top;
    }

    /// <summary>The words of the stack language: the operators, <c>str</c>, <c>out</c>, <c>du</c>, <c>pop</c>, <c>visit</c>, <c>seq</c> and the commands that are not understood.</summary>
    private void EvalWord(Box box, ref int index, string word, Scan scan)
    {
        switch (word)
        {
            case "str":
            {
                var text = new StringBuilder();
                var at = index + 1;
                for (; at < box.Items.Count; at++)
                {
                    var part = box.Items[at];
                    if (part.ValueKind == JsonValueKind.String && part.GetString() == "/str") break;
                    if (part.ValueKind == JsonValueKind.String && part.GetString() == "/ev") throw _b.Syntax("story", "a str is never closed with /str");
                    if (part.ValueKind == JsonValueKind.String && part.GetString()!.StartsWith('^')) { text.Append(Text(part.GetString()!)); continue; }
                    if (part.ValueKind == JsonValueKind.Object && part.EnumerateObject().Count() == 1 && part.TryGetProperty("->", out var to))
                    {
                        var target = ResolveBox(to.GetString()!, box);
                        if (!IsInline(target)) throw _b.Unsupported("story", "a divert inside a text to something that is not text");
                        text.Append(InlineText(target));
                        continue;
                    }
                    if (part.ValueKind == JsonValueKind.Object && part.EnumerateObject().Count() == 1 && (part.TryGetProperty("^->", out _) || part.TryGetProperty("temp=", out _))) continue;
                    if (part.ValueKind == JsonValueKind.Array && box.Children.TryGetValue(at, out var label) && label.Items.Count == 0) continue;
                    throw _b.Unsupported("story", "content other than plain text inside a text value (values printed inside choice text are not converted)");
                }
                if (at >= box.Items.Count) throw _b.Syntax("story", "a str is never closed with /str");
                scan.Stack.Add(new Const(text.ToString()));
                index = at;
                return;
            }
            case "out":
                Output(Pop(scan, "out"), scan);
                return;
            case "pop":
                if (scan.Stack.Count > 0) scan.Stack.RemoveAt(scan.Stack.Count - 1);   // a branch of a sequence pops the index its parent left; here the stack is the branch's own
                return;
            case "du":
                scan.Stack.Add(scan.Stack.Count > 0 ? scan.Stack[^1] : throw _b.Syntax("story", "du has nothing to duplicate"));
                return;
            case "visit": scan.Stack.Add(new Visit()); return;
            case "nop": return;   // the point a shuffle once / stopping skips to; the dispatch's own nop is read outside the evaluation
            case "seq":
            {
                var count = Pop(scan, "seq");
                var visit = Pop(scan, "seq");
                if (count is not Const { Value: long n }) throw _b.Unsupported("story", "a shuffle whose number of branches is not fixed");
                if (visit is Visit)
                {
                    scan.Stack.Add(new SeqIndex("shuffle", (int)n));
                    return;
                }
                // shuffle once and shuffle stopping: the position is MIN(visit, last), the shuffle is skipped when the position already is the last (the divert read in Instruction), and only the branches before the last are shuffled.
                if (visit is SeqIndex { Mode: "stopping" } capped && scan.ShuffleSkip && capped.Count == n)
                {
                    scan.ShuffleSkip = false;
                    scan.Stack.Add(new SeqIndex("shuffle-stopping", (int)n));
                    return;
                }
                throw _b.Unsupported("story", "a shuffle that is neither a plain {~a|b} nor a shuffle once or stopping in the shape the compiler is understood to write");
            }
            case "+": case "-": case "*": case "/": case "%": case "==": case "!=": case ">": case "<": case ">=": case "<=": case "&&": case "||": case "?": case "!?": case "MIN": case "MAX":
            {
                var right = Pop(scan, word);
                var left = Pop(scan, word);
                if (word is "MIN" or "%" && left is Visit && right is Const { Value: long limit })
                {
                    scan.Stack.Add(new SeqIndex(word == "MIN" ? "stopping" : "cycle", (int)limit));
                    return;
                }
                scan.Stack.Add(new Binary(word, left, right));
                return;
            }
            case "!": case "_":
                scan.Stack.Add(new Unary(word, Pop(scan, word)));
                return;
            case "turn": case "turns": case "choiceCnt": case "readc": case "rnd": case "srnd": case "POW": case "INT": case "FLOAT": case "FLOOR": case "CEIL": case "L^": case "LIST_VALUE": case "LIST_COUNT": case "LIST_MIN": case "LIST_MAX": case "LIST_ALL": case "LIST_INVERT": case "LIST_RANDOM": case "^":
                throw _b.Unsupported("story", "the command '" + word + "' (turn numbers, choice counts, random numbers, decimals and list functions are not converted)");
        }
        throw _b.Unsupported("story", "'" + word + "' in an evaluation (it is not converted)");
    }

    private void Instruction(Box box, int index, JsonElement item, Scan scan)
    {
        var names = item.EnumerateObject().Select(p => p.Name).ToList();
        if (names.Count == 1 && names[0] == "VAR?") { scan.Stack.Add(new VarRef(Lookup(item.GetProperty("VAR?").GetString()!, ScopeOf(box)))); return; }
        if (names.Count == 1 && names[0] == "CNT?")
        {
            var target = ResolveBox(item.GetProperty("CNT?").GetString()!, box);
            scan.Stack.Add(new Seen(target));
            return;
        }
        if (names.Count == 1 && names[0] == "^->") { scan.Stack.Add(new Marker()); return; }   // a divert target pushed for an inline return
        if (names.Contains("list"))
        {
            var members = new List<string>();
            foreach (var member in item.GetProperty("list").EnumerateObject()) members.Add(member.Name);
            scan.Stack.Add(new Members(members));
            return;
        }
        if (names.Contains("temp=") || names.Contains("VAR="))
        {
            var temporary = names.Contains("temp=");
            var name = item.GetProperty(temporary ? "temp=" : "VAR=").GetString()!;
            foreach (var other in names) if (other is not ("temp=" or "VAR=" or "re")) throw _b.Unsupported("story", "an assignment with '" + other + "'");
            var value = Pop(scan, "an assignment");
            if (value is Marker) return;   // the inline return label ($r)
            Assign(name, value, temporary, names.Contains("re"), scan, ScopeOf(box));
            return;
        }
        if (names.Contains("x()"))
        {
            var arguments = new List<object>();
            var count = item.TryGetProperty("exArgs", out var exArgs) && exArgs.TryGetInt32(out var n) ? n : 0;
            for (var k = 0; k < count; k++)
            {
                if (Pop(scan, "an external function call") is not Const { Value: var value }) throw _b.Function("story", "an argument of an external function is not a fixed value (arguments must be text, whole numbers or true/false)");
                arguments.Insert(0, value);
            }
            var function = item.GetProperty("x()").GetString()!;
            var signature = ImportFunction.Canonical(function, arguments);
            var allowed = _b.Allowed(signature) ?? throw _b.Function("story", "the external function " + signature + " is not on the list of allowed functions (--function '" + signature + "=fact:type'); a document can read only facts the host supplies");
            scan.Stack.Add(new Ext(_b.External(allowed, "story"), allowed.Kind));
            _b.Report.Add(ImportStatus.Converted, "external functions (allowed)", "an allowed external function became the external fact the host supplies");
            return;
        }
        if (names.Count == 2 && names.Contains("->") && names.Contains("c"))
        {
            // The skip of a shuffle once / stopping: when the position (MIN of the visit count and the last branch) already is the last branch, leave the shuffle out.
            if (Pop(scan, "a conditional divert") is Binary { Operator: "==", Left: SeqIndex { Mode: "stopping" } capped, Right: Const { Value: long last } } && capped.Count == last)
            {
                scan.ShuffleSkip = true;
                return;
            }
            throw _b.Unsupported("story", "a conditional divert inside an evaluation that is not the skip of a shuffle once or stopping");
        }
        if (names.Contains("f()")) throw _b.Unsupported("story", "the Ink function " + item.GetProperty("f()").GetString() + " (a function computes a value; only an allowed external function can be read)");
        throw _b.Unsupported("story", "the instruction {" + string.Join(", ", names.Select(n => "\"" + n + "\"")) + "} in an evaluation");
    }

    // ---- output ---------------------------------------------------------------------------------------------------------------------------

    /// <summary>A value printed with <c>out</c>: a constant becomes text, a whole-number or text variable becomes a placeholder filled when the line is reached.</summary>
    private void Output(Ast value, Scan scan)
    {
        switch (value)
        {
            case Const { Value: string text }: scan.Text.Append(text); return;
            case Const { Value: long number }: scan.Text.Append(number.ToString(CultureInfo.InvariantCulture)); return;
            case VarRef { Variable: { Kind: "int" or "string" } variable }:
                scan.Args[variable.Id] = "var." + variable.Id;
                scan.Text.Append('{').Append(variable.Id).Append('}');
                _b.Report.Add(ImportStatus.Converted, "printing a variable", "became a placeholder filled from the variable when the line is reached");
                return;
            case Ext { Kind: FactKind.Integer or FactKind.String } external:
            {
                var placeholder = external.Fact["ext.".Length..];
                scan.Args[placeholder] = external.Fact;
                scan.Text.Append('{').Append(placeholder).Append('}');
                return;
            }
        }
        throw _b.Unsupported("story", "printing " + Describe(value) + " (only constants and whole-number or text variables are converted; true/false prints differently in Ink)");
    }

    // ---- assignments ------------------------------------------------------------------------------------------------------------------------

    private void Assign(string name, Ast value, bool temporary, bool reassign, Scan scan, string scope)
    {
        if (scan.Declaring)
        {
            Declare(name, value, global: true);
            return;
        }
        var variable = temporary && !reassign
            ? Declare(name, value is Const or Members ? value : throw _b.Unsupported("story", "the temporary " + name + " starts with " + Describe(value)), global: false, scope)
            : Lookup(name, scope);
        foreach (var effect in AssignmentEffects(variable, value)) scan.Flows.Add(new Do(effect));
        _b.Report.Add(ImportStatus.Converted, "assignments", "a set or add effect on the variable");
    }

    private IEnumerable<JsonObject> AssignmentEffects(VarInfo variable, Ast value)
    {
        switch (variable.Kind)
        {
            case "int":
                if (value is Const { Value: long number })
                {
                    if (number < -1_000_000_000 || number > 1_000_000_000) throw _b.Unsupported("story", "the whole number " + number + " is outside -1000000000 to 1000000000");
                    yield return new JsonObject { ["set"] = variable.Id, ["value"] = number };
                    yield break;
                }
                if (value is Binary { Operator: "+" or "-", Left: VarRef { Variable: var same }, Right: Const { Value: long by } } step && same == variable)
                {
                    yield return new JsonObject { ["add"] = variable.Id, ["by"] = step.Operator == "-" ? -by : by };
                    yield break;
                }
                break;
            case "bool":
                if (value is Const { Value: bool flag }) { yield return new JsonObject { ["set"] = variable.Id, ["value"] = flag }; yield break; }
                if (value is Const { Value: long bit } && bit is 0 or 1) { yield return new JsonObject { ["set"] = variable.Id, ["value"] = bit == 1 }; yield break; }
                break;
            case "string":
                if (value is Const { Value: string text })
                {
                    if (text.Length > 256) throw _b.Unsupported("story", "a text longer than 256 characters in a variable");
                    yield return new JsonObject { ["set"] = variable.Id, ["value"] = text };
                    yield break;
                }
                break;
            case "list":
                if (value is Members all)
                {
                    foreach (var (qualified, id) in variable.Members) yield return new JsonObject { ["set"] = id, ["value"] = all.Names.Contains(qualified) };
                    yield break;
                }
                if (value is Binary { Operator: "+" or "-", Left: VarRef { Variable: var self }, Right: Members some } change && self == variable)
                {
                    foreach (var qualified in some.Names)
                        yield return new JsonObject { ["set"] = variable.Members.TryGetValue(qualified, out var id) ? id : throw _b.Syntax("story", qualified + " is not a member of the list " + variable.Name), ["value"] = change.Operator == "+" };
                    yield break;
                }
                break;
        }
        throw _b.Unsupported("story", "assigning " + Describe(value) + " to " + variable.Name + " (a variable is set to a constant, a whole number is added to or taken from, a list gets members added or taken away)");
    }

    // ---- conditions -------------------------------------------------------------------------------------------------------------------------

    private static string Describe(Ast value) => value switch
    {
        Const { Value: var constant } => "the value " + constant,
        VarRef variable => "the variable " + variable.Variable.Name,
        Seen => "a visit count",
        Ext external => "the external fact " + external.Fact,
        Members members => "the list value " + string.Join(", ", members.Names),
        Visit => "a visit count of the current container",
        Binary binary => "the expression " + binary.Operator,
        Unary unary => "the expression " + unary.Operator,
        SeqIndex index => "a " + index.Mode + " position",
        Marker => "a divert target",
        _ => null,
    } ?? throw new InvalidOperationException("unknown expression " + value.GetType().Name);

    /// <summary>A stack value used as a condition, in the condition grammar of the document; refuses what the grammar cannot say.</summary>
    private JsonNode Condition(Ast value)
    {
        switch (value)
        {
            case VarRef { Variable: { Kind: "bool" } flag }: return Compare("var." + flag.Id, "eq", true);
            case VarRef { Variable: { Kind: "int" } number }: return Compare("var." + number.Id, "ne", 0L);
            case Seen seen: return Compare("var." + seen.Target.CountVariable, "ne", 0L);
            case Ext { Kind: FactKind.Boolean } external: return Compare(external.Fact, "eq", true);
            case Ext { Kind: FactKind.Integer } external: return Compare(external.Fact, "ne", 0L);
            case Unary { Operator: "!" } not: return new JsonObject { ["not"] = Condition(not.Operand) };
            case Binary { Operator: "&&" } and: return new JsonObject { ["all"] = new JsonArray(Condition(and.Left), Condition(and.Right)) };
            case Binary { Operator: "||" } or: return new JsonObject { ["any"] = new JsonArray(Condition(or.Left), Condition(or.Right)) };
            case Binary { Operator: "?" or "!?", Left: VarRef { Variable: { Kind: "list" } list }, Right: Members members } has:
            {
                var parts = new JsonArray();
                foreach (var qualified in members.Names)
                    parts.Add(Compare("var." + (list.Members.TryGetValue(qualified, out var id) ? id : throw _b.Syntax("story", qualified + " is not a member of the list " + list.Name)), "eq", true));
                if (parts.Count == 0) throw _b.Unsupported("story", "a test for an empty list");
                JsonNode all = parts.Count == 1 ? parts[0]!.DeepClone() : new JsonObject { ["all"] = parts };
                return has.Operator == "?" ? all : new JsonObject { ["not"] = all };
            }
            case Binary { Operator: "==" or "!=" or "<" or ">" or "<=" or ">=" } compare:
            {
                var (fact, kind) = FactOf(compare.Left, out var flip);
                Ast other = compare.Right;
                if (fact is null)
                {
                    (fact, kind) = FactOf(compare.Right, out flip);
                    other = compare.Left;
                    flip = !flip;
                }
                if (fact is null || other is not Const constant) throw _b.Unsupported("story", "a comparison that is not a variable, visit count or allowed function against a fixed value");
                var op = compare.Operator switch
                {
                    "==" => "eq", "!=" => "ne",
                    "<" => flip ? "gt" : "lt", ">" => flip ? "lt" : "gt",
                    "<=" => flip ? "gte" : "lte", ">=" => flip ? "lte" : "gte",
                    _ => null,
                } ?? throw new InvalidOperationException("operator " + compare.Operator + " was accepted by the evaluator but not mapped");
                object literal = constant.Value;
                if (kind == "bool" && literal is long bit && bit is 0 or 1) literal = bit == 1;
                if (KindOf(literal) != kind) throw _b.Syntax("story", "a " + kind + " is compared with a " + KindOf(literal));
                if (op is not ("eq" or "ne") && kind != "int") throw _b.Syntax("story", "only whole numbers can be compared with " + compare.Operator);
                return Compare(fact, op, literal);
            }
        }
        throw _b.Unsupported("story", "a condition made of " + Describe(value));
    }

    private (string? Fact, string Kind) FactOf(Ast value, out bool flip)
    {
        flip = false;
        switch (value)
        {
            case VarRef { Variable: { Kind: "int" or "bool" or "string" } variable }: return ("var." + variable.Id, variable.Kind);
            case Seen seen: return ("var." + seen.Target.CountVariable, "int");
            case Ext external: return (external.Fact, FactTypes.Name(external.Kind));
        }
        return (null, "");
    }

    private static JsonNode Compare(string fact, string op, object value) => new JsonObject
    {
        ["fact"] = fact,
        ["op"] = op,
        ["value"] = value switch { bool b => JsonValue.Create(b), long n => JsonValue.Create(n), string s => JsonValue.Create(s), _ => null } ?? throw new InvalidOperationException("a literal of type " + value.GetType().Name),
    };
}
