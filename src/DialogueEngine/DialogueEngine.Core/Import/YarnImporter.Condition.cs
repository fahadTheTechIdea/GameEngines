using System.Globalization;
using System.Text.Json.Nodes;
using ScenarioEngine.Contracts;

namespace DialogueEngine;

internal sealed partial class YarnConversation
{
    private enum Tk { Var, Int, Str, Bool, Op, Open, Close, Word, Call, Enum, Shorthand, Decimal, End }

    private readonly record struct Token(Tk Kind, string Text, string? Name = null, List<object>? Arguments = null);

    /// <summary>
    /// What a condition compares: a fact (a variable, a visit count or an allowed function's answer) or a fixed value, or a finished true/false condition (<c>visited()</c>). <see cref="Enum"/> is the enum a
    /// variable or a written case belongs to; <see cref="Shorthand"/> marks <c>.Case</c>, whose enum is the other side's.
    /// </summary>
    private sealed record Operand(string Kind, string? Fact, object? Value, string? Enum = null, bool Shorthand = false, JsonNode? Truth = null, RollSource? Roll = null);

    /// <summary>A Yarn condition in the dialogue condition grammar: and/or/not, parentheses, and a variable, visit count or allowed function compared with a literal (or a true/false one on its own).</summary>
    private JsonNode Condition(string expression, int no)
    {
        var parser = new ConditionParser(this, Tokens(expression, no), no);
        var result = parser.Parse();
        _b.Report.Add(ImportStatus.Converted, "conditions", "and/or/not and comparisons of a variable with a value became a condition");
        return result;
    }

    /// <summary>Reads <c>name(arg, arg)</c> starting at the name; returns the name, the arguments (text, whole numbers, true/false) and the index after the closing parenthesis.</summary>
    private (string Name, List<object> Arguments, int End) ParseCall(string text, int start, int no)
    {
        var i = start;
        while (i < text.Length && (char.IsAsciiLetterOrDigit(text[i]) || text[i] == '_')) i++;
        var name = text[start..i];
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        if (i >= text.Length || text[i] != '(') throw _b.Syntax("line " + no, "the function call " + name + " has no parenthesis");
        i++;
        var arguments = new List<object>();
        while (true)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i >= text.Length) throw _b.Syntax("line " + no, "the call of " + name + " is never closed with )");
            if (text[i] == ')') { i++; break; }
            if (arguments.Count > 0)
            {
                if (text[i] != ',') throw _b.Syntax("line " + no, "the arguments of " + name + " are separated by commas");
                i++;
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            }
            if (i >= text.Length) throw _b.Syntax("line " + no, "the call of " + name + " is never closed with )");
            if (text[i] == '"')
            {
                var close = text.IndexOf('"', i + 1);
                if (close < 0) throw _b.Syntax("line " + no, "a quoted argument is never closed");
                arguments.Add(text[(i + 1)..close]);
                i = close + 1;
                continue;
            }
            var end = i;
            while (end < text.Length && text[end] != ',' && text[end] != ')' && !char.IsWhiteSpace(text[end])) end++;
            var word = text[i..end];
            if (word is "true" or "false") arguments.Add(word == "true");
            else if (long.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) arguments.Add(number);
            else if (word.Contains('.', StringComparison.Ordinal) && double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) throw _b.Unsupported("line " + no, "the decimal argument " + word + " of " + name + " (only whole numbers are converted)");
            else throw _b.Unsupported("line " + no, "the argument '" + word + "' of " + name + " is not text in quotes, a whole number or true/false (variables and expressions in arguments are not converted)");
            i = end;
        }
        return (name, arguments, i);
    }

    /// <summary>The operand a function call stands for: a visit count of a node for <c>visited</c> and <c>visited_count</c>, an allowed function's fact, or a refusal naming the function.</summary>
    private Operand CallOperand(string name, List<object> arguments, int no)
    {
        if (name is "visited" or "visited_count")
        {
            if (arguments.Count != 1 || arguments[0] is not string title) throw _b.Unsupported("line " + no, name + "(...) takes one node title in quotes");
            title = title.ToLowerInvariant();
            if (!_nodes.ContainsKey(title)) throw _b.Syntax("line " + no, name + "(\"" + title + "\"): no node is titled '" + title + "'");
            var variable = VisitVariable(title);
            if (_visited.Add(title))
                _b.Variable(new JsonObject { ["id"] = variable, ["type"] = "int", ["initial"] = 0, ["min"] = 0, ["max"] = 1_000_000_000 });
            _b.Report.Add(ImportStatus.Approximated, "visited / visited_count", "counts how often the node was entered (Yarn Spinner counts a node that was entered and left); kept in a number variable of the document");
            return name == "visited"
                ? new Operand("bool", null, null, Truth: new JsonObject { ["fact"] = "var." + variable, ["op"] = "gt", ["value"] = 0 })
                : new Operand("int", "var." + variable, null);
        }
        if (name is "random" or "random_range" or "dice") return RollOperand(name, arguments, no);
        var signature = ImportFunction.Canonical(name, arguments);
        var allowed = _b.Allowed(signature) ?? throw _b.Function("line " + no, "the function " + signature + " is not on the list of allowed functions (--function '" + signature + "=fact:type'); a document can read only facts the host supplies");
        var fact = _b.External(allowed, "line " + no);
        _b.Report.Add(ImportStatus.Converted, "function (allowed)", "an allowed function became the external fact the host supplies");
        return new Operand(FactTypes.Name(allowed.Kind), fact, null);
    }

    private Operand CallOperand(string call, int no)
    {
        var (name, arguments, end) = ParseCall(call, 0, no);
        if (end != call.Length) throw _b.Syntax("line " + no, "unexpected '" + call[end..] + "' after the call of " + name);
        return CallOperand(name, arguments, no);
    }

    private List<Token> Tokens(string text, int no)
    {
        var tokens = new List<Token>();
        for (var i = 0; i < text.Length;)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '(') { tokens.Add(new Token(Tk.Open, "(")); i++; continue; }
            if (c == ')') { tokens.Add(new Token(Tk.Close, ")")); i++; continue; }
            if (c == '$')
            {
                var j = i + 1;
                while (j < text.Length && (char.IsAsciiLetterOrDigit(text[j]) || text[j] == '_')) j++;
                if (j == i + 1) throw _b.Syntax("line " + no, "a $ must be followed by a variable name");
                tokens.Add(new Token(Tk.Var, text[(i + 1)..j]));
                i = j;
                continue;
            }
            if (c == '"')
            {
                var close = text.IndexOf('"', i + 1);
                if (close < 0) throw _b.Syntax("line " + no, "a quoted text is never closed");
                tokens.Add(new Token(Tk.Str, text[(i + 1)..close]));
                i = close + 1;
                continue;
            }
            if (c == '.' && i + 1 < text.Length && char.IsAsciiLetter(text[i + 1]))
            {
                var j = i + 1;
                while (j < text.Length && (char.IsAsciiLetterOrDigit(text[j]) || text[j] == '_')) j++;
                tokens.Add(new Token(Tk.Shorthand, text[(i + 1)..j]));
                i = j;
                continue;
            }
            if (char.IsAsciiDigit(c) || (c == '-' && i + 1 < text.Length && char.IsAsciiDigit(text[i + 1])))
            {
                var j = i + 1;
                while (j < text.Length && char.IsAsciiDigit(text[j])) j++;
                if (j < text.Length && text[j] == '.' && j + 1 < text.Length && char.IsAsciiDigit(text[j + 1]))
                {
                    j++;
                    while (j < text.Length && char.IsAsciiDigit(text[j])) j++;
                    tokens.Add(new Token(Tk.Decimal, text[i..j]));
                    i = j;
                    continue;
                }
                if (j < text.Length && text[j] == '.') throw _b.Unsupported("line " + no, "a decimal number in a condition (only whole numbers are converted)");
                tokens.Add(new Token(Tk.Int, text[i..j]));
                i = j;
                continue;
            }
            if (char.IsAsciiLetter(c) || c == '_')
            {
                var j = i + 1;
                while (j < text.Length && (char.IsAsciiLetterOrDigit(text[j]) || text[j] == '_')) j++;
                var word = text[i..j];
                var after = j;
                while (after < text.Length && char.IsWhiteSpace(text[after])) after++;
                if (after < text.Length && text[after] == '(' && word is not ("and" or "or" or "not" or "eq" or "is" or "neq" or "gt" or "lt" or "gte" or "lte"))
                {
                    var (name, arguments, end) = ParseCall(text, i, no);
                    tokens.Add(new Token(Tk.Call, name, name, arguments));
                    i = end;
                    continue;
                }
                if (j < text.Length && text[j] == '.' && j + 1 < text.Length && char.IsAsciiLetter(text[j + 1]) && _enums.ContainsKey(word))
                {
                    var k = j + 1;
                    while (k < text.Length && (char.IsAsciiLetterOrDigit(text[k]) || text[k] == '_')) k++;
                    tokens.Add(new Token(Tk.Enum, text[(j + 1)..k], word));
                    i = k;
                    continue;
                }
                i = j;
                tokens.Add(word is "true" or "false" ? new Token(Tk.Bool, word) : new Token(Tk.Word, word));
                continue;
            }
            var two = i + 1 < text.Length ? text.Substring(i, 2) : "";
            if (two is "==" or "!=" or ">=" or "<=" or "&&" or "||") { tokens.Add(new Token(Tk.Op, two)); i += 2; continue; }
            if (c is '>' or '<' or '!') { tokens.Add(new Token(Tk.Op, c.ToString())); i++; continue; }
            if (c is '+' or '-' or '*' or '/' or '%' or '^') throw _b.Unsupported("line " + no, "arithmetic or xor ('" + c + "') in a condition");
            throw _b.Syntax("line " + no, "the character '" + c + "' is not part of a condition");
        }
        tokens.Add(new Token(Tk.End, ""));
        return tokens;
    }

    private sealed class ConditionParser(YarnConversation owner, List<Token> tokens, int no)
    {
        private int _i;

        public JsonNode Parse()
        {
            var result = Or();
            if (tokens[_i].Kind != Tk.End) throw owner._b.Syntax("line " + no, "unexpected '" + tokens[_i].Text + "' in a condition");
            return result;
        }

        private bool Take(params string[] words)
        {
            var token = tokens[_i];
            if (token.Kind is not (Tk.Op or Tk.Word) || Array.IndexOf(words, token.Text) < 0) return false;
            _i++;
            return true;
        }

        private JsonNode Or()
        {
            var parts = new List<JsonNode> { And() };
            while (Take("||", "or")) parts.Add(And());
            return parts.Count == 1 ? parts[0] : new JsonObject { ["any"] = new JsonArray(parts.ToArray()) };
        }

        private JsonNode And()
        {
            var parts = new List<JsonNode> { Not() };
            while (Take("&&", "and")) parts.Add(Not());
            return parts.Count == 1 ? parts[0] : new JsonObject { ["all"] = new JsonArray(parts.ToArray()) };
        }

        private JsonNode Not() => Take("!", "not") ? new JsonObject { ["not"] = Not() } : Comparison();

        private Operand Primary()
        {
            var token = tokens[_i++];
            switch (token.Kind)
            {
                case Tk.Var:
                {
                    var name = token.Text.ToLowerInvariant();
                    if (!owner._variables.TryGetValue(name, out var variable)) throw owner._b.Syntax("line " + no, "$" + token.Text + " is not declared (<<declare $name = value>>)");
                    return new Operand(variable.Kind, "var." + name, null, variable.Enum);
                }
                case Tk.Int: return new Operand("int", null, long.Parse(token.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
                case Tk.Str: return new Operand("string", null, token.Text);
                case Tk.Bool: return new Operand("bool", null, token.Text == "true");
                case Tk.Enum:
                    if (!owner._enums[token.Name!].Contains(token.Text)) throw owner._b.Syntax("line " + no, "the enum " + token.Name + " has no case " + token.Text);
                    return new Operand("string", null, token.Text, token.Name);
                case Tk.Shorthand: return new Operand("string", null, token.Text, null, Shorthand: true);
                case Tk.Decimal: return new Operand("decimal", null, token.Text);
                case Tk.Call: return owner.CallOperand(token.Name!, token.Arguments!, no);
                case Tk.Word: throw owner._b.Unsupported("line " + no, "the word '" + token.Text + "' in a condition (an operator that is not converted, or an enum that was not declared)");
                case Tk.Op:
                case Tk.Open:
                case Tk.Close:
                case Tk.End:
                    break;
            }
            throw owner._b.Syntax("line " + no, "a condition has '" + token.Text + "' where a value was expected");
        }

        /// <summary>The comparison a token spells, or null when it is not one. Every kind is listed so a new kind is a build error.</summary>
        private static string? OperatorOf(Token token)
        {
            switch (token.Kind)
            {
                case Tk.Op: return token.Text is "==" or "!=" or ">" or "<" or ">=" or "<=" ? token.Text : null;
                case Tk.Word: return token.Text is "eq" or "is" or "neq" or "gt" or "lt" or "gte" or "lte" ? token.Text : null;
                case Tk.Var:
                case Tk.Int:
                case Tk.Str:
                case Tk.Bool:
                case Tk.Call:
                case Tk.Enum:
                case Tk.Shorthand:
                case Tk.Decimal:
                case Tk.Open:
                case Tk.Close:
                case Tk.End:
                    return null;
            }
            throw new InvalidOperationException("unknown token kind " + token.Kind);
        }

        private JsonNode Comparison()
        {
            if (tokens[_i].Kind == Tk.Open)
            {
                _i++;
                var inner = Or();
                if (tokens[_i++].Kind != Tk.Close) throw owner._b.Syntax("line " + no, "a parenthesis is never closed");
                return inner;
            }
            var left = Primary();
            var token = tokens[_i];
            var op = OperatorOf(token);
            if (op is null)
            {
                if (left.Roll is not null) throw owner._b.Unsupported("line " + no, "a random value must be compared with a number (for example dice(6) >= 5 or random() < 0.25)");
                if (left.Kind == "decimal") throw owner._b.Unsupported("line " + no, "a decimal number in a condition (only whole numbers are converted)");
                if (left.Truth is not null) return left.Truth.DeepClone();
                if (left.Fact is null || left.Kind != "bool") throw owner._b.Syntax("line " + no, "a condition must compare a variable, or be a true/false variable or function");
                return Compare(left.Fact, "eq", true);
            }
            _i++;
            var right = Primary();
            if (left.Roll is not null || right.Roll is not null) return owner.RollAtom(left, right, op, no);
            if (left.Kind == "decimal" || right.Kind == "decimal") throw owner._b.Unsupported("line " + no, "a decimal number in a condition (only whole numbers are converted)");
            if (left.Truth is not null || right.Truth is not null) throw owner._b.Unsupported("line " + no, "a visited(...) answer compared with a value (use it on its own, or with not, and, or)");
            if ((left.Fact is null) == (right.Fact is null)) throw owner._b.Unsupported("line " + no, "a comparison of two variables or of two fixed values (only a variable against a value is converted)");
            var flip = left.Fact is null;
            var variable = flip ? right : left;
            var literal = flip ? left : right;
            CheckEnum(variable, literal);
            if (variable.Kind != literal.Kind) throw owner._b.Syntax("line " + no, variable.Fact + " is " + variable.Kind + " but is compared with a " + literal.Kind);
            var name = op switch
            {
                "==" or "eq" or "is" => "eq",
                "!=" or "neq" => "ne",
                ">" or "gt" => flip ? "lt" : "gt",
                "<" or "lt" => flip ? "gt" : "lt",
                ">=" or "gte" => flip ? "lte" : "gte",
                "<=" or "lte" => flip ? "gte" : "lte",
                _ => null,
            } ?? throw new InvalidOperationException("operator " + op + " was accepted by the tokenizer but not mapped");
            if (name is not ("eq" or "ne") && variable.Kind != "int") throw owner._b.Syntax("line " + no, "only numbers can be compared with " + op);
            return Compare(variable.Fact!, name, literal.Value!);
        }

        /// <summary>An enum case may only be compared with a variable declared with that enum, and a written case must be one of it.</summary>
        private void CheckEnum(Operand variable, Operand literal)
        {
            if (literal.Shorthand)
            {
                if (variable.Enum is null) throw owner._b.Syntax("line " + no, "." + literal.Value + " needs a variable that holds an enum");
                if (!owner._enums[variable.Enum].Contains((string)literal.Value!)) throw owner._b.Syntax("line " + no, "the enum " + variable.Enum + " has no case " + literal.Value);
                return;
            }
            if (literal.Enum is not null && literal.Enum != variable.Enum) throw owner._b.Syntax("line " + no, "a case of the enum " + literal.Enum + " is compared with " + (variable.Enum is null ? "a variable that is not an enum" : "the enum " + variable.Enum));
            if (literal.Enum is null && variable.Enum is not null && literal.Kind == "string" && !owner._enums[variable.Enum].Contains((string)literal.Value!))
                throw owner._b.Syntax("line " + no, "\"" + literal.Value + "\" is not a case of the enum " + variable.Enum);
        }

        private static JsonNode Compare(string fact, string op, object value) => new JsonObject
        {
            ["fact"] = fact,
            ["op"] = op,
            ["value"] = value switch { bool b => JsonValue.Create(b), long n => JsonValue.Create(n), string s => JsonValue.Create(s), _ => null } ?? throw new InvalidOperationException("a literal of type " + value.GetType().Name),
        };
    }
}
