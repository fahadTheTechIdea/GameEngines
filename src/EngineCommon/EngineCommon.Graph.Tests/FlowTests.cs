using Xunit;
using static EngineCommon.Graph.Tests.Fx;

namespace EngineCommon.Graph.Tests;

public sealed class FlowTests
{
    private static CsrGraph Hand() => Make(4, C(0, 1, 2, 1), C(0, 2, 1, 2), C(1, 2, 1, 1), C(1, 3, 1, 3), C(2, 3, 2, 1));

    private static void CheckFlow(CsrGraph g, FlowResult r, int s, int t)
    {
        var net = new long[g.NodeCount];
        Int128 cost = 0;
        for (var e = 0; e < g.EdgeCount; e++)
        {
            Assert.InRange(r.EdgeFlow[e], 0, g.Capacity(e));
            net[g.Source(e)] -= r.EdgeFlow[e];
            net[g.Target(e)] += r.EdgeFlow[e];
            cost += (Int128)r.EdgeFlow[e] * g.Weight(e);
        }
        for (var v = 0; v < g.NodeCount; v++) if (v != s && v != t) Assert.Equal(0, net[v]);
        Assert.Equal(r.Flow, -net[s]);
        Assert.Equal(r.Flow, net[t]);
        Assert.Equal(cost, r.Cost);
        Assert.Equal(r.Flow, r.Augmentations.Sum(a => a.Amount));
        Assert.Equal(r.Cost, r.Augmentations.Aggregate((Int128)0, (sum, a) => sum + (Int128)a.Amount * a.UnitCost));
    }

    [Fact]
    public void MinCostMaxFlowMatchesAHandWorkedNetwork()
    {
        var g = Hand();
        var r = MinCostFlow.Run(g, 0, 3, GraphLimits.NoFlowLimit, 100, new GraphScratch());
        Assert.Equal("", r.Code);
        Assert.Equal(3, r.Flow);
        Assert.Equal(10, r.Cost);
        Assert.False(r.Truncated);
        CheckFlow(g, r, 0, 3);
        Assert.Equal(new long[] { 2, 1, 1, 1, 2 }, r.EdgeFlow);
        Assert.Equal("flow 3 from 0 to 3 at total cost 10 in " + r.Augmentations.Count + " augmentations", r.Explain());
        // The cheapest augmentation comes first and costs no more than any later one (successive shortest paths).
        for (var i = 1; i < r.Augmentations.Count; i++) Assert.True(r.Augmentations[i].UnitCost >= r.Augmentations[i - 1].UnitCost);
        Assert.Equal(3, r.Augmentations[0].UnitCost);
    }

    [Fact]
    public void AFlowLimitTakesTheCheapestUnitsFirst()
    {
        var g = Hand();
        var r = MinCostFlow.Run(g, 0, 3, 2, 100, new GraphScratch());
        Assert.Equal((2L, (Int128)6), (r.Flow, r.Cost));
        CheckFlow(g, r, 0, 3);
        var none = MinCostFlow.Run(g, 0, 3, 0, 100, new GraphScratch());
        Assert.Equal((0L, (Int128)0), (none.Flow, none.Cost));
        Assert.Empty(none.Augmentations);
    }

    [Fact]
    public void TheAugmentationLimitStopsTheRunAndSaysSo()
    {
        var g = Hand();
        var r = MinCostFlow.Run(g, 0, 3, GraphLimits.NoFlowLimit, 1, new GraphScratch());
        Assert.True(r.Truncated);
        Assert.Single(r.Augmentations);
        Assert.Equal(1, r.Flow);
        CheckFlow(g, r, 0, 3);
        Assert.EndsWith("(stopped at the augmentation limit)", r.Explain());
        var exact = MinCostFlow.Run(g, 0, 3, GraphLimits.NoFlowLimit, r.Augmentations.Count + 3, new GraphScratch());
        Assert.False(exact.Truncated);
    }

    [Fact]
    public void FlowEqualsABruteForceEnumerationOnSmallRandomNetworks()
    {
        var scratch = new GraphScratch();
        var checkedWithFlow = 0;
        for (ulong seed = 1; seed <= 400; seed++)
        {
            var rng = new Rng(seed);
            var n = 3 + rng.Below(3);
            var edges = new List<E>();
            var count = 3 + rng.Below(5);
            for (var i = 0; i < count; i++) edges.Add(new E(rng.Below(n), rng.Below(n), rng.Between(0, 5), rng.Between(0, 2)));
            var g = Make(n, edges);
            var s = 0;
            var t = n - 1;
            var oracle = BruteForceFlow(g, s, t);
            var r = MinCostFlow.Run(g, s, t, GraphLimits.NoFlowLimit, 1000, scratch);
            Assert.Equal(oracle.Flow, r.Flow);
            Assert.Equal((Int128)oracle.Cost, r.Cost);
            CheckFlow(g, r, s, t);
            if (r.Flow > 0) checkedWithFlow++;
        }
        Assert.True(checkedWithFlow > 100, "most samples must carry flow; " + checkedWithFlow + " did");
    }

    [Fact]
    public void FlowIsAlsoOptimalWhenLimitedAndWhenResidualCancellationIsNeeded()
    {
        // A graph where the first (cheapest) path must be partly undone to reach the maximum flow.
        var g = Make(4, C(0, 1, 1, 1), C(0, 2, 1, 4), C(1, 2, 1, 1), C(1, 3, 1, 4), C(2, 3, 1, 1));
        var r = MinCostFlow.Run(g, 0, 3, GraphLimits.NoFlowLimit, 100, new GraphScratch());
        Assert.Equal((2L, (Int128)10), (r.Flow, r.Cost));
        Assert.Contains(r.Augmentations, a => a.Arcs.Any(arc => (arc & 1) == 1));
        CheckFlow(g, r, 0, 3);
        var oracle = BruteForceFlow(g, 0, 3);
        Assert.Equal((2L, 10L), oracle);
    }

    [Fact]
    public void ADecompositionSplitsTheFinalFlowIntoPathsThatAddUp()
    {
        var g = Hand();
        var r = MinCostFlow.Run(g, 0, 3, GraphLimits.NoFlowLimit, 100, new GraphScratch());
        var d = r.Decompose(g);
        Assert.Equal(3, d.Paths.Sum(p => p.Amount));
        Assert.Equal(0, d.CancelledCycleFlow);
        Assert.Equal(0, d.LeftoverFlow);
        var rebuilt = new long[g.EdgeCount];
        foreach (var p in d.Paths)
        {
            Assert.Equal(0, g.Source(p.Edges[0]));
            Assert.Equal(3, g.Target(p.Edges[^1]));
            for (var i = 1; i < p.Edges.Length; i++) Assert.Equal(g.Target(p.Edges[i - 1]), g.Source(p.Edges[i]));
            foreach (var e in p.Edges) rebuilt[e] += p.Amount;
        }
        Assert.Equal(r.EdgeFlow, rebuilt);
        // lowest edge id first: the first path takes edge 0 (0>1) then edge 2 (1>2), the lowest at node 1
        Assert.Equal(new[] { 0, 2, 4 }, d.Paths[0].Edges);
    }

    [Fact]
    public void ADecompositionOfRandomFlowsRebuildsThemAndCancelsCycles()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var rng = new Rng(seed);
            var n = 6;
            var g = Make(n, RandomEdges(rng, n, 14, 0, 3, 3));
            var r = MinCostFlow.Run(g, 0, n - 1, GraphLimits.NoFlowLimit, 1000, new GraphScratch());
            var d = r.Decompose(g);
            Assert.Equal(r.Flow, d.Paths.Sum(p => p.Amount));
            var used = new long[g.EdgeCount];
            foreach (var p in d.Paths) foreach (var e in p.Edges) used[e] += p.Amount;
            for (var e = 0; e < g.EdgeCount; e++) Assert.True(used[e] <= r.EdgeFlow[e]);
        }
    }


    [Fact]
    public void ACycleInTheFlowIsCancelledNotReportedAsAPath()
    {
        // A zero-cost cycle 1>2>1 sits beside the path 0>1>3; the decomposition walks into it from node 1 and cancels it.
        var g = Make(4, C(0, 1, 5, 0), C(1, 2, 5, 0), C(2, 1, 5, 0), C(1, 3, 5, 0));
        var flow = new FlowResult { Source = 0, Sink = 3, EdgeFlow = [3, 2, 3, 2] };
        var d = flow.Decompose(g);
        Assert.Equal(3, d.Paths.Sum(p => p.Amount));
        Assert.Equal(2, d.CancelledCycleFlow);
        Assert.Equal(0, d.LeftoverFlow);
        Assert.Equal(new[] { 0, 2 }, d.Paths.Single().Edges);
        // A cycle through the source is cancelled too, and a circulation away from the path is left over.
        var h = Make(5, C(0, 1, 5, 0), C(1, 0, 5, 0), C(0, 4, 5, 0), C(2, 3, 5, 0), C(3, 2, 5, 0));
        var other = new FlowResult { Source = 0, Sink = 4, EdgeFlow = [2, 3, 2, 1, 1] };
        var e = other.Decompose(h);
        Assert.Equal(3, e.Paths.Single().Amount);
        Assert.Equal(2, e.CancelledCycleFlow);
        Assert.Equal(2, e.LeftoverFlow);
    }

    [Fact]
    public void DecompositionCancelsTheSmallestFlowOfACycleAndNeverLooksPastTheNodesOwnEdges()
    {
        // a cycle 1>2>1 (3 units) overlapping a path through edge 1>2 (4 units): the cycle is cancelled at its smallest remaining flow
        var g = Make(4, C(0, 1, 9, 0), C(1, 2, 9, 0), C(1, 3, 9, 0), C(2, 1, 9, 0), C(2, 3, 9, 0));
        var d = new FlowResult { Source = 0, Sink = 3, EdgeFlow = [5, 4, 4, 3, 1] }.Decompose(g);
        Assert.Equal(3, d.CancelledCycleFlow);
        Assert.Equal(0, d.LeftoverFlow);
        Assert.Equal(5, d.Paths.Sum(p => p.Amount));
        Assert.Equal(new[] { 0, 1, 4 }, d.Paths[0].Edges);
        Assert.Equal(1, d.Paths[0].Amount);
        Assert.Equal(new[] { 0, 2 }, d.Paths[1].Edges);
        Assert.Equal(4, d.Paths[1].Amount);
        // the same through the source: 0>1>2>0 carries 3 units beside a 2 unit path 0>1>3
        var h = Make(4, C(0, 1, 9, 0), C(1, 2, 9, 0), C(1, 3, 9, 0), C(2, 0, 9, 0));
        var e = new FlowResult { Source = 0, Sink = 3, EdgeFlow = [5, 3, 2, 3] }.Decompose(h);
        Assert.Equal(3, e.CancelledCycleFlow);
        Assert.Equal(2, e.Paths.Single().Amount);
        Assert.Equal(new[] { 0, 2 }, e.Paths.Single().Edges);
        Assert.Equal(0, e.LeftoverFlow);
        // a circulation that does not touch the source is left over, even when its edges directly follow the source's
        var f = Make(4, C(0, 2, 9, 0), C(1, 3, 9, 0), C(3, 1, 9, 0));
        var r = new FlowResult { Source = 0, Sink = 2, EdgeFlow = [3, 2, 2] }.Decompose(f);
        Assert.Equal(3, r.Paths.Single().Amount);
        Assert.Equal(0, r.CancelledCycleFlow);
        Assert.Equal(4, r.LeftoverFlow);
    }

    [Fact]
    public void ParallelEdgesSelfLoopsAndNoPathAreHandled()
    {
        var g = Make(3, C(0, 1, 2, 5), C(0, 1, 3, 1), C(1, 1, 9, 0), C(1, 2, 4, 0));
        var r = MinCostFlow.Run(g, 0, 2, GraphLimits.NoFlowLimit, 100, new GraphScratch());
        Assert.Equal((4L, (Int128)(3 * 1 + 1 * 5)), (r.Flow, r.Cost));
        Assert.Equal(0, r.EdgeFlow[2]);
        var disconnected = MinCostFlow.Run(Make(3, C(0, 1, 5, 1)), 0, 2, GraphLimits.NoFlowLimit, 10, new GraphScratch());
        Assert.Equal(0, disconnected.Flow);
        Assert.Equal(0, disconnected.Cost);
        var sink = Make(2, C(0, 1, GraphLimits.MaxCapacity, GraphLimits.MaxWeight), C(0, 1, GraphLimits.MaxCapacity, GraphLimits.MaxWeight));
        var big = MinCostFlow.Run(sink, 0, 1, GraphLimits.NoFlowLimit, 10, new GraphScratch());
        Assert.Equal(2 * GraphLimits.MaxCapacity, big.Flow);
        Assert.Equal((Int128)2 * GraphLimits.MaxCapacity * GraphLimits.MaxWeight, big.Cost);
    }

    [Fact]
    public void ReusingOneScratchAcrossFlowsGivesTheSameAnswersAsAFreshOne()
    {
        var shared = new GraphScratch();
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var rng = new Rng(seed);
            var n = 4 + rng.Below(6);
            var g = Make(n, RandomEdges(rng, n, 3 * n, 0, 20, 10));
            var a = MinCostFlow.Run(g, 0, n - 1, GraphLimits.NoFlowLimit, 10000, shared);
            var b = MinCostFlow.Run(g, 0, n - 1, GraphLimits.NoFlowLimit, 10000, new GraphScratch());
            Assert.Equal((a.Flow, a.Cost), (b.Flow, b.Cost));
            Assert.Equal(a.EdgeFlow, b.EdgeFlow);
        }
    }

    [Fact]
    public void FlowRefusesBadInputWithCodes()
    {
        var g = Hand();
        var s = new GraphScratch();
        Assert.Equal(GraphCodes.SourceRange, MinCostFlow.Run(g, -1, 3, 1, 10, s).Code);
        Assert.Equal(GraphCodes.SourceRange, MinCostFlow.Run(g, 4, 3, 1, 10, s).Code);
        Assert.Equal(GraphCodes.SinkRange, MinCostFlow.Run(g, 0, -1, 1, 10, s).Code);
        Assert.Equal(GraphCodes.SinkRange, MinCostFlow.Run(g, 0, 4, 1, 10, s).Code);
        Assert.Equal(GraphCodes.SameSourceSink, MinCostFlow.Run(g, 1, 1, 1, 10, s).Code);
        Assert.Equal(GraphCodes.NegativeWeight, MinCostFlow.Run(Make(2, C(0, 1, 1, -1)), 0, 1, 1, 10, s).Code);
        Assert.Equal(GraphCodes.FlowLimitRange, MinCostFlow.Run(g, 0, 3, -1, 10, s).Code);
        Assert.Equal(GraphCodes.AugmentationsRange, MinCostFlow.Run(g, 0, 3, 1, 0, s).Code);
        Assert.Equal(GraphCodes.AugmentationsRange, MinCostFlow.Run(g, 0, 3, 1, GraphLimits.MaxAugmentations + 1, s).Code);
        Assert.Equal(GraphCodes.ArgumentMissing, MinCostFlow.Run(g, 0, 3, 1, 10, null!).Code);
        Assert.Equal("refused GRAPH_SAME_SOURCE_SINK", MinCostFlow.Run(g, 1, 1, 1, 10, s).Explain());
        Assert.Equal("", MinCostFlow.Run(g, 0, 3, 0, GraphLimits.MaxAugmentations, s).Code);
    }
}
