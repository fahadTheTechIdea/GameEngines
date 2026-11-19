namespace GameContent.Catalog;

public static partial class CatalogValidator
{
    /// <summary>
    /// A recipe that is not generative may not make more of a resource than it takes in, counting the most it can make. Without this a
    /// "recycle" or "refine" recipe is a free-resource machine.
    /// </summary>
    private static void CheckConservation(CatalogSet set, CatalogReport report)
    {
        foreach (var recipe in set.Recipes.Where(r => !GenerativeKinds.Contains(r.Kind)))
        {
            var made = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var line in recipe.Outputs.Concat(recipe.Byproducts)) made[line.Resource] = made.GetValueOrDefault(line.Resource) + line.Max;
            foreach (var drop in recipe.Drops) made[drop.Resource] = made.GetValueOrDefault(drop.Resource) + drop.Max;
            foreach (var input in recipe.Inputs)
                if (made.TryGetValue(input.Resource, out var amount) && amount > input.Amount)
                    report.Add(CatalogCodes.NotConserved, "makes up to " + amount + " of '" + input.Resource + "' from " + input.Amount + "; a " + Names<RecipeKind>.Of(recipe.Kind) + " recipe may not gain what it consumes", recipe.Id);
        }
    }

    /// <summary>
    /// Finds loops of single-input recipes whose amounts multiply to more than one, so going round them makes a resource from nothing
    /// (barter arbitrage, recycle loops). Recipes with several inputs are not part of this check; conservation covers their own resource.
    /// </summary>
    private static void CheckFreeCycles(CatalogSet set, CatalogReport report)
    {
        var nodes = new Dictionary<string, int>(StringComparer.Ordinal);
        var edges = new List<(int From, int To, double Gain, string Recipe)>();
        int Node(string id) => nodes.TryGetValue(id, out var n) ? n : nodes[id] = nodes.Count;
        foreach (var recipe in set.Recipes.Where(r => !GenerativeKinds.Contains(r.Kind) && r.Inputs.Count == 1))
        {
            var input = recipe.Inputs[0];
            foreach (var output in recipe.Outputs.Concat(recipe.Byproducts).Where(o => o.Resource != input.Resource))
                edges.Add((Node(input.Resource), Node(output.Resource), Math.Log((double)(output.Max / input.Amount)), recipe.Id));
        }
        if (edges.Count == 0) return;

        // Longest-path relaxation from a virtual source: if it still improves after one pass per node, a loop gains.
        var best = new double[nodes.Count];
        var via = new int[nodes.Count];
        Array.Fill(via, -1);
        int improved = -1;
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

        // Step back far enough to be inside the loop, then name the recipes around it.
        var inside = improved;
        for (var i = 0; i < nodes.Count; i++) inside = edges[via[inside]].From;
        var loop = new List<string>();
        var at = inside;
        do
        {
            var edge = edges[via[at]];
            loop.Add(edge.Recipe);
            at = edge.From;
        }
        while (at != inside);
        loop.Reverse();
        report.Add(CatalogCodes.FreeCycle, "going round these recipes makes more than it takes: " + string.Join(" -> ", loop), loop[0]);
    }
}
