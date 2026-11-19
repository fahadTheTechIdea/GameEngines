namespace GameContent.Catalog;

public static partial class CatalogValidator
{
    private const decimal TradeProfitLimit = 1.25m, TradeLossLimit = 0.4m, CraftGainLimit = 30m, CraftLossLimit = 0.03m;
    private const double OutlierFactor = 1000;
    private const int ChokepointConsumers = 8;
    private const int MinimumSample = 4;

    /// <summary>Balance and authoring concerns. They never refuse a pack; they are reported as warnings and the shipped packs are held to none.</summary>
    private static void CheckEconomy(CatalogSet set, CatalogReport report)
    {
        CheckValueLoops(set, report);
        CheckValueOutliers(set, report);
        CheckDurationOutliers(set, report);
        CheckEnergyOutliers(set, report);
        CheckChokepoints(set, report);
    }

    /// <summary>The expected value of what a recipe makes (ranges at their mean, drops at their chance), or null if any product is unpriced.</summary>
    private static decimal? ProductValue(CatalogSet set, CatalogRecipe recipe)
    {
        decimal total = 0;
        foreach (var line in recipe.Outputs.Concat(recipe.Byproducts))
        {
            if (!set.TryResource(line.Resource, out var resource) || CatalogPrices.Of(resource) is not { } price) return null;
            total += price * (line.Min + line.Max) / 2;
        }
        foreach (var drop in recipe.Drops)
        {
            if (!set.TryResource(drop.Resource, out var resource) || CatalogPrices.Of(resource) is not { } price) return null;
            total += price * (drop.Min + drop.Max) / 2 * drop.Numerator / drop.Denominator;
        }
        return total;
    }

    private static decimal? InputValue(CatalogSet set, CatalogRecipe recipe)
    {
        decimal total = 0;
        foreach (var line in recipe.Inputs)
        {
            if (!set.TryResource(line.Resource, out var resource) || CatalogPrices.Of(resource) is not { } price) return null;
            total += price * line.Amount;
        }
        return total;
    }

    /// <summary>
    /// A loop of single-input recipes whose value multiplies to more than one makes value from nothing (buy, sell and buy again at
    /// a profit). Every product counts, so this also catches loops that break even in counts but keep a valuable side product. A pure
    /// quantity loop is already an error (FreeCycle); a value loop is a warning because crafts legitimately add value one way.
    /// </summary>
    private static void CheckValueLoops(CatalogSet set, CatalogReport report)
    {
        var nodes = new Dictionary<string, int>(StringComparer.Ordinal);
        var edges = new List<(int From, int To, double Gain, string Recipe)>();
        int Node(string id) => nodes.TryGetValue(id, out var n) ? n : nodes[id] = nodes.Count;
        foreach (var recipe in set.Recipes.Where(r => !GenerativeKinds.Contains(r.Kind) && r.Inputs.Count == 1))
        {
            if (InputValue(set, recipe) is not { } cost || ProductValue(set, recipe) is not { } made || made <= 0) continue;
            var input = recipe.Inputs[0].Resource;
            foreach (var output in recipe.Outputs.Where(o => o.Resource != input).Take(1))
                edges.Add((Node(input), Node(output.Resource), Math.Log((double)(made / cost)), recipe.Id));
        }
        if (edges.Count == 0) return;
        var best = new double[nodes.Count];
        var via = new int[nodes.Count];
        Array.Fill(via, -1);
        var improved = -1;
        for (var pass = 0; pass < nodes.Count; pass++)
        {
            improved = -1;
            for (var e = 0; e < edges.Count; e++)
            {
                var (from, to, gain, _) = edges[e];
                if (best[from] + gain > best[to] + 1e-9) { best[to] = best[from] + gain; via[to] = e; improved = to; }
            }
            if (improved < 0) return;
        }
        var inside = improved;
        for (var i = 0; i < nodes.Count; i++) inside = edges[via[inside]].From;
        var loop = new List<string>();
        var at = inside;
        do { var edge = edges[via[at]]; loop.Add(edge.Recipe); at = edge.From; } while (at != inside);
        loop.Reverse();
        report.Warn(CatalogCodes.ValueLoop, "going round these recipes returns more value than it takes: " + string.Join(" -> ", loop), loop[0]);
    }

    /// <summary>A trade should not make or lose a lot of value; a craft should neither make value from scraps nor destroy it.</summary>
    private static void CheckValueOutliers(CatalogSet set, CatalogReport report)
    {
        foreach (var recipe in set.Recipes.Where(r => !GenerativeKinds.Contains(r.Kind) && !SinkKinds.Contains(r.Kind) && r.Inputs.Count > 0))
        {
            if (InputValue(set, recipe) is not { } cost || cost <= 0 || ProductValue(set, recipe) is not { } made) continue;
            var ratio = made / cost;
            var (low, high) = recipe.Kind == RecipeKind.Trade ? (TradeLossLimit, TradeProfitLimit) : (CraftLossLimit, CraftGainLimit);
            if (ratio < low || ratio > high)
                report.Warn(CatalogCodes.ValueOutlier, "makes " + Math.Round(ratio, 2) + " times the value it consumes (expected " + low + " to " + high + " for a " + Names<RecipeKind>.Of(recipe.Kind) + ")", recipe.Id);
        }
    }

    // Indexed by TimeUnit in declaration order: S, Min, H, D.
    private static readonly double[] SecondsPerUnit = [1, 60, 3600, 86400];

    private static double Seconds(Duration duration)
    {
        return (double)duration.Value * SecondsPerUnit[(int)duration.Unit];
    }

    private static double Median(List<double> sorted) => sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;

    /// <summary>Within one kind of recipe, a duration a thousand times the median (or a thousandth of it) is almost always a slipped unit.</summary>
    private static void CheckDurationOutliers(CatalogSet set, CatalogReport report)
    {
        foreach (var group in set.Recipes.Where(r => r.Duration.Value > 0).GroupBy(r => r.Kind).Where(g => g.Count() >= MinimumSample))
        {
            var median = Median(group.Select(r => Seconds(r.Duration)).Order().ToList());
            foreach (var recipe in group)
            {
                var ratio = Seconds(recipe.Duration) / median;
                if (ratio > OutlierFactor || ratio < 1 / OutlierFactor)
                    report.Warn(CatalogCodes.DurationOutlier, "takes " + Math.Round(ratio, 3) + " times the median " + Names<RecipeKind>.Of(recipe.Kind) + " duration (" + Math.Round(median) + " s)", recipe.Id);
            }
        }
    }

    // Joules per catalog energy unit; zero for a unit that is not energy.
    private static double Joules(string unit) => unit switch { "J" => 1, "kJ" => 1e3, "MJ" => 1e6, "W.h" => 3600, "kW.h" => 3.6e6, _ => 0 };

    private static double EnergyIn(CatalogSet set, CatalogRecipe recipe) =>
        recipe.Inputs.Sum(i => set.TryResource(i.Resource, out var r) ? Joules(r.Unit) * (double)i.Amount : 0);

    /// <summary>The energy a recipe consumes, against the median of recipes that consume any: a thousandfold gap is a slipped unit.</summary>
    private static void CheckEnergyOutliers(CatalogSet set, CatalogReport report)
    {
        var users = set.Recipes.Where(r => EnergyIn(set, r) > 0).ToList();
        if (users.Count < MinimumSample) return;
        var median = Median(users.Select(r => EnergyIn(set, r)).Order().ToList());
        foreach (var recipe in users)
        {
            var ratio = EnergyIn(set, recipe) / median;
            if (ratio > OutlierFactor || ratio < 1 / OutlierFactor)
                report.Warn(CatalogCodes.EnergyOutlier, "consumes " + Math.Round(ratio, 3) + " times the median energy of recipes that use any", recipe.Id);
        }
    }

    /// <summary>A made resource with one way to make it that many recipes depend on: one failure point for the whole economy.</summary>
    private static void CheckChokepoints(CatalogSet set, CatalogReport report)
    {
        var makers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var users = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        static void Add(Dictionary<string, HashSet<string>> map, string resource, string recipe)
        {
            if (!map.TryGetValue(resource, out var members)) map[resource] = members = new HashSet<string>(StringComparer.Ordinal);
            members.Add(recipe);
        }
        foreach (var recipe in set.Recipes)
        {
            foreach (var line in recipe.Outputs.Concat(recipe.Byproducts)) Add(makers, line.Resource, recipe.Id);
            foreach (var drop in recipe.Drops) Add(makers, drop.Resource, recipe.Id);
            foreach (var line in recipe.Inputs.Concat(recipe.Catalysts)) Add(users, line.Resource, recipe.Id);
            foreach (var tool in recipe.Tools) Add(users, tool.Resource, recipe.Id);
        }
        foreach (var resource in set.Resources.Where(r => r.Origin == ResourceOrigin.Crafted))
        {
            var made = makers.TryGetValue(resource.Id, out var m) ? m.Count : 0;
            var spoils = set.Resources.Count(r => r.Perishable?.Result == resource.Id);
            if (made + spoils != 1) continue;
            var used = users.TryGetValue(resource.Id, out var u) ? u.Count : 0;
            if (used >= ChokepointConsumers)
                report.Warn(CatalogCodes.Chokepoint, "has one way to be made and " + used + " recipes need it", resource.Id);
        }
    }
}
