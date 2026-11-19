using System.Diagnostics;
using System.Text;
using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>D10b: a document of ten thousand nodes loads in well under a second, a step costs the same however large the document is, and a long history adds nothing to the state or the save. The bounds are loose; the measured numbers are in the plan.</summary>
public sealed class ScaleTests
{
    private const int Conversations = 20;
    private const int Pairs = 255;

    private static string Document(int conversations)
    {
        var all = new StringBuilder();
        for (var k = 0; k < conversations; k++)
        {
            var nodes = new StringBuilder();
            for (var i = 0; i < Pairs; i++)
            {
                nodes.Append($"{{\"id\":\"l{i}\",\"kind\":\"line\",\"speaker\":\"s\",\"portrait\":\"calm\",\"line\":\"k.c{k}.l{i}\",\"next\":\"c{i}\"}},");
                nodes.Append($"{{\"id\":\"c{i}\",\"kind\":\"choice\",\"pick\":\"player\",\"options\":[{{\"id\":\"again\",\"line\":\"k.c{k}.a{i}\",\"effects\":[{{\"add\":\"n\",\"by\":1}}],\"next\":\"l{(i + 1) % Pairs}\"}},{{\"id\":\"stop\",\"line\":\"k.c{k}.s{i}\",\"next\":\"e\"}}]}},");
            }
            nodes.Append("{\"id\":\"e\",\"kind\":\"end\",\"result\":\"done\"}");
            all.Append((k > 0 ? "," : "") + "{\"id\":\"c" + k + "\",\"start\":\"l0\",\"nodes\":[" + nodes + "]}");
        }
        return "{\"format\":3,\"id\":\"ring\",\"variables\":[{\"id\":\"n\",\"type\":\"int\",\"initial\":0,\"min\":0,\"max\":1000000000}],\"externalFacts\":[],\"speakers\":[{\"id\":\"s\",\"portraits\":[\"calm\"]}],\"quests\":[],\"conversations\":[" + all + "]}";
    }

    private static void Steps(DialogueRuntime runtime, IReadOnlyDictionary<string, FactValue> facts, int pairs)
    {
        for (var i = 0; i < pairs; i++) { runtime.Next(facts); runtime.Choose("again", facts); }
    }

    [Fact]
    public void ADocumentOfTenThousandNodesLoadsAndEveryConversationInItPlays()
    {
        var text = Document(Conversations);
        var watch = Stopwatch.StartNew();
        var set = DialogueLoader.Load(text, new Speakers("s"));
        watch.Stop();
        Assert.Equal(Conversations * (2 * Pairs + 1), set.Conversations.Values.Sum(c => c.Nodes.Count));
        Assert.True(set.Conversations.Values.Sum(c => c.Nodes.Count) > 10_000);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), "loading took " + watch.Elapsed.TotalMilliseconds + " ms");
        var facts = new Dictionary<string, FactValue>();
        foreach (var conversation in set.Conversations.Keys)
        {
            var runtime = new DialogueRuntime(set, 1);
            Assert.Equal(StepKind.Line, runtime.Start(conversation, facts).Kind);
            Assert.Equal(StepKind.Choices, runtime.Next(facts).Kind);
            Assert.Equal(StepKind.Ended, runtime.Choose("stop", facts).Kind);
        }
    }

    [Fact]
    public void AStepCostsTheSameInALargeDocumentAsInASmallOneAndAHistoryAddsNothingToTheStateOrTheSave()
    {
        var small = DialogueLoader.Load(Document(1), new Speakers("s"));
        var large = DialogueLoader.Load(Document(Conversations), new Speakers("s"));
        var facts = new Dictionary<string, FactValue>();
        long Allocated(DialogueSet set)
        {
            var runtime = new DialogueRuntime(set, 1);
            runtime.Start("c0", facts);
            Steps(runtime, facts, 2_000);                                       // warm up
            var before = GC.GetAllocatedBytesForCurrentThread();
            Steps(runtime, facts, 20_000);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        var smallBytes = Allocated(small);
        var largeBytes = Allocated(large);
        Assert.True(Math.Abs(largeBytes - smallBytes) < smallBytes / 100, "allocation per 20,000 answers: " + smallBytes + " in a small document, " + largeBytes + " in a large one");   // the cost of a step does not depend on the size of the document

        var session = new DialogueRuntime(large, 1);
        session.Start("c0", facts);
        Steps(session, facts, 5_000);
        var early = DialogueSave.Write(session).Length;
        Steps(session, facts, 195_000);
        var late = DialogueSave.Write(session).Length;
        Assert.True(late - early <= 2 * session.State.Visits.Count, "the save grew from " + early + " to " + late + " bytes over 195,000 more answers (more than the extra digits of the visit counts)");
        Assert.Equal(2 * Pairs, session.State.Visits.Count);                      // one count per node ever visited, however often
        var back = DialogueSave.Open(large, DialogueSave.Write(session));
        Assert.Equal(DialogueSave.StateDigest(session.State), DialogueSave.StateDigest(back.State));
    }
}
