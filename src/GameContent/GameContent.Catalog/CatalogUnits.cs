namespace GameContent.Catalog;

/// <summary>What a UCUM code means to the catalog: its dimension and whether quantities of it are whole numbers.</summary>
public sealed record CatalogUnit(string Code, string Dimension, bool Discrete);

/// <summary>
/// The closed table of UCUM codes a pack may use as a resource unit. UCUM is case-sensitive; codes in braces are UCUM annotations
/// (counts of things). The table is deliberately small: a unit not listed is refused, not guessed.
/// </summary>
public static class CatalogUnits
{
    public const string Count = "count";
    public const string Mass = "mass";
    public const string Volume = "volume";
    public const string Energy = "energy";
    public const string Time = "time";
    public const string Information = "information";
    public const string Money = "money";
    public const string Population = "population";

    private static readonly CatalogUnit[] All =
    [
        new("{each}", Count, true),
        new("kg", Mass, false), new("g", Mass, false), new("t", Mass, false),
        new("L", Volume, false), new("m3", Volume, false),
        new("J", Energy, false), new("kJ", Energy, false), new("MJ", Energy, false), new("W.h", Energy, false), new("kW.h", Energy, false),
        new("s", Time, false), new("min", Time, false), new("h", Time, false), new("d", Time, false),
        new("By", Information, true), new("{point}", Information, true),
        new("{coin}", Money, true),
        new("{person}", Population, true),
    ];

    private static readonly Dictionary<string, CatalogUnit> ByCode = All.ToDictionary(u => u.Code, StringComparer.Ordinal);

    public static IReadOnlyCollection<CatalogUnit> Supported => All;

    public static bool TryGet(string code, out CatalogUnit unit) => ByCode.TryGetValue(code, out unit!);

    /// <summary>The unit dimensions a resource class may be measured in. Classes that can be measured several ways list each.</summary>
    public static IReadOnlyCollection<string> DimensionsOf(ResourceClass resourceClass) => Allowed[(int)resourceClass];

    // Indexed by ResourceClass in declaration order; a value outside the enum fails the app instead of being given a default.
    private static readonly string[][] Allowed =
    [
        [Count, Mass, Volume],   // Raw
        [Count, Mass, Volume],   // Intermediate
        [Count, Mass, Volume],   // Product
        [Count, Mass, Volume],   // Consumable
        [Money],                 // Currency
        [Energy],                // Energy
        [Volume],                // Fluid
        [Volume],                // Gas
        [Information],           // Information
        [Time],                  // Labor
        [Population],            // Population
        [Count],                 // Tool
        [Count],                 // Equipment
        [Count],                 // Unique
    ];
}
