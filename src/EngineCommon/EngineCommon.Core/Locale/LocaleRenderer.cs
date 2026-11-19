using System.Globalization;
using System.Text;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace EngineCommon;

/// <summary>Turns a key and its arguments into text for one locale. Pure: the same table, key and arguments always give the same text.</summary>
public static class LocaleRenderer
{
    public static string Render(LocaleTable table, string key, IReadOnlyList<MessageArg> args)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(args);
        if (!table.Messages.TryGetValue(key, out var message)) throw new EngineRefusedException(LocaleCodes.MissingKey, "locale '" + table.Locale + "' has no message '" + key + "'");
        var template = message.Text;
        if (template is null)
        {
            var selector = args.FirstOrDefault(a => a.Name == message.Select) ?? throw new EngineRefusedException(LocaleCodes.Argument, "message '" + key + "' selects on '" + message.Select + "', which the line does not give");
            var category = Category(table.Locale, message, selector.Value, key);
            // "other" is the form every language has; a language that does not need the category simply leaves it out and the catch-all is used.
            template = message.Forms!.TryGetValue(category, out var form) ? form : message.Forms["other"];
        }
        return Fill(template, args, key, TextDirection.IsRightToLeft(table.Locale));
    }

    /// <summary>
    /// The form the argument selects. A number selects by CLDR plural category: a whole number (an integer fact) or a decimal written as text, whose trailing zeros count
    /// ("1.0" is not "1"). A text argument selects the form named by its exact value (a gender, say), and "other" when none is.
    /// </summary>
    private static string Category(string locale, LocaleMessage message, FactValue value, string key)
    {
        if (message.Mode == MessageSelect.Value)
        {
            if (value.Kind != FactKind.String) throw new EngineRefusedException(LocaleCodes.Argument, "message '" + key + "' selects on the value of '" + message.Select + "', which is not text");
            return message.Forms!.ContainsKey(value.Text!) ? value.Text! : "other";
        }
        var kind = message.Mode == MessageSelect.Ordinal ? PluralKind.Ordinal : PluralKind.Cardinal;
        switch (value.Kind)
        {
            case FactKind.Integer: return PluralRules.Category(locale, kind, PluralOperands.Of(value.Integer));
            case FactKind.String: return PluralRules.Category(locale, kind, value.Text!);
            case FactKind.Boolean: break;
        }
        throw new EngineRefusedException(LocaleCodes.Argument, "message '" + key + "' selects on '" + message.Select + "', which is not a number");
    }

    private static string Fill(string template, IReadOnlyList<MessageArg> args, string key, bool isolate)
    {
        var text = new StringBuilder(template.Length + 16);
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if (c == '{' && template[i + 1] == '{') { text.Append('{'); i++; }
            else if (c == '}' && template[i + 1] == '}') { text.Append('}'); i++; }
            else if (c == '{')
            {
                var close = template.IndexOf('}', i);
                var name = template[(i + 1)..close];
                var arg = args.FirstOrDefault(a => a.Name == name) ?? throw new EngineRefusedException(LocaleCodes.Argument, "message '" + key + "' uses '{" + name + "}', which the line does not give");
                if (isolate) text.Append(TextDirection.FirstStrongIsolate);
                text.Append(Shown(arg.Value));
                if (isolate) text.Append(TextDirection.PopDirectionalIsolate);
                i = close;
            }
            else text.Append(c);
        }
        return text.ToString();
    }

    /// <summary>The text of a value, with every kind listed so a kind added later is a build error here and not a silent guess.</summary>
    private static string Shown(FactValue value)
    {
        switch (value.Kind)
        {
            case FactKind.Integer: return value.Integer.ToString(CultureInfo.InvariantCulture);
            case FactKind.Boolean: return value.Boolean ? "true" : "false";
            case FactKind.String: return value.Text!;
        }
        throw new InvalidOperationException("a fact of kind " + value.Kind + " reached the renderer");
    }
}
