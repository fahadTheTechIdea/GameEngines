using System.Globalization;
using System.Text;
using AIEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// The text form of values in AI events: <c>name=value</c> entries joined by ';'. A number is written in the invariant culture, a boolean as true or false and a
/// text with % ; and = as %25 %3B %3D, so any text round-trips. Reading is by declaration: the kind the declaration names is the only kind accepted.
/// </summary>
public static class AiValues
{
    public static string Encode(FactValue value)
    {
        if (value.Kind == FactKind.Boolean) return value.Boolean ? "true" : "false";
        if (value.Kind == FactKind.Integer) return value.Integer.ToString(CultureInfo.InvariantCulture);
        return Escape(value.Text!);
    }

    public static string Encode(IEnumerable<KeyValuePair<string, FactValue>> entries) =>
        string.Join(';', entries.Select(e => Escape(e.Key) + "=" + Encode(e.Value)));

    public static string Encode(IEnumerable<IntentArg> args) => Encode(args.Select(a => new KeyValuePair<string, FactValue>(a.Name, a.Value)));

    private static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c == '%') builder.Append("%25");
            else if (c == ';') builder.Append("%3B");
            else if (c == '=') builder.Append("%3D");
            else builder.Append(c);
        }
        return builder.ToString();
    }

    private static string Unescape(string text) => text.Replace("%3B", ";", StringComparison.Ordinal).Replace("%3D", "=", StringComparison.Ordinal).Replace("%25", "%", StringComparison.Ordinal);

    /// <summary>One value as the declaration says it should be, or a refusal naming the declaration.</summary>
    public static FactValue Decode(ValueDef def, string text, string code)
    {
        switch (def.Kind)
        {
            case FactKind.Boolean:
                if (text is "true" or "false") return FactValue.From(text == "true");
                throw new EngineRefusedException(code, "'" + def.Id + "' must be true or false");
            case FactKind.Integer:
                if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) return FactValue.From(number);
                throw new EngineRefusedException(code, "'" + def.Id + "' must be a whole number");
            case FactKind.String: break;
        }
        return FactValue.From(Unescape(text));
    }

    /// <summary>Entries for the attributes a kind declares, each read as its declaration says. Anything not declared, missing or repeated is a refusal.</summary>
    public static Dictionary<string, FactValue> Decode(PerceptKindDef kind, string entries, string code) =>
        Decode(kind.Attrs, "kind '" + kind.Id + "'", entries, code);

    /// <summary>The arguments of an intent as it declares them.</summary>
    public static Dictionary<string, FactValue> Decode(IntentDef intent, string entries, string code) =>
        Decode(intent.Args, "intent '" + intent.Id + "'", entries, code);

    private static Dictionary<string, FactValue> Decode(IReadOnlyList<ValueDef> declared, string owner, string entries, string code)
    {
        var result = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        if (entries.Length == 0) return result;
        foreach (var entry in entries.Split(';'))
        {
            var at = entry.IndexOf('=', StringComparison.Ordinal);
            if (at < 0) throw new EngineRefusedException(code, "'" + entry + "' is not name=value");
            var name = Unescape(entry[..at]);
            var def = declared.FirstOrDefault(d => d.Id == name) ?? throw new EngineRefusedException(code, owner + " has no '" + name + "'");
            if (!result.TryAdd(name, Decode(def, entry[(at + 1)..], code))) throw new EngineRefusedException(code, "'" + name + "' is given twice");
        }
        return result;
    }
}