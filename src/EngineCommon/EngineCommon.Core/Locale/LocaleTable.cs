using System.Text.Json;
using EngineCommon;

namespace EngineCommon;

/// <summary>
/// The text of one locale, keyed by the localisation keys of the documents. Strict like every document: format, locale and messages only; a
/// message is a string or an object with the number argument it selects on, a required "other" form and any forms the locale uses.
/// </summary>
public sealed class LocaleTable
{
    public const int Format = 1;
    private static readonly StrictJson Json = new(LocaleCodes.ForLocale);

    private LocaleTable(string locale, IReadOnlyDictionary<string, LocaleMessage> messages, string digest)
    {
        Locale = locale;
        Messages = messages;
        Digest = digest;
    }

    public string Locale { get; }
    public IReadOnlyDictionary<string, LocaleMessage> Messages { get; }
    public string Digest { get; }

    /// <summary>The table as the JSON <see cref="Load"/> reads.</summary>
    public string ToText()
    {
        var messages = new System.Text.Json.Nodes.JsonObject();
        foreach (var (key, message) in Messages) messages[key] = message.ToJson();
        return new System.Text.Json.Nodes.JsonObject { ["format"] = Format, ["locale"] = Locale, ["messages"] = messages }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static LocaleTable Load(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var document = Json.Parse(text, "locale table");
        var root = Json.Object(document.RootElement, "locale table", ["format", "locale", "messages"]);
        if (root.GetProperty("format").ValueKind != JsonValueKind.Number || !root.GetProperty("format").TryGetInt32(out var format) || format != Format)
            throw Json.Refuse(LocaleCodes.Format, "locale table format must be " + Format);
        var locale = Json.Text(root, "locale", "locale table", 16);
        _ = PluralRules.Categories(locale);   // an unsupported locale is refused here
        var messages = new SortedDictionary<string, LocaleMessage>(StringComparer.Ordinal);
        var holder = root.GetProperty("messages");
        if (holder.ValueKind != JsonValueKind.Object) throw Json.Refuse(LocaleCodes.Format, "messages must be an object of key to message");
        foreach (var property in holder.EnumerateObject())
        {
            var key = Json.CheckId(property.Name, "message key");
            if (messages.ContainsKey(key)) throw Json.Refuse(LocaleCodes.Format, "message '" + key + "' appears twice");
            messages[key] = ReadMessage(property.Value, key, locale);
        }
        return new LocaleTable(locale, messages, EngineCommon.Digest.Of(text));
    }

    private static LocaleMessage ReadMessage(JsonElement value, string key, string locale)
    {
        if (value.ValueKind == JsonValueKind.String) return new LocaleMessage(Checked(value.GetString()!, key), null, null);
        if (value.ValueKind != JsonValueKind.Object) throw Json.Refuse(LocaleCodes.Format, "message '" + key + "' must be a string or an object of plural forms");
        string? select = null;
        var mode = MessageSelect.Cardinal;
        foreach (var property in value.EnumerateObject())
            if (property.Name == "type")
                mode = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() switch
                {
                    "cardinal" => MessageSelect.Cardinal,
                    "ordinal" => MessageSelect.Ordinal,
                    "select" => MessageSelect.Value,
                    _ => (MessageSelect?)null,
                } ?? throw Json.Refuse(LocaleCodes.Format, "message '" + key + "'.type must be cardinal, ordinal or select") : throw Json.Refuse(LocaleCodes.Format, "message '" + key + "'.type must be a string");
        var categories = mode == MessageSelect.Value ? null : PluralRules.Categories(locale, mode == MessageSelect.Ordinal ? PluralKind.Ordinal : PluralKind.Cardinal);
        var forms = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String) throw Json.Refuse(LocaleCodes.Format, "message '" + key + "." + property.Name + "' must be a string");
            if (property.Name == "select") select = Json.CheckId(property.Value.GetString()!, "message '" + key + "'.select");
            else if (property.Name == "type") continue;
            else if (categories is null ? IsValueId(property.Name) : categories.Contains(property.Name)) forms[property.Name] = Checked(property.Value.GetString()!, key + "." + property.Name);
            else throw Json.Refuse(LocaleCodes.Format, "message '" + key + "' has a form '" + property.Name + "' the locale does not use");
        }
        if (select is null) throw Json.Refuse(LocaleCodes.Format, "message '" + key + "' has forms but no 'select' argument");
        if (!forms.ContainsKey("other")) throw Json.Refuse(LocaleCodes.Format, "message '" + key + "' has no 'other' form");
        return new LocaleMessage(null, select, forms, mode);
    }

    private static bool IsValueId(string name) => name.Length > 0 && name.All(c => c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c is '-' or '_');

    private static string Checked(string text, string where)
    {
        if (text.Length > 1024) throw Json.Refuse(LocaleCodes.Format, "message '" + where + "' is longer than 1024");
        Scan(text, new SortedSet<string>(StringComparer.Ordinal));
        return text;
    }

    /// <summary>Walks a text for placeholders: {name} adds a name, {{ and }} are braces, anything else with a brace is a format error.</summary>
    internal static void Scan(string text, ISet<string> names)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '{')
            {
                if (i + 1 < text.Length && text[i + 1] == '{') { i++; continue; }
                var close = text.IndexOf('}', i);
                if (close < 0) throw Json.Refuse(LocaleCodes.Format, "an opening brace is never closed in '" + text + "'");
                names.Add(Json.CheckId(text[(i + 1)..close], "placeholder"));
                i = close;
            }
            else if (c == '}')
            {
                if (i + 1 < text.Length && text[i + 1] == '}') { i++; continue; }
                throw Json.Refuse(LocaleCodes.Format, "a closing brace has no opening one in '" + text + "'");
            }
        }
    }
}