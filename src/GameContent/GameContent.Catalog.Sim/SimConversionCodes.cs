namespace GameContent.Catalog;

/// <summary>Why a recipe or resource feature is reduced or left out when a pack becomes SimEngine content.</summary>
public static class SimConversionCodes
{
    // Refused: the recipe is left out because SimEngine has no equivalent of the feature.
    public const string Drops = "CATALOG_SIM_RANDOM_YIELD";
    public const string Range = "CATALOG_SIM_AMOUNT_RANGE";
    public const string Skill = "CATALOG_SIM_SKILL_GATE";
    public const string Environment = "CATALOG_SIM_ENVIRONMENT";
    public const string Quality = "CATALOG_SIM_QUALITY";
    public const string NoOutputs = "CATALOG_SIM_NO_OUTPUTS";
    public const string Instant = "CATALOG_SIM_INSTANT";
    public const string Precision = "CATALOG_SIM_PRECISION";
    public const string VehicleNoSpeed = "CATALOG_SIM_VEHICLE_NO_SPEED";
    public const string OwnedByEconomy = "CATALOG_SIM_OWNED_BY_ECONOMY";
    public const string NoOwner = "CATALOG_SIM_NO_OWNER";

    // Reduced: converted with a stated loss.
    public const string Perishable = "CATALOG_SIM_PERISHABLE_NOT_MODELLED";
    public const string Grade = "CATALOG_SIM_GRADE_NOT_MODELLED";
    public const string Stack = "CATALOG_SIM_STACK_NOT_MODELLED";
    public const string VehicleStats = "CATALOG_SIM_VEHICLE_STATS_NOT_MODELLED";
    public const string NoCombatEngine = "CATALOG_SIM_NO_COMBAT_ENGINE";
    public const string EraNotModelled = "CATALOG_SIM_ERA_NOT_MODELLED";
    public const string StationLevelsAsEntities = "CATALOG_SIM_STATION_LEVELS_AS_ENTITIES";
    public const string DiscreteUnits = "CATALOG_SIM_DISCRETE_NOT_ENFORCED";
    public const string ReverseLink = "CATALOG_SIM_REVERSE_NOT_MODELLED";
}
