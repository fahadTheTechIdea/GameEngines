namespace GameContent.Catalog;

/// <summary>Every code a pack can be refused with. A code names one rule; a diagnostic also names where.</summary>
public static class CatalogCodes
{
    // Loading
    public const string Schema = "CATALOG_SCHEMA";
    public const string FileMissing = "CATALOG_FILE_MISSING";
    public const string NotJson = "CATALOG_NOT_JSON";
    public const string PackId = "CATALOG_PACK_ID";
    public const string DocumentPack = "CATALOG_DOCUMENT_PACK";
    public const string DependencyMissing = "CATALOG_DEPENDENCY_MISSING";
    public const string DependencyVersion = "CATALOG_DEPENDENCY_VERSION";
    public const string DependencyCycle = "CATALOG_DEPENDENCY_CYCLE";
    public const string LocalePack = "CATALOG_LOCALE_PACK";
    public const string LocaleLanguage = "CATALOG_LOCALE_LANGUAGE";
    public const string LocaleDuplicate = "CATALOG_LOCALE_DUPLICATE";
    public const string LocaleUnknownId = "CATALOG_LOCALE_UNKNOWN_ID";

    // Identity and references
    public const string DuplicateId = "CATALOG_DUPLICATE_ID";
    public const string IdNamespace = "CATALOG_ID_NAMESPACE";
    public const string UnknownRef = "CATALOG_UNKNOWN_REF";
    public const string NotVisible = "CATALOG_NOT_VISIBLE";
    public const string DuplicateIngredient = "CATALOG_DUPLICATE_INGREDIENT";

    // Resources
    public const string UnknownUnit = "CATALOG_UNKNOWN_UNIT";
    public const string UnitClass = "CATALOG_UNIT_CLASS";
    public const string UnitDiscrete = "CATALOG_UNIT_DISCRETE";
    public const string Currency = "CATALOG_CURRENCY";
    public const string Stack = "CATALOG_STACK";
    public const string Durability = "CATALOG_DURABILITY";
    public const string Perishable = "CATALOG_PERISHABLE";
    public const string Origin = "CATALOG_ORIGIN";
    public const string BadPrice = "CATALOG_BAD_PRICE";

    // Recipes
    public const string BadAmount = "CATALOG_BAD_AMOUNT";
    public const string BadChance = "CATALOG_BAD_CHANCE";
    public const string NoInputs = "CATALOG_NO_INPUTS";
    public const string NoOutputs = "CATALOG_NO_OUTPUTS";
    public const string ToolConflict = "CATALOG_TOOL_CONFLICT";
    public const string StationLevel = "CATALOG_STATION_LEVEL";
    public const string SkillLevel = "CATALOG_SKILL_LEVEL";
    public const string Reverse = "CATALOG_REVERSE";
    public const string Quality = "CATALOG_QUALITY";

    // Whole-pack rules
    public const string NotConserved = "CATALOG_NOT_CONSERVED";
    public const string FreeCycle = "CATALOG_FREE_CYCLE";
    public const string Unreachable = "CATALOG_UNREACHABLE";
    public const string AmbiguousCombine = "CATALOG_AMBIGUOUS_COMBINE";

    // Eras, availability, stats and items
    public const string EraOrder = "CATALOG_ERA_ORDER";
    public const string EraSpan = "CATALOG_ERA_SPAN";
    public const string EraWorld = "CATALOG_ERA_WORLD";
    public const string EraWindow = "CATALOG_ERA_WINDOW";
    public const string EraAnachronism = "CATALOG_ERA_ANACHRONISM";
    public const string EraLineage = "CATALOG_ERA_LINEAGE";
    public const string AvailabilityDuplicate = "CATALOG_AVAILABILITY_DUPLICATE";
    public const string StatUnit = "CATALOG_STAT_UNIT";
    public const string StatShape = "CATALOG_STAT_SHAPE";
    public const string ItemResource = "CATALOG_ITEM_RESOURCE";
    public const string ItemStat = "CATALOG_ITEM_STAT";
    public const string ItemLink = "CATALOG_ITEM_LINK";
    public const string ItemLineage = "CATALOG_ITEM_LINEAGE";
    public const string ItemNoBuild = "CATALOG_ITEM_NO_BUILD";
    public const string ItemWorld = "CATALOG_ITEM_WORLD";
    public const string UnitResource = "CATALOG_UNIT_RESOURCE";
    public const string UnitEquipment = "CATALOG_UNIT_EQUIPMENT";
    public const string UnitCrew = "CATALOG_UNIT_CREW";
    public const string UnitLineage = "CATALOG_UNIT_LINEAGE";
    public const string UnitNoBuild = "CATALOG_UNIT_NO_BUILD";

    // Library rules (across the packs of one library)
    public const string DuplicateName = "CATALOG_DUPLICATE_NAME";
    public const string DuplicateSlug = "CATALOG_DUPLICATE_SLUG";

    // Warnings: balance and authoring concerns
    public const string ValueLoop = "CATALOG_VALUE_LOOP";
    public const string ValueOutlier = "CATALOG_VALUE_OUTLIER";
    public const string DurationOutlier = "CATALOG_DURATION_OUTLIER";
    public const string EnergyOutlier = "CATALOG_ENERGY_OUTLIER";
    public const string Chokepoint = "CATALOG_CHOKEPOINT";
}
