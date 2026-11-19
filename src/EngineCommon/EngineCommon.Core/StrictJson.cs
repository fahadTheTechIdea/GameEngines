using System.Text.Json;

namespace EngineCommon;

/// <summary>
/// Strict reading of an engine JSON document: every field present, none unknown, whole numbers in a stated range, identifiers of a fixed
/// alphabet, nothing defaulted. Every failure is a refusal with the engine own code.
/// </summary>
public sealed class StrictJson(RefusalCodes codes)
{
    public RefusalCodes Codes { get; } = codes;

    public EngineRefusedException Refuse(string code, string message) => new(code, message);

    /// <summary>The object itself, after checking that it has exactly the required fields plus any of the optional ones.</summary>
    public JsonElement Object(JsonElement value, string where, string[] required, params string[] optional)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Refuse(Codes.Format, where + " must be an object");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (System.Array.IndexOf(required, property.Name) < 0 && System.Array.IndexOf(optional, property.Name) < 0)
                throw Refuse(Codes.UnknownField, where + " has an unknown field '" + property.Name + "'");
            if (!seen.Add(property.Name)) throw Refuse(Codes.Duplicate, where + " repeats '" + property.Name + "'");
        }
        foreach (var field in required)
            if (!seen.Contains(field)) throw Refuse(Codes.MissingField, where + " is missing '" + field + "'");
        return value;
    }

    public bool Has(JsonElement owner, string field) => owner.TryGetProperty(field, out _);

    public long Long(JsonElement owner, string field, string where, long minimum, long maximum)
    {
        var value = owner.GetProperty(field);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var number)) throw Refuse(Codes.Format, where + "." + field + " must be a whole number");
        return number >= minimum && number <= maximum ? number : throw Refuse(Codes.Range, where + "." + field + " must be " + minimum + " to " + maximum + ", not " + number);
    }

    public int Int(JsonElement owner, string field, string where, int minimum, int maximum) => (int)Long(owner, field, where, minimum, maximum);

    public bool Bool(JsonElement owner, string field, string where)
    {
        var value = owner.GetProperty(field);
        return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : throw Refuse(Codes.Format, where + "." + field + " must be true or false");
    }

    public string Text(JsonElement owner, string field, string where, int maxLength = 256)
    {
        var value = owner.GetProperty(field);
        if (value.ValueKind != JsonValueKind.String) throw Refuse(Codes.Format, where + "." + field + " must be a string");
        var text = value.GetString()!;
        return text.Length <= maxLength ? text : throw Refuse(Codes.Range, where + "." + field + " is longer than " + maxLength);
    }

    /// <summary>A name made of lower-case letters, digits, hyphen, dot and underscore, so ids are the same in every file system, locale and language.</summary>
    public string Id(JsonElement owner, string field, string where)
    {
        var id = Text(owner, field, where, 128);
        return CheckId(id, where + "." + field);
    }

    public string CheckId(string id, string where)
    {
        if (id.Length == 0) throw Refuse(Codes.Format, where + " must not be empty");
        foreach (var c in id)
            if (!(c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c is '-' or '.' or '_'))
                throw Refuse(Codes.Format, where + " '" + id + "' may only use a-z, 0-9, hyphen, dot and underscore");
        return id;
    }

    public JsonElement Array(JsonElement owner, string field, string where, int minimum, int maximum)
    {
        var value = owner.GetProperty(field);
        if (value.ValueKind != JsonValueKind.Array) throw Refuse(Codes.Format, where + "." + field + " must be an array");
        var count = value.GetArrayLength();
        return count >= minimum && count <= maximum ? value : throw Refuse(Codes.Range, where + "." + field + " must hold " + minimum + " to " + maximum + " entries, not " + count);
    }

    public JsonDocument Parse(string json, string where)
    {
        try { return JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false, MaxDepth = 32 }); }
        catch (JsonException exception) { throw Refuse(Codes.Format, where + " is not valid JSON: " + exception.Message); }
    }
}