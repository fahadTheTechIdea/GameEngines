namespace EngineCommon.Graph;

/// <summary>
/// Bellman-Ford single-source shortest paths on integer weights that may be negative: up to V passes over all edges in edge-id order, stopping when a pass changes nothing. If a V-th pass still relaxes an edge, a
/// negative cycle is reachable from the source: the first node relaxed in that pass is walked back V parent steps (which lands on the cycle), the cycle is read off and rotated to start at its lowest node id, and no
/// distance is final. Θ(V·E) worst case. Distances inside a negative-cycle search can fall by up to one maximum weight per relaxation, so a graph where V · E · max|weight| could pass half of the 64-bit range is
/// refused (the 10,000-node, 200,000-edge, 10^9-weight corner is 2·10^18, inside it).
/// </summary>
public static class BellmanFord
{
    public static ShortestPaths Run(CsrGraph graph, int source, GraphScratch scratch, ShortestPaths? reuse = null)
    {
        var result = reuse ?? new ShortestPaths();
        result.Prepare(graph.NodeCount, source, "bellman-ford");
        if (scratch is null) { result.Code = GraphCodes.ArgumentMissing; return result; }
        if (source < 0 || source >= graph.NodeCount) { result.Code = GraphCodes.SourceRange; return result; }
        var n = graph.NodeCount;
        if ((Int128)n * Math.Max(graph.EdgeCount, 1) * Math.Max(graph.MaxAbsWeight, 1) > long.MaxValue / 2) { result.Code = GraphCodes.BellmanFordTooLarge; return result; }
        var dist = result.dist;
        var parent = result.parentEdge;
        dist[source] = 0;
        var firstRelaxed = -1;
        for (var pass = 1; pass <= n; pass++)
        {
            firstRelaxed = -1;
            for (var u = 0; u < n; u++)
            {
                if (dist[u] == ShortestPaths.Unreached) continue;
                for (var e = graph.FirstEdge(u); e < graph.EndEdge(u); e++)
                {
                    var v = graph.Target(e);
                    var through = dist[u] + graph.Weight(e);
                    if (through >= dist[v]) continue;
                    dist[v] = through;
                    parent[v] = e;
                    if (firstRelaxed < 0) firstRelaxed = v;
                }
            }
            if (firstRelaxed < 0) break;
        }
        if (firstRelaxed < 0)
        {
            for (var v = 0; v < n; v++) result.final[v] = dist[v] != ShortestPaths.Unreached;
            return result;
        }
        result.HasNegativeCycle = true;
        ReadCycle(graph, result, firstRelaxed);
        return result;
    }

    private static void ReadCycle(CsrGraph graph, ShortestPaths result, int start)
    {
        var on = start;
        for (var i = 0; i < graph.NodeCount; i++) on = graph.Source(result.parentEdge[on]);
        var length = 0;
        var at = on;
        do { at = graph.Source(result.parentEdge[at]); length++; } while (at != on);
        // Walk the parent edges backwards from a node on the cycle, then lay them out forwards starting at the lowest node.
        var backNodes = new int[length];
        var backEdges = new int[length];
        at = on;
        for (var i = 0; i < length; i++)
        {
            backNodes[i] = at;
            backEdges[i] = result.parentEdge[at];
            at = graph.Source(backEdges[i]);
        }
        var lowest = 0;
        for (var i = 1; i < length; i++) if (backNodes[i] < backNodes[lowest]) lowest = i;
        var nodes = new int[length];
        var edges = new int[length];
        long cost = 0;
        for (var i = 0; i < length; i++)
        {
            // forward order is the reverse of the backward walk: the edge that enters backNodes[k - 1] leaves backNodes[k]
            var k = (lowest - i + length) % length;
            var leaving = backEdges[(k - 1 + length) % length];
            nodes[i] = backNodes[k];
            edges[i] = leaving;
            cost += graph.Weight(leaving);
        }
        result.CycleNodes = nodes;
        result.CycleEdges = edges;
        result.CycleCost = cost;
    }
}
