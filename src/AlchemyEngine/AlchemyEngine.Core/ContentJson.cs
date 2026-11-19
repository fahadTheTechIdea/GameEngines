using System.Text.Json;

namespace AlchemyEngine;

/// <summary>Strict reading of the content JSON: every field must be present, no field may be unknown, and nothing is defaulted.</summary>
internal static class ContentJson
{
    public static JsonElement Object(JsonElement value, string where, params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Refuse(AlchemyCodes.ContentFormat, where + " must be an object");
        foreach (var property in value.EnumerateObject())
            if (Array.IndexOf(fields, property.Name) < 0) throw Refuse(AlchemyCodes.ContentUnknownField, where + " has an unknown field '" + property.Name + "'");
        foreach (var field in fields)
            if (!value.TryGetProperty(field, out _)) throw Refuse(AlchemyCodes.ContentMissingField, where + " is missing '" + field + "'");
        return value;
    }

    public static string String(JsonElement owner, string field, string where)
    {
        var value = owner.GetProperty(field);
        return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()! : throw Refuse(AlchemyCodes.ContentFormat, where + "." + field + " must be a non-empty string");
    }

    public static bool Bool(JsonElement owner, string field, string where)
    {
        var value = owner.GetProperty(field);
        return value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean() : throw Refuse(AlchemyCodes.ContentFormat, where + "." + field + " must be true or false");
    }

    public static int Int(JsonElement owner, string field, string where)
    {
        var value = owner.GetProperty(field);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number : throw Refuse(AlchemyCodes.ContentFormat, where + "." + field + " must be a whole number");
    }

    public static List<string> Ids(JsonElement owner, string field, string where)
    {
        var value = owner.GetProperty(field);
        if (value.ValueKind != JsonValueKind.Array) throw Refuse(AlchemyCodes.ContentFormat, where + "." + field + " must be an array");
        var list = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) throw Refuse(AlchemyCodes.ContentFormat, where + "." + field + " must hold strings");
            list.Add(RequireId(item.GetString()!, where + "." + field));
        }
        return list;
    }

    /// <summary>Ids are lower-case words so a list of them can be joined with commas or plus signs without ambiguity.</summary>
    public static string RequireId(string id, string where)
    {
        if (id.Length is < 1 or > 64) throw Refuse(AlchemyCodes.ContentBadId, where + ": id '" + id + "' must be 1..64 characters");
        foreach (var c in id)
            if (!(c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c is '.' or '_' or '-'))
                throw Refuse(AlchemyCodes.ContentBadId, where + ": id '" + id + "' may only use a-z, 0-9, dot, underscore and hyphen");
        return id;
    }

    public static AlchemyRefusedException Refuse(string code, string message) => new(code, message);
}
