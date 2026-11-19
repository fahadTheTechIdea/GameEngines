namespace EngineCommon.Graph;

/// <summary>
/// What a breadth-first search found. <see cref="Order"/> holds the reached nodes in the order they were reached (the first <see cref="Count"/> entries); <see cref="Hops"/> is the number of edges from the nearest
/// source (-1 for a node not reached); <see cref="ParentEdge"/> is the edge that first reached the node (-1 for a source or a node not reached). Reuse one result for repeated runs to avoid allocation.
/// </summary>
public sealed class BfsResult
{
    internal int[] hops = [];
    internal int[] parentEdge = [];
    internal int[] order = [];

    public string Code { get; internal set; } = GraphCodes.Ok;
    public int Count { get; internal set; }
    public int NodeCount { get; internal set; }
    public ReadOnlySpan<int> Hops => hops.AsSpan(0, NodeCount);
    public ReadOnlySpan<int> ParentEdge => parentEdge.AsSpan(0, NodeCount);
    public ReadOnlySpan<int> Order => order.AsSpan(0, Count);

    internal void Size(int nodes)
    {
        if (hops.Length < nodes)
        {
            hops = new int[nodes];
            parentEdge = new int[nodes];
            order = new int[nodes];
        }
        NodeCount = nodes;
        Count = 0;
    }

    /// <summary>One line a player or a log can read: how many nodes were reached and how far.</summary>
    public string Explain()
    {
        if (Code.Length != 0) return "refused " + Code;
        var deepest = 0;
        for (var i = 0; i < Count; i++) deepest = Math.Max(deepest, hops[order[i]]);
        return "reached " + Count + " of " + NodeCount + " nodes, the farthest " + deepest + " hops away";
    }
}

/// <summary>
/// Breadth-first search from one or more sources, to at most <c>maxDepth</c> edges (the k-hop reach). Neighbours are visited in CSR order (ascending target, then edge id), so the order and the parents are the same
/// for the same graph. O(V + E).
/// </summary>
public static class Bfs
{
    public static BfsResult Run(CsrGraph graph, ReadOnlySpan<int> sources, int maxDepth, BfsResult? reuse = null)
    {
        var result = reuse ?? new BfsResult();
        result.Size(graph.NodeCount);
        result.Code = GraphCodes.Ok;
        if (maxDepth < 0) { result.Code = GraphCodes.DepthRange; return result; }
        foreach (var s in sources)
            if (s < 0 || s >= graph.NodeCount) { result.Code = GraphCodes.SourceRange; return result; }
        var hops = result.hops;
        var parent = result.parentEdge;
        var order = result.order;
        Array.Fill(hops, -1, 0, graph.NodeCount);
        Array.Fill(parent, -1, 0, graph.NodeCount);
        var tail = 0;
        foreach (var s in sources)
        {
            if (hops[s] >= 0) continue;
            hops[s] = 0;
            order[tail++] = s;
        }
        for (var head = 0; head < tail; head++)
        {
            var u = order[head];
            if (hops[u] >= maxDepth) continue;
            for (var e = graph.FirstEdge(u); e < graph.EndEdge(u); e++)
            {
                var v = graph.Target(e);
                if (hops[v] >= 0) continue;
                hops[v] = hops[u] + 1;
                parent[v] = e;
                order[tail++] = v;
            }
        }
        result.Count = tail;
        return result;
    }
}

/// <summary>The weakly connected components: each node's component is the lowest node id in it, so the labels do not depend on the order edges were added in.</summary>
public sealed class ComponentsResult
{
    internal int[] component = [];

    public string Code { get; internal set; } = GraphCodes.Ok;
    public int NodeCount { get; internal set; }
    /// <summary>The number of components.</summary>
    public int Count { get; internal set; }
    public ReadOnlySpan<int> Component => component.AsSpan(0, NodeCount);

    public string Explain() => Code.Length != 0 ? "refused " + Code : Count + " components over " + NodeCount + " nodes";
}

public static class Components
{
    /// <summary>Weakly connected components (edge direction ignored) by union-find that always keeps the lower root. O((V + E) alpha(V)).</summary>
    public static ComponentsResult Run(CsrGraph graph, ComponentsResult? reuse = null)
    {
        var result = reuse ?? new ComponentsResult();
        result.Code = GraphCodes.Ok;
        var n = graph.NodeCount;
        result.NodeCount = n;
        if (result.component.Length < n) result.component = new int[n];
        var parent = result.component;
        for (var i = 0; i < n; i++) parent[i] = i;
        for (var e = 0; e < graph.EdgeCount; e++)
        {
            var a = Find(parent, graph.Source(e));
            var b = Find(parent, graph.Target(e));
            if (a < b) parent[b] = a;
            else if (b < a) parent[a] = b;
        }
        var count = 0;
        for (var i = 0; i < n; i++)
        {
            parent[i] = Find(parent, i);
            if (parent[i] == i) count++;
        }
        result.Count = count;
        return result;
    }

    private static int Find(int[] parent, int x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }
        return x;
    }
}
