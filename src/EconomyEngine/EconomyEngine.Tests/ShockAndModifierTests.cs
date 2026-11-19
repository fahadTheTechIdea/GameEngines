using System.Text.Json.Nodes;
using ScenarioEngine.Contracts;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>Shocks (what an event system applies) and modifiers (what the host's facts, such as the season, do each period).</summary>
public sealed class ShockAndModifierTests
{
    private static JsonObject Shock(string id, int duration, params JsonObject[] effects) => new()
    {
        ["id"] = id, ["key"] = "k." + id, ["duration"] = duration, ["effects"] = new JsonArray(effects.Select(e => (JsonNode)e).ToArray()),
    };

    private static JsonObject Effect(string kind, long amount, string good = "", string tag = "", string route = "") => new()
    {
        ["kind"] = kind, ["good"] = good, ["tag"] = tag, ["route"] = route, ["amount"] = amount,
    };

    private static EconomyRuntime WithShock(JsonObject shock, Action<JsonObject>? edit = null) => Mini.Runtime(root =>
    {
        Mini.TheGood(root)["speed"] = 0;
        Mini.ThePair(root)["production"] = 1000;
        Mini.ThePair(root)["consumption"] = 1000;
        root["shocks"]!.AsArray().Add(shock);
        edit?.Invoke(root);
    });

    // ---- shocks ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ASupplyShockCutsOutputFromTheNextPeriodForItsDurationOnly()
    {
        var runtime = WithShock(Shock("drought", 3, Effect("supply", -500)));
        var control = WithShock(Shock("drought", 3, Effect("supply", -500)));
        runtime.Advance(1, Fixture.NoFacts); control.Advance(1, Fixture.NoFacts);
        runtime.ApplyShock("drought", "", 1000);
        var supplies = new List<long>();
        var controlSupplies = new List<long>();
        for (var period = 2; period <= 6; period++)
        {
            runtime.Advance(period, Fixture.NoFacts); control.Advance(period, Fixture.NoFacts);
            supplies.Add(runtime.Markets[0].Pairs[0].Supply);
            controlSupplies.Add(control.Markets[0].Pairs[0].Supply);
        }
        Assert.Equal(1000, controlSupplies[0]);
        Assert.Equal([500L, 500L, 500L, 1000L, 1000L], supplies);
        Assert.Empty(runtime.ActiveShocks);
    }

    [Fact]
    public void AShockIsReportedWhenItBeginsAndWhenItEnds()
    {
        var runtime = WithShock(Shock("drought", 2, Effect("supply", -500)));
        var started = runtime.ApplyShock("drought", "m", 1000);
        Assert.Equal(new ShockChanged(0, 0, true), Assert.Single(started.Shocks));
        Assert.Empty(runtime.Advance(1, Fixture.NoFacts).Shocks);
        var ended = runtime.Advance(2, Fixture.NoFacts);
        Assert.Equal(new ShockChanged(0, 0, false), Assert.Single(ended.Shocks));
        Assert.Empty(runtime.ActiveShocks);
    }

    [Fact]
    public void ADurationOfOneMeansExactlyOnePeriod()
    {
        var runtime = WithShock(Shock("blip", 1, Effect("demand", 1000)));
        runtime.ApplyShock("blip", "", 1000);
        runtime.Advance(1, Fixture.NoFacts);
        Assert.Equal(2000, runtime.Markets[0].Pairs[0].Demand);
        runtime.Advance(2, Fixture.NoFacts);
        Assert.Equal(1000, runtime.Markets[0].Pairs[0].Demand);
    }

    [Fact]
    public void TheScaleScalesTheEffectAndTwoShocksMultiply()
    {
        var half = WithShock(Shock("drought", 5, Effect("supply", -500)));
        half.ApplyShock("drought", "", 500);
        half.Advance(1, Fixture.NoFacts);
        Assert.Equal(750, half.Markets[0].Pairs[0].Supply);
        var two = WithShock(Shock("drought", 5, Effect("supply", -500)));
        two.ApplyShock("drought", "", 1000);
        two.ApplyShock("drought", "", 1000);
        two.Advance(1, Fixture.NoFacts);
        Assert.Equal(250, two.Markets[0].Pairs[0].Supply);
        var strong = WithShock(Shock("boom", 5, Effect("demand", 500)));
        strong.ApplyShock("boom", "", 2000);
        strong.Advance(1, Fixture.NoFacts);
        Assert.Equal(2000, strong.Markets[0].Pairs[0].Demand);
    }

    [Fact]
    public void AShockOnOneMarketLeavesTheOthersAlone()
    {
        var runtime = WithShock(Shock("drought", 5, Effect("supply", -500)), root =>
        {
            root["markets"]!.AsArray().Add(Mini.Market("n", "c", Mini.Pair("g", 1000, 1000, 100)));
        });
        runtime.ApplyShock("drought", "n", 1000);
        runtime.Advance(1, Fixture.NoFacts);
        Assert.Equal(1000, runtime.Markets[0].Pairs[0].Supply);
        Assert.Equal(500, runtime.Markets[1].Pairs[0].Supply);
    }

    [Fact]
    public void AShockNamesGoodsByIdOrByTag()
    {
        var runtime = Mini.Runtime(root =>
        {
            Mini.TheGood(root)["speed"] = 0;
            Mini.TheGood(root)["tags"] = new JsonArray("food");
            var h = Mini.Good("h"); h["speed"] = 0;
            root["goods"]!.AsArray().Add(h);
            var i = Mini.Good("i"); i["speed"] = 0; i["tags"] = new JsonArray("food");
            root["goods"]!.AsArray().Add(i);
            Mini.TheMarket(root)["pairs"] = new JsonArray(Mini.Pair("g", 1000, 0, 0), Mini.Pair("h", 1000, 0, 0), Mini.Pair("i", 1000, 0, 0));
            root["shocks"]!.AsArray().Add(Shock("by-tag", 5, Effect("supply", -500, tag: "food")));
            root["shocks"]!.AsArray().Add(Shock("by-id", 5, Effect("supply", -100, good: "h")));
        });
        runtime.ApplyShock("by-tag", "", 1000);
        runtime.ApplyShock("by-id", "", 1000);
        runtime.Advance(1, Fixture.NoFacts);
        Assert.Equal(500, runtime.Markets[0].Pairs[0].Supply);     // food
        Assert.Equal(900, runtime.Markets[0].Pairs[1].Supply);     // by id
        Assert.Equal(500, runtime.Markets[0].Pairs[2].Supply);     // food
    }

    [Fact]
    public void APriceShockJumpsThePriceAtOnceAndNoFartherThanTheBounds()
    {
        var runtime = WithShock(Shock("panic", 1, Effect("price", 500)));
        runtime.ApplyShock("panic", "", 1000);
        Assert.Equal(15_000, runtime.Markets[0].Pairs[0].Price);
        Assert.Equal(runtime.BandOf(1500, 0), runtime.Markets[0].Pairs[0].Band);
        var huge = WithShock(Shock("crash", 1, Effect("price", -1000)));
        huge.ApplyShock("crash", "", 1000);
        Assert.Equal(1000, huge.Markets[0].Pairs[0].Price);      // the floor, 100 per mille of 10,000
        var up = WithShock(Shock("mania", 1, Effect("price", 10_000)));
        up.ApplyShock("mania", "", 1000);
        Assert.Equal(40_000, up.Markets[0].Pairs[0].Price);      // the ceiling, 4,000 per mille
    }

    [Fact]
    public void AMoneyGrowthShockRaisesThePriceLevelWhileItLastsAndStops()
    {
        var shocked = WithShock(Shock("press", 3, Effect("growth", 50_000)));
        var control = WithShock(Shock("press", 3, Effect("growth", 50_000)));
        shocked.ApplyShock("press", "", 1000);
        for (var period = 1; period <= 6; period++)
        {
            shocked.Advance(period, Fixture.NoFacts); control.Advance(period, Fixture.NoFacts);
        }
        Assert.Equal(1_000_000, control.Currencies[0].Level);
        Assert.Equal(1_157_625, shocked.Currencies[0].Level);          // 1.05 ^ 3
        Assert.Equal(shocked.Currencies[0].Minted, shocked.Currencies[0].World);
        Assert.All(shocked.Conservation(), c => Assert.Equal(0, c));
    }

    [Fact]
    public void AShockThatBreaksARuleIsRefusedWhollyAndChangesNothing()
    {
        var runtime = WithShock(Shock("drought", 5, Effect("supply", -500)), root => root["limits"]!["maxShocks"] = 1);
        var digest = runtime.StateDigest();
        Assert.Equal(EconomyCodes.UnknownShock, Fixture.Refusal(() => runtime.ApplyShock("nothing", "", 1000)));
        Assert.Equal(EconomyCodes.UnknownMarket, Fixture.Refusal(() => runtime.ApplyShock("drought", "nowhere", 1000)));
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => runtime.ApplyShock("drought", "", 0)));
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => runtime.ApplyShock("drought", "", EconomyRuntime.MaxShockScale + 1)));
        Assert.Equal(digest, runtime.StateDigest());
        runtime.ApplyShock("drought", "", 1);
        Assert.Equal(EconomyCodes.Capacity, Fixture.Refusal(() => runtime.ApplyShock("drought", "", 1000)));
        var widest = WithShock(Shock("drought", 5, Effect("supply", -100)));
        widest.ApplyShock("drought", "", EconomyRuntime.MaxShockScale);
        widest.Advance(1, Fixture.NoFacts);
        Assert.Equal(0, widest.Markets[0].Pairs[0].Supply);          // -100 per mille at a scale of 10,000 is -1000: nothing is made
    }

    [Fact]
    public void ThereIsRoomForOnlyTheDocumentsNumberOfShocksAtOnce()
    {
        var runtime = WithShock(Shock("drought", 5, Effect("supply", -500)), root => root["limits"]!["maxShocks"] = 2);
        runtime.ApplyShock("drought", "", 1000);
        runtime.ApplyShock("drought", "", 1000);
        Assert.Equal(EconomyCodes.Capacity, Fixture.Refusal(() => runtime.ApplyShock("drought", "", 1000)));
        Assert.Equal(2, runtime.ActiveShocks.Count);
    }

    [Fact]
    public void AShockAppliedAtAPeriodFirstActsInTheNextOne()
    {
        var runtime = WithShock(Shock("drought", 1, Effect("supply", -500)));
        runtime.Advance(1, Fixture.NoFacts);
        runtime.Advance(2, Fixture.NoFacts);
        runtime.ApplyShock("drought", "", 1000);
        Assert.Equal(1000, runtime.Markets[0].Pairs[0].Supply);
        runtime.Advance(3, Fixture.NoFacts);
        Assert.Equal(500, runtime.Markets[0].Pairs[0].Supply);
        runtime.Advance(4, Fixture.NoFacts);
        Assert.Equal(1000, runtime.Markets[0].Pairs[0].Supply);
    }

    // ---- modifiers ---------------------------------------------------------------------------------------------------------------------

    private static JsonObject Modifier(string id, string kind, int permille, string fact, string op, JsonNode value, string good = "", string tag = "") => new()
    {
        ["id"] = id, ["kind"] = kind, ["good"] = good, ["tag"] = tag, ["permille"] = permille, ["when"] = new JsonObject { ["fact"] = fact, ["op"] = op, ["value"] = value },
    };

    private static EconomyRuntime Seasonal(Action<JsonObject>? edit = null) => Mini.Runtime(root =>
    {
        Mini.TheGood(root)["speed"] = 0;
        Mini.ThePair(root)["production"] = 1000;
        Mini.ThePair(root)["consumption"] = 1000;
        root["facts"]!.AsArray().Add(new JsonObject { ["id"] = "season", ["type"] = "string" });
        root["facts"]!.AsArray().Add(new JsonObject { ["id"] = "plague", ["type"] = "int", ["min"] = 0, ["max"] = 3 });
        root["modifiers"]!.AsArray().Add(Modifier("winter", "demand", 300, "season", "eq", "winter"));
        root["modifiers"]!.AsArray().Add(Modifier("harvest", "supply", 400, "season", "eq", "autumn"));
        root["modifiers"]!.AsArray().Add(Modifier("sick", "supply", -300, "plague", "gte", 2));
        edit?.Invoke(root);
    });

    private static IReadOnlyDictionary<string, FactValue> Facts(string season, long plague = 0) => new Dictionary<string, FactValue> { ["season"] = FactValue.From(season), ["plague"] = FactValue.From(plague) };

    [Fact]
    public void AModifierActsOnlyWhileItsConditionHoldsOnTheHostFacts()
    {
        var winter = Seasonal(); var autumn = Seasonal(); var spring = Seasonal(); var sick = Seasonal();
        winter.Advance(1, Facts("winter")); autumn.Advance(1, Facts("autumn")); spring.Advance(1, Facts("spring")); sick.Advance(1, Facts("spring", 2));
        Assert.Equal(1300, winter.Markets[0].Pairs[0].Demand);
        Assert.Equal(1000, winter.Markets[0].Pairs[0].Supply);
        Assert.Equal(1400, autumn.Markets[0].Pairs[0].Supply);
        Assert.Equal(1000, spring.Markets[0].Pairs[0].Supply);
        Assert.Equal(1000, spring.Markets[0].Pairs[0].Demand);
        Assert.Equal(700, sick.Markets[0].Pairs[0].Supply);
    }

    [Fact]
    public void ModifiersThatHoldTogetherMultiplyAndAnEmptySelectorMeansEveryGood()
    {
        var runtime = Seasonal(root => root["modifiers"]!.AsArray().Add(Modifier("fest", "demand", 500, "season", "eq", "winter")));
        runtime.Advance(1, Facts("winter"));
        Assert.Equal(1950, runtime.Markets[0].Pairs[0].Demand);        // 1000 * 1.3 * 1.5
        var gone = Seasonal(root => root["modifiers"]!.AsArray().Add(Modifier("ban", "supply", -1000, "plague", "gte", 0)));
        gone.Advance(1, Facts("spring"));
        Assert.Equal(0, gone.Markets[0].Pairs[0].Supply);
    }

    [Fact]
    public void AModifierNamesGoodsByIdOrTag()
    {
        var runtime = Seasonal(root =>
        {
            Mini.TheGood(root)["tags"] = new JsonArray("food");
            var h = Mini.Good("h"); h["speed"] = 0;
            root["goods"]!.AsArray().Add(h);
            Mini.TheMarket(root)["pairs"]!.AsArray().Add(Mini.Pair("h", 1000, 1000, 100));
            root["modifiers"] = new JsonArray(Modifier("food-up", "demand", 1000, "season", "eq", "winter", tag: "food"), Modifier("h-up", "supply", 500, "season", "eq", "winter", good: "h"));
        });
        runtime.Advance(1, Facts("winter"));
        Assert.Equal(2000, runtime.Markets[0].Pairs[0].Demand);
        Assert.Equal(1000, runtime.Markets[0].Pairs[1].Demand);
        Assert.Equal(1500, runtime.Markets[0].Pairs[1].Supply);
        Assert.Equal(1000, runtime.Markets[0].Pairs[0].Supply);
    }

    [Fact]
    public void AMissingOrWrongFactRefusesTheAdvanceBeforeAnythingMoves()
    {
        var runtime = Seasonal();
        var digest = runtime.StateDigest();
        Assert.Equal(EconomyCodes.FactMissing, Fixture.Refusal(() => runtime.Advance(1, new Dictionary<string, FactValue> { ["season"] = FactValue.From("winter") })));
        Assert.Equal(EconomyCodes.FactInvalid, Fixture.Refusal(() => runtime.Advance(1, new Dictionary<string, FactValue> { ["season"] = FactValue.From(3L), ["plague"] = FactValue.From(0L) })));
        Assert.Equal(EconomyCodes.FactInvalid, Fixture.Refusal(() => runtime.Advance(1, Facts("winter", 4))));
        Assert.Equal(EconomyCodes.FactInvalid, Fixture.Refusal(() => runtime.Advance(1, Facts("winter", -1))));
        Assert.Equal(digest, runtime.StateDigest());
        Assert.Equal(0, runtime.Step);
    }

    [Fact]
    public void FactsAreOnlyNeededWhenAPeriodCloses()
    {
        var runtime = Seasonal();
        runtime.Advance(0, Fixture.NoFacts);
        Assert.Equal(0, runtime.Step);
        var step = Mini.Runtime(root => Mini.TheMarket(root)["treasury"] = 1);
        step.Advance(1, Fixture.NoFacts);
        Assert.Equal(1, step.Period);
    }

    [Fact]
    public void ADocumentWithoutModifiersNeedsNoFactsAtAll()
    {
        var runtime = Mini.Runtime();
        runtime.Advance(5, Fixture.NoFacts);
        Assert.Equal(5, runtime.Period);
    }
}
