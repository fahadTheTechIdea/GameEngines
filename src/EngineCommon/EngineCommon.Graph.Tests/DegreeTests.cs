using Xunit;
using static EngineCommon.Graph.Tests.Fx;

namespace EngineCommon.Graph.Tests;

public sealed class DegreeTests
{
    [Fact]
    public void DegreesAndWeightedDegreesMatchAHandCount()
    {
        var g = Make(4, W(0, 1, 5), W(0, 2, 3), W(1, 2, -2), W(2, 2, 4), W(3, 0, 1));
        var t = DegreeTable.Run(g);
        Assert.Equal(new long[] { 2, 1, 1, 1 }, t.OutDegree.ToArray());
        Assert.Equal(new long[] { 1, 1, 3, 0 }, t.InDegree.ToArray());
        Assert.Equal(new long[] { 8, -2, 4, 1 }, t.OutWeight.ToArray());
        Assert.Equal(new long[] { 1, 5, 5, 0 }, t.InWeight.ToArray());
    }

    [Fact]
    public void DegreesEqualACountOverTheEdgesOnRandomGraphs()
    {
        var table = new DegreeTable();
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var rng = new Rng(seed);
            var n = 1 + rng.Below(15);
            var edges = RandomEdges(rng, n, rng.Below(60), -9, 9, 0);
            DegreeTable.Run(Make(n, edges), table);
            for (var v = 0; v < n; v++)
            {
                Assert.Equal(edges.Count(e => e.From == v), table.OutDegree[v]);
                Assert.Equal(edges.Count(e => e.To == v), table.InDegree[v]);
                Assert.Equal(edges.Where(e => e.From == v).Sum(e => e.Weight), table.OutWeight[v]);
                Assert.Equal(edges.Where(e => e.To == v).Sum(e => e.Weight), table.InWeight[v]);
            }
        }
    }

    [Fact]
    public void TheTopNodesAreLargestFirstWithTheLowestIdWinningATie()
    {
        var g = Make(6, W(0, 1, 5), W(1, 0, 5), W(2, 0, 9), W(3, 0, 1), W(4, 5, 5), W(5, 4, 5));
        var t = DegreeTable.Run(g);
        var byWeight = t.Top(DegreeMeasure.OutWeight, 3);
        Assert.Equal("", byWeight.Code);
        Assert.Equal(new[] { 2, 0, 1 }, byWeight.Nodes);
        Assert.Equal(new long[] { 9, 5, 5 }, byWeight.Values);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }.Length, t.Top(DegreeMeasure.OutDegree, 100).Nodes.Length);
        Assert.Equal(new[] { 0, 1 }, t.Top(DegreeMeasure.InWeight, 2).Nodes); // in-weights: node 0 has 15, then 1, 4 and 5 tie at 5
        Assert.Equal(new[] { 0 }, t.Top(DegreeMeasure.InDegree, 1).Nodes);
        Assert.Equal(new long[] { 3 }, t.Top(DegreeMeasure.InDegree, 1).Values);
    }

    [Fact]
    public void TopRanksEqualAnIndependentSortOnRandomGraphs()
    {
        for (ulong seed = 1; seed <= 80; seed++)
        {
            var rng = new Rng(seed);
            var n = 1 + rng.Below(20);
            var edges = RandomEdges(rng, n, rng.Below(80), 0, 9, 0);
            var t = DegreeTable.Run(Make(n, edges));
            var k = 1 + rng.Below(8);
            var expected = Enumerable.Range(0, n).OrderByDescending(v => t.InWeight[v]).ThenBy(v => v).Take(k).ToArray();
            Assert.Equal(expected, t.Top(DegreeMeasure.InWeight, k).Nodes);
            var expectedOut = Enumerable.Range(0, n).OrderByDescending(v => t.OutDegree[v]).ThenBy(v => v).Take(k).ToArray();
            Assert.Equal(expectedOut, t.Top(DegreeMeasure.OutDegree, k).Nodes);
        }
    }

    [Fact]
    public void TopRefusesARankOutsideItsBoundsAndAnEmptyGraphHasNoTop()
    {
        var t = DegreeTable.Run(Make(3));
        Assert.Equal(GraphCodes.TopRange, t.Top(DegreeMeasure.OutDegree, 0).Code);
        Assert.Equal(GraphCodes.TopRange, t.Top(DegreeMeasure.OutDegree, GraphLimits.MaxTop + 1).Code);
        Assert.Equal("", t.Top(DegreeMeasure.OutDegree, GraphLimits.MaxTop).Code);
        Assert.Empty(DegreeTable.Run(Make(0)).Top(DegreeMeasure.InWeight, 5).Nodes);
    }

    [Fact]
    public void ReusingATableAcrossGrowingGraphsKeepsTheMeasuresApart()
    {
        var table = DegreeTable.Run(Make(2, W(0, 1, 7)));
        DegreeTable.Run(Make(5, W(4, 3, 2), W(4, 3, 2)), table);
        Assert.Equal(new long[] { 0, 0, 0, 0, 2 }, table.OutDegree.ToArray());
        Assert.Equal(new long[] { 0, 0, 0, 2, 0 }, table.InDegree.ToArray());
        Assert.Equal(new long[] { 0, 0, 0, 0, 4 }, table.OutWeight.ToArray());
        Assert.Equal(new long[] { 0, 0, 0, 4, 0 }, table.InWeight.ToArray());
        Assert.Equal(new[] { 3 }, table.Top(DegreeMeasure.InWeight, 1).Nodes);
        Assert.Equal(new[] { 4 }, table.Top(DegreeMeasure.OutWeight, 1).Nodes);
    }
}
