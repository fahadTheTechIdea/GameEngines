namespace EngineCommon.Graph;

/// <summary>
/// The result of a topological sort. <see cref="Order"/> holds the nodes that could be ordered (all of them when the graph is acyclic), every edge pointing from an earlier node to a later one. When the graph has a cycle
/// the code is <see cref="GraphCodes.Cycle"/>, <see cref="Order"/> holds the nodes ordered before the sort got stuck, and <see cref="Cycle"/> names one cycle: its nodes in edge direction, starting at its lowest node.
/// </summary>
public sealed class TopologicalOrderResult
{
    internal int[] order = [];

    public string Code { get; internal set; } = GraphCodes.Ok;
    public int NodeCount { get; internal set; }
    public int Count { get; internal set; }
    public ReadOnlySpan<int> Order => order.AsSpan(0, Count);

    /// <summary>One cycle when <see cref="Code"/> is <see cref="GraphCodes.Cycle"/>, else empty. A self-loop is a cycle of one node.</summary>
    public int[] Cycle { get; internal set; } = [];

    public string Explain() => Code.Length != 0 && Code != GraphCodes.Cycle ? "refused " + Code
        : Code == GraphCodes.Cycle ? "cycle of " + Cycle.Length + " nodes: " + string.Join(" > ", Cycle) + " > " + Cycle[0]
        : "ordered " + Count + " nodes";
}

/// <summary>
/// Topological order by Kahn's algorithm with a stated tie-break: among the nodes whose predecessors are all placed, the lowest node index goes first (a heap keyed by node index), so the order is the same for the same
/// graph whatever order the edges were added in. Parallel edges each count once toward the in-degree. O((V + E) log V).
/// </summary>
public static class TopologicalSort
{
    public static TopologicalOrderResult Run(CsrGraph graph, GraphScratch scratch, TopologicalOrderResult? reuse = null)
    {
        var result = reuse ?? new TopologicalOrderResult();
        var n = graph.NodeCount;
        result.NodeCount = n;
        result.Count = 0;
        result.Code = GraphCodes.Ok;
        result.Cycle = [];
        if (scratch is null) { result.Code = GraphCodes.ArgumentMissing; return result; }
        if (result.order.Length < n) result.order = new int[n];
        var waiting = scratch.Ints(0, n);
        Array.Clear(waiting, 0, n);
        for (var e = 0; e < graph.EdgeCount; e++) waiting[graph.Target(e)]++;
        var heap = scratch.Heap;
        heap.Clear();
        for (var v = 0; v < n; v++)
            if (waiting[v] == 0) heap.Push(0, v);
        while (heap.Count > 0)
        {
            var (_, u) = heap.Pop();
            result.order[result.Count++] = u;
            for (var e = graph.FirstEdge(u); e < graph.EndEdge(u); e++)
            {
                var v = graph.Target(e);
                if (--waiting[v] == 0) heap.Push(0, v);
            }
        }
        if (result.Count == n) return result;
        result.Code = GraphCodes.Cycle;
        result.Cycle = FindCycle(graph, scratch, waiting);
        return result;
    }

    /// <summary>
    /// Every node still waiting has at least one waiting predecessor (otherwise it would have been placed), so walking back from the lowest waiting node along the first waiting predecessor must repeat a node;
    /// the repeated stretch is a cycle. It is read off in edge direction and rotated to start at its lowest node.
    /// </summary>
    private static int[] FindCycle(CsrGraph graph, GraphScratch scratch, int[] waiting)
    {
        var n = graph.NodeCount;
        var before = scratch.Ints(1, n);
        Array.Fill(before, -1, 0, n);
        for (var e = 0; e < graph.EdgeCount; e++)
        {
            var from = graph.Source(e);
            var to = graph.Target(e);
            if (waiting[from] > 0 && waiting[to] > 0 && before[to] < 0) before[to] = from;
        }
        var seen = scratch.Ints(2, n);
        Array.Fill(seen, -1, 0, n);
        var start = 0;
        while (waiting[start] == 0) start++;
        var walk = new List<int>();
        var at = start;
        while (seen[at] < 0)
        {
            seen[at] = walk.Count;
            walk.Add(at);
            at = before[at];
        }
        var loop = walk.GetRange(seen[at], walk.Count - seen[at]);
        loop.Reverse(); // the walk went against the edges
        var lowest = 0;
        for (var i = 1; i < loop.Count; i++)
            if (loop[i] < loop[lowest]) lowest = i;
        var cycle = new int[loop.Count];
        for (var i = 0; i < cycle.Length; i++) cycle[i] = loop[(lowest + i) % loop.Count];
        return cycle;
    }
}
