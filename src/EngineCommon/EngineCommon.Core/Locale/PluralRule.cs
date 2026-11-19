using System.Globalization;
using System.Text.RegularExpressions;
using EngineCommon;

namespace EngineCommon;

/// <summary>
/// One CLDR plural rule: a category and the condition that selects it, in the rule language of UTS 35 part 3 (<c>i = 1 and v = 0</c>, <c>n % 100 = 3..10</c>, <c>i = 0 or n = 1</c>).
/// Read once from the table; <see cref="Matches"/> is pure.
/// </summary>
public sealed partial class PluralRule
{
    private readonly List<List<Relation>> _alternatives;

    private PluralRule(string category, string source, List<List<Relation>> alternatives)
    {
        Category = category;
        Source = source;
        _alternatives = alternatives;
    }

    public string Category { get; }

    /// <summary>The condition as CLDR writes it; empty for the "other" rule, which is the fallback.</summary>
    public string Source { get; }

    /// <summary>True when any of the alternatives (joined by <c>or</c>) has all of its relations (joined by <c>and</c>) true. An empty condition matches nothing.</summary>
    public bool Matches(PluralOperands operands) => _alternatives.Any(all => all.All(r => r.Holds(operands)));

    private sealed record Relation(char Operand, decimal? Modulus, bool Negated, IReadOnlyList<(decimal Low, decimal High)> Ranges)
    {
        public bool Holds(PluralOperands operands)
        {
            var value = operands.Operand(Operand);
            if (Modulus is { } modulus) value %= modulus;
            // A range of the rule language holds whole numbers only: 2.5 is not in 2..4.
            var inside = value == decimal.Truncate(value) && Ranges.Any(r => value >= r.Low && value <= r.High);
            return inside != Negated;
        }
    }

    [GeneratedRegex(@"\G\s*(and|or|!=|=|%|\.\.|,|\d+|[nivwftce])")]
    private static partial Regex Token();

    public static PluralRule Parse(string category, string condition)
    {
        var alternatives = new List<List<Relation>>();
        var text = condition.Trim();
        if (text.Length == 0) return new PluralRule(category, condition, alternatives);
        var tokens = new List<string>();
        var at = 0;
        while (at < text.Length)
        {
            var match = Token().Match(text, at);
            if (!match.Success) throw Bad(condition, "unexpected text at " + at);
            tokens.Add(match.Groups[1].Value);
            at = match.Index + match.Length;
        }
        var position = 0;
        alternatives.Add(new List<Relation>());
        while (true)
        {
            alternatives[^1].Add(ReadRelation(tokens, ref position, condition));
            if (position == tokens.Count) break;
            if (tokens[position] == "or") alternatives.Add(new List<Relation>());
            else if (tokens[position] != "and") throw Bad(condition, "expected and/or, found '" + tokens[position] + "'");
            position++;
        }
        return new PluralRule(category, condition, alternatives);
    }

    private static Relation ReadRelation(List<string> tokens, ref int p, string source)
    {
        string Next(ref int at) => at < tokens.Count ? tokens[at++] : throw Bad(source, "ends too soon");
        var operand = Next(ref p);
        if (operand.Length != 1 || !"nivwftce".Contains(operand[0])) throw Bad(source, "'" + operand + "' is not an operand");
        decimal? modulus = null;
        var op = Next(ref p);
        if (op == "%")
        {
            modulus = Number(Next(ref p), source);
            if (modulus == 0) throw Bad(source, "modulus 0");
            op = Next(ref p);
        }
        if (op is not ("=" or "!=")) throw Bad(source, "expected = or !=, found '" + op + "'");
        var ranges = new List<(decimal, decimal)>();
        while (true)
        {
            var low = Number(Next(ref p), source);
            var high = low;
            if (p < tokens.Count && tokens[p] == "..")
            {
                p++;
                high = Number(Next(ref p), source);
                if (high < low) throw Bad(source, "range " + low + ".." + high + " is backwards");
            }
            ranges.Add((low, high));
            if (p < tokens.Count && tokens[p] == ",") p++;
            else break;
        }
        return new Relation(operand[0], modulus, op == "!=", ranges);
    }

    private static decimal Number(string token, string source) =>
        token.All(char.IsAsciiDigit) ? decimal.Parse(token, CultureInfo.InvariantCulture) : throw Bad(source, "'" + token + "' is not a number");

    private static EngineRefusedException Bad(string source, string why) => new(LocaleCodes.Format, "plural rule '" + source + "': " + why);
}
