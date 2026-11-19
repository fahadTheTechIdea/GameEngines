using System.Text.Json.Nodes;

namespace EconomyEngine.Tests;

internal sealed record RecipeSpec(string Id, (string Good, long Amount)[] In, (string Good, long Amount)[] Out, int Duration = 0, bool Locked = false, (string Good, long Amount)[]? Requires = null, Dictionary<string, (long Units, long Runs)>? Wear = null);

/// <summary>A line of a market; <paramref name="Costs"/> are the wage and overhead of a run and whether they follow the price level (the throttle must be given).</summary>
internal sealed record LineSpec(string Recipe, long Capacity, int ThrottleStop = -1, int ThrottleFull = -1, (long Wage, long Overhead, bool Indexed)? Costs = null);

/// <summary>One market of a production test: the stock of each good it trades (it makes and uses nothing on its own, so only its lines change a stock), the warehouse size and its lines.</summary>
internal sealed record MarketSpec(string Id, Dictionary<string, long> Stocks, long Capacity = 1_000_000, LineSpec[]? Lines = null);

/// <summary>Builds documents for the production tests: goods with prices that do not matter to a stock, recipes, and markets that trade every good with no production, consumption or decay of their own.</summary>
internal static class Chain
{
    public static JsonObject Root(string[] goods, RecipeSpec[] recipes, MarketSpec[] markets, Action<JsonObject>? edit = null) =>
        Mini.Root(root =>
        {
            root["goods"] = new JsonArray(goods.Select(g => (JsonNode)Mini.Good(g)).ToArray());
            root["recipes"] = new JsonArray(recipes.Select(r =>
            {
                var recipe = new JsonObject { ["id"] = r.Id, ["key"] = "k.recipe." + r.Id, ["inputs"] = Amounts(r.In), ["outputs"] = Amounts(r.Out), ["duration"] = r.Duration, ["locked"] = r.Locked };
                if (r.Requires is not null)
                {
                    var requires = Amounts(r.Requires);
                    foreach (var need in requires.Select(n => n!.AsObject()))
                        if (r.Wear is not null && r.Wear.TryGetValue(need["good"]!.GetValue<string>(), out var wear)) need["wear"] = new JsonObject { ["units"] = wear.Units, ["runs"] = wear.Runs };
                    recipe["requires"] = requires;
                }
                return (JsonNode)recipe;
            }).ToArray());
            root["markets"] = new JsonArray(markets.Select(m =>
            {
                var market = Mini.Market(m.Id, "c", goods.Select(g => Mini.Pair(g, 0, 0, m.Stocks.GetValueOrDefault(g), 0, m.Capacity)).ToArray());
                market["lines"] = new JsonArray((m.Lines ?? []).Select(l =>
                {
                    var line = new JsonObject { ["recipe"] = l.Recipe, ["capacity"] = l.Capacity };
                    if (l.ThrottleStop >= 0)
                    {
                        var throttle = new JsonObject { ["stop"] = l.ThrottleStop, ["full"] = l.ThrottleFull };
                        if (l.Costs is { } costs) throttle["costs"] = new JsonObject { ["wage"] = costs.Wage, ["overhead"] = costs.Overhead, ["indexed"] = costs.Indexed };
                        line["throttle"] = throttle;
                    }
                    return (JsonNode)line;
                }).ToArray());
                return (JsonNode)market;
            }).ToArray());
            edit?.Invoke(root);
        });

    private static JsonArray Amounts((string Good, long Amount)[] amounts) => new(amounts.Select(a => (JsonNode)new JsonObject { ["good"] = a.Good, ["amount"] = a.Amount }).ToArray());

    public static EconomyRuntime Runtime(string[] goods, RecipeSpec[] recipes, MarketSpec[] markets, Action<JsonObject>? edit = null) =>
        EconomyRuntime.Create(EconomyLoader.Load(Root(goods, recipes, markets, edit).ToJsonString()));

    public static long Stock(EconomyRuntime runtime, string market, string good)
    {
        var m = runtime.MarketIndex(market);
        return runtime.Markets[m].Pairs[runtime.PairIndex(m, runtime.GoodIndex(good))].Stock;
    }

    public static LineState Line(EconomyRuntime runtime, string market, string recipe)
    {
        var m = runtime.MarketIndex(market);
        return runtime.Markets[m].Lines[runtime.Set.LineOf[m][runtime.Set.RecipeById[recipe].Index]];
    }

    /// <summary>One period: the step moves by exactly one period.</summary>
    public static EconomyChanges Period(EconomyRuntime runtime) => runtime.Advance(runtime.Step + runtime.Set.PeriodSteps, Fixture.NoFacts);
}
