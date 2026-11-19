using System.Diagnostics;
using System.Globalization;
using System.Text;
using Xunit;
using static EngineCommon.Graph.Tests.Fx;

namespace EngineCommon.Graph.Tests;

/// <summary>
/// Cost at the sizes the engines will use: 10,000 nodes x 20 edges (traversal, shortest paths, degrees), 200 markets x 400 routes (min-cost flow with a super source and sink), 1,000 actors x 20 edges (label
/// propagation and trust iteration). Each is the median of 15 repeats after a warm-up; a run writes its numbers to <c>graph-scale-results.csv</c> beside the test assembly. The budgets are about ten times the medians
/// recorded in docs/ENGINE_GRAPH.md (measured in Release), so they fail on a change of order of magnitude and not on a loaded machine; in Debug they are not checked.
/// </summary>
public sealed class ScaleTests
{
    private const int Repeats = 15;
    private const int BigNodes = 10_000;
    private const int BigDegree = 20;

    private static (double Median, double Min, double Max) Measure(Action work)
    {
        work();
        var times = new double[Repeats];
        for (var i = 0; i < Repeats; i++)
        {
            var clock = Stopwatch.StartNew();
            work();
            times[i] = clock.Elapsed.TotalMilliseconds;
        }
        Array.Sort(times);
        return (times[Repeats / 2], times[0], times[^1]);
    }

    private static CsrGraph Big(ulong seed, long minWeight, long maxWeight)
    {
        var rng = new Rng(seed);
        return Make(BigNodes, RandomEdges(rng, BigNodes, BigNodes * BigDegree, minWeight, maxWeight, 0));
    }

    private static CsrGraph Markets(out int source, out int sink)
    {
        var rng = new Rng(99);
        var edges = new List<E>();
        for (var i = 0; i < 400; i++)
        {
            var a = rng.Below(200);
            var b = (a + 1 + rng.Below(199)) % 200;
            edges.Add(C(a, b, rng.Between(1, 100), rng.Between(1, 50)));
        }
        source = 200;
        sink = 201;
        for (var i = 0; i < 40; i++) { edges.Add(C(200, i * 5, 150, 0)); edges.Add(C(i * 5 + 2, 201, 150, 0)); }
        return Make(202, edges);
    }

    private static CsrGraph Actors()
    {
        var rng = new Rng(5);
        var edges = new List<E>();
        for (var a = 0; a < 1000; a++)
            for (var k = 0; k < 20; k++)
            {
                var inside = k < 15;
                var to = inside ? a / 100 * 100 + rng.Below(100) : rng.Below(1000);
                edges.Add(W(a, to, rng.Between(1, 10)));
            }
        return Make(1000, edges);
    }

    [Fact]
    public void TheGraphLibraryStaysCheapAtGameSizes()
    {
        var rows = new List<(string Name, (double Median, double Min, double Max) Time, string Unit, int Repeats, double Budget)>();
        void Row(string name, double budgetMs, Action work) => rows.Add((name, Measure(work), "ms", Repeats, budgetMs));

        var scratch = new GraphScratch();
        Row("build 10,000 nodes x 20 edges", 1000, () => Big(1, 1, 20));
        var g = Big(1, 1, 20);
        var tree = new ShortestPaths();
        Row("dijkstra full tree", 150, () => Dijkstra.Run(g, 0, -1, scratch, tree));
        var bf = new ShortestPaths();
        Row("bellman-ford full tree", 100, () => BellmanFord.Run(g, 0, scratch, bf));
        var bfs = new BfsResult();
        Row("bfs full reach", 20, () => Bfs.Run(g, [0], BigNodes, bfs));
        var comps = new ComponentsResult();
        Row("connected components", 20, () => Components.Run(g, comps));
        var degrees = new DegreeTable();
        Row("degrees and top 10", 20, () => { DegreeTable.Run(g, degrees); degrees.Top(DegreeMeasure.InWeight, 10); });
        var market = Markets(out var s, out var t);
        FlowResult? last = null;
        Row("min-cost flow 200 markets x 400 routes", 50, () => last = MinCostFlow.Run(market, s, t, GraphLimits.NoFlowLimit, 100_000, scratch));
        Assert.True(last!.Flow > 0);
        var actors = Actors();
        var communities = new CommunitiesResult();
        Row("label propagation 1,000 actors x 20 edges", 30, () => LabelPropagation.Run(actors, [], 30, scratch, communities));
        var trust = new TrustResult();
        Row("trust iteration 1,000 actors x 20 edges", 300, () => TrustIteration.Run(actors, [], 150_000_000, 50, 20_000, scratch, trust));

        var before = GC.GetAllocatedBytesForCurrentThread();
        Dijkstra.Run(g, 0, -1, scratch, tree);
        var dijkstraBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        MinCostFlow.Run(market, s, t, GraphLimits.NoFlowLimit, 100_000, scratch);
        var flowBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        TrustIteration.Run(actors, [], 150_000_000, 50, 20_000, scratch, trust);
        var trustBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        LabelPropagation.Run(actors, [], 30, scratch, communities);
        var labelBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        var csv = new StringBuilder("measurement,median,min,max,unit,repeats\n");
        foreach (var row in rows)
            csv.Append(string.Join(',', row.Name, F(row.Time.Median), F(row.Time.Min), F(row.Time.Max), row.Unit, row.Repeats.ToString(CultureInfo.InvariantCulture))).Append('\n');
        foreach (var (name, bytes) in new[] { ("allocation of a reused dijkstra run", dijkstraBytes), ("allocation of a min-cost flow run", flowBytes), ("allocation of a trust iteration run", trustBytes), ("allocation of a label propagation run", labelBytes) })
            csv.Append(name).Append(',').Append(bytes.ToString(CultureInfo.InvariantCulture)).Append(',').Append(bytes.ToString(CultureInfo.InvariantCulture)).Append(',').Append(bytes.ToString(CultureInfo.InvariantCulture)).Append(",bytes,1\n");
        csv.Append("flow result,").Append(last!.Flow.ToString(CultureInfo.InvariantCulture)).Append(" units at cost ").Append(last.Cost.ToString(CultureInfo.InvariantCulture)).Append(" in ").Append(last.Augmentations.Count.ToString(CultureInfo.InvariantCulture)).Append(" augmentations,,,,\n");
        csv.Append("label propagation result,").Append(communities.CommunityCount.ToString(CultureInfo.InvariantCulture)).Append(" communities in ").Append(communities.Rounds.ToString(CultureInfo.InvariantCulture)).Append(" rounds,,,,\n");
        csv.Append("trust result,converged ").Append(trust.Converged ? "yes" : "no").Append(" after ").Append(trust.Iterations.ToString(CultureInfo.InvariantCulture)).Append(" iterations residual ").Append(trust.Residual.ToString(CultureInfo.InvariantCulture)).Append(",,,,\n");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "graph-scale-results.csv"), csv.ToString());

        Assert.Equal(0, dijkstraBytes);
        Assert.Equal(0, trustBytes);
        Assert.True(labelBytes < 4096, "label propagation allocated " + labelBytes);
#if !DEBUG
        foreach (var row in rows) Assert.True(row.Time.Median < row.Budget, row.Name + " took " + row.Time.Median + " ms");
#endif
    }

    private static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

}
