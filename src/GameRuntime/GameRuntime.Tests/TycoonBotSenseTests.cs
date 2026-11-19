using GameRuntime.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>What the tycoon bot tells its agent: exactly what the player has learned, only when it changed, each refusal once and in order, and forgotten after the agent took it in.</summary>
public sealed class TycoonBotSenseTests
{
    private sealed class Port : IEventPort
    {
        public List<object> Events { get; } = new();
        public void Emit<T>(in T evt) where T : struct => Events.Add(evt);
        public void Connect<T>(Action<T> handler) where T : struct { }
    }

    private static PlayerView View(long goal = 1500, bool founded = true, long stock = 40, long profit = 12) =>
        new(goal) { Founded = founded, Stock = stock, Profit = profit };

    private static Dictionary<string, string> Keys(Port port) => port.Events.OfType<AiKeyPosted>().ToDictionary(k => k.Key, k => k.Value);

    [Fact]
    public void TheFirstSenseAddsTheAgentAndTellsItEverythingTheAgentNeeds()
    {
        var port = new Port();
        var bot = TycoonBots.Create("cautious", 1, goalProfit: 1500)!;
        bot.Sense(port, View());
        Assert.IsType<AiAgentAddRequested>(port.Events[0]);
        var keys = Keys(port);
        Assert.Equal("1500", keys["goal"]);
        Assert.Equal("true", keys["founded"]);
        Assert.Equal("40", keys["stock"]);
        Assert.Equal("12", keys["profit"]);
        Assert.Equal("0", keys["in-flight"]);
        Assert.InRange(long.Parse(keys["lot-start"]), 3, 5);
        Assert.All(port.Events.OfType<AiKeyPosted>(), k => Assert.Equal(bot.Component.Id, k.Component));
    }

    [Fact]
    public void OnlyWhatChangedIsToldAgainAndTheNumbersAreClampedToWhatTheDocumentHolds()
    {
        var port = new Port();
        var bot = TycoonBots.Create("greedy", 1, goalProfit: 500_000_000_000_000)!;
        bot.Sense(port, View());
        port.Events.Clear();
        bot.Sense(port, View());
        Assert.Empty(port.Events);
        bot.Sense(port, View(stock: 39, profit: 12));
        Assert.Equal(["stock"], port.Events.OfType<AiKeyPosted>().Select(k => k.Key));
        port.Events.Clear();
        bot.Sense(port, View(stock: -1, founded: false, profit: 900_000_000_000_000));
        var keys = Keys(port);
        Assert.Equal("-1", keys["stock"]);
        Assert.Equal("100000000000", keys["profit"]);       // clamped
        var first = new Port();
        TycoonBots.Create("greedy", 1, goalProfit: 500_000_000_000_000)!.Sense(first, View());
        Assert.Equal("100000000000", Keys(first)["goal"]);   // the goal too
        var negative = new Port();
        TycoonBots.Create("greedy", 1, goalProfit: 7)!.Sense(negative, View(stock: -50));
        Assert.Equal("-1", Keys(negative)["stock"]);
    }

    [Fact]
    public void ARefusalIsToldOnceWithItsCodeAndUnitsInTheOrderItCameAndForgottenAfterTheAgentThought()
    {
        var port = new Port();
        var bot = TycoonBots.Create("greedy", 1, goalProfit: 1500)!;
        var view = View();
        view.RefusalList.Add(new RefusedSale("a", 8, 3, "NO_STOCK"));
        view.RefusalList.Add(new RefusedSale("b", 4, 3, "OTHER"));
        view.InFlight["x"] = (1, 2);
        bot.Sense(port, view);
        var told = port.Events.OfType<AiPerceptPosted>().ToList();
        Assert.Equal(["r000000000", "r000000001"], told.Select(t => t.Id));
        Assert.Equal(["no-stock=true;units=8", "no-stock=false;units=4"], told.Select(t => t.Attrs));
        Assert.Equal("1", Keys(port)["in-flight"]);
        port.Events.Clear();
        view.RefusalList.Clear();
        view.RefusalList.Add(new RefusedSale("c", 2, 5, "NO_STOCK"));
        bot.Sense(port, view);
        Assert.Equal(["r000000002"], port.Events.OfType<AiPerceptPosted>().Select(t => t.Id));
        port.Events.Clear();
        Assert.Empty(bot.Actions(port));
        Assert.Equal(["r000000000", "r000000001", "r000000002"], port.Events.OfType<AiPerceptLost>().Select(l => l.Id));
        port.Events.Clear();
        Assert.Empty(bot.Actions(port));
        Assert.Empty(port.Events);
    }

    [Fact]
    public void AnIntentOfAnotherComponentIsNotTheBotsAndSellAndSpeedBecomeTheActionsPressed()
    {
        var port = new Port();
        var bot = TycoonBots.Create("greedy", 1, goalProfit: 1500)!;
        bot.Collect(new AiIntentRaised("someone-else", 1, "player", "n", "sell", "units=99"));
        bot.Collect(new AiIntentRaised(bot.Component.Id, 1, "player", "n", "speed", "numerator=2;denominator=1"));
        bot.Collect(new AiIntentRaised(bot.Component.Id, 1, "player", "n", "sell", "units=7"));
        Assert.Equal([PlayerAction.Speed(2, 1), PlayerAction.Sell(7)], bot.Actions(port));
    }
}
