using System.Globalization;
using System.Text;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// The text forms every engine's journal shares: how a text is escaped so the separators of a line can sit inside it, and how a set of host facts is written as one token and read back. A journal line is
/// <c>|</c>-separated; the characters <c>%</c> <c>|</c> <c>;</c> <c>=</c> and a line end inside a text are written as <c>%25</c> <c>%7C</c> <c>%3B</c> <c>%3D</c> <c>%0A</c>. Facts are <c>id=K:value;...</c> with K one of B, I or S.
/// </summary>
public static class JournalText
{
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c == '%') builder.Append("%25");
            else if (c == '|') builder.Append("%7C");
            else if (c == ';') builder.Append("%3B");
            else if (c == '=') builder.Append("%3D");
            else if (c == '\n') builder.Append("%0A");
            else builder.Append(c);
        }
        return builder.ToString();
    }

    public static string Unescape(string text) =>
        text.Replace("%0A", "\n", StringComparison.Ordinal).Replace("%3D", "=", StringComparison.Ordinal).Replace("%3B", ";", StringComparison.Ordinal).Replace("%7C", "|", StringComparison.Ordinal).Replace("%25", "%", StringComparison.Ordinal);

    /// <summary>The facts in a fixed order, each as <c>id=K:value</c>, so the same facts always give the same text.</summary>
    public static string CanonicalFacts(IReadOnlyDictionary<string, FactValue> facts) =>
        string.Join(';', facts.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => Escape(f.Key) + "=" + FactToken(f.Value)));

    private static string FactToken(FactValue value)
    {
        switch (value.Kind)
        {
            case FactKind.Boolean: return "B:" + (value.Boolean ? "true" : "false");
            case FactKind.Integer: return "I:" + value.Integer.ToString(CultureInfo.InvariantCulture);
            case FactKind.String: return "S:" + Escape(value.Text!);
        }
        throw new InvalidOperationException("unknown fact kind " + value.Kind);
    }

    /// <summary>Reads facts written by <see cref="CanonicalFacts"/>, strictly; <paramref name="bad"/> makes the engine's own refusal for a line number and a reason.</summary>
    public static Dictionary<string, FactValue> ParseFacts(string text, int line, Func<int, string, EngineRefusedException> bad)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(bad);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        if (text.Length == 0) return facts;
        foreach (var part in text.Split(';'))
        {
            var at = part.IndexOf('=', StringComparison.Ordinal);
            if (at < 1 || part.Length < at + 3 || part[at + 2] != ':') throw bad(line, "'" + part + "' is not id=K:value");
            var value = part[(at + 3)..];
            var kind = part[at + 1];
            FactValue fact;
            if (kind == 'B' && value is "true" or "false") fact = FactValue.From(value == "true");
            else if (kind == 'I' && long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) fact = FactValue.From(number);
            else if (kind == 'S') fact = FactValue.From(Unescape(value));
            else throw bad(line, "'" + part + "' has a value of the wrong kind");
            if (!facts.TryAdd(Unescape(part[..at]), fact)) throw bad(line, "fact '" + part[..at] + "' is given twice");
        }
        return facts;
    }
}
