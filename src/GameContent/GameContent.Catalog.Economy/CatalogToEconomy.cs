using System.Text.Json.Nodes;
using EngineCommon.Graph;

namespace GameContent.Catalog;

/// <summary>
/// Turns a validated pack into EconomyEngine production: a recipe becomes market-level production over an acyclic recipe graph (inputs and outputs per run, a duration in periods, a locked flag), and the resources
/// it touches become goods with a base price. A recipe the Economy cannot run is refused with a code; a feature it can only approximate is reduced with a note. Nothing is dropped without a note. The markets,
/// currencies and production lines of a world are the game's: the conversion supplies the <c>goods</c> and <c>recipes</c> arrays (see <see cref="EconomyConversion.Compose"/>).
/// </summary>
/// <remarks>
/// Raw output without inputs (extraction, harvest) is not a recipe here: a market's <c>production</c> of a pair already owns it, so a second owner would be a duplicate. Amounts are whole numbers, so a
/// recipe with fractional amounts is run in the smallest batch that makes them whole (the note says by how much). Cycles are refused one recipe at a time, the most "backward" kind first.
/// </remarks>
public static class CatalogToEconomy
{
    /// <summary>The Economy document's own limits, which a conversion must stay inside (<c>EconomyReader</c> enforces the same numbers).</summary>
    public const long MaxAmount = 1_000_000;
    public const int MaxGoodsPerSide = 8;
    public const int MaxDuration = 64;
    public const int MaxBatch = 1000;
    public const int MaxTags = 8;
    public const long MinBase = 1000;
    public const long MaxBase = 1_000_000_000;

    /// <summary>A good a recipe needs held in stock. <paramref name="WearRuns"/> is 0 for a catalyst (kept by definition); for a tool it is the catalog runs one item lasts (<c>uses</c>), so the Economy's wear is the batch's catalog runs lost in that many of its runs.</summary>
    private sealed record Held(string Good, long Amount, long WearRuns);

    private sealed record Draft(CatalogRecipe Recipe, long Batch, (string Good, long Amount)[] Inputs, (string Good, long Amount)[] Outputs, int Duration, Held[] Requires);

    // Indexed by ResourceClass in declaration order (raw, intermediate, product, consumable, currency, energy, fluid, gas, information, labor, population, tool, equipment, unique); a value outside the enum fails the app.
    private static readonly bool[] GoodClasses = [true, true, true, true, false, true, true, true, false, false, false, true, true, false];

    private static bool IsGood(ResourceClass resourceClass) => GoodClasses[(int)resourceClass];

    // Indexed by TimeUnit in declaration order (s, min, h, d).
    private static readonly decimal[] UnitSeconds = [1m, 60m, 3600m, 86400m];

    private static decimal SecondsOf(TimeUnit unit) => UnitSeconds[(int)unit];

    /// <summary>How readily a recipe is given up to break a cycle: the steps that undo or maintain something go first.</summary>
    private static int GiveUpRank(RecipeKind kind) => GiveUp[(int)kind];

    // Indexed by RecipeKind in declaration order: extract, harvest, grow, transform, smelt, refine, craft, assemble, disassemble, cook, brew, combine, upgrade, repair, construct, research, trade, upkeep, consume, decay, loot.
    private static readonly int[] GiveUp = [8, 8, 8, 8, 8, 8, 8, 8, 0, 8, 8, 8, 3, 2, 8, 6, 5, 7, 4, 1, 8];

    public static EconomyConversion Convert(CatalogSet set, EconomyConversionOptions options)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(options);
        if (options.MilliPerPriceUnit < 1) throw new ArgumentException("a price unit is worth at least one milli", nameof(options));
        if (options.SecondsPerPeriod <= 0) throw new ArgumentException("a period lasts some time", nameof(options));
        foreach (var locked in options.Locked)
            if (!set.TryRecipe(locked, out _)) throw new ArgumentException("the recipe '" + locked + "' to lock is not in the pack", nameof(options));
        var report = new ConversionReport();
        var drafts = new List<Draft>();
        foreach (var recipe in set.Recipes.OrderBy(r => r.Id, StringComparer.Ordinal))
            if (Admit(set, options, recipe, report) is { } draft) drafts.Add(draft);
        drafts = BreakCycles(drafts, report);

        var goodIds = drafts.SelectMany(d => d.Inputs.Concat(d.Outputs).Concat(d.Requires.Select(h => (h.Good, h.Amount)))).Select(a => a.Good).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var goods = new JsonArray();
        var names = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in goodIds)
        {
            set.TryResource(id, out var resource);
            goods.Add(GoodNode(resource, options, report, drafts.SelectMany(d => d.Requires).Where(h => h.Good == id && h.WearRuns > 0).Select(h => h.WearRuns).Distinct().Order().ToList()));
            names["good." + id] = resource.Name;
        }
        var recipes = new JsonArray();
        foreach (var draft in drafts)
        {
            recipes.Add(new JsonObject
            {
                ["id"] = draft.Recipe.Id, ["key"] = "recipe." + draft.Recipe.Id,
                ["inputs"] = Amounts(draft.Inputs), ["outputs"] = Amounts(draft.Outputs), ["duration"] = draft.Duration, ["locked"] = options.Locked.Contains(draft.Recipe.Id), ["requires"] = Requirements(draft),
            });
            names["recipe." + draft.Recipe.Id] = draft.Recipe.Name;
        }
        if (drafts.Any(d => set.TryAvailability(d.Recipe.Id, out _)))
            report.Reduce(EconomyConversionCodes.EraNotModelled, "pack", "recipe availability by era is not modelled: a recipe is open or locked, and the game unlocks it when its era comes");
        return new EconomyConversion(goods.ToJsonString(), recipes.ToJsonString(), goodIds, drafts.Select(d => d.Recipe.Id).ToList(), names, report);
    }

    private static JsonArray Amounts((string Good, long Amount)[] amounts) => new(amounts.Select(a => (JsonNode)new JsonObject { ["good"] = a.Good, ["amount"] = a.Amount }).ToArray());

    /// <summary>A recipe's held goods as the document writes them: a tool also states how it wears, <c>{"units": catalog runs in a run, "runs": its uses}</c>, which is the catalog's "each run wears 1/uses of one item" exactly.</summary>
    private static JsonArray Requirements(Draft draft) => new(draft.Requires.Select(h =>
    {
        var node = new JsonObject { ["good"] = h.Good, ["amount"] = h.Amount };
        if (h.WearRuns > 0) node["wear"] = new JsonObject { ["units"] = draft.Batch, ["runs"] = h.WearRuns };
        return (JsonNode)node;
    }).ToArray());

    private static long BaseOf(CatalogResource resource, EconomyConversionOptions options) =>
        (long)Math.Round(CatalogPrices.Of(resource)!.Value * options.MilliPerPriceUnit, MidpointRounding.AwayFromZero);

    /// <summary>The good as the document writes it. <paramref name="toolUses"/> are the distinct <c>uses</c> with which the kept recipes hold this good as a worn tool: they are the only wear the Economy has for it.</summary>
    private static JsonObject GoodNode(CatalogResource resource, EconomyConversionOptions options, ConversionReport report, List<long> toolUses)
    {
        var tuning = options.Tuning;
        var tags = resource.Tags.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (tags.Count > MaxTags)
        {
            report.Reduce(EconomyConversionCodes.TagsTruncated, resource.Id, "the Economy keeps " + MaxTags + " tags a good; the first " + MaxTags + " in order are kept of " + tags.Count);
            tags = tags.Take(MaxTags).ToList();
        }
        if (resource.Perishable is not null || resource.Grade is not null || resource.StackMax > 1)
            report.Reduce(EconomyConversionCodes.ResourceFields, resource.Id, "spoilage, grade and stack size are not modelled; the good's decay is the tuning's (" + tuning.Decay + " per mille a period)");
        if (resource.DurabilityUses is { } life)
            report.Reduce(EconomyConversionCodes.DurabilityNotMapped, resource.Id, "the item's total life (durability uses " + life + ") is not read: the Economy wears a good only through the recipes that hold it as a tool, and a recipe's uses (each run wears 1/uses of one item) is a count of its own, not tied to the item's life. "
                + (toolUses.Count == 0 ? "No kept recipe holds this good as a worn tool, so it does not wear in the Economy."
                    : "The recipes' own uses wear it (" + string.Join(", ", toolUses) + "); the item's life changes nothing."));
        return new JsonObject
        {
            ["id"] = resource.Id, ["key"] = "good." + resource.Id, ["base"] = BaseOf(resource, options), ["demandElasticity"] = tuning.DemandElasticity, ["supplyElasticity"] = tuning.SupplyElasticity,
            ["floor"] = tuning.Floor, ["ceiling"] = tuning.Ceiling, ["speed"] = tuning.Speed, ["maxStep"] = tuning.MaxStep, ["inertia"] = tuning.Inertia, ["decay"] = tuning.Decay, ["weight"] = tuning.Weight,
            ["tags"] = new JsonArray(tags.Select(t => (JsonNode)t).ToArray()),
        };
    }

    /// <summary>Whether the Economy can run the recipe as production; if so its draft, else null with the refusal recorded.</summary>
    private static Draft? Admit(CatalogSet set, EconomyConversionOptions options, CatalogRecipe recipe, ConversionReport report)
    {
        string? code = null;
        var detail = "";
        if (options.Ownership is { } ownership)
        {
            var layer = ownership.LayerOf(recipe.Id);
            if (layer is null) (code, detail) = (EconomyConversionCodes.NoOwner, "the ownership declaration does not say which layer runs this recipe");
            else if (layer == RecipeLayer.Sim) (code, detail) = (EconomyConversionCodes.OwnedBySim, "the ownership declaration gives this recipe to SimEngine; it runs there, not as Economy production");
        }
        if (code is null)
        {
            if (recipe.Drops.Count > 0) (code, detail) = (EconomyConversionCodes.RandomYield, "chance-based drops: production makes fixed amounts");
            else if (recipe.Outputs.Concat(recipe.Byproducts).Any(o => o.Min != o.Max)) (code, detail) = (EconomyConversionCodes.AmountRange, "output ranges: production makes fixed amounts");
            else if (recipe.Skill is not null) (code, detail) = (EconomyConversionCodes.SkillGate, "the Economy has no skill levels");
            else if (recipe.Environment.Count > 0) (code, detail) = (EconomyConversionCodes.Environment, "environment tags have no equivalent in production");
            else if (recipe.Quality is not null) (code, detail) = (EconomyConversionCodes.Quality, "the Economy has no quality model");
            else if (recipe.Inputs.Count == 0) (code, detail) = (EconomyConversionCodes.NoInputs, "production consumes goods; what a market makes from nothing is the pair's own production");
            else if (recipe.Outputs.Count + recipe.Byproducts.Count == 0) (code, detail) = (EconomyConversionCodes.NoOutputs, "production makes goods; a recipe that only consumes is not production");
        }
        var consumed = recipe.Inputs.Select(i => (i.Resource, i.Amount)).ToList();
        var made = recipe.Outputs.Concat(recipe.Byproducts).GroupBy(o => o.Resource, StringComparer.Ordinal).Select(g => (Resource: g.Key, Amount: g.Sum(o => o.Max))).OrderBy(o => o.Resource, StringComparer.Ordinal).ToList();
        if (code is null)
            foreach (var resource in consumed.Select(c => c.Resource).Concat(made.Select(m => m.Resource)).Distinct(StringComparer.Ordinal))
            {
                set.TryResource(resource, out var found);
                if (!IsGood(found.Class)) { (code, detail) = (EconomyConversionCodes.NotAGood, "'" + resource + "' is " + Names<ResourceClass>.Of(found.Class) + ", not a tradable good"); break; }
                if (CatalogPrices.Of(found) is null) { (code, detail) = (EconomyConversionCodes.NoPrice, "'" + resource + "' has no base-price, so a market cannot price it"); break; }
                var price = BaseOf(found, options);
                if (price < MinBase || price > MaxBase) { (code, detail) = (EconomyConversionCodes.PriceRange, "'" + resource + "' is worth " + price + " milli, outside " + MinBase + " to " + MaxBase); break; }
            }
        if (code is null && (consumed.Count > MaxGoodsPerSide || made.Count > MaxGoodsPerSide || recipe.Catalysts.Count + recipe.Tools.Count > MaxGoodsPerSide)) (code, detail) = (EconomyConversionCodes.TooManyGoods, "more than " + MaxGoodsPerSide + " goods on one side of the recipe (inputs, outputs, or catalysts and tools together)");
        long batch = 1;
        if (code is null)
        {
            var batchOrNull = BatchOf(consumed.Select(c => c.Amount).Concat(made.Select(m => m.Amount)));
            if (batchOrNull is null || consumed.Concat(made).Any(a => a.Amount * batchOrNull.Value > MaxAmount)) (code, detail) = (EconomyConversionCodes.Precision, "an amount cannot be made whole by a batch of at most " + MaxBatch + " runs, or passes " + MaxAmount);
            else batch = batchOrNull.Value;
        }
        var seconds = recipe.Duration.Value * SecondsOf(recipe.Duration.Unit);
        var periods = seconds == 0 ? 0 : Math.Ceiling(seconds / options.SecondsPerPeriod);
        if (code is null && periods > MaxDuration) (code, detail) = (EconomyConversionCodes.DurationTooLong, "the recipe takes " + periods + " periods; production holds at most " + MaxDuration);
        if (code is not null)
        {
            report.Refuse(code, recipe.Id, detail);
            return null;
        }
        if (batch > 1) report.Reduce(EconomyConversionCodes.BatchScaled, recipe.Id, "amounts are whole numbers, so a run is " + batch + " catalog runs at once (same duration)");
        if (periods * options.SecondsPerPeriod != seconds) report.Reduce(EconomyConversionCodes.DurationRounded, recipe.Id, seconds + " seconds are rounded up to " + periods + " periods of " + options.SecondsPerPeriod + " seconds");
        var requires = Requirements(set, options, recipe, consumed.Select(c => c.Resource).ToList(), made.Select(m => m.Resource).ToList(), report);
        if (recipe.Station is not null) report.Reduce(EconomyConversionCodes.StationAsLine, recipe.Id, "station '" + recipe.Station.Station + "' level " + recipe.Station.Level + " is the market's production line for the recipe; the level is not modelled");
        if (recipe.Byproducts.Count > 0) report.Reduce(EconomyConversionCodes.ByproductsMerged, recipe.Id, "byproducts are outputs of the run");
        if (recipe.Reverse is not null) report.Reduce(EconomyConversionCodes.ReverseNotModelled, recipe.Id, "the reverse link is not modelled");
        return new Draft(recipe, batch, consumed.Select(c => (c.Resource, (long)(c.Amount * batch))).OrderBy(c => c.Resource, StringComparer.Ordinal).ToArray(),
            made.Select(m => (m.Resource, (long)(m.Amount * batch))).ToArray(), (int)periods, requires);
    }

    /// <summary>
    /// What the recipe needs held in stock but never uses up: its catalysts (kept by definition) and its tools (one held tool stands for the tool the recipe needs). They become the Economy's <c>requires</c>, whole
    /// amounts of tradable priced goods, not scaled by the batch (a batch of runs needs the same tool). A catalyst or tool that is not a tradable priced good, one the recipe also consumes or makes, or an amount that
    /// is not whole, is reduced with a note rather than refusing a recipe the Economy can otherwise run. A tool's wear is the catalog's own declaration (<c>uses</c>: each run wears 1/uses of one item) and is supported
    /// exactly, so no note says it was lost; a catalyst never wears.
    /// </summary>
    private static Held[] Requirements(CatalogSet set, EconomyConversionOptions options, CatalogRecipe recipe, List<string> consumed, List<string> made, ConversionReport report)
    {
        var held = recipe.Catalysts.Select(c => (c.Resource, c.Amount, Kind: "catalyst", Uses: 0L)).Concat(recipe.Tools.Select(t => (t.Resource, Amount: 1m, Kind: "tool", Uses: (long)t.Uses))).ToList();
        var result = new List<Held>();
        foreach (var (resource, amount, kind, uses) in held)
        {
            set.TryResource(resource, out var found);
            string? why = null;
            if (!IsGood(found.Class)) why = "is " + Names<ResourceClass>.Of(found.Class) + ", not a tradable good";
            else if (CatalogPrices.Of(found) is null) why = "has no base-price, so a market cannot price it";
            else if (BaseOf(found, options) is var price && (price < MinBase || price > MaxBase)) why = "is worth " + price + " milli, outside " + MinBase + " to " + MaxBase;
            else if (consumed.Contains(resource) || made.Contains(resource)) { report.Reduce(EconomyConversionCodes.RequirementSelf, recipe.Id, kind + " '" + resource + "' is also consumed or made by the recipe; a requirement on its own good is not modelled, so it is left out"); continue; }
            if (why is not null)
            {
                report.Reduce(EconomyConversionCodes.RequirementNotAGood, recipe.Id, kind + " '" + resource + "' " + why + "; the recipe runs without it");
                continue;
            }
            var whole = (long)Math.Ceiling(amount);
            if (whole != amount) report.Reduce(EconomyConversionCodes.RequirementRounded, recipe.Id, kind + " '" + resource + "' needs " + amount + ", rounded up to " + whole + " held");
            result.Add(new Held(resource, whole, uses));       // the catalog schema bounds a tool's uses to 1 to 2,000,000,000, which is exactly the Economy's range of wear runs
        }
        return result.OrderBy(r => r.Good, StringComparer.Ordinal).ToArray();
    }

    /// <summary>The smallest number of runs (1 to <see cref="MaxBatch"/>) that makes every amount a whole number, or null when none does.</summary>
    private static long? BatchOf(IEnumerable<decimal> amounts)
    {
        var list = amounts.ToList();
        for (var k = 1; k <= MaxBatch; k++)
            if (list.All(a => a * k == decimal.Truncate(a * k))) return k;
        return null;
    }

    /// <summary>
    /// Refuses recipes until the rest form no cycle (production runs over a chain, in order). Each round finds one cycle with the graph library, and gives up the recipe of the ring that is most readily given up
    /// (a disassembly before a repair before a craft, ...), the highest id on a tie, so the same pack always loses the same recipes.
    /// </summary>
    private static List<Draft> BreakCycles(List<Draft> drafts, ConversionReport report)
    {
        var live = drafts.ToList();
        var scratch = new GraphScratch();
        while (live.Count > 0)
        {
            var order = live.OrderBy(d => d.Recipe.Id, StringComparer.Ordinal).ToList();
            var builder = Builder(order.Count);
            var makers = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (var i = 0; i < order.Count; i++)
                foreach (var output in order[i].Outputs)
                {
                    if (!makers.TryGetValue(output.Good, out var list)) makers[output.Good] = list = new List<int>();
                    list.Add(i);
                }
            for (var i = 0; i < order.Count; i++)
                foreach (var input in order[i].Inputs.Concat(order[i].Requires.Select(h => (h.Good, h.Amount))))
                    if (makers.TryGetValue(input.Good, out var list))
                        foreach (var maker in list)
                            if (builder.Add(maker, i, 0).Length > 0) throw new InvalidOperationException("the recipe graph is too large for the graph library");
            var built = builder.Build();
            if (!built.Ok) throw new InvalidOperationException("the recipe graph could not be built: " + built.Code);
            var sorted = TopologicalSort.Run(built.Graph!, scratch);
            if (sorted.Code != GraphCodes.Cycle) return order;
            var ring = sorted.Cycle.Select(n => order[n]).ToList();
            if (DropOneRequirement(ring, live, report)) continue;
            var victim = ring.OrderBy(d => GiveUpRank(d.Recipe.Kind)).ThenByDescending(d => d.Recipe.Id, StringComparer.Ordinal).First();
            report.Refuse(EconomyConversionCodes.Cycle, victim.Recipe.Id, "recipes " + string.Join(" > ", ring.Select(d => d.Recipe.Id)) + " feed each other in a cycle; production runs over a chain without cycles, so this one is left out");
            live.Remove(victim);
        }
        return live;
    }

    /// <summary>
    /// A cycle that closes only through a held requirement (a tool or catalyst made by the chain it is needed in) is broken by giving up that requirement, with a note, not the recipe: a requirement is the
    /// less essential link. Returns false when every link of the ring is a consumed input, so a recipe must go instead.
    /// </summary>
    private static bool DropOneRequirement(List<Draft> ring, List<Draft> live, ConversionReport report)
    {
        for (var i = 0; i < ring.Count; i++)
        {
            var maker = ring[i];
            var user = ring[(i + 1) % ring.Count];
            if (user.Inputs.Any(x => maker.Outputs.Any(o => o.Good == x.Good))) continue;
            var dropped = user.Requires.Where(r => maker.Outputs.Any(o => o.Good == r.Good)).ToArray();
            if (dropped.Length == 0) continue;
            live[live.IndexOf(user)] = user with { Requires = user.Requires.Except(dropped).ToArray() };
            foreach (var gone in dropped)
                report.Reduce(EconomyConversionCodes.RequirementCycle, user.Recipe.Id, "'" + gone.Good + "' is held by this recipe and made by '" + maker.Recipe.Id + "', which would close a cycle in the chain; the requirement is left out and the recipe kept");
            return true;
        }
        return false;
    }

    private static GraphBuilder Builder(int nodes)
    {
        var code = GraphBuilder.Create(nodes, out var builder);
        if (code.Length > 0) throw new InvalidOperationException("the recipe graph is too large for the graph library: " + code);
        return builder!;
    }
}
