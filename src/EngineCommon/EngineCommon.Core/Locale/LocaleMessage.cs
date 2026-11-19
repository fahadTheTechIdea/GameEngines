namespace EngineCommon;

/// <summary>
/// A message: plain text, or a set of forms chosen by an argument named by <paramref name="Select"/> (by CLDR plural category for <see cref="MessageSelect.Cardinal"/> and
/// <see cref="MessageSelect.Ordinal"/>, by exact value for <see cref="MessageSelect.Value"/>). Placeholders are {name}; {{ and }} are braces.
/// </summary>
public sealed record LocaleMessage(string? Text, string? Select, IReadOnlyDictionary<string, string>? Forms, MessageSelect Mode = MessageSelect.Cardinal)
{
    /// <summary>The message as the JSON a locale table holds: a string, or an object of forms (with its <c>type</c> when it is not a cardinal one).</summary>
    public System.Text.Json.Nodes.JsonNode ToJson()
    {
        if (Text is not null) return System.Text.Json.Nodes.JsonValue.Create(Text)!;
        var forms = new System.Text.Json.Nodes.JsonObject();
        if (Mode != MessageSelect.Cardinal) forms["type"] = Mode == MessageSelect.Ordinal ? "ordinal" : "select";
        forms["select"] = Select;
        foreach (var (name, text) in Forms!) forms[name] = text;
        return forms;
    }

    /// <summary>Every placeholder name any form of the message uses, sorted.</summary>
    public IReadOnlyList<string> Placeholders()
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        if (Text is not null) LocaleTable.Scan(Text, names);
        if (Forms is not null) foreach (var form in Forms.Values) LocaleTable.Scan(form, names);
        if (Select is not null) names.Add(Select);
        return names.ToList();
    }
}