using AIEngine;
using GameRuntime.Headless;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>The two tycoon bots are behaviours of <c>tycoon-bots.json</c>. These cases drive them directly, with no game around them, and pin each rule the hand-written policies had.</summary>
public sealed class TycoonBotBehaviourTests
{
    private const long Budget = 1_000_000_000;

    private static AiRuntime Bot(string behavior, long lot = 8, long goal = 1500, long stock = 100, long profit = 0, bool founded = true, long inFlight = 0)
    {
        var r = new AiRuntime(TycoonBots.Set, 1);
        r.Add("player", behavior, 0);
        Host(r, "lot-start", lot); Host(r, "goal", goal); Host(r, "stock", stock); Host(r, "profit", profit); Host(r, "in-flight", inFlight);
        r.SetKey("player", "founded", FactValue.From(founded));
        return r;
    }

    private static void Host(AiRuntime r, string key, long value) => r.SetKey("player", key, FactValue.From(value));

    private static void Refuse(AiRuntime r, int number, long units, bool noStock = true) =>
        r.Perceive("player", "refusal", "r" + number.ToString("D3"), "", new Dictionary<string, FactValue> { ["no-stock"] = FactValue.From(noStock), ["units"] = FactValue.From(units) }, r.LastTick);

    private static List<string> Step(AiRuntime r, long tick)
    {
        var lines = r.Step(tick, Budget).Intents.Select(i => i.Name + " " + string.Join(' ', i.Args.Select(a => a.Name + "=" + a.Value.Integer))).ToList();
        foreach (var known in r.Known("player", "refusal").ToList()) r.Lose("player", "refusal", known.Id);   // the game forgets a refusal once the agent has taken it in
        return lines;
    }

    // ---- greedy ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void GreedySpeedsTheGameUpOnceThenSellsItsLotEveryTick()
    {
        var r = Bot("greedy", lot: 8);
        Assert.Equal(["speed denominator=1 numerator=2", "sell units=8"], Step(r, 1));
        Assert.Equal(["sell units=8"], Step(r, 2));
        Assert.Equal(["sell units=8"], Step(r, 3));
    }

    [Fact]
    public void GreedyDoesNothingBeforeTheCompanyExistsAndStopsWhenTheGoalIsMetExactly()
    {
        Assert.Empty(Step(Bot("greedy", founded: false), 1));
        Assert.NotEmpty(Step(Bot("greedy", goal: 1500, profit: 1499), 1));
        Assert.Empty(Step(Bot("greedy", goal: 1500, profit: 1500), 1));
        Assert.Empty(Step(Bot("greedy", goal: 1500, profit: 9000), 1));
    }

    [Fact]
    public void ARefusalOfTheCurrentLotHalvesItAndARefusalOfAnOlderLotTeachesNothing()
    {
        var r = Bot("greedy", lot: 8);
        Step(r, 1);
        Refuse(r, 1, units: 8);
        Assert.Equal(["sell units=4"], Step(r, 2));
        Refuse(r, 2, units: 8);                    // asked with the old lot after it was already halved
        Assert.Equal(["sell units=4"], Step(r, 3));
        Refuse(r, 3, units: 4, noStock: false);    // some other refusal
        Assert.Equal(["sell units=4"], Step(r, 4));
        Refuse(r, 4, units: 4);
        Refuse(r, 5, units: 2);                    // in the order they came: 4 halves to 2, and then the refusal of 2 halves to 1
        Assert.Equal(["sell units=1"], Step(r, 5));
    }

    [Fact]
    public void GreedyGivesUpWhenASingleUnitIsRefused()
    {
        var r = Bot("greedy", lot: 2);
        Step(r, 1);
        Refuse(r, 1, 2);
        Assert.Equal(["sell units=1"], Step(r, 2));
        Refuse(r, 2, 1);
        Assert.Empty(Step(r, 3));
        Assert.Empty(Step(r, 4));
        Assert.Equal(FactValue.From(true), r.Key("player", "exhausted"));
    }

    // ---- cautious --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void CautiousSellsTheSmallerOfItsLotAndTheStockThenWaitsFortyFiveTicks()
    {
        var r = Bot("cautious", lot: 4, stock: 3);
        Assert.Equal(["sell units=3"], Step(r, 1));
        for (var tick = 2; tick <= 45; tick++) Assert.Empty(Step(r, tick));
        Assert.Equal(["sell units=3"], Step(r, 46));          // 45 ticks after the sale at 1
        Host(r, "stock", 20);
        for (var tick = 47; tick <= 90; tick++) Assert.Empty(Step(r, tick));
        Assert.Equal(["sell units=4"], Step(r, 91));
    }

    [Fact]
    public void CautiousWaitsForTheAnswerToItsLastSaleAndSellsNothingWithoutStock()
    {
        var r = Bot("cautious", lot: 4, inFlight: 1);
        Assert.Empty(Step(r, 100));
        Host(r, "in-flight", 0);
        Assert.Equal(["sell units=4"], Step(r, 101));
        Assert.Empty(Step(Bot("cautious", stock: 0), 100));
        Assert.Empty(Step(Bot("cautious", stock: -1), 100));
        Assert.Empty(Step(Bot("cautious", founded: false), 100));
        Assert.Empty(Step(Bot("cautious", goal: 1500, profit: 1500), 100));
        Assert.NotEmpty(Step(Bot("cautious", goal: 1500, profit: 1499), 100));
    }

    [Fact]
    public void CautiousHalvesItsLotForEveryRefusalForStockWhateverTheUnitsAndNeverBelowOne()
    {
        var r = Bot("cautious", lot: 9);
        Refuse(r, 1, units: 3);
        Refuse(r, 2, units: 99);
        Refuse(r, 3, units: 1, noStock: false);
        Assert.Equal(["sell units=2"], Step(r, 100));          // 9 to 4 to 2; the other refusal is ignored
        var small = Bot("cautious", lot: 1);
        Refuse(small, 1, 1);
        Assert.Equal(["sell units=1"], Step(small, 100));
    }

    [Fact]
    public void TheSeedDrawsTheFirstLotInsideTheRangeTheDocumentGivesEachBot()
    {
        var greedy = Enumerable.Range(1, 200).Select(s => LotOf("greedy", (ulong)s)).ToList();
        var cautious = Enumerable.Range(1, 200).Select(s => LotOf("cautious", (ulong)s)).ToList();
        Assert.Equal(new long[] { 6, 7, 8, 9, 10 }, greedy.Distinct().Order());
        Assert.Equal(new long[] { 3, 4, 5 }, cautious.Distinct().Order());
        Assert.Equal(greedy, Enumerable.Range(1, 200).Select(s => LotOf("greedy", (ulong)s)));
    }

    private static long LotOf(string name, ulong seed)
    {
        var bot = TycoonBots.Create(name, seed, 1500)!;
        var field = typeof(TycoonBot).GetField("_lotStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (long)field.GetValue(bot)!;
    }
}
