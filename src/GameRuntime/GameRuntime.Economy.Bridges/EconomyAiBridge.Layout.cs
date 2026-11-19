using AIEngine;
using EconomyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

// The percept layout of the bridge: which goods and recipes each percept kind tells about, the attribute names, the check of every kind against the AI document, and the values.
public sealed partial class EconomyAiBridge
{
    /// <summary>The attribute id of a good's price ratio in the percept.</summary>
    public static string RatioAttribute(GoodDef good) => "ratio_" + Safe(good.Id);

    /// <summary>The attribute id of a good's shortage flag in the percept.</summary>
    public static string ShortAttribute(GoodDef good) => "short_" + Safe(good.Id);

    /// <summary>The attribute id of the best value a unit of the good could fetch from the market, over the market's own price, in the percept.</summary>
    public static string ReachAttribute(GoodDef good) => "reach_" + Safe(good.Id);

    /// <summary>The attribute id of the production margin of a recipe in the percept.</summary>
    public static string MarginAttribute(RecipeDef recipe) => "margin_" + Safe(recipe.Id);

    private static string Safe(string id) => id.Replace('.', '_').Replace('-', '_');

    /// <summary>
    /// Every good of the document, in document order, split into goods percept kinds of at most <see cref="GoodsPerKind"/> goods: the first is named <paramref name="kindPrefix"/>, the next
    /// <paramref name="kindPrefix"/>-2, <paramref name="kindPrefix"/>-3 and so on. Nothing is left out; an AI document declares as many kinds as this returns.
    /// </summary>
    public static IReadOnlyList<EconomyGoodsPercept> PlanGoods(EconomySet set, string kindPrefix)
    {
        ArgumentNullException.ThrowIfNull(set);
        return Chunks(set.Goods.Select(g => g.Id).ToList(), GoodsPerKind).Select((ids, i) => new EconomyGoodsPercept(KindName(kindPrefix, i), ids)).ToList();
    }

    /// <summary>Every recipe of the document, in document order, split into margin percept kinds of at most <see cref="RecipesPerKind"/> recipes, named like <see cref="PlanGoods"/> names its kinds.</summary>
    public static IReadOnlyList<EconomyMarginPercept> PlanMargins(EconomySet set, string kindPrefix)
    {
        ArgumentNullException.ThrowIfNull(set);
        return Chunks(set.Recipes.Select(r => r.Id).ToList(), RecipesPerKind).Select((ids, i) => new EconomyMarginPercept(KindName(kindPrefix, i), ids)).ToList();
    }

    private static string KindName(string prefix, int index) => index == 0 ? prefix : prefix + "-" + (index + 1);

    private static IEnumerable<IReadOnlyList<string>> Chunks(IReadOnlyList<string> ids, int size)
    {
        for (var start = 0; start < ids.Count; start += size) yield return ids.Skip(start).Take(size).ToList();
    }

    private static List<(string Kind, IReadOnlyList<GoodDef> Goods)> ResolveGoods(EconomySet set, IReadOnlyList<EconomyGoodsPercept> groups, HashSet<string> kinds)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<(string, IReadOnlyList<GoodDef>)>();
        foreach (var group in groups)
        {
            if (!kinds.Add(group.Kind)) throw new EngineRefusedException(BadKind, "percept kind '" + group.Kind + "' is named by two groups");
            if (group.Goods.Count > GoodsPerKind) throw new EngineRefusedException(BadKind, "percept kind '" + group.Kind + "' would tell about " + group.Goods.Count + " goods, three attributes each; an AI percept kind holds at most 16 attributes, so " + GoodsPerKind + " goods: split the goods over several kinds (PlanGoods does)");
            var tracked = new List<GoodDef>();
            foreach (var good in group.Goods)
            {
                if (!set.GoodById.TryGetValue(good, out var def)) throw new EngineRefusedException(BadKind, "the percept is to tell about good '" + good + "', which the economy document does not have");
                if (!seen.Add(good)) throw new EngineRefusedException(BadKind, "good '" + good + "' is named twice");
                tracked.Add(def);
            }
            result.Add((group.Kind, tracked.OrderBy(g => g.Index).ToList()));
        }
        return result;
    }

    private static List<(string Kind, IReadOnlyList<RecipeDef> Recipes)> ResolveMargins(EconomySet set, IReadOnlyList<EconomyMarginPercept> groups, HashSet<string> kinds)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<(string, IReadOnlyList<RecipeDef>)>();
        foreach (var group in groups)
        {
            if (!kinds.Add(group.Kind)) throw new EngineRefusedException(BadKind, "percept kind '" + group.Kind + "' is named by two groups");
            if (group.Recipes.Count > RecipesPerKind) throw new EngineRefusedException(BadKind, "percept kind '" + group.Kind + "' would tell about " + group.Recipes.Count + " recipes; an AI percept kind holds at most 16 attributes, so " + RecipesPerKind + " recipes: split them over several kinds (PlanMargins does)");
            var tracked = new List<RecipeDef>();
            foreach (var recipe in group.Recipes)
            {
                if (!set.RecipeById.TryGetValue(recipe, out var def)) throw new EngineRefusedException(BadKind, "the percept is to tell about recipe '" + recipe + "', which the economy document does not have");
                if (!seen.Add(recipe)) throw new EngineRefusedException(BadKind, "recipe '" + recipe + "' is named twice");
                tracked.Add(def);
            }
            result.Add((group.Kind, tracked.OrderBy(r => r.Index).ToList()));
        }
        return result;
    }

    private static void CheckGoods(AiSet ai, IReadOnlyList<GoodDef> goods, string kind)
    {
        var expected = new List<(string Id, FactKind Kind, long Low, long High)>();
        foreach (var good in goods)
        {
            expected.Add((RatioAttribute(good), FactKind.Integer, 0, EconomyFacts.MaxRatio));
            expected.Add((ShortAttribute(good), FactKind.Boolean, 0, 0));
            expected.Add((ReachAttribute(good), FactKind.Integer, 0, EconomyFacts.MaxRatio));
        }
        CheckKind(ai, kind, expected);
    }

    private static void CheckMargins(AiSet ai, IReadOnlyList<RecipeDef> recipes, string kind) =>
        CheckKind(ai, kind, recipes.Select(r => (MarginAttribute(r), FactKind.Integer, 0L, (long)EconomyFacts.MaxRatio)).ToList());

    private static void CheckKind(AiSet ai, string kind, List<(string Id, FactKind Kind, long Low, long High)> expected)
    {
        if (!ai.TryPercept(kind, out var def)) throw new EngineRefusedException(BadKind, "the AI document has no percept kind '" + kind + "'");
        if (def.Owned) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' is owned, but a market's state belongs to no one else");
        if (def.Attrs.Count != expected.Count) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' must declare exactly " + string.Join(", ", expected.Select(e => e.Id)));
        foreach (var (attr, type, low, high) in expected)
        {
            if (!def.TryAttr(attr, out var declared) || declared.Kind != type) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' must declare '" + attr + "' as " + type);
            if (type == FactKind.Integer && (declared.Minimum > low || declared.Maximum < high)) throw new EngineRefusedException(BadKind, "attribute '" + attr + "' of '" + kind + "' must allow " + low + " to " + high);
        }
    }

    /// <summary>The best value a unit could fetch from the market (selling here, or sending it on) over its price here, per mille, at most the attribute's ceiling.</summary>
    private static long Reach(EconomyRuntime runtime, int market, int pair)
    {
        var price = runtime.Markets[market].Pairs[pair].Price;
        return Math.Min(EconomyFacts.MaxRatio, runtime.Reach(market, pair).Value * 1000 / price);
    }

    private static string GoodsAttributes(EconomyRuntime runtime, int market, IReadOnlyList<GoodDef> goods)
    {
        var set = runtime.Set;
        var pairs = new List<KeyValuePair<string, FactValue>>();
        foreach (var good in goods)
        {
            var slot = set.PairOf[market][good.Index];
            pairs.Add(new KeyValuePair<string, FactValue>(RatioAttribute(good), FactValue.From(slot >= 0 ? runtime.Ratio(market, slot) : 0)));
            pairs.Add(new KeyValuePair<string, FactValue>(ShortAttribute(good), FactValue.From(slot >= 0 && runtime.Markets[market].Pairs[slot].Short)));
            pairs.Add(new KeyValuePair<string, FactValue>(ReachAttribute(good), FactValue.From(slot >= 0 ? Reach(runtime, market, slot) : 0)));
        }
        return AiValues.Encode(pairs);
    }

    private static string MarginAttributes(EconomyRuntime runtime, int market, IReadOnlyList<RecipeDef> recipes)
    {
        var set = runtime.Set;
        var pairs = new List<KeyValuePair<string, FactValue>>();
        foreach (var recipe in recipes)
            pairs.Add(new KeyValuePair<string, FactValue>(MarginAttribute(recipe), FactValue.From(set.LineOf[market][recipe.Index] >= 0 ? runtime.MarginOfLine(market, recipe.Index).Ratio : 0)));
        return AiValues.Encode(pairs);
    }
}
