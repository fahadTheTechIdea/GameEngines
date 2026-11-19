using AIEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace AIEngine.Tests;

/// <summary>A3: the bound worked out from the document is exact for the worst case of each kind of node, so the budget a host sets means what it says.</summary>
public sealed class NodeCostTests
{
    private static long WorstCase(string root, Action<AiRuntime>? tell = null)
    {
        var set = Fixture.LoadMini(root);
        var runtime = new AiRuntime(set, 1);
        runtime.Add("a", "t", 0);
        tell?.Invoke(runtime);
        var spent = runtime.Step(1, set.MaxCost).Spent;
        Assert.True(spent <= set.MaxCost);
        return spent == set.MaxCost ? spent : -spent;   // negative: below the bound
    }

    private static void Sixteen(AiRuntime runtime) { for (var i = 0; i < 16; i++) Fixture.Tell(runtime, "a", "u" + i.ToString("00"), hostile: true); }

    private const string Rest = "{ \"id\": \"r\", \"kind\": \"act\", \"intent\": \"rest\", \"args\": { } }";

    [Fact]
    public void ACountAndASelectCostOnePlusEveryPerceptOfTheirKindTheAgentHolds()
    {
        var count = "{ \"id\": \"c\", \"kind\": \"count\", \"from\": \"unit\", \"into\": \"foes\" }";
        Assert.Equal(17, Fixture.LoadMini(count).MaxCost);
        Assert.Equal(17, WorstCase(count, Sixteen));
        var select = "{ \"id\": \"s\", \"kind\": \"select\", \"from\": \"unit\", \"into\": \"target\", \"pick\": { \"nth\": 0 } }";
        Assert.Equal(17, Fixture.LoadMini(select).MaxCost);
        Assert.Equal(17, WorstCase(select, Sixteen));
        // With nothing known the same node costs one.
        var set = Fixture.LoadMini(count);
        var empty = new AiRuntime(set, 1);
        empty.Add("a", "t", 0);
        Assert.Equal(1, empty.Step(1, set.MaxCost).Spent);
    }

    [Fact]
    public void ALoopCostsItsScanPlusLimitTimesItsBodyAndItsWorstCaseIsReached()
    {
        var loop = "{ \"id\": \"e\", \"kind\": \"forEach\", \"from\": \"unit\", \"limit\": 4, \"child\": { \"id\": \"b\", \"kind\": \"condition\", \"when\": { \"fact\": \"tick\", \"op\": \"gte\", \"value\": 0 } } }";
        Assert.Equal(21, Fixture.LoadMini(loop).MaxCost);          // 1 + 16 + 4 x 1
        Assert.Equal(21, WorstCase(loop, Sixteen));
    }

    [Fact]
    public void ASequenceCostsOnePlusItsChildrenAndAUtilityNodeScoresEveryOptionBeforeRunningOne()
    {
        var sequence = "{ \"id\": \"s\", \"kind\": \"sequence\", \"resume\": false, \"children\": [ " + Rest + ", { \"id\": \"r2\", \"kind\": \"act\", \"intent\": \"rest\", \"args\": { } } ] }";
        Assert.Equal(3, Fixture.LoadMini(sequence).MaxCost);
        Assert.Equal(3, WorstCase(sequence));
        var utility = "{ \"id\": \"u\", \"kind\": \"utility\", \"momentum\": 0, \"options\": [ "
            + "{ \"id\": \"a\", \"weight\": 1000, \"considerations\": [ { \"input\": 900, \"min\": 0, \"max\": 1000, \"curve\": { \"kind\": \"linear\" } }, { \"input\": 900, \"min\": 0, \"max\": 1000, \"curve\": { \"kind\": \"linear\" } } ], \"child\": " + Rest + " }, "
            + "{ \"id\": \"b\", \"weight\": 1000, \"considerations\": [ { \"input\": 100, \"min\": 0, \"max\": 1000, \"curve\": { \"kind\": \"linear\" } } ], \"child\": { \"id\": \"r3\", \"kind\": \"act\", \"intent\": \"rest\", \"args\": { } } } ] }";
        Assert.Equal(1 + (1 + 2) + (1 + 1) + 1, Fixture.LoadMini(utility).MaxCost);   // the node, a (1 + 2 considerations), b (1 + 1), then one child
        Assert.Equal(7, WorstCase(utility));
    }
}
