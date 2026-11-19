using Beep.SimEngine.Content;
using Beep.SimEngine.Standard;
using EconomyEngine;
using EngineCommon;
using GameContent.Catalog;

namespace GameRuntime;

/// <summary>
/// One recipe runs in one layer. A recipe authored once in the catalog may be generated into SimEngine (a per-entity transformation) and into the Economy (market-level production); a game that composes both must say
/// which layer owns each, and this refuses an assembly where a recipe would run in both. The rule itself, and the declaration it enforces, are <see cref="RecipeOwnership"/>'s (the catalog owns recipes); this reads the
/// two sets of recipe ids out of the loaded documents and asks it.
/// </summary>
public static class EconomyRecipeLayers
{
    public const string DoubleOwner = "ECON_RECIPE_DOUBLE_OWNER";

    /// <summary>Refuses the economy document and the composed SimEngine content when they both run a recipe of the same id.</summary>
    public static void Check(EconomySet economy, ResolvedContent sim)
    {
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(sim);
        var report = RecipeOwnership.CheckConversions(sim.All<ITransformationDefinition>().Select(t => t.Id), economy.Recipes.Select(r => r.Id));
        if (!report.IsValid) throw new EngineRefusedException(DoubleOwner, string.Join("; ", report.Errors.Select(e => e.Message)));
    }
}
