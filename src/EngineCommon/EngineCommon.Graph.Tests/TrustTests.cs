using System.Numerics;
using Xunit;
using static EngineCommon.Graph.Tests.Fx;

namespace EngineCommon.Graph.Tests;

public sealed class TrustTests
{
    private const long S = TrustIteration.Scale;

    /// <summary>The same stated arithmetic written independently: dense BigInteger matrices, LINQ ordering, no scratch, one edge per (from, to) pair.</summary>
    private static long[] Oracle(CsrGraph g, long[] pre, long alpha, int iterations)
    {
        var n = g.NodeCount;
        var scale = new BigInteger(S);
        var c = new BigInteger[n, n];
        var dangling = new bool[n];
        for (var i = 0; i < n; i++)
        {
            var row = Enumerable.Range(0, n).Select(j => (J: j, W: Enumerable.Range(0, g.EdgeCount).Where(e => g.Source(e) == i && g.Target(e) == j).Select(e => Math.Max(g.Weight(e), 0)).DefaultIfEmpty(0).Sum())).ToList();
            var sum = new BigInteger(row.Sum(x => x.W));
            if (sum.IsZero) { dangling[i] = true; continue; }
            var floors = row.Select(x => BigInteger.Divide(x.W * scale, sum)).ToArray();
            var rems = row.Select(x => BigInteger.Remainder(x.W * scale, sum)).ToArray();
            var deficit = (int)(scale - floors.Aggregate(BigInteger.Zero, (a, b) => a + b));
            foreach (var k in Enumerable.Range(0, n).OrderByDescending(k => rems[k]).ThenBy(k => k).Take(deficit)) floors[k] += 1;
            for (var j = 0; j < n; j++) c[i, j] = floors[j];
        }
        var t = pre.Select(v => new BigInteger(v)).ToArray();
        var p = pre.Select(v => new BigInteger(v)).ToArray();
        var a = new BigInteger(alpha);
        var keep = scale - a;
        for (var it = 0; it < iterations; it++)
        {
            var next = new BigInteger[n];
            var dmass = BigInteger.Zero;
            for (var i = 0; i < n; i++)
            {
                if (dangling[i]) { dmass += t[i]; continue; }
                for (var j = 0; j < n; j++) next[j] += keep * c[i, j] * t[i];
            }
            var exact = new BigInteger[n];
            for (var j = 0; j < n; j++) exact[j] = next[j] + keep * p[j] * dmass + a * scale * p[j];
            var unit = scale * scale;
            var q = exact.Select(x => BigInteger.Divide(x, unit)).ToArray();
            var r = exact.Select(x => BigInteger.Remainder(x, unit)).ToArray();
            var deficit = (int)(scale - q.Aggregate(BigInteger.Zero, (x, y) => x + y));
            foreach (var j in Enumerable.Range(0, n).OrderByDescending(j => r[j]).ThenBy(j => j).Take(deficit)) q[j] += 1;
            t = q;
        }
        return t.Select(v => (long)v).ToArray();
    }

    private static long[] Uniform(int n) => Enumerable.Range(0, n).Select(i => S / n + (i < S % n ? 1 : 0)).ToArray();

    [Fact]
    public void LocalTrustClipsNegativesAndEachRowSumsToExactlyTheScale()
    {
        var g = Make(4, W(0, 1, 1), W(0, 2, 1), W(0, 3, 1), W(1, 0, 5), W(1, 2, -3), W(2, 2, 0));
        var r = TrustIteration.Run(g, [], 100_000_000, 1, 0, new GraphScratch());
        Assert.Equal("", r.Code);
        Assert.Equal(new[] { 333_333_334L, 333_333_333, 333_333_333, S, 0, 0 }, r.LocalTrust.ToArray());
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var rng = new Rng(seed);
            var h = Make(8, RandomEdges(rng, 8, 30, -3, 100, 0));
            var t = TrustIteration.Run(h, [], 150_000_000, 3, 0, new GraphScratch());
            for (var i = 0; i < 8; i++)
            {
                long row = 0;
                var positive = false;
                for (var e = h.FirstEdge(i); e < h.EndEdge(i); e++)
                {
                    row += t.LocalTrust[e];
                    if (h.Weight(e) <= 0) Assert.Equal(0, t.LocalTrust[e]);
                    else positive = true;
                }
                Assert.Equal(positive ? S : 0, row);
            }
        }
    }

    [Fact]
    public void ADanglingNodeHandsItsMassToThePreTrustAsAHandSolvedFixedPointShows()
    {
        // 0 trusts 1; 1 trusts nobody so its row is the pre-trust (all on node 0). With a = 0.2: t0 = 0.8 t1 + 0.2, t1 = 0.8 t0, so t0 = 0.2 / 0.36 = 0.5555..., t1 = 0.4444...
        var g = Make(2, W(0, 1, 7));
        var r = TrustIteration.Run(g, [S, 0], 200_000_000, 500, 20, new GraphScratch());
        Assert.True(r.Converged);
        Assert.Equal(S, r.Scores.ToArray().Sum());
        Assert.InRange(r.Scores[0], 555_555_540, 555_555_570);
        Assert.InRange(r.Scores[1], 444_444_430, 444_444_460);
        Assert.Contains("converged after", r.Explain());
    }

    [Fact]
    public void ARingWithUniformPreTrustStaysUniformToWithinRounding()
    {
        var g = Make(3, W(0, 1, 1), W(1, 2, 1), W(2, 0, 1));
        var r = TrustIteration.Run(g, [], 150_000_000, 200, 20, new GraphScratch());
        Assert.True(r.Converged);
        Assert.Equal(S, r.Scores.ToArray().Sum());
        foreach (var v in r.Scores.ToArray()) Assert.InRange(v, S / 3 - 10, S / 3 + 10);
    }

    [Fact]
    public void ScoresAreAnExactMatchForTheIndependentBigIntegerOracle()
    {
        var scratch = new GraphScratch();
        var result = new TrustResult();
        for (ulong seed = 1; seed <= 150; seed++)
        {
            var rng = new Rng(seed);
            var n = 2 + rng.Below(8);
            var pairs = new HashSet<(int, int)>();
            var edges = new List<E>();
            for (var i = 0; i < 3 * n; i++)
            {
                var from = rng.Below(n);
                var to = rng.Below(n);
                if (pairs.Add((from, to))) edges.Add(W(from, to, rng.Between(-2, 50)));
            }
            var g = Make(n, edges);
            var pre = rng.Below(2) == 0 ? Uniform(n) : SkewedPre(rng, n);
            var alpha = rng.Below(4) == 0 ? 0 : rng.Between(1, S);
            var iterations = 1 + rng.Below(12);
            TrustIteration.Run(g, pre, alpha, iterations, 0, scratch, result);
            Assert.Equal(Oracle(g, pre, alpha, result.Iterations), result.Scores.ToArray());
            Assert.Equal(S, result.Scores.ToArray().Sum());
        }
    }

    private static long[] SkewedPre(Rng rng, int n)
    {
        var pre = new long[n];
        var left = S;
        for (var i = 0; i < n - 1; i++) { pre[i] = rng.Between(0, left / 2); left -= pre[i]; }
        pre[n - 1] = left;
        return pre;
    }

    [Fact]
    public void WithAPositivePreTrustWeightTheResidualFallsGeometricallyToTheRoundingFloor()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var rng = new Rng(seed);
            var n = 30;
            var g = Make(n, RandomEdges(rng, n, 120, 1, 30, 0));
            const long alpha = 200_000_000;
            foreach (var k in new[] { 3, 8, 20, 40 })
            {
                var r = TrustIteration.Run(g, [], alpha, k, 0, new GraphScratch());
                var bound = (long)(2 * new BigInteger(S) * BigInteger.Pow(8, k - 1) / BigInteger.Pow(10, k - 1)) + 10 * n;
                Assert.True(r.Residual <= bound, "seed " + seed + " k " + k + " residual " + r.Residual + " bound " + bound);
            }
            var done = TrustIteration.Run(g, [], alpha, 300, 20 * n, new GraphScratch());
            Assert.True(done.Converged);
            Assert.Equal(S, done.Scores.ToArray().Sum());
        }
    }

    [Fact]
    public void FullPreTrustWeightGivesThePreTrustAndNoPreTrustWeightFollowsTheEdges()
    {
        var g = Make(3, W(0, 1, 1), W(1, 2, 1), W(2, 0, 1));
        var pre = new[] { S, 0L, 0L };
        var all = TrustIteration.Run(g, pre, S, 5, 0, new GraphScratch());
        Assert.Equal(pre, all.Scores.ToArray());
        Assert.Equal(1, all.Iterations);
        Assert.True(all.Converged);
        var none = TrustIteration.Run(g, pre, 0, 1, 0, new GraphScratch());
        Assert.Equal(new[] { 0L, S, 0L }, none.Scores.ToArray());
        Assert.False(none.Converged);
        Assert.Equal(2 * S, none.Residual);
        Assert.StartsWith("stopped after 1 iterations", none.Explain());
    }

    [Fact]
    public void ContributorsNameWhoTrustsANode()
    {
        var g = Make(4, W(0, 3, 3), W(1, 3, 1), W(2, 3, 1), W(2, 0, 1));
        var r = TrustIteration.Run(g, [], 150_000_000, 50, 0, new GraphScratch());
        var top = r.Contributors(g, 3, 2);
        Assert.Equal(2, top.Count);
        Assert.Equal(0, top[0].From);
        Assert.Equal(g.Source(top[0].Edge), top[0].From);
        Assert.True(top[0].Amount >= top[1].Amount);
        foreach (var c in r.Contributors(g, 3, 10)) Assert.Equal((long)((System.Int128)r.LocalTrust[c.Edge] * r.Scores[c.From] / S), c.Amount);
        Assert.Equal(3, r.Contributors(g, 3, 10).Count);
        Assert.Empty(r.Contributors(g, 1, 3));
    }

    [Fact]
    public void EmptyAndEdgelessGraphsAreHandled()
    {
        var spent = TrustIteration.Run(Make(2, W(0, 1, 1)), [], 100_000_000, 1, 0, new GraphScratch());
        Assert.True(spent.Residual > 0);
        Assert.Equal(0, TrustIteration.Run(Make(0), [], 1, 1, 0, new GraphScratch(), spent).Residual);
        Assert.Equal(0, TrustIteration.Run(Make(2), [], -1, 1, 0, new GraphScratch(), spent).Residual);
        var star = Make(60, Enumerable.Range(1, 50).Select(i => W(0, i, 1)));
        var starScores = TrustIteration.Run(star, [], 100_000_000, 3, 0, new GraphScratch());
        Assert.Equal(S, starScores.Scores.ToArray().Sum());
        Assert.Equal(50, starScores.LocalTrust.ToArray().Count(v => v >= S / 50));
        var empty = TrustIteration.Run(Make(0), [], 1, 1, 0, new GraphScratch());
        Assert.True(empty.Converged);
        Assert.Equal(0, empty.Scores.Length);
        var lone = TrustIteration.Run(Make(1), [], 1, 5, 0, new GraphScratch());
        Assert.Equal(new[] { S }, lone.Scores.ToArray());
        var edgeless = TrustIteration.Run(Make(3), [], 100_000_000, 10, 0, new GraphScratch());
        Assert.Equal(Uniform(3), edgeless.Scores.ToArray());
    }

    [Fact]
    public void TrustRefusesBadInputWithCodes()
    {
        var g = Make(2, W(0, 1, 1));
        var s = new GraphScratch();
        Assert.Equal(GraphCodes.PreTrustLength, TrustIteration.Run(g, [S], 1, 1, 0, s).Code);
        Assert.Equal(GraphCodes.PreTrustNegative, TrustIteration.Run(g, [-1, S + 1], 1, 1, 0, s).Code);
        Assert.Equal(GraphCodes.PreTrustSum, TrustIteration.Run(g, [S, 1], 1, 1, 0, s).Code);
        Assert.Equal(GraphCodes.PreTrustSum, TrustIteration.Run(g, [1, 1], 1, 1, 0, s).Code);
        Assert.Equal(GraphCodes.AlphaRange, TrustIteration.Run(g, [], -1, 1, 0, s).Code);
        Assert.Equal(GraphCodes.AlphaRange, TrustIteration.Run(g, [], S + 1, 1, 0, s).Code);
        Assert.Equal(GraphCodes.IterationsRange, TrustIteration.Run(g, [], 1, 0, 0, s).Code);
        Assert.Equal(GraphCodes.IterationsRange, TrustIteration.Run(g, [], 1, GraphLimits.MaxIterations + 1, 0, s).Code);
        Assert.Equal(GraphCodes.EpsilonRange, TrustIteration.Run(g, [], 1, 1, -1, s).Code);
        Assert.Equal(GraphCodes.ArgumentMissing, TrustIteration.Run(g, [], 1, 1, 0, null!).Code);
        Assert.Equal("refused GRAPH_ALPHA_RANGE", TrustIteration.Run(g, [], -1, 1, 0, s).Explain());
        Assert.Equal("", TrustIteration.Run(g, [], 0, GraphLimits.MaxIterations, 0, s).Code);
        Assert.Equal("", TrustIteration.Run(g, [], S, 1, 0, s).Code);
    }
}
