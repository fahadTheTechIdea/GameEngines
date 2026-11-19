using Xunit;
using static EngineCommon.Graph.Tests.Fx;

namespace EngineCommon.Graph.Tests;

public sealed class CommunityTests
{
    private static List<E> Clique(int first, int size, long weight = 1)
    {
        var edges = new List<E>();
        for (var a = first; a < first + size; a++)
            for (var b = first; b < first + size; b++)
                if (a != b) edges.Add(W(a, b, weight));
        return edges;
    }

    [Fact]
    public void TwoTrianglesJoinedByABridgeMergeAsAHandWorkedRunPredicts()
    {
        var edges = new List<E>();
        foreach (var (a, b) in new[] { (0, 1), (1, 2), (0, 2), (3, 4), (4, 5), (3, 5), (2, 3) })
        {
            edges.Add(W(a, b, 1));
            edges.Add(W(b, a, 1));
        }
        var g = Make(6, edges);
        var r = LabelPropagation.Run(g, [], 10, new GraphScratch());
        Assert.Equal("", r.Code);
        Assert.Equal(new[] { 1, 1, 1, 1, 1, 1 }, r.Labels.ToArray());
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0 }, r.Community.ToArray());
        Assert.Equal(new[] { 5, 0 }, r.ChangesPerRound);
        Assert.Equal((2, true, 1), (r.Rounds, r.Converged, r.CommunityCount));
        Assert.Equal("1 communities over 6 nodes after 2 rounds (converged)", r.Explain());
    }

    [Fact]
    public void SeparateCliquesBecomeSeparateCommunitiesNamedByTheirLowestNode()
    {
        var edges = new List<E>();
        for (var c = 0; c < 5; c++) edges.AddRange(Clique(c * 6, 6));
        var g = Make(30, edges);
        var r = LabelPropagation.Run(g, [], 20, new GraphScratch());
        Assert.True(r.Converged);
        Assert.Equal(5, r.CommunityCount);
        for (var v = 0; v < 30; v++) Assert.Equal(v / 6 * 6, r.Community[v]);
        var support = r.Support(g, 4);
        Assert.Equal(new LabelSupport(r.Labels[4], 5, 5, 5, 5), support);
    }

    [Fact]
    public void HeavierEdgesWinAndNonPositiveEdgesAreIgnored()
    {
        // node 2 points at 0 (weight 5) and 1 (weight 1); 0 and 1 hold different labels from the start.
        var g = Make(4, W(2, 0, 5), W(2, 1, 1), W(2, 3, -9), W(3, 3, 0));
        var r = LabelPropagation.Run(g, [0, 1, 2, 3], 5, new GraphScratch());
        Assert.Equal(new[] { 0, 1, 0, 3 }, r.Labels.ToArray());
        var s = r.Support(g, 2);
        Assert.Equal(new LabelSupport(0, 1, 2, 5, 6), s);
        Assert.Equal(new LabelSupport(3, 0, 0, 0, 0), r.Support(g, 3));
    }

    [Fact]
    public void TheTieBreakKeepsTheOwnLabelElseTakesTheLowest()
    {
        // 1 points equally at nodes holding labels 3 and 2 and its own label is 1: lowest heaviest, 2.
        var g = Make(4, W(1, 2, 1), W(1, 3, 1));
        Assert.Equal(2, LabelPropagation.Run(g, [0, 1, 2, 3], 1, new GraphScratch()).Labels[1]);
        // Its own label is among the heaviest: it stays even though a lower label ties.
        Assert.Equal(3, LabelPropagation.Run(g, [0, 3, 2, 3], 1, new GraphScratch()).Labels[1]);
        // A lone heavier label beats a lower lighter one.
        var h = Make(3, W(0, 1, 1), W(0, 2, 2));
        Assert.Equal(2, LabelPropagation.Run(h, [0, 1, 2], 1, new GraphScratch()).Labels[0]);
    }

    [Fact]
    public void TheRoundLimitStopsARunThatHasNotConverged()
    {
        var g = Make(12, Clique(0, 6).Concat(Clique(6, 6)));
        var r = LabelPropagation.Run(g, [], 1, new GraphScratch());
        Assert.Equal((1, false), (r.Rounds, r.Converged));
        Assert.Contains("stopped at the round limit", r.Explain());
        var done = LabelPropagation.Run(g, [], 10, new GraphScratch(), r);
        Assert.Same(r, done);
        Assert.True(done.Converged);
    }

    [Fact]
    public void TheSameEdgesInAnyOrderGiveTheSameCommunities()
    {
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var rng = new Rng(seed);
            var edges = RandomEdges(rng, 40, 160, -1, 5, 0);
            var a = LabelPropagation.Run(Make(40, edges), [], 30, new GraphScratch());
            var b = LabelPropagation.Run(Make(40, Shuffled(rng, edges)), [], 30, new GraphScratch());
            Assert.Equal(a.Labels.ToArray(), b.Labels.ToArray());
            Assert.Equal(a.Rounds, b.Rounds);
            Assert.Equal(a.ChangesPerRound, b.ChangesPerRound);
        }
    }

    [Fact]
    public void ARunIsAFixedPointOfItself()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var rng = new Rng(seed);
            var g = Make(30, RandomEdges(rng, 30, 120, 1, 4, 0));
            var first = LabelPropagation.Run(g, [], 100, new GraphScratch());
            if (!first.Converged) continue;
            var again = LabelPropagation.Run(g, first.Labels.ToArray(), 5, new GraphScratch());
            Assert.Equal(first.Labels.ToArray(), again.Labels.ToArray());
            Assert.Equal(1, again.Rounds);
            // every node's label has the greatest weight among its neighbours' labels
            for (var v = 0; v < 30; v++)
            {
                var weights = new Dictionary<int, long>();
                for (var e = g.FirstEdge(v); e < g.EndEdge(v); e++) weights[first.Labels[g.Target(e)]] = weights.GetValueOrDefault(first.Labels[g.Target(e)]) + g.Weight(e);
                if (weights.Count == 0) continue;
                Assert.Equal(weights.Values.Max(), weights.GetValueOrDefault(first.Labels[v]));
            }
        }
    }

    [Fact]
    public void LabelPropagationRefusesBadInputWithCodes()
    {
        var g = Make(3, W(0, 1, 1));
        var s = new GraphScratch();
        var earlier = LabelPropagation.Run(g, [], 3, s);
        Assert.True(earlier.CommunityCount > 0);
        var refused = LabelPropagation.Run(g, [], 0, s, earlier);
        Assert.Equal((0, false, 0, 0), (refused.Rounds, refused.Converged, refused.CommunityCount, refused.ChangesPerRound.Length));
        Assert.Equal(GraphCodes.RoundsRange, LabelPropagation.Run(g, [], 0, s).Code);
        Assert.Equal(GraphCodes.RoundsRange, LabelPropagation.Run(g, [], GraphLimits.MaxRounds + 1, s).Code);
        Assert.Equal(GraphCodes.LabelsLength, LabelPropagation.Run(g, [0, 1], 3, s).Code);
        Assert.Equal(GraphCodes.LabelRange, LabelPropagation.Run(g, [0, 1, 3], 3, s).Code);
        Assert.Equal(GraphCodes.LabelRange, LabelPropagation.Run(g, [0, -1, 2], 3, s).Code);
        Assert.Equal(GraphCodes.ArgumentMissing, LabelPropagation.Run(g, [], 3, null!).Code);
        Assert.Equal("refused GRAPH_ROUNDS_RANGE", LabelPropagation.Run(g, [], 0, s).Explain());
        Assert.Equal("", LabelPropagation.Run(g, [], GraphLimits.MaxRounds, s).Code);
    }
}
