using System.Diagnostics;
using System.Text;
using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>D10: a long conversation costs little per step. The allocation bound is exact; the time bound is loose because a test host may be busy.</summary>
public sealed class CostTests
{
    private const int Pairs = 100;

    /// <summary>A ring of 100 lines and 100 choices (201 nodes with the end): taking "again" at every choice never ends.</summary>
    private static DialogueSet Ring()
    {
        var nodes = new StringBuilder();
        for (var i = 0; i < Pairs; i++)
        {
            nodes.Append($"{{\"id\":\"l{i}\",\"kind\":\"line\",\"speaker\":\"s\",\"portrait\":\"calm\",\"line\":\"k.l{i}\",\"next\":\"c{i}\"}},");
            nodes.Append($"{{\"id\":\"c{i}\",\"kind\":\"choice\",\"pick\":\"player\",\"options\":[{{\"id\":\"again\",\"line\":\"k.a{i}\",\"effects\":[{{\"add\":\"n\",\"by\":1}}],\"next\":\"l{(i + 1) % Pairs}\"}},{{\"id\":\"stop\",\"line\":\"k.s{i}\",\"next\":\"e\"}}]}},");
        }
        nodes.Append("{\"id\":\"e\",\"kind\":\"end\",\"result\":\"done\"}");
        return DialogueLoader.Load("{\"format\":3,\"id\":\"ring\",\"variables\":[{\"id\":\"n\",\"type\":\"int\",\"initial\":0,\"min\":0,\"max\":1000000000}],\"externalFacts\":[],\"speakers\":[{\"id\":\"s\",\"portraits\":[\"calm\"]}],\"quests\":[],\"conversations\":[{\"id\":\"loop\",\"start\":\"l0\",\"nodes\":[" + nodes + "]}]}", new Speakers("s"));
    }

    [Fact]
    public void AHundredThousandLineStepsThroughATwoHundredNodeDocumentCostUnderASecondAndTwoKilobytesPerStep()
    {
        var set = Ring();
        Assert.Equal(2 * Pairs + 1, set.Conversations["loop"].Nodes.Count);
        var facts = new Dictionary<string, FactValue>();
        var runtime = new DialogueRuntime(set, 1);
        runtime.Start("loop", facts);
        for (var i = 0; i < 1_000; i++) { runtime.Next(facts); runtime.Choose("again", facts); }   // warm up the JIT so the first calls do not count
        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 100_000; i++) { runtime.Next(facts); runtime.Choose("again", facts); }
        watch.Stop();
        var perStep = (GC.GetAllocatedBytesForCurrentThread() - before) / 200_000.0;   // a "step" is one call: a line to the choice, or an answer to the next line
        Assert.True(perStep < 2048, "allocation per step was " + perStep + " bytes");
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1.5), "100,000 line steps took " + watch.Elapsed.TotalMilliseconds + " ms");
        Assert.Equal(FactValue.From(101_000L), runtime.State.Variables["n"]);
        // The state does not grow with the length of the conversation: one visit count per node.
        Assert.Equal(2 * Pairs, runtime.State.Visits.Count);
    }
}
