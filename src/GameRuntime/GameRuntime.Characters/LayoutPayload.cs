using System.Text.Json;
using Beep.SimEngine;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>Reading of the optional spawn layout shared by the formation and individual spawn effects.</summary>
internal static class LayoutPayload
{
    /// <summary>A layout is explicit null (a game with no map) or an object with x, y, columns and spacing.</summary>
    public static bool Layout(JsonElement root, out SpawnLayout layout)
    {
        layout = SpawnLayout.None;
        var element = root.GetProperty("layout");
        if (element.ValueKind == JsonValueKind.Null) return true;
        if (element.ValueKind != JsonValueKind.Object) return false;
        int x = 0, y = 0, columns = 0, spacing = 0, seen = 0;
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out var number)) return false;
            int bit;
            switch (property.Name)
            {
                case "x": x = number; bit = 1; break;
                case "y": y = number; bit = 2; break;
                case "columns": columns = number; bit = 4; break;
                case "spacing": spacing = number; bit = 8; break;
                default: return false;
            }
            if ((seen & bit) != 0) return false;
            seen |= bit;
        }
        if (seen != 15) return false;
        layout = SpawnLayout.Grid(new TileCoord(x, y), columns, spacing);
        return true;
    }
}
