using AIEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace AIEngine.Tests;

/// <summary>A3, A4 and A6: the cost of a think is bounded, the budget is strict, agents are served oldest first and nothing depends on the order they were added.</summary>
public sealed class SchedulerTests
{
    private static string Rest(string id = "t") => "{ \"id\": \"" + id + "\", \"kind\": \"act\", \"intent\": \"rest\", \"args\": { } }";

    [Fact]
    public void AStepNeverSpendsMoreThanItsBudgetAndTheBudgetMustCoverTheLargestBehaviour()
    {
        var set = Fixture.Load();
        var runtime = new AiRuntime(set, 1);
        Assert.Equal(AiCodes.Budget, Fixture.Refusal(() => runtime.Step(1, set.MaxCost - 1)));
        Assert.Equal(0, runtime.LastTick);                                 // a refused step changed nothing
        for (var i = 0; i < 40; i++) runtime.Add("c" + i.ToString("00"), "commander", 0);
        foreach (var agent in runtime.Agents) { runtime.SetKey(agent, "ready", FactValue.From(true)); for (var u = 0; u < 6; u++) Fixture.Tell(runtime, agent, "u" + u, hostile: u % 2 == 0); }
        var budget = set.MaxCost * 3;
        for (var tick = 1; tick <= 40; tick++)
        {
            var report = runtime.Step(tick, budget);
            Assert.True(report.Spent <= budget, $"tick {tick} spent {report.Spent} of {budget}");
            Assert.True(report.Thought <= 3 || report.Spent <= budget);
        }
    }

    [Fact]
    public void AThinkNeverCostsMoreThanTheBoundWorkedOutFromTheDocument()
    {
        var set = Fixture.Load();
        var random = new Random(12345);   // test data only: the engine itself rolls nothing here
        foreach (var behavior in new[] { "commander", "worker", "scout" })
        {
            long highest = 0;
            for (var trial = 0; trial < 300; trial++)
            {
                var runtime = new AiRuntime(set, (ulong)trial);
                runtime.Add("a", behavior, 0);
                runtime.SetKey("a", "ready", FactValue.From(random.Next(2) == 0));
                runtime.SetKey("a", "hunger", FactValue.From((long)random.Next(101)));
                var units = random.Next(17);
                for (var u = 0; u < units; u++) Fixture.Tell(runtime, "a", "u" + u.ToString("00"), hostile: random.Next(2) == 0, standing: random.Next(4) != 0, health: random.Next(1001));
                for (var s = 0; s < random.Next(9); s++) runtime.Perceive("a", "stock", "s" + s, s % 2 == 0 ? "depot-a" : "depot-b", new Dictionary<string, FactValue> { ["held"] = FactValue.From((long)random.Next(1000)) }, 0);
                for (var tick = 1; tick <= 40; tick++)
                {
                    var spent = runtime.Step(tick, set.MaxCost).Spent;
                    Assert.True(spent <= set.Behaviors[behavior].MaxCost, $"{behavior}: spent {spent} above the bound {set.Behaviors[behavior].MaxCost}");
                    highest = Math.Max(highest, spent);
                }
            }
            Assert.True(highest > 0);
            Assert.True(highest * 20 >= set.Behaviors[behavior].MaxCost || behavior == "scout" || behavior == "commander", "the bound is not absurdly loose for " + behavior);
        }
    }

    [Fact]
    public void WhenNotEveryoneFitsTheOldestDueGoFirstAndTheRestKeepTheirTurnForNextTime()
    {
        var runtime = new AiRuntime(Fixture.LoadMini(Rest()), 1);
        foreach (var id in new[] { "e", "c", "a", "d", "b" }) runtime.Add(id, "t", 0);
        // Each think costs 1 and the budget is 2: two agents a step, in id order among those due the same tick, the others waiting.
        var order = new List<string>();
        var waits = new List<long>();
        for (var tick = 1; tick <= 4; tick++)
        {
            var report = runtime.Step(tick, 2);
            order.AddRange(report.Intents.Select(i => i.Agent));
            waits.Add(report.LongestWait);
        }
        Assert.Equal(["a", "b", "c", "d", "e", "a", "b", "c"], order);
        Assert.Equal([1, 2, 3, 2], waits);   // the longest wait among those served that tick: a and b (due 0) at 1; c and d at 2; e at 3; b (due 2) at 4
    }

    [Fact]
    public void AnAgentThatThoughtLateIsDueAgainOneIntervalAfterWhenItActuallyThought()
    {
        var runtime = new AiRuntime(Fixture.LoadMini(Rest(), every: 5), 1);
        runtime.Add("a", "t", 0);
        runtime.Add("b", "t", 0);
        var due = new[] { runtime.Due("a"), runtime.Due("b") };
        var late = due.Max() + 2;
        runtime.Step(late, 1);                                   // room for one: the one due first goes
        var served = due[0] <= due[1] ? "a" : "b";
        Assert.Equal(late + 5, runtime.Due(served));              // five ticks after it thought, not five after it was due
    }

    [Fact]
    public void ADueAgentThatDoesNotFitStaysDueAndIsReported()
    {
        var runtime = new AiRuntime(Fixture.LoadMini(Rest()), 1);
        for (var i = 0; i < 10; i++) runtime.Add("a" + i, "t", 0);
        var report = runtime.Step(1, 4);
        Assert.Equal((4, 4, 6), (report.Thought, (int)report.Spent, report.Deferred));
        Assert.All(new[] { "a4", "a5", "a6", "a7", "a8", "a9" }, id => Assert.True(runtime.Due(id) <= 1));
        Assert.All(new[] { "a0", "a1", "a2", "a3" }, id => Assert.Equal(2, runtime.Due(id)));
    }

    [Fact]
    public void UnderOverloadEveryAgentStillThinksAndTheLongestWaitIsBounded()
    {
        var runtime = new AiRuntime(Fixture.LoadMini(Rest(), every: 2), 1);
        for (var i = 0; i < 100; i++) runtime.Add("a" + i.ToString("000"), "t", 0);
        // 100 agents of cost 1, each wanting a think every 2 ticks, with room for 30 a tick: 50 wanted per tick, so it cannot keep up with 2 ticks, and settles near 100/30.
        var thinks = new Dictionary<string, int>();
        long longest = 0;
        for (var tick = 1; tick <= 60; tick++)
        {
            var report = runtime.Step(tick, 30);
            foreach (var intent in report.Intents) thinks[intent.Agent] = thinks.GetValueOrDefault(intent.Agent) + 1;
            longest = Math.Max(longest, report.LongestWait);
        }
        Assert.Equal(100, thinks.Count);
        Assert.All(thinks.Values, count => Assert.InRange(count, 10, 20));
        Assert.InRange(longest, 1, 8);
    }

    [Fact]
    public void AgentsAddedTogetherAreSpreadOverTheirThinkIntervalByASeededOffset()
    {
        List<long> Dues(ulong seed)
        {
            var runtime = new AiRuntime(Fixture.LoadMini(Rest(), every: 10), seed);
            for (var i = 0; i < 100; i++) runtime.Add("a" + i.ToString("000"), "t", 5);
            return runtime.Agents.Select(runtime.Due).ToList();
        }
        var one = Dues(1);
        Assert.All(one, due => Assert.InRange(due, 5, 14));
        Assert.True(one.Distinct().Count() >= 9);
        Assert.Equal(one, Dues(1));
        Assert.NotEqual(one, Dues(2));
    }

    [Fact]
    public void TheOrderAgentsWereAddedInChangesNothingAboutWhatTheyDecideOrTheStateTheyEndIn()
    {
        string Run(IEnumerable<string> order)
        {
            var runtime = new AiRuntime(Fixture.Load(), 7);
            foreach (var id in order) runtime.Add(id, id.StartsWith('s') ? "scout" : "worker", 0);
            var lines = new List<string>();
            for (var tick = 1; tick <= 60; tick++)
            {
                if (tick % 7 == 0) foreach (var id in runtime.Agents) runtime.SetKey(id, "hunger", FactValue.From((long)(tick * 3 % 100)));
                lines.Add(Fixture.Lines(runtime.Step(tick, 10_000)));
            }
            return string.Join('\n', lines) + "\n" + AiSave.StateDigest(runtime);
        }
        var ids = Enumerable.Range(0, 12).Select(i => (i % 2 == 0 ? "s" : "w") + i).ToList();
        var one = Run(ids);
        Assert.Equal(one, Run(Enumerable.Reverse(ids)));
        Assert.Equal(one, Run(ids.OrderBy(id => id.GetHashCode() ^ 0x5bd1e995)));
    }

    [Fact]
    public void TicksNeverRunBackwardsAndAgentsAreAddedAndRemovedByName()
    {
        var runtime = new AiRuntime(Fixture.LoadMini(Rest()), 1);
        runtime.Add("a", "t", 0);
        runtime.Step(10, 100);
        Assert.Equal(AiCodes.TickOrder, Fixture.Refusal(() => runtime.Step(9, 100)));
        Assert.Equal(AiCodes.TickOrder, Fixture.Refusal(() => runtime.Add("b", "t", 5)));
        Assert.Equal(AiCodes.TickOrder, Fixture.Refusal(() => runtime.Step(AiRuntime.TickLimit + 1, 100)));
        Assert.Equal(AiCodes.DuplicateAgent, Fixture.Refusal(() => runtime.Add("a", "t", 10)));
        Assert.Equal(AiCodes.UnknownBehavior, Fixture.Refusal(() => runtime.Add("c", "nothing", 10)));
        Assert.Equal(AiCodes.Format, Fixture.Refusal(() => runtime.Add("Bad Name", "t", 10)));
        Assert.Equal(AiCodes.Format, Fixture.Refusal(() => runtime.Add("", "t", 10)));
        runtime.Add("b", "t", 10);
        runtime.Remove("a");
        Assert.Equal(["b"], runtime.Agents);
        Assert.Equal(AiCodes.UnknownAgent, Fixture.Refusal(() => runtime.Remove("a")));
        Assert.Equal(AiCodes.UnknownAgent, Fixture.Refusal(() => runtime.Due("a")));
        Assert.Equal("t", runtime.BehaviorOf("b"));
        Assert.Equal(["b"], runtime.Step(11, 100).Intents.Select(i => i.Agent).Distinct());   // a removed agent is not thought
    }

    [Fact]
    public void TheSameDocumentSeedAndInputsGiveTheSameIntentsEveryTimeAndOtherSeedsDifferWhereTheyRoll()
    {
        string Run(ulong seed)
        {
            var runtime = new AiRuntime(Fixture.Load(), seed);
            for (var i = 0; i < 6; i++) runtime.Add("s" + i, "scout", 0);
            var lines = new List<string>();
            for (var tick = 1; tick <= 40; tick++)
            {
                if (tick == 4) foreach (var id in runtime.Agents) runtime.SetKey(id, "ready", FactValue.From(true));
                if (tick == 9) foreach (var id in runtime.Agents) runtime.SetKey(id, "ready", FactValue.From(false));
                lines.Add(Fixture.Lines(runtime.Step(tick, 10_000)));
            }
            return string.Join('\n', lines);
        }
        var one = Run(1);
        Assert.Equal(one, Run(1));
        Assert.NotEqual(one, Run(2));
        Assert.Contains("heads", one);
        Assert.Contains("tails", one);
    }
}