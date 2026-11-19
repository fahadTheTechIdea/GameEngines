using Xunit;
using static EngineCommon.Graph.Tests.Fx;

namespace EngineCommon.Graph.Tests;

/// <summary>
/// Reweighting keeps the topology and replaces the weights, and must leave exactly the graph a fresh build with those weights gives. The oracle is the fresh build itself (a separate path through the builder's sort),
/// compared edge by edge, by the summary properties and by Dijkstra's tree, on fixed-seed random graphs with parallel edges, self-loops and many equal weights (the cases where the order of parallel edges changes).
/// </summary>
public sealed class ReweightTests
{
    private static CsrGraph Fresh(int nodes, List<E> edges, long[] weights)
    {
        var list = new List<E>();
        for (var i = 0; i < edges.Count; i++) list.Add(edges[i] with { Weight = weights[i] });
        return Make(nodes, list);
    }

    private static void SameInEveryWay(CsrGraph expected, CsrGraph actual, string context)
    {
        Assert.True(SameGraph(expected, actual), context + ": the edges differ");
        for (var n = 0; n <= expected.NodeCount; n++) Assert.Equal(expected.FirstEdge(n), actual.FirstEdge(n));
        Assert.Equal(expected.HasNegativeWeight, actual.HasNegativeWeight);
        Assert.Equal(expected.MaxAbsWeight, actual.MaxAbsWeight);
    }

    [Fact]
    public void AReweightedGraphIsExactlyTheGraphAFreshBuildWithThoseWeightsGivesAndSearchesAgree()
    {
        var rng = new Rng(20261044);
        var scratch = new GraphScratch();
        var fromReweighted = new ShortestPaths();
        var fromFresh = new ShortestPaths();
        var compared = 0;
        for (var round = 0; round < 300; round++)
        {
            var nodes = 2 + rng.Below(14);
            var count = 1 + rng.Below(60);
            var spread = round % 3 == 0 ? 3 : 50; // a small range makes ties between parallel edges common
            var edges = RandomEdges(rng, nodes, count, 0, spread, round % 2 == 0 ? 4 : 0);
            var graph = Make(nodes, edges);
            // Reweights are given in the order the edges were added, whatever the edge ids have become.
            for (var pass = 0; pass < 6; pass++)
            {
                var weights = new long[count];
                for (var i = 0; i < count; i++) weights[i] = rng.Between(0, spread);
                Assert.Equal("", graph.Reweight(weights));
                var fresh = Fresh(nodes, edges, weights);
                SameInEveryWay(fresh, graph, $"round {round} pass {pass}");
                var source = rng.Below(nodes);
                Dijkstra.Run(graph, source, -1, scratch, fromReweighted);
                Dijkstra.Run(fresh, source, -1, scratch, fromFresh);
                for (var n = 0; n < nodes; n++)
                {
                    Assert.Equal(fromFresh.Distance(n), fromReweighted.Distance(n));
                    Assert.Equal(fromFresh.ParentEdge(n), fromReweighted.ParentEdge(n));
                }
                compared++;
            }
        }
        Assert.Equal(1800, compared);
    }

    [Fact]
    public void AReweightedGraphDoesNotDependOnTheOrderTheEdgesWereAddedIn()
    {
        var rng = new Rng(44);
        for (var round = 0; round < 100; round++)
        {
            var nodes = 2 + rng.Below(8);
            var edges = RandomEdges(rng, nodes, 1 + rng.Below(40), 0, 4, 3);
            // Every edge is distinct by its position; give each position a weight, add the edges in a shuffled order and give each its own weight again.
            var weights = edges.Select(_ => rng.Between(0, 4)).ToArray();
            var order = Enumerable.Range(0, edges.Count).ToList();
            for (var i = order.Count - 1; i > 0; i--)
            {
                var j = rng.Below(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            var first = Make(nodes, edges);
            var second = Make(nodes, order.Select(i => edges[i]).ToList());
            Assert.Equal("", first.Reweight(weights));
            Assert.Equal("", second.Reweight(order.Select(i => weights[i]).ToArray()));
            SameInEveryWay(first, second, "round " + round);
        }
    }

    [Fact]
    public void ReweightingAllocatesNothing()
    {
        var rng = new Rng(7);
        var edges = RandomEdges(rng, 200, 1500, 0, 50, 5);
        var graph = Make(200, edges);
        var weights = new long[1500];
        for (var i = 0; i < weights.Length; i++) weights[i] = rng.Between(0, 50);
        Assert.Equal("", graph.Reweight(weights)); // warm up
        var refused = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 20; i++)
        {
            weights[i] = rng.Between(0, 50);
            if (graph.Reweight(weights).Length != 0) refused++; // asserted after the measurement: an assertion call may allocate
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, refused);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void NegativeWeightsAreAcceptedAndTheSummaryFollowsThem()
    {
        var graph = Make(3, W(0, 1, 5), W(1, 2, 7));
        Assert.False(graph.HasNegativeWeight);
        Assert.Equal(7, graph.MaxAbsWeight);
        Assert.Equal("", graph.Reweight([-9, 2]));
        Assert.True(graph.HasNegativeWeight);
        Assert.Equal(9, graph.MaxAbsWeight);
        Assert.Equal(GraphCodes.NegativeWeight, Dijkstra.Run(graph, 0, -1, new GraphScratch()).Code);
        Assert.Equal("", graph.Reweight([1, 2]));
        Assert.False(graph.HasNegativeWeight);
        Assert.Equal(2, graph.MaxAbsWeight);
    }

    [Fact]
    public void AGraphWithNoEdgesReweightsToNothingAndARefusalChangesNothing()
    {
        var empty = Make(4);
        Assert.Equal("", empty.Reweight([]));
        Assert.Equal(GraphCodes.WeightsLength, empty.Reweight([1]));

        var edges = new List<E> { W(0, 1, 3), W(0, 1, 1), W(1, 0, 2) };
        var graph = Make(2, edges);
        var snapshot = Make(2, edges);
        Assert.Equal(GraphCodes.WeightsLength, graph.Reweight([1, 2]));
        Assert.Equal(GraphCodes.WeightsLength, graph.Reweight([1, 2, 3, 4]));
        Assert.Equal(GraphCodes.WeightRange, graph.Reweight([1, GraphLimits.MaxWeight + 1, 3]));
        Assert.Equal(GraphCodes.WeightRange, graph.Reweight([-GraphLimits.MaxWeight - 1, 1, 3]));
        SameInEveryWay(snapshot, graph, "after refusals");
        Assert.Equal("", graph.Reweight([GraphLimits.MaxWeight, -GraphLimits.MaxWeight, 0]));
        Assert.Equal(GraphLimits.MaxWeight, graph.MaxAbsWeight);
    }

    [Fact]
    public void ParallelEdgesComeBackInWeightThenCapacityOrderAndKeepTheirOwnCapacity()
    {
        // Two parallel edges 0 -> 1 told apart by capacity (a route rank in the Economy): the cheaper must come first, equal weights by capacity, and the capacity travels with its edge.
        var graph = Make(2, C(0, 1, 10, 5), C(0, 1, 20, 5));
        Assert.Equal(10, graph.Capacity(0));
        Assert.Equal("", graph.Reweight([9, 4]));      // the capacity-20 edge is now cheaper
        Assert.Equal((20L, 4L), (graph.Capacity(0), graph.Weight(0)));
        Assert.Equal((10L, 9L), (graph.Capacity(1), graph.Weight(1)));
        Assert.Equal("", graph.Reweight([6, 6]));      // a tie: lower capacity first, as a fresh build orders it
        Assert.Equal((10L, 6L), (graph.Capacity(0), graph.Weight(0)));
        Assert.Equal((20L, 6L), (graph.Capacity(1), graph.Weight(1)));
    }
}
