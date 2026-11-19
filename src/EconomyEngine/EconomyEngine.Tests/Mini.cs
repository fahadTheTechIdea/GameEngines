using System.Text.Json.Nodes;

namespace EconomyEngine.Tests;

/// <summary>
/// A document with one market, one good and one pair, whose numbers a test sets, so the arithmetic of one period can be checked against an independent computation. The good is worth 10,000 milli (10 minor units),
/// the price bounds are 100 to 4,000 per mille, there is no band margin and no noise.
/// </summary>
internal static class Mini
{
    public static JsonObject Root(Action<JsonObject>? edit = null)
    {
        var root = new JsonObject
        {
            ["format"] = 6, ["id"] = "mini", ["seed"] = 7,
            ["time"] = new JsonObject { ["stepsPerDay"] = 1, ["periodSteps"] = 1 },
            ["limits"] = new JsonObject { ["maxShipments"] = 50, ["maxLoans"] = 8, ["maxShocks"] = 4, ["history"] = 8 },
            ["price"] = new JsonObject { ["responseMin"] = 200, ["responseMax"] = 3000, ["settlePeriods"] = 4, ["bandMargin"] = 0, ["noise"] = 0, ["arrearsPenalty"] = 300 },
            ["routing"] = new JsonObject { ["mode"] = "direct" },
            ["recipes"] = new JsonArray(),
            ["currencies"] = new JsonArray { new JsonObject { ["id"] = "c", ["key"] = "k.c", ["reference"] = true, ["anchor"] = 1_000_000, ["money"] = 1_000_000, ["growthPpm"] = 0, ["maxRateStepPpm"] = 0 } },
            ["goods"] = new JsonArray { Good("g") },
            ["bands"] = new JsonArray { Band("low", 0), Band("mid", 900), Band("high", 1100) },
            ["schedules"] = new JsonArray { Schedule("pay", 3, 0, false), Schedule("hand", 1, 0, true) },
            ["policies"] = new JsonArray(),
            ["modifiers"] = new JsonArray(),
            ["shocks"] = new JsonArray(),
            ["markets"] = new JsonArray { Market("m", "c") },
            ["routes"] = new JsonArray(),
            ["loans"] = new JsonArray(),
            ["facts"] = new JsonArray(),
        };
        edit?.Invoke(root);
        return root;
    }

    public static string Text(Action<JsonObject>? edit = null) => Root(edit).ToJsonString();

    public static EconomySet Set(Action<JsonObject>? edit = null) => EconomyLoader.Load(Text(edit));

    public static EconomyRuntime Runtime(Action<JsonObject>? edit = null) => EconomyRuntime.Create(Set(edit));

    public static JsonObject Good(string id) => new()
    {
        ["id"] = id, ["key"] = "k." + id, ["base"] = 10_000, ["demandElasticity"] = 1000, ["supplyElasticity"] = 1000, ["floor"] = 100, ["ceiling"] = 4000, ["speed"] = 500, ["maxStep"] = 200, ["inertia"] = 0,
        ["decay"] = 0, ["weight"] = 1, ["tags"] = new JsonArray(),
    };

    public static JsonObject Band(string id, int from) => new() { ["id"] = id, ["key"] = "k.band." + id, ["from"] = from };

    public static JsonObject Schedule(string id, int every, int offset, bool manual) => new() { ["id"] = id, ["key"] = "k.sched." + id, ["every"] = every, ["offset"] = offset, ["manual"] = manual };

    public static JsonObject Pair(string good, long production, long consumption, long stock, int cover = 0, long capacity = 1_000_000) => new()
    {
        ["good"] = good, ["production"] = production, ["consumption"] = consumption, ["stock"] = stock, ["cover"] = cover, ["capacity"] = capacity,
    };

    public static JsonObject Market(string id, string currency, params JsonObject[] pairs) => new()
    {
        ["id"] = id, ["key"] = "k.m." + id, ["currency"] = currency, ["treasury"] = 10_000, ["staff"] = 0, ["spread"] = 0,
        ["pairs"] = new JsonArray((pairs.Length == 0 ? [Pair("g", 100, 100, 100)] : pairs).Select(p => (JsonNode)p).ToArray()), ["policies"] = new JsonArray(), ["budget"] = new JsonArray(), ["lines"] = new JsonArray(),
    };

    public static JsonObject TheGood(JsonObject root) => root["goods"]!.AsArray()[0]!.AsObject();
    public static JsonObject ThePair(JsonObject root) => root["markets"]!.AsArray()[0]!["pairs"]!.AsArray()[0]!.AsObject();
    public static JsonObject TheMarket(JsonObject root) => root["markets"]!.AsArray()[0]!.AsObject();
    public static JsonObject ThePrice(JsonObject root) => root["price"]!.AsObject();
}
