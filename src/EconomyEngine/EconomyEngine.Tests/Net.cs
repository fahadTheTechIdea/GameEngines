using System.Text.Json.Nodes;

namespace EconomyEngine.Tests;

internal sealed record GoodSpec(string Id, long Base = 10_000, long Weight = 1);

internal sealed record PairSpec(string Market, string Good, long Production = 0, long Consumption = 0, long Stock = 0);

internal sealed record RouteSpec(string Id, string From, string To, long Capacity = 1_000_000, long CostPerWeight = 0, int Margin = 0, int Share = 1000, int Delay = 1);

/// <summary>A tariff on one market: kind is "import" or "export", the rate is per mille of the price.</summary>
internal sealed record TariffSpec(string Market, string Kind, int Rate);

/// <summary>SplitMix64: the fixed-seed generator the oracles draw their random worlds from.</summary>
internal sealed class Rng
{
    private ulong _state;

    public Rng(ulong seed) { _state = seed; }

    public ulong Next()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A whole number from <paramref name="low"/> up to but not including <paramref name="high"/>.</summary>
    public long Between(long low, long high) => low + (long)(Next() % (ulong)(high - low));

    public int Pick(int count) => (int)Between(0, count);

    public List<T> Shuffled<T>(IEnumerable<T> items)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Pick(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}

/// <summary>Builds small trade networks as documents: markets, goods with frozen prices (speed 0), pairs, routes and tariffs, in exactly the order they are given.</summary>
internal static class Net
{
    /// <summary>
    /// A document for the networks the routing tests use. Prices do not move (speed 0) unless <paramref name="speed"/> says otherwise; a market is in currency "c" (the reference) or "d" (worth half a c at the start);
    /// the shipment limit is high enough never to bind.
    /// </summary>
    public static JsonObject Root(string mode, IEnumerable<GoodSpec> goods, IEnumerable<(string Id, string Currency)> markets, IEnumerable<PairSpec> pairs, IEnumerable<RouteSpec> routes,
        IEnumerable<TariffSpec>? tariffs = null, int speed = 0, Action<JsonObject>? edit = null)
    {
        var pairList = pairs.ToList();
        var tariffList = (tariffs ?? []).ToList();
        return Mini.Root(root =>
        {
            root["routing"]!["mode"] = mode;
            root["limits"]!["maxShipments"] = 100_000;
            root["currencies"]!.AsArray().Add(new JsonObject { ["id"] = "d", ["key"] = "k.d", ["reference"] = false, ["anchor"] = 500_000, ["money"] = 1000, ["growthPpm"] = 0, ["maxRateStepPpm"] = 0 });
            root["goods"] = new JsonArray(goods.Select(g =>
            {
                var good = Mini.Good(g.Id);
                good["base"] = g.Base;
                good["weight"] = g.Weight;
                good["speed"] = speed;
                return (JsonNode)good;
            }).ToArray());
            root["policies"] = new JsonArray(tariffList.Select(t => (JsonNode)new JsonObject
            {
                ["id"] = "tariff-" + t.Market + "-" + t.Kind, ["kind"] = t.Kind, ["rate"] = t.Rate, ["perUnit"] = 0, ["tags"] = new JsonArray(), ["schedule"] = "hand",
            }).ToArray());
            root["markets"] = new JsonArray(markets.Select(m =>
            {
                var market = Mini.Market(m.Id, m.Currency, pairList.Where(p => p.Market == m.Id).Select(p => Mini.Pair(p.Good, p.Production, p.Consumption, p.Stock)).ToArray());
                market["policies"] = new JsonArray(tariffList.Where(t => t.Market == m.Id).Select(t => (JsonNode)("tariff-" + t.Market + "-" + t.Kind)).ToArray());
                return (JsonNode)market;
            }).ToArray());
            root["routes"] = new JsonArray(routes.Select(r => (JsonNode)new JsonObject
            {
                ["id"] = r.Id, ["from"] = r.From, ["to"] = r.To, ["delay"] = r.Delay, ["capacity"] = r.Capacity, ["costPerWeight"] = r.CostPerWeight, ["margin"] = r.Margin, ["share"] = r.Share,
            }).ToArray());
            edit?.Invoke(root);
        });
    }

    public static EconomyRuntime Runtime(JsonObject root) => EconomyRuntime.Create(EconomyLoader.Load(root.ToJsonString()));

    public static PairState Pair(EconomyRuntime runtime, string market, string good)
    {
        var m = runtime.MarketIndex(market);
        return runtime.Markets[m].Pairs[runtime.PairIndex(m, runtime.GoodIndex(good))];
    }

    public static void Price(EconomyRuntime runtime, string market, string good, long price) => Pair(runtime, market, good).Price = price;

    public static Reachable Reach(EconomyRuntime runtime, string market, string good)
    {
        var m = runtime.MarketIndex(market);
        return runtime.Reach(m, runtime.PairIndex(m, runtime.GoodIndex(good)));
    }
}
