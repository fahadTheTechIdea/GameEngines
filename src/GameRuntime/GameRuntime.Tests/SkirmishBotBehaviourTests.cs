using AIEngine;
using GameRuntime.Combat.Headless;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>The four skirmish bots are behaviours of <c>skirmish-bots.json</c>. These cases drive them directly, with no game around them, and pin each distance, cover and cadence.</summary>
public sealed class SkirmishBotBehaviourTests
{
    private const long Budget = 100_000_000_000;

    private static AiRuntime Bot(string behavior, long start = 300, long distance = -1, bool fighting = false, bool ready = true)
    {
        var r = new AiRuntime(SkirmishBots.Set, 1);
        r.Add("commander", behavior, 0);
        r.SetKey("commander", "can-act", FactValue.From(ready));
        r.SetKey("commander", "fighting", FactValue.From(fighting));
        r.SetKey("commander", "settled", FactValue.From(distance >= 0));
        r.SetKey("commander", "distance", FactValue.From(Math.Max(0, distance)));
        r.SetKey("commander", "start", FactValue.From(start));
        r.SetKey("commander", "seconds", FactValue.From(7L));
        r.SetKey("commander", "terrain", FactValue.From("hills"));
        return r;
    }

    private static AiRuntime Duel(string behavior, long range = 100, long start = 300, long distance = -1, bool fighting = false, bool ready = true)
    {
        var r = Bot(behavior, start, distance, fighting, ready);
        r.Perceive("commander", "group", "a", "", Attrs(1, 100, range, true), 0);
        r.Perceive("commander", "group", "b", "", Attrs(2, 100, 50, true), 0);
        return r;
    }

    private static Dictionary<string, FactValue> Attrs(long side, long health, long range, bool standing) => new()
    { ["side"] = FactValue.From(side), ["health"] = FactValue.From(health), ["range"] = FactValue.From(range), ["standing"] = FactValue.From(standing) };

    private static string Order(AiRuntime r, long firstTick = 1, long lastTick = 15)
    {
        var lines = new List<string>();
        for (var tick = firstTick; tick <= lastTick; tick++)
            foreach (var i in r.Step(tick, Budget).Intents)
                lines.Add($"{tick} {i.Name} " + string.Join(' ', i.Args.Select(a => a.Name + "=" + (a.Value.Kind == FactKind.String ? a.Value.Text : a.Value.Kind == FactKind.Boolean ? (a.Value.Boolean ? "true" : "false") : a.Value.Integer.ToString(System.Globalization.CultureInfo.InvariantCulture)))));
        return string.Join("; ", lines);
    }

    [Fact]
    public void EveryBotActsOnlyOnTheBeatOfFifteenAndOnlyWhenTheGameIsReady()
    {
        foreach (var bot in new[] { "aggressor", "defender", "skirmisher" })
        {
            Assert.NotEqual("", Order(Duel(bot)));
            Assert.Equal("", Order(Duel(bot), 1, 14));
            Assert.Equal("", Order(Duel(bot, ready: false)));
            Assert.StartsWith("30 ", Order(Duel(bot), 16, 30));
        }
    }

    [Fact]
    public void TheAggressorMarchesInTheApproachAndFightsFromTheOpenOnceTheMarchSettled()
    {
        Assert.Equal("15 march group=a metres=300 seconds=7 terrain=hills", Order(Duel("aggressor")));
        Assert.Equal("15 march group=a metres=120 seconds=7 terrain=hills", Order(Duel("aggressor", distance: 120)));
        Assert.Equal("15 engage attacker=a cover=open defender=b metres=120 mutual=true seconds=7 terrain=hills", Order(Duel("aggressor", distance: 120, fighting: true)));
        Assert.Equal("", Order(Duel("aggressor", distance: 120).Also(r => r.Lose("commander", "group", "b"))));   // a duel needs two groups
    }

    [Fact]
    public void TheDefenderHoldsBehindHeavyCoverWithTheSecondGroupFiringAtTheFirst()
    {
        Assert.Equal("15 engage attacker=b cover=heavy defender=a metres=300 mutual=true seconds=7 terrain=hills", Order(Duel("defender")));
        Assert.Equal("15 engage attacker=b cover=heavy defender=a metres=90 mutual=true seconds=7 terrain=hills", Order(Duel("defender", distance: 90, fighting: true)));
    }

    [Theory]
    [InlineData(200, 300, -1, false, "march")]       // wants 180, is at 300
    [InlineData(200, 300, 180, false, "engage")]     // at 180: close enough
    [InlineData(200, 300, 181, false, "march")]
    [InlineData(111, 300, 95, false, "engage")]      // nine tenths of 111 is 99: at 95 it stands (eight tenths, 88, would still march)
    [InlineData(111, 300, 100, false, "march")]
    [InlineData(1, 300, 50, false, "march")]         // never wants less than arm's length, 2 m: at 50 it is still far
    [InlineData(500, 300, 300, false, "engage")]     // never farther than the start distance
    [InlineData(200, 300, 250, true, "engage")]      // a fight follows a march whatever the distance
    public void TheSkirmisherClosesToNineTenthsOfItsRangeThenStandsInLightCover(long range, long start, long distance, bool fighting, string expected)
    {
        var line = Order(Duel("skirmisher", range, start, distance, fighting));
        Assert.StartsWith("15 " + expected + " ", line);
        if (expected == "engage") Assert.Contains("cover=light", line);
    }

    [Fact]
    public void TheMeleeFiresEveryStandingGroupAtTheWeakestStandingGroupOfAnotherSideAndHalvesTheDistance()
    {
        var r = Bot("melee", start: 300, distance: 101);
        var sides = new Dictionary<string, (int Side, long Health, bool Standing)> { ["a"] = (1, 100, true), ["b"] = (2, 50, true), ["c"] = (3, 20, true), ["d"] = (2, 10, false) };
        foreach (var (id, s) in sides)
        {
            r.Perceive("commander", "group", id, "", Attrs(s.Side, s.Health, 0, s.Standing), 0);
            foreach (var (other, o) in sides.Where(x => x.Value.Side != s.Side))
                r.Perceive("commander", "foe", id + ">" + other, id, new Dictionary<string, FactValue>
                { ["group"] = FactValue.From(other), ["health"] = FactValue.From(o.Health), ["standing"] = FactValue.From(o.Standing) }, 0);
        }
        // a hits c (the weakest standing), b hits c, c hits b, d is down and fires nothing; the distance 101 halves to 50.
        Assert.Equal("15 fire attacker=a cover=open defender=c metres=50 terrain=hills; 15 fire attacker=b cover=open defender=c metres=50 terrain=hills; 15 fire attacker=c cover=open defender=b metres=50 terrain=hills", Order(r));
    }

    [Fact]
    public void TheMeleeStartsAtTheStartDistanceAndNeverComesCloserThanArmsLength()
    {
        string At(long distance)
        {
            var r = Bot("melee", start: 300, distance: distance);
            r.Perceive("commander", "group", "a", "", Attrs(1, 10, 0, true), 0);
            r.Perceive("commander", "group", "b", "", Attrs(2, 10, 0, true), 0);
            r.Perceive("commander", "foe", "a>b", "a", new Dictionary<string, FactValue> { ["group"] = FactValue.From("b"), ["health"] = FactValue.From(10L), ["standing"] = FactValue.From(true) }, 0);
            return Order(r).Split("metres=")[1].Split(' ')[0];
        }
        Assert.Equal("300", At(-1));
        Assert.Equal("2", At(3));
        Assert.Equal("2", At(0));
        Assert.Equal("5", At(11));
    }
}

internal static class AlsoExtension
{
    public static T Also<T>(this T value, Action<T> action) { action(value); return value; }
}
