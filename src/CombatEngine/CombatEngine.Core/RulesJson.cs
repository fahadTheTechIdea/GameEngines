using System.Text.Json;

namespace CombatEngine;

/// <summary>Strict reading of the rules JSON: every field present, none unknown, whole numbers in a stated range, nothing defaulted.</summary>
internal static class RulesJson
{
    public static JsonElement Object(JsonElement value, string where, params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Refuse(CombatCodes.RulesFormat, where + " must be an object");
        foreach (var property in value.EnumerateObject())
            if (Array.IndexOf(fields, property.Name) < 0) throw Refuse(CombatCodes.RulesUnknownField, where + " has an unknown field '" + property.Name + "'");
        foreach (var field in fields)
            if (!value.TryGetProperty(field, out _)) throw Refuse(CombatCodes.RulesMissingField, where + " is missing '" + field + "'");
        return value;
    }

    public static int Int(JsonElement owner, string field, string where, int minimum, int maximum)
    {
        var value = owner.GetProperty(field);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number)) throw Refuse(CombatCodes.RulesFormat, where + "." + field + " must be a whole number");
        return number >= minimum && number <= maximum ? number : throw Refuse(CombatCodes.RulesRange, where + "." + field + " must be " + minimum + " to " + maximum + ", not " + number);
    }

    public static bool Bool(JsonElement owner, string field, string where)
    {
        var value = owner.GetProperty(field);
        return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : throw Refuse(CombatCodes.RulesFormat, where + "." + field + " must be true or false");
    }

    public static string Id(JsonElement owner, string field, string where)
    {
        var value = owner.GetProperty(field);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(value.GetString())) throw Refuse(CombatCodes.RulesFormat, where + "." + field + " must be a non-empty string");
        var id = value.GetString()!;
        foreach (var c in id)
            if (!(c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c == '-')) throw Refuse(CombatCodes.RulesFormat, where + "." + field + " '" + id + "' may only use a-z, 0-9 and hyphen");
        return id;
    }

    public static IEnumerable<JsonElement> Items(JsonElement owner, string field, string where)
    {
        var value = owner.GetProperty(field);
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0) throw Refuse(CombatCodes.RulesFormat, where + "." + field + " must be a non-empty array");
        return value.EnumerateArray();
    }

    public static CombatRefusedException Refuse(string code, string message) => new(code, message);
}
