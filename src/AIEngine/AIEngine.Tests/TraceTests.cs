using AIEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace AIEngine.Tests;

/// <summary>The explain facility: why an agent did what it did, the same each time, bounded, and never changing a decision.</summary>
public sealed class TraceTests
{
    private const long Budget = 1_000_000;

    private static string Act(string id, string what) => "{ \"id\": \"" + id + "\", \"kind\": \"act\", \"intent\": \"order\", \"args\": { \"what\": \"" + what + "\", \"amount\": 1 } }";

    /// <summary>A tree that uses most node kinds: it counts, writes a key, scores two options (one with momentum), and acts.</summary>
    private static readonly string Root = "{ \"id\": \"plan\", \"kind\": \"sequence\", \"resume\": false, \"children\": [ "
        + "{ \"id\": \"how-many\", \"kind\": \"count\", \"from\": \"stock\", \"into\": \"foes\", \"of\": \"depot-a\" }, "
        + "{ \"id\": \"note\", \"kind\": \"let\", \"key\": \"small\", \"value\": 2 }, "
        + "{ \"id\": \"enough\", \"kind\": \"condition\", \"when\": { \"fact\": \"key.foes\", \"op\": \"gt\", \"value\": 0 } }, "
        + "{ \"id\": \"choose\", \"kind\": \"utility\", \"momentum\": 100, \"options\": [ "
        + "{ \"id\": \"calm\", \"weight\": 1000, \"considerations\": [ { \"input\": { \"key\": \"hunger\" }, \"min\": 0, \"max\": 100, \"curve\": { \"kind\": \"linear\" } } ], \"child\": " + Act("go-calm", "calm") + " }, "
        + "{ \"id\": \"rush\", \"weight\": 1000, \"considerations\": [ { \"input\": 450, \"min\": 0, \"max\": 1000, \"curve\": { \"kind\": \"linear\" } } ], \"child\": " + Act("go-rush", "rush") + " } ] } ] }";

    private static AiRuntime Agent(string root = "", ulong seed = 1)
    {
        var runtime = new AiRuntime(Fixture.LoadMini(root.Length == 0 ? Root : root), seed);
        runtime.Add("a", "t", 0);
        runtime.SetKey("a", "hunger", FactValue.From(40L));
        foreach (var (id, owner) in new[] { ("s1", "depot-a"), ("s2", "depot-a"), ("s3", "depot-b") })
            runtime.Perceive("a", "stock", id, owner, new Dictionary<string, FactValue> { ["held"] = FactValue.From(3L) }, 0);
        return runtime;
    }

    [Fact]
    public void ATraceNamesEveryNodeThinkRanInOrderWithWhatItReportedAndDecided()
    {
        var runtime = Agent();
        runtime.Trace("a", true);
        var report = runtime.Step(1, Budget);
        var trace = runtime.Explain("a")!;
        Assert.Equal("agent a behaviour t tick 1 spent " + report.Spent + " steps 6\n"
            + "plan [sequence] success\n"
            + "  how-many [count] success - foes = 2\n"
            + "  note [let] success - small = 2\n"
            + "  enough [condition] success - holds\n"
            + "  choose [utility] success - calm=400, rush=450; chose rush\n"
            + "    go-rush [act] success - raises order(amount=1, what='rush')\n", trace.ToText());
        Assert.Equal(report.Spent, trace.Spent);
        Assert.False(trace.Truncated);
    }

    [Fact]
    public void AUtilityTraceShowsEveryScoreAndSaysWhenMomentumKeptTheChoice()
    {
        var runtime = Agent();
        runtime.Trace("a", true);
        runtime.Step(1, Budget);                               // rush: 450 against 400
        runtime.SetKey("a", "hunger", FactValue.From(46L));    // calm now 460, rush 450 + momentum 100 = 550
        runtime.Step(2, Budget);
        var line = runtime.Explain("a")!.Steps.Single(s => s.Node == "choose").Detail;
        Assert.Equal("calm=460, rush=450; chose rush (kept, momentum 100)", line);
        runtime.SetKey("a", "hunger", FactValue.From(60L));    // calm 600 beats rush 450 + 100
        runtime.Step(3, Budget);
        Assert.Equal("calm=600, rush=450; chose calm", runtime.Explain("a")!.Steps.Single(s => s.Node == "choose").Detail);
    }

    [Fact]
    public void ANodeThatFailsOrIsNotReachedIsShownAsItWas()
    {
        var runtime = Agent();
        runtime.Perceive("a", "stock", "s1", "depot-a", new Dictionary<string, FactValue> { ["held"] = FactValue.From(3L) }, 0);
        Assert.True(runtime.Lose("a", "stock", "s1"));
        Assert.True(runtime.Lose("a", "stock", "s2"));         // no stock is held by depot-a now
        runtime.Trace("a", true);
        runtime.Step(1, Budget);
        var text = runtime.Explain("a")!.ToText();
        Assert.Contains("enough [condition] failure - does not hold", text);
        Assert.Contains("plan [sequence] failure", text);
        Assert.DoesNotContain("choose", text);                 // the sequence stopped before it
        Assert.DoesNotContain("go-rush", text);
    }

    [Fact]
    public void AnAgentThatIsNotExplainedHasNoTraceAndStoppingDropsIt()
    {
        var runtime = Agent();
        runtime.Step(1, Budget);
        Assert.Null(runtime.Explain("a"));
        runtime.Trace("a", true);
        Assert.Null(runtime.Explain("a"));                      // it has not thought since
        runtime.Step(2, Budget);
        Assert.NotNull(runtime.Explain("a"));
        runtime.Trace("a", false);
        Assert.Null(runtime.Explain("a"));
        runtime.Step(3, Budget);
        Assert.Null(runtime.Explain("a"));
        Assert.Equal(AiCodes.UnknownAgent, Fixture.Refusal(() => runtime.Trace("nobody", true)));
        Assert.Equal(AiCodes.UnknownAgent, Fixture.Refusal(() => runtime.Explain("nobody")));
    }

    [Fact]
    public void ExplainingChangesNeitherTheDecisionsNorTheCostNorTheSavedState()
    {
        string Run(bool explain)
        {
            var runtime = Agent(seed: 9);
            if (explain) runtime.Trace("a", true);
            var lines = new List<string>();
            for (var tick = 1; tick <= 30; tick++)
            {
                runtime.SetKey("a", "hunger", FactValue.From(tick * 7L % 101));
                var report = runtime.Step(tick, Budget);
                lines.Add(Fixture.Lines(report) + "|" + report.Spent);
            }
            return string.Join('\n', lines) + "\n" + Convert.ToHexString(AiSave.Write(runtime));
        }
        Assert.Equal(Run(false), Run(true));
    }

    [Fact]
    public void TheSameStateGivesTheSameTraceText()
    {
        string Text()
        {
            var runtime = Agent(seed: 4);
            runtime.Trace("a", true);
            runtime.Step(1, Budget);
            return runtime.Explain("a")!.ToText();
        }
        Assert.Equal(Text(), Text());
    }

    [Fact]
    public void ALongThinkIsCutAtTheLimitAndSaysSo()
    {
        // 1000 stocks, each running a sequence of four nodes: 1 + 1000 x 5 steps, far over the limit.
        var body = "{ \"id\": \"each\", \"kind\": \"forEach\", \"from\": \"stock\", \"limit\": 1000, \"child\": { \"id\": \"inner\", \"kind\": \"sequence\", \"resume\": false, \"children\": [ "
            + "{ \"id\": \"one\", \"kind\": \"let\", \"key\": \"small\", \"value\": 1 }, { \"id\": \"two\", \"kind\": \"let\", \"key\": \"small\", \"value\": 2 }, { \"id\": \"three\", \"kind\": \"let\", \"key\": \"small\", \"value\": 3 }, { \"id\": \"four\", \"kind\": \"let\", \"key\": \"small\", \"value\": 3 } ] } }";
        var document = Fixture.Mini(body).Replace("\"capacity\": 8, \"ownerCapacity\": 4,", "\"capacity\": 1000, \"ownerCapacity\": 4,", StringComparison.Ordinal);
        var runtime = new AiRuntime(AiLoader.Load(document), 1);
        runtime.Add("a", "t", 0);
        for (var i = 0; i < 1000; i++) runtime.Perceive("a", "stock", "s" + i.ToString("0000"), "d" + (i / 4).ToString("000"), new Dictionary<string, FactValue> { ["held"] = FactValue.From(1L) }, 0);
        runtime.Trace("a", true);
        runtime.Step(1, 1_000_000);
        var trace = runtime.Explain("a")!;
        Assert.True(trace.Truncated);
        Assert.Equal(ThinkTrace.MaxSteps, trace.Steps.Count);
        Assert.Equal("each", trace.Steps[0].Node);
        Assert.EndsWith("the think ran more nodes than the trace holds (4096)\n", trace.ToText());
        Assert.All(trace.Steps, s => Assert.True(s.Detail.Length <= ThinkTrace.MaxDetail));
    }

    [Fact]
    public void ADetailLongerThanTheLimitIsCutAndEndsWithDots()
    {
        var options = string.Join(", ", Enumerable.Range(0, 9).Select(i =>
            "{ \"id\": \"option-with-a-rather-long-name-" + i + "\", \"weight\": 1000, \"considerations\": [ { \"input\": " + (100 + i) + ", \"min\": 0, \"max\": 1000, \"curve\": { \"kind\": \"linear\" } } ], \"child\": " + Act("act-" + i, "x" + i) + " }"));
        var runtime = Agent("{ \"id\": \"u\", \"kind\": \"utility\", \"momentum\": 0, \"options\": [ " + options + " ] }");
        runtime.Trace("a", true);
        runtime.Step(1, Budget);
        var detail = runtime.Explain("a")!.Steps.Single(s => s.Node == "u").Detail;
        Assert.Equal(ThinkTrace.MaxDetail, detail.Length);
        Assert.EndsWith("...", detail);
        Assert.StartsWith("option-with-a-rather-long-name-0=100, option-with-a-rather-long-name-1=101", detail);
    }
}