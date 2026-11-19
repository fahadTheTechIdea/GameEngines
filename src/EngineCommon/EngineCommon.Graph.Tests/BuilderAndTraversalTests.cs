using Xunit;
using static EngineCommon.Graph.Tests.Fx;

namespace EngineCommon.Graph.Tests;

public sealed class BuilderAndTraversalTests
{
    [Fact]
    public void EdgesAreSortedByFromToWeightCapacityAndHaveCsrOffsets()
    {
        var g = Make(4, new E(2, 1, 5, 0), new E(0, 3, 9, 1), new E(0, 1, 7, 2), new E(0, 1, 7, 1), new E(0, 1, 3, 9), new E(3, 3, -2, 0));
        var expected = new[] { (0, 1, 3L, 9L), (0, 1, 7L, 1L), (0, 1, 7L, 2L), (0, 3, 9L, 1L), (2, 1, 5L, 0L), (3, 3, -2L, 0L) };
        Assert.Equal(6, g.EdgeCount);
        for (var e = 0; e < 6; e++)
            Assert.Equal(expected[e], (g.Source(e), g.Target(e), g.Weight(e), g.Capacity(e)));
        Assert.Equal((0, 4), (g.FirstEdge(0), g.EndEdge(0)));
        Assert.Equal((4, 4), (g.FirstEdge(1), g.EndEdge(1)));
        Assert.Equal((4, 5), (g.FirstEdge(2), g.EndEdge(2)));
        Assert.Equal((5, 6), (g.FirstEdge(3), g.EndEdge(3)));
        Assert.Equal(4, g.OutDegree(0));
        Assert.True(g.HasNegativeWeight);
        Assert.Equal(9, g.MaxAbsWeight);
    }

    [Fact]
    public void AnEmptyGraphAndAnEdgelessGraphBuild()
    {
        var empty = Make(0);
        Assert.Equal((0, 0, false, 0L), (empty.NodeCount, empty.EdgeCount, empty.HasNegativeWeight, empty.MaxAbsWeight));
        var lone = Make(3);
        Assert.Equal(0, lone.EdgeCount);
        Assert.Equal(0, lone.OutDegree(2));
    }

    [Fact]
    public void TheSameEdgesInAnyOrderBuildTheSameGraph()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var rng = new Rng(seed);
            var edges = RandomEdges(rng, 9, 30, -5, 20, 6);
            var reference = Make(9, edges);
            for (var shuffle = 0; shuffle < 3; shuffle++)
                Assert.True(SameGraph(reference, Make(9, Shuffled(rng, edges))), "seed " + seed);
        }
    }

    [Fact]
    public void AnUndirectedLinkIsTwoEdgesAndAnAddIsAllOrNothing()
    {
        GraphBuilder.Create(3, out var b);
        Assert.Equal("", b!.AddUndirected(0, 2, 4, 7));
        Assert.Equal(2, b.EdgeCount);
        Assert.Equal(GraphCodes.NodeRange, b.AddUndirected(0, 5, 1));
        Assert.Equal(2, b.EdgeCount);
        Assert.Equal(GraphCodes.NodeRange, b.AddUndirected(5, 0, 1));
        Assert.Equal(2, b.EdgeCount);
        var g = b.Build().Graph!;
        Assert.Equal((0, 2, 4L, 7L), (g.Source(0), g.Target(0), g.Weight(0), g.Capacity(0)));
        Assert.Equal((2, 0, 4L, 7L), (g.Source(1), g.Target(1), g.Weight(1), g.Capacity(1)));
    }

    [Fact]
    public void TheBuilderRefusesEverythingOverALimitWithItsCode()
    {
        Assert.Equal(GraphCodes.NodeCount, GraphBuilder.Create(-1, out var none));
        Assert.Null(none);
        Assert.Equal(GraphCodes.NodeCount, GraphBuilder.Create(GraphLimits.MaxNodes + 1, out _));
        Assert.Equal("", GraphBuilder.Create(GraphLimits.MaxNodes, out _));
        GraphBuilder.Create(3, out var b);
        Assert.Equal(GraphCodes.NodeRange, b!.Add(-1, 0, 1));
        Assert.Equal(GraphCodes.NodeRange, b.Add(0, 3, 1));
        Assert.Equal(GraphCodes.NodeRange, b.Add(3, 0, 1));
        Assert.Equal(GraphCodes.WeightRange, b.Add(0, 1, GraphLimits.MaxWeight + 1));
        Assert.Equal(GraphCodes.WeightRange, b.Add(0, 1, -GraphLimits.MaxWeight - 1));
        Assert.Equal(GraphCodes.CapacityRange, b.Add(0, 1, 0, -1));
        Assert.Equal(GraphCodes.CapacityRange, b.Add(0, 1, 0, GraphLimits.MaxCapacity + 1));
        Assert.Equal("", b.Add(0, 1, GraphLimits.MaxWeight, GraphLimits.MaxCapacity));
        Assert.Equal("", b.Add(0, 1, -GraphLimits.MaxWeight, 0));
        Assert.Equal(2, b.EdgeCount);
    }

    [Fact]
    public void TheEdgeLimitIsEnforcedAtTheLimit()
    {
        GraphBuilder.Create(2, out var b);
        for (var i = 0; i < GraphLimits.MaxEdges; i++) Assert.Equal("", b!.Add(0, 1, 1));
        Assert.Equal(GraphCodes.EdgeCount, b!.Add(0, 1, 1));
        Assert.Equal(GraphCodes.EdgeCount, b.AddUndirected(0, 1, 1));
        Assert.Equal(GraphLimits.MaxEdges, b.EdgeCount);
    }

    [Fact]
    public void AnUndirectedAddThatDoesNotFitAddsNeitherEdge()
    {
        GraphBuilder.Create(2, out var b);
        for (var i = 0; i < GraphLimits.MaxEdges - 1; i++) b!.Add(0, 1, 1);
        Assert.Equal(GraphCodes.EdgeCount, b!.AddUndirected(0, 1, 1));
        Assert.Equal(GraphLimits.MaxEdges - 1, b.EdgeCount);
        Assert.Equal("", b.Add(1, 0, 1));
    }

    [Fact]
    public void ABreadthFirstRefusalLeavesAnEmptyResultAndAFullReachExplainsItself()
    {
        var g = Make(3, W(0, 1, 1), W(1, 2, 1));
        var refused = Bfs.Run(g, [0], -1);
        Assert.Equal(GraphCodes.DepthRange, refused.Code);
        Assert.Equal(0, refused.Count);
        Assert.Empty(refused.Order.ToArray());
        Assert.Equal(GraphCodes.SourceRange, Bfs.Run(g, [3], 1, refused).Code);
        Assert.Equal(GraphCodes.SourceRange, Bfs.Run(g, [-1], 1, refused).Code);
        Assert.Equal(GraphCodes.SourceRange, Bfs.Run(g, [0, 5], 1, refused).Code);
        Assert.Equal(0, refused.Count);
        var full = Bfs.Run(g, [0], 9, refused);
        Assert.Equal("reached 3 of 3 nodes, the farthest 2 hops away", full.Explain());
    }

    [Fact]
    public void BreadthFirstSearchMatchesAHandWorkedGraph()
    {
        var g = Make(6, W(0, 1, 1), W(0, 2, 1), W(1, 3, 1), W(2, 3, 1), W(3, 4, 1));
        var r = Bfs.Run(g, [0], 10);
        Assert.Equal("", r.Code);
        Assert.Equal(new[] { 0, 1, 1, 2, 3, -1 }, r.Hops.ToArray());
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, r.Order.ToArray());
        Assert.Equal(5, r.Count);
        Assert.Equal(1, g.Source(r.ParentEdge[3])); // reached first from node 1, whose edge to 3 comes before node 2's
        Assert.Equal(-1, r.ParentEdge[0]);
        Assert.Equal(-1, r.ParentEdge[5]);
        var limited = Bfs.Run(g, [0], 2, r);
        Assert.Same(r, limited);
        Assert.Equal(new[] { 0, 1, 1, 2, -1, -1 }, limited.Hops.ToArray());
        Assert.Equal(4, limited.Count);
        var zero = Bfs.Run(g, [0], 0);
        Assert.Equal(new[] { 0 }, zero.Order.ToArray());
        Assert.Contains("reached 4 of 6 nodes, the farthest 2 hops", limited.Explain());
    }

    [Fact]
    public void BreadthFirstSearchWithSeveralSourcesAndDuplicatesStartsThemAllAtZero()
    {
        var g = Make(5, W(0, 1, 1), W(3, 4, 1), W(4, 1, 1));
        var r = Bfs.Run(g, [3, 0, 3], 5);
        Assert.Equal(new[] { 3, 0, 4, 1 }, r.Order.ToArray());
        Assert.Equal(new[] { 0, 1, -1, 0, 1 }, r.Hops.ToArray());
    }

    [Fact]
    public void BreadthFirstSearchEqualsAnIndependentReachabilityAndDistanceOracle()
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var rng = new Rng(seed);
            var g = Make(10, RandomEdges(rng, 10, 5 + rng.Below(25), 1, 1, 0));
            var reach = Reach(g);
            var hops = FloydWarshall(g);
            var s = rng.Below(10);
            var r = Bfs.Run(g, [s], 100);
            for (var v = 0; v < 10; v++)
            {
                Assert.Equal(reach[s, v], r.Hops[v] >= 0);
                if (reach[s, v]) Assert.Equal(hops[s, v], r.Hops[v]);
            }
            var k = rng.Below(4);
            var near = Bfs.Run(g, [s], k);
            for (var v = 0; v < 10; v++) Assert.Equal(reach[s, v] && hops[s, v] <= k, near.Hops[v] >= 0);
        }
    }

    [Fact]
    public void ComponentsAreNamedByTheirLowestNodeAndMatchAReachabilityOracle()
    {
        var g = Make(6, W(0, 1, 1), W(3, 2, 1), W(3, 4, 1));
        var r = Components.Run(g);
        Assert.Equal(new[] { 0, 0, 2, 2, 2, 5 }, r.Component.ToArray());
        Assert.Equal(3, r.Count);
        Assert.Equal("3 components over 6 nodes", r.Explain());
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var rng = new Rng(seed);
            var h = Make(12, RandomEdges(rng, 12, rng.Below(14), 1, 1, 0));
            var reach = Reach(h);
            var c = Components.Run(h, r);
            for (var a = 0; a < 12; a++)
                for (var b = 0; b < 12; b++)
                    Assert.Equal(Linked(reach, a, b), c.Component[a] == c.Component[b]);
            for (var a = 0; a < 12; a++)
            {
                Assert.True(c.Component[a] <= a);
                Assert.Equal(c.Component[a], c.Component[c.Component[a]]);
            }
            Assert.Equal(Enumerable.Range(0, 12).Count(a => c.Component[a] == a), c.Count);
        }
    }

    // Weak connectivity from the closure: join the undirected closure by repeating until nothing changes.
    private static bool Linked(bool[,] reach, int a, int b)
    {
        var n = reach.GetLength(0);
        var group = new bool[n, n];
        for (var i = 0; i < n; i++) for (var j = 0; j < n; j++) group[i, j] = reach[i, j] || reach[j, i];
        for (var k = 0; k < n; k++) for (var i = 0; i < n; i++) if (group[i, k]) for (var j = 0; j < n; j++) if (group[k, j]) group[i, j] = true;
        return group[a, b];
    }
}
