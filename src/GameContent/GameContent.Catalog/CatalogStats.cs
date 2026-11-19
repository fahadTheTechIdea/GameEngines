using System.Text;

namespace GameContent.Catalog;

/// <summary>What a pack contains and how deep its crafting runs, for an author who wants to see whether it is broad or thin.</summary>
public sealed record CatalogStats(
    string Pack,
    int Resources,
    int Recipes,
    int Stations,
    int Skills,
    IReadOnlyDictionary<string, int> ResourcesByClass,
    IReadOnlyDictionary<string, int> RecipesByKind,
    int Natural,
    int Crafted,
    int PricedResources,
    IReadOnlyDictionary<string, (int Translated, int Total)> Languages,
    int Tiers,
    double AverageInputs,
    IReadOnlyDictionary<string, int> WarningsByCode)
{
    /// <summary>Statistics of the pack's own content (not its dependencies), with warnings counted over the whole loaded set.</summary>
    public static CatalogStats Of(CatalogSet set, CatalogReport report)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(report);
        var own = set.Root;
        return new CatalogStats(
            own.Id, own.Resources.Count, own.Recipes.Count, own.Stations.Count, own.Skills.Count,
            own.Resources.GroupBy(r => Names<ResourceClass>.Of(r.Class)).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()),
            own.Recipes.GroupBy(r => Names<RecipeKind>.Of(r.Kind)).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()),
            own.Resources.Count(r => r.Origin == ResourceOrigin.Natural), own.Resources.Count(r => r.Origin == ResourceOrigin.Crafted),
            own.Resources.Count(r => CatalogPrices.Of(r) is not null), CatalogText.Coverage(own), Depth(set),
            own.Recipes.Count == 0 ? 0 : own.Recipes.Average(r => (double)r.Inputs.Count),
            report.Warnings.GroupBy(w => w.Code).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()));
    }

    /// <summary>The longest chain of crafting steps from natural resources to anything the set can make, taking each resource by its shortest route.</summary>
    private static int Depth(CatalogSet set)
    {
        var depth = set.Resources.Where(r => r.Origin == ResourceOrigin.Natural).ToDictionary(r => r.Id, _ => 0, StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            foreach (var recipe in set.Recipes)
            {
                var needs = recipe.Inputs.Concat(recipe.Catalysts).Select(i => i.Resource).Concat(recipe.Tools.Select(t => t.Resource)).ToList();
                if (needs.Any(n => !depth.ContainsKey(n))) continue;
                var level = needs.Count == 0 ? 1 : needs.Max(n => depth[n]) + 1;
                foreach (var made in recipe.Outputs.Select(o => o.Resource).Concat(recipe.Byproducts.Select(b => b.Resource)).Concat(recipe.Drops.Select(d => d.Resource)))
                    if (!depth.TryGetValue(made, out var current) || level < current) { depth[made] = level; changed = true; }
            }
        }
        while (changed);
        return depth.Count == 0 ? 0 : depth.Values.Max();
    }

    public string Format()
    {
        var text = new StringBuilder();
        text.Append("pack ").Append(Pack).Append(": ").Append(Resources).Append(" resources (").Append(Natural).Append(" natural, ").Append(Crafted).Append(" crafted, ")
            .Append(PricedResources).Append(" priced), ").Append(Recipes).Append(" recipes, ").Append(Stations).Append(" stations, ").Append(Skills).Append(" skills\n");
        text.Append("  languages: ").Append(string.Join(", ", Languages.Select(l => l.Key + " " + l.Value.Translated + "/" + l.Value.Total))).Append('\n');
        text.Append("  tiers=").Append(Tiers).Append(" average-inputs=").Append(AverageInputs.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        text.Append("  classes: ").Append(string.Join(", ", ResourcesByClass.Select(c => c.Key + "=" + c.Value))).Append('\n');
        text.Append("  kinds: ").Append(string.Join(", ", RecipesByKind.Select(k => k.Key + "=" + k.Value))).Append('\n');
        text.Append("  warnings: ").Append(WarningsByCode.Count == 0 ? "none" : string.Join(", ", WarningsByCode.Select(w => w.Key + "=" + w.Value)));
        return text.ToString();
    }
}
