using System.Text.Json;

namespace GameRuntime;

/// <summary>
/// Strict reading of a director effect's JSON payload: every field is present, none is unknown, numbers are whole. A payload that does
/// not read is refused by the effect's Check with the engine's invalid-argument code, so a malformed request never reaches the world.
/// </summary>
public static class EffectPayload
{
    public static JsonElement? Parse(string json, params string[] fields)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(json); }
        catch (JsonException) { return null; }
        // The document is cloned out of its pooled buffer; the clone is read-only data owned by the caller.
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (Array.IndexOf(fields, property.Name) < 0 || !seen.Add(property.Name)) return null;

            }
            if (seen.Count != fields.Length) return null;
            return root.Clone();
        }
    }

    public static bool Int(JsonElement root, string name, out int value)
    {
        value = 0;
        return root.GetProperty(name).ValueKind == JsonValueKind.Number && root.GetProperty(name).TryGetInt32(out value);
    }

    public static bool ULong(JsonElement root, string name, out ulong value)
    {
        value = 0;
        return root.GetProperty(name).ValueKind == JsonValueKind.Number && root.GetProperty(name).TryGetUInt64(out value);
    }

    public static bool Text(JsonElement root, string name, out string value)
    {
        value = "";
        var element = root.GetProperty(name);
        if (element.ValueKind != JsonValueKind.String) return false;
        value = element.GetString()!;
        return value.Length > 0;
    }
}
