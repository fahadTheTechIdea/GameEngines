using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DialogueEngine;

/// <summary>
/// Yarn Spinner's random functions, on the engine's own seeded roll. Yarn Spinner documents (docs.yarnspinner.dev, Functions, read 2026-10-28): <c>random()</c> "a random number between 0 and 1",
/// <c>random_range(a, b)</c> "a random number between a and b, inclusive", <c>dice(sides)</c> "a random integer between 1 and sides, inclusive". Here a roll is a random node of the document, so the
/// same seed and the same number of earlier rolls give the same value, in any order of asking, and the roll counter is in the save. Whole numbers only: <c>dice</c> and <c>random_range</c> take whole
/// number arguments, <c>random()</c> can only be compared with a decimal in a condition (a probability to the thousandth), never stored.
/// </summary>
internal sealed partial class YarnConversation
{
    /// <summary>What a random call stands for: a whole number from Lo to Hi, each value equally likely, or (Unit) a number in [0, 1).</summary>
    private sealed record RollSource(long Lo, long Hi, bool Unit);

    /// <summary>The most values one assignment can roll over (a d100): one node sets each value, and a conversation holds 512 nodes. Two stages of at most 16 outcomes reach it.</summary>
    private const int MaxRollValues = 100;

    private const long RollBound = 1_000_000_000;

    private Operand RollOperand(string name, List<object> arguments, int no)
    {
        _b.Report.Add(ImportStatus.Converted, "dice / random_range", "a seeded roll of the document (the same seed and rolls so far give the same value, and the roll counter is saved)");
        switch (name)
        {
            case "random":
                if (arguments.Count != 0) throw _b.Syntax("line " + no, "random() takes no argument");
                return new Operand("roll", null, null, Roll: new RollSource(0, 0, true));
            case "dice":
            {
                if (arguments.Count != 1 || arguments[0] is not long sides) throw _b.Unsupported("line " + no, "dice(sides) takes one whole number");
                if (sides < 1 || sides > RollBound) throw _b.Syntax("line " + no, "dice(" + sides + "): the sides must be from 1 to " + RollBound);
                return new Operand("roll", null, null, Roll: new RollSource(1, sides, false));
            }
            case "random_range":
            {
                if (arguments.Count != 2 || arguments[0] is not long low || arguments[1] is not long high) throw _b.Unsupported("line " + no, "random_range(a, b) takes two whole numbers");
                if (low > high || low < -RollBound || high > RollBound) throw _b.Syntax("line " + no, "random_range(" + low + ", " + high + "): a must not exceed b, and both must be within " + RollBound);
                return new Operand("roll", null, null, Roll: new RollSource(low, high, false));
            }
        }
        throw new InvalidOperationException("random function " + name + " has no reader");
    }

    // ---- rolls in conditions ------------------------------------------------------------------------------------------------------------

    /// <summary>Takes the rolls the conditions parsed since the last call asked for.</summary>
    private List<Stmt> TakeRolls()
    {
        var taken = new List<Stmt>(_pendingRolls);
        _pendingRolls.Clear();
        return taken;
    }

    private void RequireNoPendingRolls()
    {
        if (_pendingRolls.Count > 0) throw new InvalidOperationException("a roll asked for by a condition was not placed before its statement");
    }

    /// <summary>
    /// A random value against a number in a condition, such as <c>dice(6) &gt;= 5</c>, <c>random_range(1, 10) &lt; 4</c> or <c>random() &lt; 0.25</c>. The comparison is decided by one roll made just before the
    /// statement whose condition holds it, into a hidden true/false variable that the condition then reads: true with the probability the comparison has. Approximated: every roll of an if / elseif chain is
    /// made before the first branch is tested (Yarn Spinner tests them one at a time), and a probability is kept to the thousandth.
    /// </summary>
    private JsonNode RollAtom(Operand left, Operand right, string? op, int no)
    {
        if (op is null) throw _b.Unsupported("line " + no, "a random value must be compared with a number");
        if (left.Roll is not null && right.Roll is not null) throw _b.Unsupported("line " + no, "two random values compared with each other");
        var flip = left.Roll is null;
        var roll = (flip ? right : left).Roll!;
        var literal = flip ? left : right;
        if (literal.Fact is not null || literal.Truth is not null || literal.Value is null || literal.Kind is not ("int" or "decimal"))
            throw _b.Unsupported("line " + no, "a random value can only be compared with a number written in the script (not with a variable)");
        var compare = op switch
        {
            "==" or "eq" or "is" => "eq",
            "!=" or "neq" => "ne",
            ">" or "gt" => flip ? "lt" : "gt",
            "<" or "lt" => flip ? "gt" : "lt",
            ">=" or "gte" => flip ? "lte" : "gte",
            "<=" or "lte" => flip ? "gte" : "lte",
            _ => null,
        } ?? throw new InvalidOperationException("operator " + op + " was accepted by the tokenizer but not mapped");
        long numerator;
        long denominator;
        if (roll.Unit)
        {
            if (compare is "eq" or "ne") throw _b.Unsupported("line " + no, "random() compared with == or != (a number in [0, 1) is never exactly a value; use < or >)");
            var text = literal.Kind == "int" ? ((long)literal.Value).ToString(CultureInfo.InvariantCulture) : (string)literal.Value;
            if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) throw _b.Syntax("line " + no, "'" + text + "' is not a number");
            var thousandths = value * 1000m;
            if (thousandths != decimal.Truncate(thousandths)) throw _b.Unsupported("line " + no, "random() compared with " + text + ": a probability is kept to the thousandth (three decimals)");
            var below = (long)Math.Clamp(thousandths, 0m, 1000m);
            numerator = compare is "lt" or "lte" ? below : 1000 - below;
            denominator = 1000;
        }
        else
        {
            if (literal.Kind != "int") throw _b.Unsupported("line " + no, "dice and random_range give whole numbers; compare them with a whole number");
            var number = (long)literal.Value;
            // Count the values of the range that satisfy the comparison, in closed form (a range can hold a billion values).
            long Count(long from, long to) => to < from ? 0 : to - from + 1;
            denominator = roll.Hi - roll.Lo + 1;
            numerator = (compare switch
            {
                "eq" => (long?)(number >= roll.Lo && number <= roll.Hi ? 1 : 0),
                "ne" => denominator - (number >= roll.Lo && number <= roll.Hi ? 1 : 0),
                "gt" => Count(Math.Max(roll.Lo, number + 1), roll.Hi),
                "gte" => Count(Math.Max(roll.Lo, number), roll.Hi),
                "lt" => Count(roll.Lo, Math.Min(roll.Hi, number - 1)),
                "lte" => Count(roll.Lo, Math.Min(roll.Hi, number)),
                _ => null,
            }) ?? throw new InvalidOperationException("comparison " + compare + " has no count");
        }
        var divisor = Gcd(numerator, denominator);
        numerator /= divisor;
        denominator /= divisor;
        if (denominator > 1000)
        {
            numerator = (long)Math.Round(numerator * 1000m / denominator, MidpointRounding.AwayFromZero);
            denominator = 1000;
            _b.Report.Add(ImportStatus.Approximated, "random in conditions", "a probability is kept to the thousandth");
        }
        var variable = "roll-" + (++_rolls);
        _b.Variable(new JsonObject { ["id"] = variable, ["type"] = "bool", ["initial"] = false });
        _pendingRolls.Add(new RollBoolStmt(no, variable, (int)numerator, (int)denominator));
        _b.Report.Add(ImportStatus.Approximated, "random in conditions", "decided by one seeded roll made before the statement that holds the condition (all rolls of an if / elseif chain are made first)");
        return new JsonObject { ["fact"] = "var." + variable, ["op"] = "eq", ["value"] = true };
    }

    private static long Gcd(long a, long b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a == 0 ? 1 : a;
    }

    // ---- rolls in <<set>> ---------------------------------------------------------------------------------------------------------------

    private static readonly Regex SetRoll = new(@"^\$([A-Za-z_][A-Za-z0-9_]*)\s*(?:to|=)\s*((?:dice|random_range|random)\s*\(.*\))$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    /// <summary>A <c>&lt;&lt;set&gt;&gt;</c>: a roll when the value is <c>dice(n)</c> or <c>random_range(a, b)</c>, otherwise the effect <see cref="Set"/> makes.</summary>
    private Stmt SetStatement(string rest, int no)
    {
        var match = SetRoll.Match(rest);
        if (!match.Success) return new EffectStmt(no, Set(rest, no));
        var name = match.Groups[1].Value.ToLowerInvariant();
        if (!_variables.TryGetValue(name, out var variable)) throw _b.Syntax("line " + no, "$" + match.Groups[1].Value + " is not declared (<<declare $name = value>>)");
        if (variable.Kind != "int" || variable.Enum is not null) throw _b.Syntax("line " + no, "$" + name + " is not a number");
        var call = match.Groups[2].Value;
        var (function, arguments, end) = ParseCall(call, 0, no);
        if (end != call.Length) throw _b.Syntax("line " + no, "unexpected '" + call[end..] + "' after the call of " + function);
        var operand = RollOperand(function, arguments, no);
        var roll = operand.Roll!;
        if (roll.Unit) throw _b.Unsupported("line " + no, "random() gives a decimal between 0 and 1 and variables are whole numbers; use dice(n) or random_range(a, b), or compare random() in a condition");
        var count = roll.Hi - roll.Lo + 1;
        if (count > MaxRollValues) throw _b.Unsupported("line " + no, function + " with " + count + " values: an assignment rolls over at most " + MaxRollValues + " values");
        _b.Report.Add(ImportStatus.Converted, "set (roll)", "$" + name + " is set from a seeded roll, every value equally likely");
        return new RollIntStmt(no, name, roll.Lo, roll.Hi);
    }

    // ---- emitting -----------------------------------------------------------------------------------------------------------------------

    private string SetNode(string variable, JsonNode value, string next)
    {
        var id = _b.NewId("roll-set");
        _b.Add(new JsonObject { ["id"] = id, ["kind"] = "effects", ["effects"] = new JsonArray(new JsonObject { ["set"] = variable, ["value"] = value }), ["next"] = next });
        return id;
    }

    private string RandomNodeOf(IEnumerable<(int Weight, string Next)> outcomes)
    {
        var id = _b.NewId("roll");
        var list = new JsonArray();
        foreach (var (weight, next) in outcomes) list.Add(new JsonObject { ["weight"] = weight, ["next"] = next });
        _b.Add(new JsonObject { ["id"] = id, ["kind"] = "random", ["outcomes"] = list });
        return id;
    }

    /// <summary>The node a roll of a whole number starts at: up to 16 values in one random node, more in two stages (a bucket by its size, then a value in it), so each value stays equally likely.</summary>
    private string EmitRollInt(RollIntStmt roll, string after)
    {
        var count = (int)(roll.Hi - roll.Lo + 1);
        string Value(int k) => SetNode(roll.Variable, JsonValue.Create(roll.Lo + k)!, after);
        if (count == 1) return Value(0);
        if (count <= 16) return RandomNodeOf(Enumerable.Range(0, count).Select(k => (1, Value(k))));
        var buckets = new List<(int Weight, string Next)>();
        for (var start = 0; start < count; start += 16)
        {
            var size = Math.Min(16, count - start);
            buckets.Add((size, size == 1 ? Value(start) : RandomNodeOf(Enumerable.Range(start, size).Select(k => (1, Value(k))))));
        }
        return RandomNodeOf(buckets);
    }

    private string EmitRollBool(RollBoolStmt roll, string after)
    {
        if (roll.Num <= 0) return SetNode(roll.Variable, JsonValue.Create(false)!, after);
        if (roll.Num >= roll.Den) return SetNode(roll.Variable, JsonValue.Create(true)!, after);
        return RandomNodeOf([(roll.Num, SetNode(roll.Variable, JsonValue.Create(true)!, after)), (roll.Den - roll.Num, SetNode(roll.Variable, JsonValue.Create(false)!, after))]);
    }
}
