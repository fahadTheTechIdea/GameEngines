namespace GameContent.Catalog;

/// <summary>Why a recipe or resource feature is reduced or left out when a pack becomes EconomyEngine production.</summary>
public static class EconomyConversionCodes
{
    // Refused: the recipe is left out because the Economy has no equivalent of the feature, or because it would not be right to run it there.
    public const string RandomYield = "CATALOG_ECONOMY_RANDOM_YIELD";
    public const string AmountRange = "CATALOG_ECONOMY_AMOUNT_RANGE";
    public const string SkillGate = "CATALOG_ECONOMY_SKILL_GATE";
    public const string Environment = "CATALOG_ECONOMY_ENVIRONMENT";
    public const string Quality = "CATALOG_ECONOMY_QUALITY";
    public const string NoInputs = "CATALOG_ECONOMY_NO_INPUTS";
    public const string NoOutputs = "CATALOG_ECONOMY_NO_OUTPUTS";
    public const string NotAGood = "CATALOG_ECONOMY_NOT_A_GOOD";
    public const string NoPrice = "CATALOG_ECONOMY_NO_PRICE";
    public const string PriceRange = "CATALOG_ECONOMY_PRICE_RANGE";
    public const string Precision = "CATALOG_ECONOMY_PRECISION";
    public const string TooManyGoods = "CATALOG_ECONOMY_TOO_MANY_GOODS";
    public const string DurationTooLong = "CATALOG_ECONOMY_DURATION_TOO_LONG";
    public const string Cycle = "CATALOG_ECONOMY_CYCLE";
    public const string OwnedBySim = "CATALOG_ECONOMY_OWNED_BY_SIM";
    public const string NoOwner = "CATALOG_ECONOMY_NO_OWNER";

    // Reduced: converted, with a stated loss.
    public const string BatchScaled = "CATALOG_ECONOMY_BATCH_SCALED";
    public const string DurationRounded = "CATALOG_ECONOMY_DURATION_ROUNDED";
    public const string RequirementNotAGood = "CATALOG_ECONOMY_REQUIREMENT_NOT_A_GOOD";
    public const string RequirementRounded = "CATALOG_ECONOMY_REQUIREMENT_ROUNDED";
    public const string RequirementSelf = "CATALOG_ECONOMY_REQUIREMENT_ON_OWN_GOOD";
    public const string RequirementCycle = "CATALOG_ECONOMY_REQUIREMENT_CYCLE";
    public const string StationAsLine = "CATALOG_ECONOMY_STATION_AS_LINE";
    public const string ReverseNotModelled = "CATALOG_ECONOMY_REVERSE_NOT_MODELLED";
    public const string ByproductsMerged = "CATALOG_ECONOMY_BYPRODUCTS_AS_OUTPUTS";
    public const string ResourceFields = "CATALOG_ECONOMY_RESOURCE_FIELDS_NOT_MODELLED";
    public const string DurabilityNotMapped = "CATALOG_ECONOMY_DURABILITY_NOT_MAPPED";
    public const string TagsTruncated = "CATALOG_ECONOMY_TAGS_TRUNCATED";
    public const string EraNotModelled = "CATALOG_ECONOMY_ERA_NOT_MODELLED";
}
