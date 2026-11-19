using System.Numerics;
using Xunit;

namespace EngineCommon.Graph.Tests;

/// <summary>A fixed-seed generator for the tests (splitmix64): the same numbers on every machine and run.</summary>
internal sealed class Rng(ulong seed)
{
    private ulong state = seed;

    public ulong Next()
    {
        state += 0x9E3779B97F4A7C15UL;
        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public int Below(int bound) => (int)(Next() % (ulong)bound);
    public long Between(long low, long high) => low + (long)(Next() % (ulong)(high - low + 1));
}

internal readonly record struct E(int From, int To, long Weight, long Capacity);

internal static class Fx
{
    public static CsrGraph Make(int nodes, params E[] edges) => Make(nodes, (IEnumerable<E>)edges);

    public static CsrGraph Make(int nodes, IEnumerable<E> edges)
    {
        Assert.Equal("", GraphBuilder.Create(nodes, out var builder));
        foreach (var e in edges) Assert.Equal("", builder!.Add(e.From, e.To, e.Weight, e.Capacity));
        var built = builder!.Build();
        Assert.True(built.Ok);
        return built.Graph!;
    }

    public static E W(int from, int to, long weight) => new(from, to, weight, 0);
    public static E C(int from, int to, long capacity, long cost) => new(from, to, cost, capacity);

    public static List<E> RandomEdges(Rng rng, int nodes, int count, long minWeight, long maxWeight, long maxCapacity)
    {
        var list = new List<E>();
        for (var i = 0; i < count; i++) list.Add(new E(rng.Below(nodes), rng.Below(nodes), rng.Between(minWeight, maxWeight), maxCapacity == 0 ? 0 : rng.Between(0, maxCapacity)));
        return list;
    }

    public static List<E> Shuffled(Rng rng, List<E> edges)
    {
        var copy = new List<E>(edges);
        for (var i = copy.Count - 1; i > 0; i--)
        {
            var j = rng.Below(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return copy;
    }

    public static bool SameGraph(CsrGraph a, CsrGraph b)
    {
        if (a.NodeCount != b.NodeCount || a.EdgeCount != b.EdgeCount) return false;
        for (var e = 0; e < a.EdgeCount; e++)
            if (a.Source(e) != b.Source(e) || a.Target(e) != b.Target(e) || a.Weight(e) != b.Weight(e) || a.Capacity(e) != b.Capacity(e)) return false;
        return true;
    }

    /// <summary>All-pairs distances by Floyd-Warshall, an independent oracle: long.MaxValue is "no path". Valid when no negative cycle exists on a path between the nodes asked about.</summary>
    public static long[,] FloydWarshall(CsrGraph g)
    {
        var n = g.NodeCount;
        var d = new long[n, n];
        for (var i = 0; i < n; i++) for (var j = 0; j < n; j++) d[i, j] = i == j ? 0 : long.MaxValue;
        for (var e = 0; e < g.EdgeCount; e++) if (g.Weight(e) < d[g.Source(e), g.Target(e)]) d[g.Source(e), g.Target(e)] = g.Weight(e);
        for (var k = 0; k < n; k++)
            for (var i = 0; i < n; i++)
            {
                if (d[i, k] == long.MaxValue) continue;
                for (var j = 0; j < n; j++)
                {
                    if (d[k, j] == long.MaxValue) continue;
                    if (d[i, k] + d[k, j] < d[i, j]) d[i, j] = d[i, k] + d[k, j];
                }
            }
        return d;
    }

    /// <summary>Reachability by repeated squaring of the adjacency matrix (independent of the BFS under test).</summary>
    public static bool[,] Reach(CsrGraph g)
    {
        var n = g.NodeCount;
        var r = new bool[n, n];
        for (var i = 0; i < n; i++) r[i, i] = true;
        for (var e = 0; e < g.EdgeCount; e++) r[g.Source(e), g.Target(e)] = true;
        for (var k = 0; k < n; k++) for (var i = 0; i < n; i++) if (r[i, k]) for (var j = 0; j < n; j++) if (r[k, j]) r[i, j] = true;
        return r;
    }

    /// <summary>The best (value, then least cost) integer flow by trying every assignment of 0..capacity to every edge. Only for a handful of edges.</summary>
    public static (long Flow, long Cost) BruteForceFlow(CsrGraph g, int s, int t)
    {
        var m = g.EdgeCount;
        var flow = new long[m];
        long bestFlow = -1;
        long bestCost = 0;
        void Walk(int e)
        {
            if (e == m)
            {
                var net = new long[g.NodeCount];
                long cost = 0;
                for (var i = 0; i < m; i++) { net[g.Source(i)] -= flow[i]; net[g.Target(i)] += flow[i]; cost += flow[i] * g.Weight(i); }
                for (var v = 0; v < g.NodeCount; v++) if (v != s && v != t && net[v] != 0) return;
                var value = -net[s] - 0; // a self-loop adds nothing to the net, so this is the flow leaving s
                if (value > bestFlow || (value == bestFlow && cost < bestCost)) { bestFlow = value; bestCost = cost; }
                return;
            }
            for (long f = 0; f <= g.Capacity(e); f++) { flow[e] = f; Walk(e + 1); }
            flow[e] = 0;
        }
        Walk(0);
        return (bestFlow, bestCost);
    }

    public static BigInteger Big(long v) => new(v);
}
