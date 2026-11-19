namespace GameContent.Catalog;

/// <summary>
/// The semantic rules of the standard, the ones a schema cannot state: identity and namespaces, references and visibility, units,
/// recipe shape, conservation, free-resource cycles, reachability and ambiguous combinations. It reports every finding with a code.
/// </summary>
public static partial class CatalogValidator
{
    /// <summary>Kinds that may bring a resource into the world without consuming it.</summary>
    public static readonly IReadOnlySet<RecipeKind> GenerativeKinds = new HashSet<RecipeKind> { RecipeKind.Extract, RecipeKind.Harvest, RecipeKind.Grow, RecipeKind.Loot };

    /// <summary>Kinds that may end with nothing made: needs consumed, upkeep paid, goods spoiled.</summary>
    public static readonly IReadOnlySet<RecipeKind> SinkKinds = new HashSet<RecipeKind> { RecipeKind.Consume, RecipeKind.Upkeep, RecipeKind.Decay };

    public static CatalogReport Validate(CatalogSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var report = new CatalogReport();
        CheckIdentity(set, report);
        CheckResources(set, report);
        CheckRecipes(set, report);
        CheckLocales(set, report);
        CheckOrigin(set, report);
        CheckEras(set, report);
        CheckStats(set, report);
        CheckItems(set, report);
        CheckFormations(set, report);
        // Whole-pack rules read the references the checks above have already vetted, so they run only on a coherent set.
        if (report.IsValid)
        {
            CheckConservation(set, report);
            CheckFreeCycles(set, report);
            CheckReachability(set, report);
            CheckAmbiguousCombines(set, report);
            CheckAnachronism(set, report);
            CheckItemRules(set, report);
            CheckFormationRules(set, report);
            CheckEconomy(set, report);
        }
        return report;
    }
}
