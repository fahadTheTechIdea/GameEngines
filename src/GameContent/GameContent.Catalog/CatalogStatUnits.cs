namespace GameContent.Catalog;

/// <summary>
/// The closed table of units a stat may be measured in, by dimension. A stat names a dimension and a unit; the unit must belong to the
/// dimension, so "range in kilograms" cannot be written. An enum stat is dimensionless.
/// </summary>
public static class CatalogStatUnits
{
    private static readonly Dictionary<string, string[]> ByDimension = new(StringComparer.Ordinal)
    {
        ["length"] = ["m", "km"],
        ["speed"] = ["m/s", "km/h"],
        ["mass"] = ["kg", "g", "t"],
        ["energy"] = ["J", "kJ", "MJ"],
        ["power"] = ["W", "kW", "MW"],
        ["time"] = ["s", "min", "h"],
        ["count"] = ["{each}"],
        ["rate"] = ["/min", "/s"],
        ["ratio"] = ["1", "%"],
        ["volume"] = ["L", "m3"],
        ["angle"] = ["deg"],
        ["none"] = ["1"],
    };

    public static bool Allows(string dimension, string unit) => ByDimension.TryGetValue(dimension, out var units) && units.Contains(unit, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, string[]> Table => ByDimension;
}
