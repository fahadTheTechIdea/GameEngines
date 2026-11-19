using System.Text.Json;
using EngineCommon.Graph;

namespace EconomyEngine;

internal sealed partial class EconomyReader
{
    public const int MaxRecipes = 1000;
    public const int MaxRecipeGoods = 8;
    public const long MaxRecipeAmount = 1_000_000;
    public const long MaxLineCapacity = 1_000_000;
    public const int MaxRecipeDuration = 64;
    public const long MaxWearUnits = 1_000_000;
    public const long MaxWearRuns = 2_000_000_000;

    private IReadOnlyList<RecipeDef> _recipes = Array.Empty<RecipeDef>();
    private Dictionary<string, int> _recipeIndex = new(StringComparer.Ordinal);

    /// <summary>
    /// Reads the recipes. A recipe consumes at least one good (what a market makes out of nothing is the pair's own production, not a recipe) and makes at least one; a good is named once on each side; the amounts and
    /// the duration are whole numbers. A recipe that is both a consumer and a maker of one good is a cycle of one, and is refused with the others by <see cref="OrderRecipes"/>.
    /// </summary>
    private IReadOnlyList<RecipeDef> ReadRecipes(JsonElement array)
    {
        var list = new List<RecipeDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "recipes[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "inputs", "outputs", "duration", "locked"], "requires");
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "recipe");
            var inputs = ReadAmounts(_json.Array(element, "inputs", where, 1, MaxRecipeGoods), where + ".inputs", "consumes");
            var outputs = ReadAmounts(_json.Array(element, "outputs", where, 1, MaxRecipeGoods), where + ".outputs", "makes");
            var requires = _json.Has(element, "requires") ? ReadRequirements(_json.Array(element, "requires", where, 0, MaxRecipeGoods), where + ".requires") : [];
            foreach (var need in requires)
            {
                if (inputs.Any(a => a.Good == need.Good)) throw _json.Refuse(EconomyCodes.Recipe, where + " requires good '" + _goods[need.Good].Id + "' and also consumes it; a good is either used up (an input) or only held (a requirement)");
                if (outputs.Any(a => a.Good == need.Good)) throw _json.Refuse(EconomyCodes.RecipeCycle, where + " requires good '" + _goods[need.Good].Id + "' and also makes it: a recipe cannot depend on its own output (production runs over a chain without cycles)");
            }
            list.Add(new RecipeDef(list.Count, id, _json.Id(element, "key", where), inputs, outputs, _json.Int(element, "duration", where, 0, MaxRecipeDuration), _json.Bool(element, "locked", where), requires));
        }
        _recipeIndex = Index(list, r => r.Id);
        return list;
    }

    private IReadOnlyList<RecipeAmount> ReadAmounts(JsonElement array, string where, string verb) =>
        ReadSide(array, where, verb, [], (_, _, good, amount) => new RecipeAmount(good, amount));

    /// <summary>The goods a recipe requires held: each with a whole amount and, optionally, how it wears out (<c>"wear": {"units": u, "runs": n}</c>: u units lost for every n runs).</summary>
    private IReadOnlyList<RecipeRequirement> ReadRequirements(JsonElement array, string where) =>
        ReadSide(array, where, "requires", ["wear"], (element, at, good, amount) => new RecipeRequirement(good, amount, ReadWear(element, at)));

    private IReadOnlyList<T> ReadSide<T>(JsonElement array, string where, string verb, string[] optional, Func<JsonElement, string, int, long, T> make) where T : RecipeAmount
    {
        var list = new List<T>();
        foreach (var element in array.EnumerateArray())
        {
            var at = where + "[" + list.Count + "]";
            _json.Object(element, at, ["good", "amount"], optional);
            var good = Ref(element, "good", at, _goodIndex, "good");
            if (list.Any(a => a.Good == good)) throw _json.Refuse(EconomyCodes.Recipe, at + " " + verb + " good '" + _goods[good].Id + "' twice; name a good once and give its whole amount");
            list.Add(make(element, at, good, _json.Long(element, "amount", at, 1, MaxRecipeAmount)));
        }
        return list;
    }

    private WearDef? ReadWear(JsonElement requirement, string where)
    {
        if (!_json.Has(requirement, "wear")) return null;
        var at = where + ".wear";
        var element = _json.Object(requirement.GetProperty("wear"), at, ["units", "runs"]);
        return new WearDef(_json.Long(element, "units", at, 1, MaxWearUnits), _json.Long(element, "runs", at, 1, MaxWearRuns));
    }

    /// <summary>
    /// The lines of a market: for each, the recipe and the most runs it starts a period. The market must trade every good of the recipe (a line works on the market's stock), and has at most one line for a recipe.
    /// </summary>
    private IReadOnlyList<LineDef> ReadLines(JsonElement array, int market, IReadOnlyList<PairDef> pairs, string owner, ref int lineCount)
    {
        var list = new List<LineDef>();
        var seen = new HashSet<int>();
        foreach (var element in array.EnumerateArray())
        {
            var where = owner + ".lines[" + list.Count + "]";
            _json.Object(element, where, ["recipe", "capacity"], "throttle");
            var recipe = Ref(element, "recipe", where, _recipeIndex, "recipe");
            if (!seen.Add(recipe)) throw _json.Refuse(EconomyCodes.Duplicate, where + " runs recipe '" + _recipes[recipe].Id + "' twice in one market");
            var capacity = _json.Long(element, "capacity", where, 1, MaxLineCapacity);
            var def = _recipes[recipe];
            list.Add(new LineDef(lineCount++, market, recipe, capacity, PairsOf(def.Inputs, pairs, where, def), PairsOf(def.Outputs, pairs, where, def), PairsOf(def.Requires, pairs, where, def), ReadThrottle(element, where)));
        }
        return list;
    }

    /// <summary>
    /// The optional margin throttle of a line: <c>{"stop": a, "full": b}</c> in per mille of what one run earns over what it costs, 0 up to a ratio the attribute range of the AI percept allows, stop under full. Absent: the
    /// line never looks at prices. With <c>"costs": {"wage": w, "overhead": o, "indexed": bool}</c> a run also costs its wage and overhead (the profit margin); without it a run costs its inputs only.
    /// </summary>
    private ThrottleDef? ReadThrottle(JsonElement line, string where)
    {
        if (!_json.Has(line, "throttle")) return null;
        var at = where + ".throttle";
        var element = _json.Object(line.GetProperty("throttle"), at, ["stop", "full"], "costs");
        var stop = _json.Int(element, "stop", at, 0, MaxThrottleRatio);
        var full = _json.Int(element, "full", at, 1, MaxThrottleRatio);
        if (stop >= full) throw _json.Refuse(EconomyCodes.Range, at + ".stop (" + stop + ") must be under full (" + full + ")");
        return new ThrottleDef(stop, full, ReadCosts(element, at));
    }

    private LineCostsDef? ReadCosts(JsonElement throttle, string where)
    {
        if (!_json.Has(throttle, "costs")) return null;
        var at = where + ".costs";
        var element = _json.Object(throttle.GetProperty("costs"), at, ["wage", "overhead", "indexed"]);
        return new LineCostsDef(_json.Long(element, "wage", at, 0, EconomyFacts.MaxPrice), _json.Long(element, "overhead", at, 0, EconomyFacts.MaxPrice), _json.Bool(element, "indexed", at));
    }

    public const int MaxThrottleRatio = 100_000;

    private int[] PairsOf(IReadOnlyList<RecipeAmount> amounts, IReadOnlyList<PairDef> pairs, string where, RecipeDef recipe)
    {
        var slots = new int[amounts.Count];
        for (var i = 0; i < slots.Length; i++)
        {
            var good = amounts[i].Good;
            var pair = -1;
            for (var p = 0; p < pairs.Count; p++)
                if (pairs[p].Good == good) { pair = p; break; }
            if (pair < 0) throw _json.Refuse(EconomyCodes.Line, where + " runs recipe '" + recipe.Id + "' but the market does not trade '" + _goods[good].Id + "'");
            slots[i] = pair;
        }
        return slots;
    }

    /// <summary>
    /// The order production runs in: recipes sorted so that a recipe comes after every recipe that makes one of its inputs, ties broken by the recipe id (ordinally), so a reordered document changes nothing. A
    /// recipe graph with a cycle (a chain that feeds itself) is refused, naming the recipes and the goods that close it. Returns the recipe indexes in that order.
    /// </summary>
    private int[] OrderRecipes(IReadOnlyList<RecipeDef> recipes)
    {
        if (recipes.Count == 0) return [];
        var byId = Enumerable.Range(0, recipes.Count).ToArray();
        Array.Sort(byId, (a, b) => string.CompareOrdinal(recipes[a].Id, recipes[b].Id));
        var node = new int[recipes.Count];
        for (var rank = 0; rank < byId.Length; rank++) node[byId[rank]] = rank;
        if (GraphBuilder.Create(recipes.Count, out var builder).Length > 0) throw _json.Refuse(EconomyCodes.Range, "too many recipes for the recipe graph");
        var makers = new List<int>[_goods.Count];
        foreach (var recipe in recipes)
            foreach (var output in recipe.Outputs) (makers[output.Good] ??= new List<int>()).Add(recipe.Index);
        foreach (var recipe in recipes)
            foreach (var input in recipe.Inputs.Concat(recipe.Requires))
                if (makers[input.Good] is { } made)
                    foreach (var maker in made)
                        if (builder!.Add(node[maker], node[recipe.Index], 0).Length > 0) throw _json.Refuse(EconomyCodes.Range, "the recipes link more goods than the recipe graph can hold");
        var built = builder!.Build();
        if (!built.Ok) throw _json.Refuse(EconomyCodes.Range, "the recipe graph could not be built: " + built.Code);
        var graph = built.Graph!;
        var sorted = TopologicalSort.Run(graph, new GraphScratch());
        if (sorted.Code == GraphCodes.Cycle)
        {
            var ring = sorted.Cycle;
            var links = new List<string>();
            for (var i = 0; i < ring.Length; i++)
            {
                var from = recipes[byId[ring[i]]];
                var to = recipes[byId[ring[(i + 1) % ring.Length]]];
                var good = from.Outputs.First(o => to.Inputs.Concat(to.Requires).Any(x => x.Good == o.Good)).Good;
                links.Add(from.Id + " makes " + _goods[good].Id + " for " + to.Id);
            }
            throw _json.Refuse(EconomyCodes.RecipeCycle, "recipes feed each other in a cycle (" + string.Join(", ", links) + "); production runs over a chain without cycles");
        }
        var order = new int[recipes.Count];
        for (var i = 0; i < order.Length; i++) order[i] = byId[sorted.Order[i]];
        return order;
    }
}
