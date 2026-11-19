namespace GameContent.Catalog;

/// <summary>Why a recipe or resource is reduced or left out when a pack becomes AlchemyEngine content.</summary>
public static class AlchemyConversionCodes
{
    // Refused: left out of the alchemy content. What alchemy genuinely cannot say.
    public const string VesselTooBig = "CATALOG_ALCHEMY_VESSEL_TOO_BIG";
    public const string Unreachable = "CATALOG_ALCHEMY_UNREACHABLE";

    // Reduced: converted, with a stated loss or a stated stand-in.
    public const string Amounts = "CATALOG_ALCHEMY_AMOUNTS_NOT_MODELLED";
    public const string Duration = "CATALOG_ALCHEMY_DURATION_NOT_MODELLED";
    public const string StationAsEnvironment = "CATALOG_ALCHEMY_STATION_AS_ENVIRONMENT";
    public const string SkillAsEnvironment = "CATALOG_ALCHEMY_SKILL_AS_ENVIRONMENT";
    public const string QualityNotModelled = "CATALOG_ALCHEMY_QUALITY_NOT_MODELLED";
    public const string ReverseNotModelled = "CATALOG_ALCHEMY_REVERSE_NOT_MODELLED";
    public const string ToolsAsCatalysts = "CATALOG_ALCHEMY_TOOLS_AS_CATALYSTS";
    public const string ByproductsAsOutputs = "CATALOG_ALCHEMY_BYPRODUCTS_AS_OUTPUTS";
    public const string GatheringAsToolUse = "CATALOG_ALCHEMY_GATHERING_AS_TOOL_USE";
    public const string HandCatalyst = "CATALOG_ALCHEMY_HAND_CATALYST";
    public const string ConsumptionOnly = "CATALOG_ALCHEMY_CONSUMPTION_ONLY";
    public const string DropAsSeparateReaction = "CATALOG_ALCHEMY_DROP_AS_SEPARATE_REACTION";
    public const string DropAsRecipeChance = "CATALOG_ALCHEMY_DROP_AS_RECIPE_CHANCE";
    public const string SpoilageAsReaction = "CATALOG_ALCHEMY_SPOILAGE_AS_REACTION";
    public const string ResourceFields = "CATALOG_ALCHEMY_RESOURCE_FIELDS_NOT_MODELLED";
    public const string EraAsEnvironment = "CATALOG_ALCHEMY_ERA_AS_ENVIRONMENT";
    public const string ItemStats = "CATALOG_ALCHEMY_ITEM_STATS_NOT_MODELLED";
    public const string MethodTag = "CATALOG_ALCHEMY_METHOD_TAG";
}
