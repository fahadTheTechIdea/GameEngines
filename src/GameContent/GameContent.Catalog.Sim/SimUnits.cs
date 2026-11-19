namespace GameContent.Catalog;

/// <summary>
/// How a catalog UCUM unit becomes a SimEngine unit. SimEngine measures capacity in one unit per dimension, so every UCUM unit of a
/// dimension is converted to that dimension's base unit by an exact factor, and amounts are multiplied when written.
/// </summary>
public static class SimUnits
{
    public sealed record Base(string Id, string Dimension, bool Discrete);

    private static readonly Dictionary<string, Base> BaseByDimension = new(StringComparer.Ordinal)
    {
        [CatalogUnits.Count] = new("catalog.count", "count", true),
        [CatalogUnits.Mass] = new("catalog.mass", "mass", false),
        [CatalogUnits.Volume] = new("catalog.volume", "volume", false),
        [CatalogUnits.Energy] = new("catalog.energy", "energy", false),
        [CatalogUnits.Time] = new("catalog.time", "time", false),
        [CatalogUnits.Information] = new("catalog.information", "information", true),
        [CatalogUnits.Money] = new("catalog.money", "money", true),
        [CatalogUnits.Population] = new("catalog.population", "population", true),
    };

    // Factor from the UCUM code to its dimension's base unit (kg, L, kJ, h; counts are their own base).
    private static readonly Dictionary<string, decimal> Factors = new(StringComparer.Ordinal)
    {
        ["{each}"] = 1m, ["{point}"] = 1m, ["By"] = 1m, ["{coin}"] = 1m, ["{person}"] = 1m,
        ["kg"] = 1m, ["g"] = 0.001m, ["t"] = 1000m,
        ["L"] = 1m, ["m3"] = 1000m,
        ["kJ"] = 1m, ["J"] = 0.001m, ["MJ"] = 1000m, ["W.h"] = 3.6m, ["kW.h"] = 3600m,
        ["h"] = 1m, ["s"] = 1m / 3600m, ["min"] = 1m / 60m, ["d"] = 24m,
    };

    public static IReadOnlyCollection<Base> Bases => BaseByDimension.Values;

    /// <summary>The base SimEngine unit for a UCUM code. The code must be one the catalog supports.</summary>
    public static Base BaseOf(string ucum) => BaseByDimension[CatalogUnits.TryGet(ucum, out var unit) ? unit.Dimension : throw new ArgumentException("unsupported unit " + ucum, nameof(ucum))];

    /// <summary>The amount in the dimension's base unit.</summary>
    public static decimal ToBase(string ucum, decimal amount) => amount * Factors[ucum];
}
