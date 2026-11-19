using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;
using static EconomyEngine.Tests.Chain;

namespace EconomyEngine.Tests;

/// <summary>
/// Production chains: the pass over an acyclic recipe graph in topological order, with its limits and its trace. Every expected number is worked by hand or comes from <see cref="ProductionOracle"/>, a brute-force
/// simulation that shares no code with the engine (it finds the runs of a line by trying them from the capacity downward, and orders recipes by a quadratic scan).
/// </summary>
public sealed class ProductionTests
{
    private static readonly string[] SteelGoods = ["ore", "coal", "steel", "tools"];

    private static RecipeSpec Smelt(int duration = 1) => new("smelt", [("ore", 2), ("coal", 1)], [("steel", 1)], duration);

    private static RecipeSpec Smith(int duration = 0) => new("smith", [("steel", 2)], [("tools", 3)], duration);

    private static MarketSpec Forge(long ore, long coal, long smeltRuns = 40, long smithRuns = 10, long capacity = 1_000_000) =>
        new("m", new Dictionary<string, long> { ["ore"] = ore, ["coal"] = coal }, capacity, [new LineSpec("smelt", smeltRuns), new LineSpec("smith", smithRuns)]);

    // ---- a chain worked by hand --------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARecipeNeedsItsInputsAndTheChainRunsInTheOrderOfItsGoodsWorkedByHand()
    {
        // ore 100, coal 30. smelt (ore 2 + coal 1 -> steel 1, one period, 40 runs) comes before smith (steel 2 -> tools 3, at once, 10 runs) because it makes smith's input.
        var runtime = Runtime(SteelGoods, [Smith(), Smelt()], [Forge(100, 30)]);   // declared the other way round
        Assert.Equal(["smelt", "smith"], runtime.Set.RecipeOrder.Select(i => runtime.Set.Recipes[i].Id).ToList());

        // Period 1. Smelt: capacity 40, ore allows 100/2 = 50, coal allows 30/1 = 30: 30 runs, held by the coal. It takes 60 ore and 30 coal; 30 steel are due in period 2.
        // Smith: no steel yet, so no run, held by the steel.
        var first = Period(runtime);
        Assert.Equal((40L, 0L, 0L, 0L), (Stock(runtime, "m", "ore"), Stock(runtime, "m", "coal"), Stock(runtime, "m", "steel"), Stock(runtime, "m", "tools")));
        var batch = Assert.Single(runtime.Batches);
        Assert.Equal(("smelt", 30L, 2L), (runtime.Set.Recipes[batch.Recipe].Id, batch.Runs, batch.Completes));
        Assert.Equal((30L, LineLimit.Input), (Line(runtime, "m", "smelt").LastRuns, Line(runtime, "m", "smelt").Limit));
        Assert.Equal("coal", runtime.Set.Goods[Line(runtime, "m", "smelt").LimitGood].Id);
        Assert.Equal((0L, LineLimit.Input), (Line(runtime, "m", "smith").LastRuns, Line(runtime, "m", "smith").Limit));
        Assert.Equal("steel", runtime.Set.Goods[Line(runtime, "m", "smith").LimitGood].Id);
        Assert.Equal(2, first.Limits.Count);   // both lines went from the capacity they started at to a shortage

        // Period 2. The 30 steel arrive first. Smelt has no coal: nothing. Smith: capacity 10, steel allows 30/2 = 15: 10 runs, held by the capacity; 20 steel become 30 tools at once.
        var second = Period(runtime);
        Assert.Equal((40L, 0L, 10L, 30L), (Stock(runtime, "m", "ore"), Stock(runtime, "m", "coal"), Stock(runtime, "m", "steel"), Stock(runtime, "m", "tools")));
        Assert.Empty(runtime.Batches);
        Assert.Equal((10L, LineLimit.Capacity, -1), (Line(runtime, "m", "smith").LastRuns, Line(runtime, "m", "smith").Limit, Line(runtime, "m", "smith").LimitGood));
        var change = Assert.Single(second.Limits);   // smelt stayed short of coal and said nothing again; smith is back at its capacity
        Assert.Equal((LineLimit.Input, LineLimit.Capacity), (change.From, change.To));

        // Period 3. Smith: steel allows 10/2 = 5 runs, under its capacity of 10, held by the steel: 10 steel become 15 tools.
        Period(runtime);
        Assert.Equal((40L, 0L, 0L, 45L), (Stock(runtime, "m", "ore"), Stock(runtime, "m", "coal"), Stock(runtime, "m", "steel"), Stock(runtime, "m", "tools")));
        Assert.Equal((5L, LineLimit.Input), (Line(runtime, "m", "smith").LastRuns, Line(runtime, "m", "smith").Limit));
        Assert.All(runtime.Conservation(), c => Assert.Equal(0, c));
    }

    [Fact]
    public void ThePartialRunTheEmptyRunAndTheTieAreExactAndNameTheirInput()
    {
        // 5 ore against 2 a run: 2 runs, 1 ore left over. Coal is plentiful, so the ore is named.
        var runtime = Runtime(SteelGoods, [Smelt(0)], [new MarketSpec("m", new() { ["ore"] = 5, ["coal"] = 99 }, 1_000_000, [new LineSpec("smelt", 40)])]);
        var plans = runtime.PlanLines(0);
        Assert.Equal(new LinePlan(0, 40, 2, LineLimit.Input, runtime.GoodIndex("ore")), Assert.Single(plans));
        Period(runtime);
        Assert.Equal((1L, 97L, 2L), (Stock(runtime, "m", "ore"), Stock(runtime, "m", "coal"), Stock(runtime, "m", "steel")));

        // One ore: not enough for one run. Nothing runs, nothing is taken, and the line says which input it lacks.
        var none = Runtime(SteelGoods, [Smelt(0)], [new MarketSpec("m", new() { ["ore"] = 1, ["coal"] = 99 }, 1_000_000, [new LineSpec("smelt", 40)])]);
        Period(none);
        Assert.Equal((1L, 99L, 0L), (Stock(none, "m", "ore"), Stock(none, "m", "coal"), Stock(none, "m", "steel")));
        Assert.Equal((0L, LineLimit.Input), (Line(none, "m", "smelt").LastRuns, Line(none, "m", "smelt").Limit));

        // Both inputs allow 10 runs (20 ore, 10 coal): the tie goes to the input listed first, which is the ore.
        var tie = Runtime(SteelGoods, [Smelt(0)], [new MarketSpec("m", new() { ["ore"] = 20, ["coal"] = 10 }, 1_000_000, [new LineSpec("smelt", 40)])]);
        var line = Assert.Single(tie.PlanLines(0));
        Assert.Equal((10L, LineLimit.Input, "ore"), (line.Runs, line.Limit, tie.Set.Goods[line.Good].Id));
    }

    [Fact]
    public void WhenCapacityInputAndRoomAllowTheSameRunsTheCapacityIsNamedThenTheInputThenTheRoom()
    {
        LinePlan Plan(long ore, long coal, long steel, long warehouse, long capacity)
        {
            var runtime = Runtime(SteelGoods, [Smelt(0)], [new MarketSpec("m", new() { ["ore"] = ore, ["coal"] = coal, ["steel"] = steel }, warehouse, [new LineSpec("smelt", capacity)])]);
            return Assert.Single(runtime.PlanLines(0));
        }
        // 10 runs wanted; ore allows 10 (20 / 2), coal and room are ample: the capacity is reported, not the input that merely agrees with it.
        Assert.Equal((10L, LineLimit.Capacity), (Plan(20, 99, 0, 1_000, 10).Runs, Plan(20, 99, 0, 1_000, 10).Limit));
        // 10 runs wanted; the room is exactly 10: the capacity again.
        Assert.Equal((10L, LineLimit.Capacity, -1), (Plan(99, 99, 90, 100, 10).Runs, Plan(99, 99, 90, 100, 10).Limit, Plan(99, 99, 90, 100, 10).Good));
        // The input allows 5 and the room allows 5, with 10 wanted: the input is named (an input is looked at before a room).
        var both = Plan(10, 99, 95, 100, 10);
        Assert.Equal((5L, LineLimit.Input), (both.Runs, both.Limit));
        // The room alone allows 5.
        var room = Plan(99, 99, 95, 100, 10);
        Assert.Equal((5L, LineLimit.Room), (room.Runs, room.Limit));
    }

    [Fact]
    public void NoRoomInTheWarehouseHoldsARunBackAndAnOutputOnItsWayCountsAsRoomTaken()
    {
        // Every warehouse holds 120. Ore and coal would allow 40 runs of 1 steel, but with 117 steel already in stock only 3 fit.
        var spec = new MarketSpec("m", new() { ["ore"] = 100, ["coal"] = 100, ["steel"] = 117 }, 120, [new LineSpec("smelt", 40)]);
        var instant = Runtime(SteelGoods, [Smelt(0)], [spec]);
        var plan = Assert.Single(instant.PlanLines(0));
        Assert.Equal((3L, LineLimit.Room, "steel"), (plan.Runs, plan.Limit, instant.Set.Goods[plan.Good].Id));
        Period(instant);
        Assert.Equal(120, Stock(instant, "m", "steel"));
        Assert.Equal(0, Assert.Single(instant.PlanLines(0)).Runs);   // full now

        // With a one-period recipe the 3 steel are on their way and take the room: the next period does not start 3 more.
        var later = Runtime(SteelGoods, [Smelt(1)], [spec]);
        Period(later);
        Assert.Equal(117, Stock(later, "m", "steel"));
        Assert.Equal(3, Assert.Single(later.Batches).Runs);
        Assert.Equal(3, later.Markets[0].Pairs[later.PairIndex(0, later.GoodIndex("steel"))].Incoming);   // the three on the way are counted as room taken
        Assert.Equal(0, Assert.Single(later.PlanLines(0)).Runs);
        Period(later);
        Assert.Equal(120, Stock(later, "m", "steel"));   // they arrived into the room that was kept for them: nothing was lost
        Assert.Empty(later.Batches);
    }

    [Fact]
    public void AnOutputThatArrivesIntoARoomATraderFilledInTheMeantimeIsClampedToTheWarehouseAndCountedAsSupplyOnlyForWhatFitted()
    {
        // The room for 3 steel was kept for the batch, but a sale to the market is checked against the stock alone and may take it: 117 + 3 sold = 120.
        var runtime = Runtime(SteelGoods, [Smelt(1)], [new MarketSpec("m", new() { ["ore"] = 100, ["coal"] = 100, ["steel"] = 117 }, 120, [new LineSpec("smelt", 40)])]);
        Period(runtime);
        Assert.Equal(3, Assert.Single(runtime.Batches).Runs);
        runtime.Trade("m", "steel", -3);
        Assert.Equal(120, Stock(runtime, "m", "steel"));
        var steel = runtime.Markets[0].Pairs[runtime.PairIndex(0, runtime.GoodIndex("steel"))];
        Period(runtime);
        Assert.Equal(120, steel.Stock);   // the 3 that arrived did not fit: they are lost, as a shipment's are
        Assert.Equal(0, steel.Incoming);
        Assert.Empty(runtime.Batches);
    }

    [Fact]
    public void TheCapacityIsTheLimitWhenTheInputsAreAmpleAndAnArrearsPenaltyCutsIt()
    {
        var ample = Runtime(SteelGoods, [Smelt(0)], [new MarketSpec("m", new() { ["ore"] = 1000, ["coal"] = 1000 }, 1_000_000, [new LineSpec("smelt", 40)])]);
        var plan = Assert.Single(ample.PlanLines(0));
        Assert.Equal(new LinePlan(0, 40, 40, LineLimit.Capacity, -1), plan);

        // A market that owes its wages loses 300 per mille of its output (the document's arrears penalty): 40 runs become 28.
        var owing = Runtime(SteelGoods, [Smelt(0)], [new MarketSpec("m", new() { ["ore"] = 1000, ["coal"] = 1000 }, 1_000_000, [new LineSpec("smelt", 40)])], root =>
        {
            Mini.TheMarket(root)["treasury"] = 0;
            Mini.TheMarket(root)["staff"] = 10;
            Mini.TheMarket(root)["budget"] = new JsonArray(new JsonObject { ["id"] = "wages", ["kind"] = "wage", ["schedule"] = "pay", ["amount"] = 500, ["perHead"] = 0, ["indexed"] = false });
        });
        for (var i = 0; i < 3 && !owing.InArrears(0); i++) Period(owing);   // the "pay" schedule fires every third period and the treasury is empty
        Assert.True(owing.InArrears(0));
        var cut = Assert.Single(owing.PlanLines(0));
        Assert.Equal(new LinePlan(0, 28, 28, LineLimit.Capacity, -1), cut);
    }

    // ---- the recipe graph --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARecipeAfterTheOnesThatMakeItsInputsWithTiesByIdWhateverOrderTheDocumentUses()
    {
        // a makes x and b makes y, both for c; d stands alone. Ready at the start: a, b, d, and the lowest id goes first: a, then b. That frees c, which is ready beside d, and c < d: the order is a, b, c, d.
        var goods = new[] { "s", "x", "y", "z", "w", "v" };
        RecipeSpec[] recipes =
        [
            new("a", [("s", 1)], [("x", 1)]), new("b", [("s", 1)], [("y", 1)]), new("c", [("x", 1), ("y", 1)], [("z", 1)]), new("d", [("s", 1)], [("w", 1)]),
        ];
        var expected = new[] { "a", "b", "c", "d" };
        var rng = new Rng(46);
        for (var round = 0; round < 24; round++)
        {
            var shuffled = rng.Shuffled(recipes).ToArray();
            var runtime = Runtime(goods, shuffled, [new MarketSpec("m", new() { ["s"] = 10 })]);
            Assert.Equal(expected, runtime.Set.RecipeOrder.Select(i => runtime.Set.Recipes[i].Id).ToArray());
            Assert.Equal(expected.Select((id, position) => (id, position)).ToDictionary(p => p.id, p => p.position), runtime.Set.Recipes.ToDictionary(r => r.Id, r => runtime.Set.RecipePosition[r.Index]));
        }
    }

    [Fact]
    public void ACycleOfTwoOfThreeOrOneIsRefusedByNameWithTheGoodsThatCloseIt()
    {
        var two = Assert.Throws<EngineRefusedException>(() => EconomyLoader.Load(Root(["p", "q", "r"], [new("f", [("p", 1)], [("q", 1)]), new("g", [("q", 1)], [("p", 1)])], [new MarketSpec("m", new())]).ToJsonString()));
        Assert.Equal(EconomyCodes.RecipeCycle, two.Code);
        Assert.Contains("f makes q for g", two.Message, StringComparison.Ordinal);
        Assert.Contains("g makes p for f", two.Message, StringComparison.Ordinal);

        var three = Assert.Throws<EngineRefusedException>(() => EconomyLoader.Load(Root(["p", "q", "r"], [new("f", [("p", 1)], [("q", 1)]), new("g", [("q", 1)], [("r", 1)]), new("h", [("r", 1)], [("p", 1)])], [new MarketSpec("m", new())]).ToJsonString()));
        Assert.Equal(EconomyCodes.RecipeCycle, three.Code);
        Assert.Contains("f makes q for g", three.Message, StringComparison.Ordinal);
        Assert.Contains("g makes r for h", three.Message, StringComparison.Ordinal);
        Assert.Contains("h makes p for f", three.Message, StringComparison.Ordinal);

        // A recipe that consumes the good it makes is a cycle of one (a catalyst is not an input: the converter keeps it out).
        var one = Assert.Throws<EngineRefusedException>(() => EconomyLoader.Load(Root(["p", "q", "r"], [new("f", [("p", 2)], [("p", 3)])], [new MarketSpec("m", new())]).ToJsonString()));
        Assert.Equal(EconomyCodes.RecipeCycle, one.Code);
        Assert.Contains("f makes p for f", one.Message, StringComparison.Ordinal);

        // A cycle hidden behind an acyclic prefix is still found, and an acyclic diamond is not mistaken for one.
        Assert.Equal(EconomyCodes.RecipeCycle, Assert.Throws<EngineRefusedException>(() => EconomyLoader.Load(Root(["p", "q", "r"],
            [new("e", [("r", 1)], [("p", 1)]), new("f", [("p", 1)], [("q", 1)]), new("g", [("q", 1)], [("p", 1)])], [new MarketSpec("m", new())]).ToJsonString())).Code);
        Assert.Equal(3, EconomyLoader.Load(Root(["p", "q", "r"], [new("f", [("p", 1)], [("q", 1), ("r", 1)]), new("g", [("q", 1)], [("r", 1)]), new("h", [("p", 1), ("q", 1)], [("r", 1)])], [new MarketSpec("m", new())]).ToJsonString()).Recipes.Count);
    }

    // ---- the rules of the document ----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("inputs", 0, EconomyCodes.Range)]
    [InlineData("inputs", 9, EconomyCodes.Range)]
    [InlineData("outputs", 0, EconomyCodes.Range)]
    public void ARecipeConsumesAndMakesBetweenOneAndEightGoods(string side, int count, string code)
    {
        var goods = Enumerable.Range(0, 10).Select(i => "g" + i).ToArray();
        var text = Root(goods, [new("r", [("g0", 1)], [("g9", 1)])], [new MarketSpec("m", new())], root =>
            root["recipes"]!.AsArray()[0]![side] = new JsonArray(Enumerable.Range(side == "inputs" ? 0 : 1, count).Select(i => (JsonNode)new JsonObject { ["good"] = "g" + i, ["amount"] = 1 }).ToArray())).ToJsonString();
        Assert.Equal(code, Assert.Throws<EngineRefusedException>(() => EconomyLoader.Load(text)).Code);
    }

    [Fact]
    public void AmountsDurationsAndCapacitiesHaveTheirBounds()
    {
        string Load(Action<JsonObject> edit) => Assert.Throws<EngineRefusedException>(() => EconomyLoader.Load(Root(SteelGoods, [Smelt(), Smith()], [Forge(10, 10)], edit).ToJsonString())).Code;
        JsonObject TheRecipe(JsonObject root) => root["recipes"]!.AsArray()[0]!.AsObject();
        JsonObject TheLine(JsonObject root) => Mini.TheMarket(root)["lines"]!.AsArray()[0]!.AsObject();
        Assert.Equal(EconomyCodes.Range, Load(root => TheRecipe(root)["inputs"]!.AsArray()[0]!["amount"] = 0));
        Assert.Equal(EconomyCodes.Range, Load(root => TheRecipe(root)["inputs"]!.AsArray()[0]!["amount"] = 1_000_001));
        Assert.Equal(EconomyCodes.Range, Load(root => TheRecipe(root)["duration"] = 65));
        Assert.Equal(EconomyCodes.Range, Load(root => TheRecipe(root)["duration"] = -1));
        Assert.Equal(EconomyCodes.Range, Load(root => TheLine(root)["capacity"] = 0));
        Assert.Equal(EconomyCodes.Range, Load(root => TheLine(root)["capacity"] = 1_000_001));
        Assert.Equal(EconomyCodes.Format, Load(root => TheRecipe(root)["locked"] = "yes"));
        Assert.Equal(EconomyCodes.UnknownField, Load(root => TheRecipe(root)["catalyst"] = "x"));
        Assert.Equal(EconomyCodes.MissingField, Load(root => TheRecipe(root).Remove("duration")));
        Assert.Equal(EconomyCodes.MissingField, Load(root => Mini.TheMarket(root).Remove("lines")));
        Assert.Equal(EconomyCodes.Duplicate, Load(root => root["recipes"]!.AsArray().Add(JsonNode.Parse(TheRecipe(root).ToJsonString()))));
        Assert.Equal(EconomyCodes.Reference, Load(root => TheRecipe(root)["inputs"]!.AsArray()[0]!["good"] = "nothing"));
        // The limits themselves load.
        var set = EconomyLoader.Load(Root(SteelGoods, [Smelt(64), Smith()], [Forge(10, 10, 1_000_000)], root => TheRecipe(root)["inputs"]!.AsArray()[0]!["amount"] = 1_000_000).ToJsonString());
        Assert.Equal((64, 1_000_000L, 1_000_000L), (set.Recipes[0].Duration, set.Recipes[0].Inputs[0].Amount, set.Markets[0].Lines[0].Capacity));
    }

    // ---- derived demand and supply move the prices ------------------------------------------------------------------------------------

    [Fact]
    public void AChainMovesThePricesOfItsGoodsThroughThePriceRuleAgainstASameProcessControl()
    {
        // Grain is plentiful and flour has no other source. With the mill running, the grain it takes counts as demand (its price rises against the control) and the flour it makes as supply (its price falls).
        RecipeSpec mill = new("mill", [("grain", 3)], [("flour", 2)], 1);
        string[] goods = ["grain", "flour"];
        MarketSpec Town(bool line) => new("m", new() { ["grain"] = 3000, ["flour"] = 200 }, 1_000_000, line ? new[] { new LineSpec("mill", 300) } : Array.Empty<LineSpec>());
        void Balanced(JsonObject root)
        {
            foreach (var pair in Mini.TheMarket(root)["pairs"]!.AsArray()) { pair!["production"] = 0; pair["consumption"] = 100; pair["cover"] = 2; }
        }
        var with = Runtime(goods, [mill], [Town(true)], Balanced);
        var control = Runtime(goods, [mill], [Town(false)], Balanced);
        for (var i = 0; i < 10; i++) { Period(with); Period(control); }
        long Price(EconomyRuntime r, string good) => r.Markets[0].Pairs[r.PairIndex(0, r.GoodIndex(good))].Price;
        Assert.True(Price(with, "grain") > Price(control, "grain"), $"grain {Price(with, "grain")} against {Price(control, "grain")}");
        Assert.True(Price(with, "flour") < Price(control, "flour"), $"flour {Price(with, "flour")} against {Price(control, "flour")}");
        // The mill's runs are the stock it took: 10 periods of up to 300 runs of 3 grain.
        Assert.True(Stock(with, "m", "grain") < Stock(control, "m", "grain"));
    }

    // ---- the conservation law and the brute-force oracle ------------------------------------------------------------------------------

    [Fact]
    public void RandomAcyclicChainsEqualTheBruteForceOracleEveryPeriodAndConserveGoodsAndMoney()
    {
        var rng = new Rng(20261046);
        var compared = 0;
        var partial = 0;
        for (var round = 0; round < 120; round++)
        {
            var world = RandomWorld(rng, tightWarehouses: round % 3 == 0);
            var runtime = EconomyRuntime.Create(EconomyLoader.Load(world.Text));
            var oracle = new ProductionOracle(world);
            for (var period = 1; period <= 14; period++)
            {
                var before = TotalsOf(runtime);
                Period(runtime);
                oracle.Close(period);
                // The oracle's stocks, batches and runs equal the engine's.
                for (var m = 0; m < world.Markets.Length; m++)
                    for (var g = 0; g < world.Goods.Length; g++)
                        Assert.Equal(oracle.Stock[m][g], Stock(runtime, world.Markets[m].Id, world.Goods[g]));
                Assert.Equal(oracle.Batches.Select(b => (b.Market, b.Recipe, b.Runs, b.Completes)).ToList(),
                    runtime.Batches.Select(b => (b.Market, world.Recipes.ToList().FindIndex(r => r.Id == runtime.Set.Recipes[b.Recipe].Id), b.Runs, b.Completes)).ToList());
                for (var m = 0; m < world.Markets.Length; m++)
                    foreach (var line in world.Markets[m].Lines!)
                    {
                        var ran = Line(runtime, world.Markets[m].Id, line.Recipe).LastRuns;
                        Assert.Equal(oracle.LastRuns[(m, line.Recipe)], ran);
                        if (ran < line.Capacity) partial++;
                    }
                // Conservation: for each good, stock plus what is on its way changed by exactly (made - taken) over the runs that started.
                var after = TotalsOf(runtime);
                var net = new long[world.Goods.Length];
                for (var m = 0; m < world.Markets.Length; m++)
                    foreach (var line in world.Markets[m].Lines!)
                    {
                        var runs = Line(runtime, world.Markets[m].Id, line.Recipe).LastRuns;
                        var spec = world.Recipes.First(r => r.Id == line.Recipe);
                        foreach (var (good, amount) in spec.In) net[Array.IndexOf(world.Goods, good)] -= runs * amount;
                        foreach (var (good, amount) in spec.Out) net[Array.IndexOf(world.Goods, good)] += runs * amount;
                    }
                for (var g = 0; g < net.Length; g++) Assert.Equal(net[g], after[g] - before[g]);
                Assert.All(runtime.Conservation(), c => Assert.Equal(0, c));
                compared++;
            }
        }
        Assert.Equal(120 * 14, compared);
        Assert.True(partial > 200, "the random worlds must hold lines back often enough to test partial runs: " + partial);
    }

    /// <summary>The units of each good in the economy: in stock plus in batches on their way.</summary>
    private static long[] TotalsOf(EconomyRuntime runtime)
    {
        var set = runtime.Set;
        var totals = new long[set.Goods.Count];
        foreach (var market in set.Markets)
            foreach (var pair in market.Pairs) totals[pair.Good] += runtime.Markets[market.Index].Pairs[pair.Index].Stock;
        foreach (var batch in runtime.Batches)
            foreach (var output in set.Recipes[batch.Recipe].Outputs) totals[output.Good] += batch.Runs * output.Amount;
        return totals;
    }

    internal sealed record World(string Text, string[] Goods, RecipeSpec[] Recipes, MarketSpec[] Markets);

    private static World RandomWorld(Rng rng, bool tightWarehouses)
    {
        var goods = Enumerable.Range(0, 7).Select(i => "g" + i).ToArray();
        var recipes = new List<RecipeSpec>();
        var count = 3 + rng.Pick(5);
        for (var i = 0; i < count; i++)
        {
            // Outputs are always of a higher-numbered good than every input, so the graph has no cycle whatever is drawn.
            var inputs = rng.Shuffled(Enumerable.Range(0, goods.Length - 1)).Take(1 + rng.Pick(3)).OrderBy(x => x).ToList();
            var top = inputs.Max();
            var outputs = rng.Shuffled(Enumerable.Range(top + 1, goods.Length - top - 1)).Take(1 + rng.Pick(2)).OrderBy(x => x).ToList();
            recipes.Add(new RecipeSpec("r" + i + "-" + (char)('a' + rng.Pick(26)), inputs.Select(x => (goods[x], 1 + rng.Between(0, 4))).ToArray(), outputs.Select(x => (goods[x], 1 + rng.Between(0, 4))).ToArray(), rng.Pick(4)));
        }
        var shuffled = rng.Shuffled(recipes).ToArray();
        var markets = Enumerable.Range(0, 2 + rng.Pick(2)).Select(m =>
        {
            var stocks = goods.ToDictionary(g => g, g => rng.Between(0, 120));
            var lines = rng.Shuffled(shuffled).Take(1 + rng.Pick(shuffled.Length)).Select(r => new LineSpec(r.Id, 1 + rng.Between(0, 30))).ToArray();
            return new MarketSpec("m" + m, stocks, tightWarehouses ? 150 : 1_000_000, lines);
        }).ToArray();
        return new World(Root(goods, shuffled, markets).ToJsonString(), goods, shuffled, markets);
    }

    /// <summary>
    /// A production simulation that shares nothing with the engine: recipes are ordered by a quadratic scan for the lowest id that is ready, the runs of a line are found by trying them from the capacity down until
    /// the inputs and the room allow them, and batches are a plain list.
    /// </summary>
    internal sealed class ProductionOracle
    {
        private readonly World _world;
        private readonly string[] _order;
        public long[][] Stock { get; }
        public List<(int Market, int Recipe, long Runs, long Completes)> Batches { get; } = new();
        public Dictionary<(int Market, string Recipe), long> LastRuns { get; } = new();

        public ProductionOracle(World world)
        {
            _world = world;
            Stock = world.Markets.Select(m => world.Goods.Select(g => m.Stocks.GetValueOrDefault(g)).ToArray()).ToArray();
            var placed = new List<string>();
            while (placed.Count < world.Recipes.Length)
            {
                var ready = world.Recipes.Where(r => !placed.Contains(r.Id) && world.Recipes.All(maker => maker.Id == r.Id || placed.Contains(maker.Id) || !maker.Out.Any(o => r.In.Any(i => i.Good == o.Good)))).Select(r => r.Id).OrderBy(id => id, StringComparer.Ordinal).First();
                placed.Add(ready);
            }
            _order = placed.ToArray();
        }

        private int G(string good) => Array.IndexOf(_world.Goods, good);

        public void Close(long period)
        {
            foreach (var batch in Batches.Where(b => b.Completes <= period).ToList())
            {
                var spec = _world.Recipes[batch.Recipe];
                foreach (var (good, amount) in spec.Out) Stock[batch.Market][G(good)] = Math.Min(_world.Markets[batch.Market].Capacity, Stock[batch.Market][G(good)] + batch.Runs * amount);
                Batches.Remove(batch);
            }
            for (var m = 0; m < _world.Markets.Length; m++)
                foreach (var id in _order)
                {
                    var line = _world.Markets[m].Lines!.FirstOrDefault(l => l.Recipe == id);
                    if (line is null) continue;
                    var recipeIndex = Array.FindIndex(_world.Recipes, r => r.Id == id);
                    var spec = _world.Recipes[recipeIndex];
                    var runs = line.Capacity;
                    while (runs > 0 && !Feasible(m, spec, runs)) runs--;
                    LastRuns[(m, id)] = runs;
                    if (runs == 0) continue;
                    foreach (var (good, amount) in spec.In) Stock[m][G(good)] -= runs * amount;
                    if (spec.Duration == 0) foreach (var (good, amount) in spec.Out) Stock[m][G(good)] += runs * amount;
                    else Batches.Add((m, recipeIndex, runs, period + spec.Duration));
                }
        }

        private bool Feasible(int market, RecipeSpec spec, long runs)
        {
            foreach (var (good, amount) in spec.In)
                if (Stock[market][G(good)] < runs * amount) return false;
            foreach (var (good, amount) in spec.Out)
            {
                var incoming = Batches.Where(b => b.Market == market).Sum(b => _world.Recipes[b.Recipe].Out.Where(o => o.Good == good).Sum(o => b.Runs * o.Amount));
                if (_world.Markets[market].Capacity - Stock[market][G(good)] - incoming < runs * amount) return false;
            }
            return true;
        }
    }

    // ---- unlocking ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ALockedRecipeDoesNotRunUntilItIsUnlockedAndHearingOfItTwiceDoesNoHarm()
    {
        var runtime = Runtime(SteelGoods, [Smelt(0) with { Locked = true }], [new MarketSpec("m", new() { ["ore"] = 50, ["coal"] = 50 }, 1_000_000, [new LineSpec("smelt", 10)])]);
        Assert.False(runtime.IsUnlocked(0));
        Assert.Equal(LineLimit.Locked, Line(runtime, "m", "smelt").Limit);
        var locked = Period(runtime);
        Assert.Equal((50L, 0L), (Stock(runtime, "m", "ore"), Stock(runtime, "m", "steel")));
        Assert.Empty(locked.Limits);   // it started locked and stayed locked: no change to report
        var before = runtime.StateDigest();
        var unlock = runtime.UnlockRecipe("smelt");
        Assert.Equal(0, Assert.Single(unlock.Unlocks).Recipe);
        Assert.True(runtime.IsUnlocked(0));
        Assert.NotEqual(before, runtime.StateDigest());   // the unlocked set is state, and the digest covers it
        var again = runtime.StateDigest();
        Assert.Empty(runtime.UnlockRecipe("smelt").Unlocks);
        Assert.Equal(again, runtime.StateDigest());
        var ran = Period(runtime);
        Assert.Equal((30L, 10L), (Stock(runtime, "m", "ore"), Stock(runtime, "m", "steel")));   // 10 runs of 2 ore + 1 coal -> 1 steel
        var change = Assert.Single(ran.Limits);
        Assert.Equal((LineLimit.Locked, LineLimit.Capacity), (change.From, change.To));
    }

    [Fact]
    public void AnUnlockedRecipeSurvivesASaveAndAnOpenWithItsLinesAndBatchesInFlight()
    {
        var runtime = Runtime(SteelGoods, [Smelt(3) with { Locked = true }, Smith(2)], [Forge(500, 500, 20, 7)]);
        runtime.UnlockRecipe("smelt");
        for (var i = 0; i < 6; i++) Period(runtime);
        Assert.NotEmpty(runtime.Batches);   // steel and tools are on their way
        var reopened = EconomySave.Open(runtime.Set, EconomySave.Write(runtime));
        Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
        // A save groups batches by line, so the order they are held in is not state: the same batches, in any order.
        Assert.Equal(runtime.Batches.Select(b => (b.Market, b.Recipe, b.Runs, b.Completes)).Order(), reopened.Batches.Select(b => (b.Market, b.Recipe, b.Runs, b.Completes)).Order());
        Assert.True(reopened.IsUnlocked(runtime.Set.RecipeById["smelt"].Index));
        // The derived room taken by batches on their way is rebuilt from the batches, not saved.
        for (var m = 0; m < runtime.Markets.Length; m++)
            for (var p = 0; p < runtime.Markets[m].Pairs.Length; p++) Assert.Equal(runtime.Markets[m].Pairs[p].Incoming, reopened.Markets[m].Pairs[p].Incoming);
        // Lived on from the save, it ends where the one that never stopped ends, at every period for 12 more.
        for (var i = 0; i < 12; i++)
        {
            Period(runtime);
            Period(reopened);
            Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
        }
    }

    [Fact]
    public void AnEconomyOpenedFromASaveAtEveryPointOfARandomChainEndsWhereTheOneThatNeverStoppedEnds()
    {
        var rng = new Rng(4646);
        for (var round = 0; round < 12; round++)
        {
            var world = RandomWorld(rng, tightWarehouses: round % 2 == 0);
            var set = EconomyLoader.Load(world.Text);
            var straight = EconomyRuntime.Create(set);
            var resumed = EconomyRuntime.Create(set);
            for (var period = 1; period <= 12; period++)
            {
                Period(straight);
                Period(resumed);
                resumed = EconomySave.Open(set, EconomySave.Write(resumed));   // saved and opened after every period
                Assert.Equal(straight.StateDigest(), resumed.StateDigest());
            }
        }
    }

    [Fact]
    public void TheDigestChangesWithABatchALineAndTheUnlockedSet()
    {
        var a = Runtime(SteelGoods, [Smelt(1), Smith()], [Forge(100, 100)]);
        var b = Runtime(SteelGoods, [Smelt(1), Smith()], [Forge(100, 100)]);
        Assert.Equal(a.StateDigest(), b.StateDigest());
        Period(a);
        Assert.NotEqual(a.StateDigest(), b.StateDigest());
        Period(b);
        Assert.Equal(a.StateDigest(), b.StateDigest());
        var image = Tamper.Image(EconomySave.Write(a));
        Assert.Single(image.Batches);
        Assert.Equal(2, image.Markets[0].Lines.Count);
        Assert.StartsWith("economy-state-6\n", EconomyImage.Canonical(image), StringComparison.Ordinal);
        Assert.Contains("\nbatches 0|0|", EconomyImage.Canonical(image), StringComparison.Ordinal);
        Assert.Contains("\nunlocked 1,1", EconomyImage.Canonical(image), StringComparison.Ordinal);
        Assert.Contains("\n line ", EconomyImage.Canonical(image), StringComparison.Ordinal);
    }

    // ---- forged production state is refused as state -----------------------------------------------------------------------------------

    private static EconomySet ForgerySet() => EconomyLoader.Load(Root(SteelGoods, [Smelt(2) with { Locked = true }, Smith(1), new("open", [("ore", 1)], [("coal", 1)], 0)], [Forge(500, 500, 20, 7) with { Lines = [new LineSpec("smelt", 20), new LineSpec("smith", 7), new LineSpec("open", 3)] }, new MarketSpec("n", new())]).ToJsonString());

    private static ImageBatchGroup SmeltBatch(EconomyImage image) => image.Batches.First(b => b.Recipe == 0);

    private static EconomyRuntime ForgeryBase()
    {
        // 'open' consumes ore and makes coal; smelt consumes both; no cycle: open -> smelt -> smith.
        var runtime = EconomyRuntime.Create(ForgerySet());
        runtime.UnlockRecipe("smelt");
        for (var i = 0; i < 4; i++) Period(runtime);
        Assert.NotEmpty(runtime.Batches);
        return runtime;
    }

    public static IEnumerable<object[]> Forgeries() => ForgeryCases.Select((c, index) => new object[] { index, c.Name });

    private static readonly (string Name, Action<EconomyImage> Edit)[] ForgeryCases =
    [
        ("a recipe flag missing", i => i.Unlocked.RemoveAt(0)),
        ("a recipe the document never locked, locked", i => i.Unlocked[1] = false),
        ("a line missing", i => i.Markets[0].Lines.RemoveAt(0)),
        ("runs above the line's capacity", i => i.Markets[0].Lines[0].LastRuns = 21),
        ("negative runs", i => i.Markets[0].Lines[0].LastRuns = -1),
        ("a limit that does not exist", i => i.Markets[0].Lines[0].Limit = 6),
        ("a limiting good outside the goods", i => { i.Markets[0].Lines[0].Limit = 2; i.Markets[0].Lines[0].LimitGood = 99; }),
        ("an input limit naming a good that is not an input", i => { i.Markets[0].Lines[0].Limit = 2; i.Markets[0].Lines[0].LimitGood = 2; }),
        ("a room limit naming an input", i => { i.Markets[0].Lines[0].Limit = 3; i.Markets[0].Lines[0].LimitGood = 0; }),
        ("a capacity limit naming a good", i => { i.Markets[0].Lines[0].Limit = 1; i.Markets[0].Lines[0].LimitGood = 0; }),
        ("a batch of a market that does not exist", i => SmeltBatch(i).Market = 5),
        ("a batch of a recipe that does not exist", i => SmeltBatch(i).Recipe = 9),
        ("a batch in a market with no line for its recipe", i => SmeltBatch(i).Market = 1),
        ("a batch with no runs", i => SmeltBatch(i).Runs[0] = 0),
        ("a batch with more runs than its line's capacity", i => SmeltBatch(i).Runs[0] = 99),
        ("a batch completing in the past", i => SmeltBatch(i).Completes[0] = 0),
        ("a batch completing beyond its recipe's duration", i => SmeltBatch(i).Completes[0] += 50),
        ("a batch completing one period beyond its recipe's duration", i => SmeltBatch(i).Completes[0] = i.Step + 3),
        ("two batches of one line completing together", i => { SmeltBatch(i).Runs.Add(1); SmeltBatch(i).Completes.Add(SmeltBatch(i).Completes[0]); }),
        ("a batch of a recipe that takes no time", i => SmeltBatch(i).Recipe = 2),
    ];

    [Theory]
    [MemberData(nameof(Forgeries))]
    public void AForgedProductionStateWithARecomputedDigestIsRefusedAsState(int index, string name)
    {
        var good = EconomySave.Write(ForgeryBase());
        EconomySave.Open(ForgerySet(), good);   // the unforged save opens
        var forged = Tamper.Forge(good, ForgeryCases[index].Edit);
        var code = Assert.Throws<EngineRefusedException>(() => EconomySave.Open(ForgerySet(), forged)).Code;
        Assert.True(code == EconomyCodes.SaveState, name + " was refused as " + code);
    }

    [Fact]
    public void ABatchOfALockedRecipeAndALockedLineThatRanAreRefusedAsState()
    {
        var runtime = EconomyRuntime.Create(ForgerySet());   // 'smelt' is locked and not unlocked
        for (var i = 0; i < 2; i++) Period(runtime);
        var good = EconomySave.Write(runtime);
        EconomySave.Open(runtime.Set, good);
        var smelt = runtime.Set.RecipeById["smelt"].Index;
        var line = runtime.Set.Markets[0].Lines.First(l => l.Recipe == smelt).Index;
        Assert.Equal(EconomyCodes.SaveState, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(runtime.Set, Tamper.Forge(good, i => i.Markets[0].Lines[line].LastRuns = 1))).Code);
        Assert.Equal(EconomyCodes.SaveState, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(runtime.Set, Tamper.Forge(good, i => i.Markets[0].Lines[line].Limit = 1))).Code);
        Assert.Equal(EconomyCodes.SaveState, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(runtime.Set, Tamper.Forge(good, i => i.Batches.Add(new ImageBatchGroup { Market = 0, Recipe = smelt, Runs = [1], Completes = [3] })))).Code);
    }

    [Fact]
    public void ABatchesCompletionPeriodIsPartOfTheDigestAndTheEdgesOfItsRangeAreExact()
    {
        var runtime = Runtime(SteelGoods, [Smelt(3), Smith()], [Forge(500, 500, 20, 7)]);
        Period(runtime);   // one smelt batch, due three periods on
        var good = EconomySave.Write(runtime);
        var batch = Assert.Single(Tamper.Image(good).Batches);
        Assert.Equal(4, Assert.Single(batch.Completes));
        // Earlier by one, still inside the range: only the digest can tell (the completion is covered by it).
        Assert.Equal(EconomyCodes.SaveDigest, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(runtime.Set, Tamper.Stale(good, i => i.Batches[0].Completes[0] = 3))).Code);
        // The first and the last period the range allows are accepted when the digest is right; one beyond either end is refused as state.
        EconomySave.Open(runtime.Set, Tamper.Forge(good, i => i.Batches[0].Completes[0] = 2));
        EconomySave.Open(runtime.Set, Tamper.Forge(good, i => i.Batches[0].Completes[0] = 4));
        Assert.Equal(EconomyCodes.SaveState, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(runtime.Set, Tamper.Forge(good, i => i.Batches[0].Completes[0] = 1))).Code);
        Assert.Equal(EconomyCodes.SaveState, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(runtime.Set, Tamper.Forge(good, i => i.Batches[0].Completes[0] = 5))).Code);
    }

    [Fact]
    public void AProductionRangeIsRefusedAsStateEvenWithTheOldDigest()
    {
        var good = EconomySave.Write(ForgeryBase());
        var stale = Tamper.Stale(good, i => i.Markets[0].Lines[0].LastRuns = 999);
        Assert.Equal(EconomyCodes.SaveState, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(ForgerySet(), stale)).Code);
        var wrongDigest = Tamper.Stale(good, i => i.Currencies[0].Money += 1);   // in range, so only the digest can refuse it
        Assert.Equal(EconomyCodes.SaveDigest, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(ForgerySet(), wrongDigest)).Code);
    }

    // ---- the trace ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheExplanationOfALineSaysWhatEveryInputAndOutputAllowsAndWhichOneLimits()
    {
        var runtime = Runtime(SteelGoods, [Smith(), Smelt()], [Forge(100, 30)]);
        var smelt = EconomyExplain.Production(runtime, "m", "smelt");
        Assert.Equal(30, smelt.Value);
        Assert.Equal("input", smelt.State);
        var text = smelt.Text();
        Assert.Contains("production of smelt in m = 30 (input)", text, StringComparison.Ordinal);
        Assert.Contains("input ore: 50 (needs 2 a run; stock 100 at this line's turn)", text, StringComparison.Ordinal);
        Assert.Contains("input coal: 30 (needs 1 a run; stock 30 at this line's turn)", text, StringComparison.Ordinal);
        Assert.Contains("room steel: 1000000 (makes 1 a run;", text, StringComparison.Ordinal);
        Assert.Contains("runs smelt: 30 (of 40 wanted: short of coal)", text, StringComparison.Ordinal);
        Assert.Contains("order smelt: 1 (of 2 lines of m)", text, StringComparison.Ordinal);

        // Smith's turn comes after smelt's: the trace shows what smelt left, not what is in the warehouse now. Instant smelt: 30 steel are there at smith's turn.
        var instant = Runtime(SteelGoods, [Smith(), Smelt(0)], [Forge(100, 30)]);
        var smith = EconomyExplain.Production(instant, "m", "smith").Text();
        Assert.Contains("input steel: 15 (needs 2 a run; stock 30 at this line's turn)", smith, StringComparison.Ordinal);
        Assert.Contains("order smith: 2 (of 2 lines of m; after smelt)", smith, StringComparison.Ordinal);
        Assert.Contains("runs smith: 10 (of 10 wanted: the line's capacity)", smith, StringComparison.Ordinal);
        // The explanation changed nothing.
        Assert.Equal(Runtime(SteelGoods, [Smith(), Smelt(0)], [Forge(100, 30)]).StateDigest(), instant.StateDigest());

        Period(runtime);
        Assert.Contains("last-period smelt: 30 (short of coal)", EconomyExplain.Production(runtime, "m", "smelt").Text(), StringComparison.Ordinal);
        Assert.Contains("batch smelt: 30 (runs, outputs due at period 2)", EconomyExplain.Production(runtime, "m", "smelt").Text(), StringComparison.Ordinal);

        var locked = Runtime(SteelGoods, [Smelt() with { Locked = true }], [new MarketSpec("m", new() { ["ore"] = 9, ["coal"] = 9 }, 1_000_000, [new LineSpec("smelt", 5)])]);
        var lockedText = EconomyExplain.Production(locked, "m", "smelt");
        Assert.Equal("locked", lockedText.State);
        Assert.Contains("locked until discovered", lockedText.Text(), StringComparison.Ordinal);
        Assert.Contains("the recipe is locked", lockedText.Text(), StringComparison.Ordinal);
    }

    [Fact]
    public void ACallThatNamesNothingTheEngineHasIsRefusedByCode()
    {
        var runtime = Runtime(SteelGoods, [Smelt()], [Forge(1, 1, 1, 1) with { Lines = [new LineSpec("smelt", 1)] }]);
        Assert.Equal(EconomyCodes.UnknownRecipe, Assert.Throws<EngineRefusedException>(() => EconomyExplain.Production(runtime, "m", "nothing")).Code);
        Assert.Equal(EconomyCodes.UnknownMarket, Assert.Throws<EngineRefusedException>(() => EconomyExplain.Production(runtime, "nowhere", "smelt")).Code);
        Assert.Equal(EconomyCodes.UnknownRecipe, Assert.Throws<EngineRefusedException>(() => runtime.UnlockRecipe("nothing")).Code);
        Assert.Equal(EconomyCodes.RecipeOpen, Assert.Throws<EngineRefusedException>(() => runtime.UnlockRecipe("smelt")).Code);
        Assert.Equal(EconomyCodes.UnknownName, Assert.Throws<EngineRefusedException>(() => runtime.UnlockRecipe("Smelt")).Code);
    }

    // ---- the town sample --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheTownSampleRunsItsChainsAndShowsAShortageOfOre()
    {
        var runtime = Fixture.Runtime();
        var riverton = runtime.MarketIndex("riverton");
        Assert.Equal(5, runtime.Set.Recipes.Count);
        Assert.False(runtime.IsUnlocked(runtime.Set.RecipeById["alloy-steel"].Index));
        Assert.True(runtime.Set.RecipePosition[runtime.Set.RecipeById["mill"].Index] < runtime.Set.RecipePosition[runtime.Set.RecipeById["bakery"].Index]);
        Assert.True(runtime.Set.RecipePosition[runtime.Set.RecipeById["smelt-steel"].Index] < runtime.Set.RecipePosition[runtime.Set.RecipeById["smith-tools"].Index]);
        Fixture.Periods(runtime, 10);
        Assert.True(Line(runtime, "riverton", "mill").LastRuns > 0);
        Assert.True(Stock(runtime, "riverton", "flour") > 0);
        Assert.True(Line(runtime, "hillfort", "smelt-steel").LastRuns > 0);
        Assert.Equal(0, Line(runtime, "hillfort", "alloy-steel").LastRuns);   // still locked
        Assert.NotEmpty(runtime.PlanLines(riverton));
        Fixture.Periods(runtime, 35);
        var smelt = Line(runtime, "hillfort", "smelt-steel");
        Assert.Equal(LineLimit.Input, smelt.Limit);
        Assert.Equal("ore", runtime.Set.Goods[smelt.LimitGood].Id);
        Assert.True(smelt.LastRuns < 40);
        Assert.Contains("short of ore", EconomyExplain.Production(runtime, "hillfort", "smelt-steel").Text(), StringComparison.Ordinal);
    }
}
