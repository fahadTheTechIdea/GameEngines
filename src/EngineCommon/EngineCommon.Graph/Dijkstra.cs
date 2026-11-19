namespace EngineCommon.Graph;

/// <summary>
/// Dijkstra's single-source shortest paths on non-negative integer weights, with a binary heap keyed by (cost, node index).
/// Tie-break, stated: the heap pops the smallest cost, and among equal costs the lowest node index; a node's parent is the first edge, in pop order and then CSR order (ascending target, then weight, then capacity),
/// that reaches its final cost, because a later edge replaces a parent only when it is strictly cheaper. The tree is therefore the same for the same graph whatever order the edges were added in.
/// O((V + E) log E); the heap holds at most one entry per improvement, so at most E + 1 entries.
/// </summary>
public static class Dijkstra
{
    /// <summary>Runs from <paramref name="source"/>; with <paramref name="target"/> 0 or more it stops when that node is settled (-1 settles everything reachable).</summary>
    public static ShortestPaths Run(CsrGraph graph, int source, int target, GraphScratch scratch, ShortestPaths? reuse = null)
    {
        var result = reuse ?? new ShortestPaths();
        result.Prepare(graph.NodeCount, source, "dijkstra");
        if (scratch is null) { result.Code = GraphCodes.ArgumentMissing; return result; }
        if (source < 0 || source >= graph.NodeCount) { result.Code = GraphCodes.SourceRange; return result; }
        if (target < -1 || target >= graph.NodeCount) { result.Code = GraphCodes.SinkRange; return result; }
        if (graph.HasNegativeWeight) { result.Code = GraphCodes.NegativeWeight; return result; }
        var heap = scratch.Heap;
        heap.Clear();
        var dist = result.dist;
        dist[source] = 0;
        heap.Push(0, source);
        while (heap.Count > 0)
        {
            var (cost, u) = heap.Pop();
            if (cost != dist[u]) continue; // a stale entry: the node was reached more cheaply after this was pushed
            result.final[u] = true;
            result.settleOrder[result.SettledCount++] = u;
            if (u == target) break;
            for (var e = graph.FirstEdge(u); e < graph.EndEdge(u); e++)
            {
                var v = graph.Target(e);
                var through = cost + graph.Weight(e);
                if (through >= dist[v]) continue;
                dist[v] = through;
                result.parentEdge[v] = e;
                heap.Push(through, v);
            }
        }
        return result;
    }
}
