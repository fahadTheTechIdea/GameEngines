using System.Text.Json.Nodes;

namespace GameContent.Catalog;

/// <summary>
/// How prices behave for the goods a conversion makes. They are not catalog data (the catalog says what a resource is worth, not how a market's price responds), so the game states them; there is no hidden default.
/// Elasticities, floor, ceiling, speed, maximum step and inertia are per mille, decay is per mille a period, weight is the freight units a unit weighs.
/// </summary>
public sealed record GoodTuning(int DemandElasticity, int SupplyElasticity, int Floor, int Ceiling, int Speed, int MaxStep, int Inertia, int Decay, long Weight)
{
    /// <summary>The tuning the town sample's goods use. A game picks it, or states its own; nothing picks it for it.</summary>
    public static GoodTuning Town { get; } = new(600, 700, 250, 4000, 300, 80, 400, 0, 1);
}

/// <summary>
/// What the Economy needs to know that the catalog does not say. <paramref name="MilliPerPriceUnit"/>: thousandths of a minor unit of the reference currency in one unit of the catalog's <c>base-price</c>.
/// <paramref name="SecondsPerPeriod"/>: how long an economy period is, to turn a recipe's duration into periods. <paramref name="Locked"/>: the recipes the document declares locked (discovered later by a request).
/// <paramref name="Ownership"/>: when given, only the recipes it gives to the Economy are converted.
/// </summary>
public sealed record EconomyConversionOptions(long MilliPerPriceUnit, decimal SecondsPerPeriod, GoodTuning Tuning, IReadOnlyCollection<string> Locked, RecipeOwnership? Ownership = null);

/// <summary>
/// Economy content generated from a pack: the <c>goods</c> and <c>recipes</c> arrays of an economy document (text), the ids of both, the display names the document's localisation keys stand for, and the notes on
/// everything reduced or left out. The arrays are generated artifacts; the pack is the source. <see cref="Compose"/> puts them into a world that supplies everything else (currencies, markets, lines).
/// </summary>
public sealed record EconomyConversion(string GoodsJson, string RecipesJson, IReadOnlyList<string> Goods, IReadOnlyList<string> Recipes, IReadOnlyDictionary<string, string> Names, ConversionReport Report)
{
    /// <summary>
    /// The economy document made of <paramref name="world"/> (an economy document with no <c>goods</c> and no <c>recipes</c> field, whose markets trade the goods and run the recipes by their ids) and these two arrays. A world that already
    /// has either field is a caller's mistake: two owners of one fact.
    /// </summary>
    public string Compose(string world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (JsonNode.Parse(world) is not JsonObject root) throw new ArgumentException("an economy world is a JSON object", nameof(world));
        if (root.ContainsKey("goods") || root.ContainsKey("recipes")) throw new ArgumentException("the world already has goods or recipes; the conversion is their only source", nameof(world));
        root["goods"] = JsonNode.Parse(GoodsJson);
        root["recipes"] = JsonNode.Parse(RecipesJson);
        return root.ToJsonString();
    }
}
