namespace EngineCommon.Graph;

/// <summary>One path and its cost: the nodes from source to target and the edge ids between them (one fewer than the nodes).</summary>
public sealed record PathResult(bool Found, long Cost, int[] Nodes, int[] Edges)
{
    public string Explain() => Found ? "path of " + Edges.Length + " edges, cost " + Cost + ": " + string.Join(" > ", Nodes) : "no path";
}

/// <summary>
/// The single-source shortest-path tree that Dijkstra and Bellman-Ford fill: a distance and a parent edge for each node, which nodes are final (Dijkstra stops early when a target is set, so only the settled nodes are final;
/// Bellman-Ford makes every reached node final when it finds no negative cycle) and, for Bellman-Ford, the negative cycle it found. Reuse one object for repeated runs to avoid allocation.
/// </summary>
public sealed class ShortestPaths
{
    public const long Unreached = long.MaxValue;

    internal long[] dist = [];
    internal int[] parentEdge = [];
    internal bool[] final = [];
    internal int[] settleOrder = [];

    public string Code { get; internal set; } = GraphCodes.Ok;
    /// <summary>"dijkstra" or "bellman-ford".</summary>
    public string Algorithm { get; internal set; } = "";
    public int Source { get; internal set; }
    public int NodeCount { get; internal set; }
    /// <summary>Dijkstra: how many nodes were settled, in <see cref="SettleOrder"/>.</summary>
    public int SettledCount { get; internal set; }
    public bool HasNegativeCycle { get; internal set; }
    /// <summary>The negative cycle found (Bellman-Ford): its nodes in walking order starting at the lowest node id, and the edge from each node to the next. Empty when there is none.</summary>
    public int[] CycleNodes { get; internal set; } = [];
    public int[] CycleEdges { get; internal set; } = [];
    public long CycleCost { get; internal set; }

    public ReadOnlySpan<long> Distances => dist.AsSpan(0, NodeCount);
    public ReadOnlySpan<int> SettleOrder => settleOrder.AsSpan(0, SettledCount);
    public long Distance(int node) => dist[node];
    public int ParentEdge(int node) => parentEdge[node];
    public bool IsFinal(int node) => final[node];

    internal void Prepare(int nodes, int source, string algorithm)
    {
        if (dist.Length < nodes)
        {
            dist = new long[nodes];
            parentEdge = new int[nodes];
            final = new bool[nodes];
            settleOrder = new int[nodes];
        }
        Array.Fill(dist, Unreached, 0, nodes);
        Array.Fill(parentEdge, -1, 0, nodes);
        Array.Fill(final, false, 0, nodes);
        Code = GraphCodes.Ok;
        Algorithm = algorithm;
        Source = source;
        NodeCount = nodes;
        SettledCount = 0;
        HasNegativeCycle = false;
        CycleNodes = [];
        CycleEdges = [];
        CycleCost = 0;
    }

    /// <summary>The path from the source to the target by the parent edges, or "no path" when the target is not final (not reached, not yet settled, or a negative cycle was found).</summary>
    public PathResult PathTo(CsrGraph graph, int target)
    {
        if (Code.Length != 0 || target < 0 || target >= NodeCount || !final[target]) return new PathResult(false, 0, [], []);
        var length = 0;
        for (var v = target; v != Source; v = graph.Source(parentEdge[v])) length++;
        var nodes = new int[length + 1];
        var edges = new int[length];
        var at = target;
        for (var i = length; i > 0; i--)
        {
            nodes[i] = at;
            edges[i - 1] = parentEdge[at];
            at = graph.Source(parentEdge[at]);
        }
        nodes[0] = Source;
        return new PathResult(true, dist[target], nodes, edges);
    }

    public string Explain()
    {
        if (Code.Length != 0) return "refused " + Code;
        if (HasNegativeCycle) return Algorithm + ": negative cycle of cost " + CycleCost + " through " + string.Join(" > ", CycleNodes);
        var reached = 0;
        for (var i = 0; i < NodeCount; i++) if (final[i]) reached++;
        return Algorithm + " from " + Source + ": " + reached + " of " + NodeCount + " nodes final";
    }
}
