using Xunit;
using static EngineCommon.Graph.Tests.Fx;

namespace EngineCommon.Graph.Tests;

public sealed class ShortestPathTests
{
    private static CsrGraph Classic() => Make(5, W(0, 1, 4), W(0, 2, 1), W(2, 1, 2), W(1, 3, 1), W(2, 3, 5));

    [Fact]
    public void DijkstraMatchesAHandWorkedGraph()
    {
        var g = Classic();
        var scratch = new GraphScratch();
        var r = Dijkstra.Run(g, 0, -1, scratch);
        Assert.Equal("", r.Code);
        Assert.Equal(new long[] { 0, 3, 1, 4, ShortestPaths.Unreached }, r.Distances.ToArray());
        Assert.Equal(new[] { 0, 2, 1, 3 }, r.SettleOrder.ToArray());
        Assert.Equal(4, r.SettledCount);
        Assert.False(r.IsFinal(4));
        var path = r.PathTo(g, 3);
        Assert.True(path.Found);
        Assert.Equal(4, path.Cost);
        Assert.Equal(new[] { 0, 2, 1, 3 }, path.Nodes);
        Assert.Equal(3, path.Edges.Length);
        Assert.Equal("path of 3 edges, cost 4: 0 > 2 > 1 > 3", path.Explain());
        Assert.Equal(new PathResult(true, 0, [0], []).Nodes, r.PathTo(g, 0).Nodes);
        Assert.False(r.PathTo(g, 4).Found);
        Assert.Equal("no path", r.PathTo(g, 4).Explain());
        Assert.False(r.PathTo(g, 9).Found);
        Assert.False(r.PathTo(g, -1).Found);
        Assert.Equal("dijkstra from 0: 4 of 5 nodes final", r.Explain());
    }

    [Fact]
    public void DijkstraStopsWhenTheTargetIsSettledAndOnlySettledNodesAreFinal()
    {
        var g = Classic();
        var r = Dijkstra.Run(g, 0, 2, new GraphScratch());
        Assert.Equal(new[] { 0, 2 }, r.SettleOrder.ToArray());
        Assert.True(r.IsFinal(2));
        Assert.False(r.IsFinal(1));
        Assert.False(r.PathTo(g, 1).Found);
        Assert.Equal(1, r.Distance(2));
    }

    [Fact]
    public void TheTieBreakIsTheLowestNodeIndexAndTheFirstEdgeToReachTheFinalCost()
    {
        // Two equal routes 0>1>3 and 0>2>3: node 1 pops before node 2, so it reaches 3 first and keeps it.
        var diamond = Make(4, W(0, 1, 1), W(0, 2, 1), W(1, 3, 1), W(2, 3, 1));
        var path = Dijkstra.Run(diamond, 0, -1, new GraphScratch()).PathTo(diamond, 3);
        Assert.Equal(new[] { 0, 1, 3 }, path.Nodes);
        // The same edges added in another order are the same graph, so the same branch.
        var mirrored = Make(4, W(0, 2, 1), W(0, 1, 1), W(2, 3, 1), W(1, 3, 1));
        Assert.Equal(new[] { 0, 1, 3 }, Dijkstra.Run(mirrored, 0, -1, new GraphScratch()).PathTo(mirrored, 3).Nodes);
        // Parallel edges of equal cost: the lower capacity-sorted one (first in CSR order) is the parent.
        var parallel = Make(2, new E(0, 1, 5, 9), new E(0, 1, 5, 2));
        var tree = Dijkstra.Run(parallel, 0, -1, new GraphScratch());
        Assert.Equal(0, tree.ParentEdge(1));
        Assert.Equal(2, parallel.Capacity(0));
        // Equal-cost zero-weight alternatives: the settle order is (cost, node).
        var zero = Make(4, W(0, 3, 0), W(0, 1, 0), W(3, 2, 0), W(1, 2, 0));
        Assert.Equal(new[] { 0, 1, 2, 3 }, Dijkstra.Run(zero, 0, -1, new GraphScratch()).SettleOrder.ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, Dijkstra.Run(zero, 0, -1, new GraphScratch()).PathTo(zero, 2).Nodes);
    }

    [Fact]
    public void ShortestPathTreesDoNotDependOnTheOrderEdgesWereAdded()
    {
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var rng = new Rng(seed);
            var edges = RandomEdges(rng, 12, 40, 0, 4, 0);
            var a = Make(12, edges);
            var b = Make(12, Shuffled(rng, edges));
            var ra = Dijkstra.Run(a, 0, -1, new GraphScratch());
            var rb = Dijkstra.Run(b, 0, -1, new GraphScratch());
            Assert.Equal(ra.Distances.ToArray(), rb.Distances.ToArray());
            Assert.Equal(ra.SettleOrder.ToArray(), rb.SettleOrder.ToArray());
            for (var v = 0; v < 12; v++) Assert.Equal(ra.ParentEdge(v), rb.ParentEdge(v));
        }
    }

    [Fact]
    public void DijkstraAndBellmanFordAgreeWithFloydWarshallOnRandomGraphs()
    {
        var scratch = new GraphScratch();
        var d = new ShortestPaths();
        var b = new ShortestPaths();
        for (ulong seed = 1; seed <= 300; seed++)
        {
            var rng = new Rng(seed);
            var n = 2 + rng.Below(11);
            var g = Make(n, RandomEdges(rng, n, rng.Below(4 * n), 0, 9, 0));
            var oracle = FloydWarshall(g);
            var s = rng.Below(n);
            Dijkstra.Run(g, s, -1, scratch, d);
            BellmanFord.Run(g, s, scratch, b);
            Assert.False(b.HasNegativeCycle);
            for (var v = 0; v < n; v++)
            {
                Assert.Equal(oracle[s, v], d.Distance(v));
                Assert.Equal(oracle[s, v], b.Distance(v));
                Assert.Equal(oracle[s, v] != long.MaxValue, d.IsFinal(v));
                Assert.Equal(d.IsFinal(v), b.IsFinal(v));
                if (!d.IsFinal(v)) continue;
                var path = d.PathTo(g, v);
                Assert.Equal(oracle[s, v], path.Cost);
                long sum = 0;
                for (var i = 0; i < path.Edges.Length; i++)
                {
                    Assert.Equal(path.Nodes[i], g.Source(path.Edges[i]));
                    Assert.Equal(path.Nodes[i + 1], g.Target(path.Edges[i]));
                    sum += g.Weight(path.Edges[i]);
                }
                Assert.Equal(oracle[s, v], sum);
                Assert.Equal(oracle[s, v], b.PathTo(g, v).Cost);
            }
        }
    }

    [Fact]
    public void BellmanFordHandlesNegativeWeightsAndFindsTheNegativeCycleAFloydWarshallOracleSees()
    {
        var withNegative = Make(4, W(0, 1, 4), W(0, 2, 5), W(1, 3, -3), W(2, 1, -4), W(3, 2, 2 + 10));
        var tree = BellmanFord.Run(withNegative, 0, new GraphScratch());
        Assert.False(tree.HasNegativeCycle);
        Assert.Equal(new long[] { 0, 1, 5, -2 }, tree.Distances.ToArray());

        var scratch = new GraphScratch();
        var r = new ShortestPaths();
        var cycles = 0;
        for (ulong seed = 1; seed <= 400; seed++)
        {
            var rng = new Rng(seed);
            var n = 2 + rng.Below(7);
            var g = Make(n, RandomEdges(rng, n, rng.Below(3 * n), -6, 9, 0));
            var oracle = FloydWarshall(g);
            var s = rng.Below(n);
            var reach = Reach(g);
            var negativeReachable = false;
            for (var v = 0; v < n; v++) if (reach[s, v] && reach[v, v] && oracle[v, v] < 0) negativeReachable = true;
            BellmanFord.Run(g, s, scratch, r);
            Assert.Equal(negativeReachable, r.HasNegativeCycle);
            if (r.HasNegativeCycle)
            {
                cycles++;
                Assert.True(r.CycleCost < 0);
                Assert.NotEmpty(r.CycleNodes);
                Assert.Equal(r.CycleNodes.Min(), r.CycleNodes[0]);
                long sum = 0;
                for (var i = 0; i < r.CycleNodes.Length; i++)
                {
                    Assert.Equal(r.CycleNodes[i], g.Source(r.CycleEdges[i]));
                    Assert.Equal(r.CycleNodes[(i + 1) % r.CycleNodes.Length], g.Target(r.CycleEdges[i]));
                    sum += g.Weight(r.CycleEdges[i]);
                }
                Assert.Equal(sum, r.CycleCost);
                Assert.True(reach[s, r.CycleNodes[0]]);
                Assert.False(r.PathTo(g, s).Found);
                Assert.StartsWith("bellman-ford: negative cycle of cost", r.Explain());
            }
            else
            {
                for (var v = 0; v < n; v++) Assert.Equal(oracle[s, v], r.Distance(v));
            }
        }
        Assert.True(cycles > 20, "the sample must contain negative cycles; it had " + cycles);
    }

    [Fact]
    public void ANegativeSelfLoopAndATwoNodeCycleAreFound()
    {
        var loop = Make(1, W(0, 0, -1));
        var r = BellmanFord.Run(loop, 0, new GraphScratch());
        Assert.True(r.HasNegativeCycle);
        Assert.Equal(new[] { 0 }, r.CycleNodes);
        Assert.Equal(-1, r.CycleCost);
        var two = Make(3, W(0, 1, 1), W(1, 2, -3), W(2, 1, 1));
        var t = BellmanFord.Run(two, 0, new GraphScratch());
        Assert.Equal(new[] { 1, 2 }, t.CycleNodes);
        Assert.Equal(-2, t.CycleCost);
        var unreachable = Make(3, W(1, 2, -3), W(2, 1, 1));
        Assert.False(BellmanFord.Run(unreachable, 0, new GraphScratch()).HasNegativeCycle);
    }

    [Fact]
    public void TheNegativeCycleReportedIsTheOneThroughTheFirstNodeRelaxedInTheLastPass()
    {
        // two negative cycles are reachable from node 0: {0,1} and {2,3}; the first node relaxed in the last pass decides
        var g = Make(4, W(0, 1, -1), W(1, 0, -1), W(0, 2, 1), W(2, 3, -5), W(3, 2, 1));
        var r = BellmanFord.Run(g, 0, new GraphScratch());
        Assert.True(r.HasNegativeCycle);
        Assert.Equal(new[] { 0, 1 }, r.CycleNodes);
        Assert.Equal(-2, r.CycleCost);
    }

    [Fact]
    public void BellmanFordRefusesAGraphWhoseDistancesCouldPassHalfTheLongRange()
    {
        GraphBuilder.Create(GraphLimits.MaxNodes, out var b);
        for (var i = 0; i < 50_000; i++) b!.Add(i, i + 1, GraphLimits.MaxWeight);
        var g = b!.Build().Graph!;
        Assert.Equal(GraphCodes.BellmanFordTooLarge, BellmanFord.Run(g, 0, new GraphScratch()).Code);
        var edgeless = Make(GraphLimits.MaxNodes);
        Assert.Equal("", BellmanFord.Run(edgeless, 0, new GraphScratch()).Code);
    }

    [Fact]
    public void BothRefuseBadInputWithCodes()
    {
        var g = Classic();
        var s = new GraphScratch();
        Assert.Equal(GraphCodes.SourceRange, Dijkstra.Run(g, -1, -1, s).Code);
        Assert.Equal(GraphCodes.SourceRange, Dijkstra.Run(g, 5, -1, s).Code);
        Assert.Equal(GraphCodes.SinkRange, Dijkstra.Run(g, 0, 5, s).Code);
        Assert.Equal(GraphCodes.SinkRange, Dijkstra.Run(g, 0, -2, s).Code);
        Assert.Equal(GraphCodes.ArgumentMissing, Dijkstra.Run(g, 0, -1, null!).Code);
        Assert.Equal(GraphCodes.NegativeWeight, Dijkstra.Run(Make(2, W(0, 1, -1)), 0, -1, s).Code);
        Assert.Equal(GraphCodes.SourceRange, BellmanFord.Run(g, 5, s).Code);
        Assert.Equal(GraphCodes.SourceRange, BellmanFord.Run(g, -1, s).Code);
        Assert.Equal(GraphCodes.ArgumentMissing, BellmanFord.Run(g, 0, null!).Code);
        Assert.Equal("refused GRAPH_SOURCE_RANGE", Dijkstra.Run(g, 9, -1, s).Explain());
        Assert.False(Dijkstra.Run(g, 9, -1, s).PathTo(g, 0).Found);
    }

    [Fact]
    public void ARefusedRunClearsWhatAnEarlierRunLeftInTheReusedResult()
    {
        var g = Classic();
        var s = new GraphScratch();
        var r = Dijkstra.Run(g, 0, -1, s);
        Dijkstra.Run(g, 9, -1, s, r);
        Assert.Equal(GraphCodes.SourceRange, r.Code);
        Assert.Equal(0, r.SettledCount);
        Assert.All(Enumerable.Range(0, 5), v => Assert.False(r.IsFinal(v)));
    }

    [Fact]
    public void RepeatedRunsWithReusedScratchAndResultAllocateNothing()
    {
        var rng = new Rng(7);
        var g = Make(2000, RandomEdges(rng, 2000, 20000, 1, 20, 0));
        var scratch = new GraphScratch();
        var tree = new ShortestPaths();
        var bf = new ShortestPaths();
        var bfs = new BfsResult();
        var comps = new ComponentsResult();
        for (var warm = 0; warm < 2; warm++)
        {
            Dijkstra.Run(g, 0, -1, scratch, tree);
            BellmanFord.Run(g, 0, scratch, bf);
            Bfs.Run(g, [0], 50, bfs);
            Components.Run(g, comps);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 5; i++)
        {
            Dijkstra.Run(g, i, -1, scratch, tree);
            BellmanFord.Run(g, i, scratch, bf);
            Bfs.Run(g, [i], 50, bfs);
            Components.Run(g, comps);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
