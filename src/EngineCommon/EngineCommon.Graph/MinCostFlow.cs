namespace EngineCommon.Graph;

/// <summary>One augmentation of the successive-shortest-path method: <see cref="Amount"/> units pushed along the cheapest residual path, at <see cref="UnitCost"/> per unit. <see cref="Arcs"/> are residual arcs: 2e is edge e used forward, 2e + 1 is edge e's flow cancelled.</summary>
public sealed record FlowAugmentation(long Amount, long UnitCost, int[] Arcs);

/// <summary>One path of the final flow: <see cref="Amount"/> units along <see cref="Edges"/> (edge ids, in walking order).</summary>
public sealed record FlowPath(long Amount, int[] Edges);

/// <summary>The final flow split into source-to-sink paths. Flow on a cycle met while walking is cancelled and counted (<see cref="CancelledCycleFlow"/>, in units of cycle flow); flow that is on no source-to-sink walk is the circulation left in <see cref="LeftoverFlow"/>.</summary>
public sealed record FlowDecomposition(List<FlowPath> Paths, long CancelledCycleFlow, long LeftoverFlow);

/// <summary>The outcome of a min-cost max-flow run. <see cref="EdgeFlow"/> is the flow on each edge id; <see cref="Cost"/> is the sum of flow x weight over edges (128-bit).</summary>
public sealed class FlowResult
{
    public string Code { get; internal set; } = GraphCodes.Ok;
    public long Flow { get; internal set; }
    public Int128 Cost { get; internal set; }
    public int Source { get; internal set; }
    public int Sink { get; internal set; }
    /// <summary>True when the run stopped at the augmentation limit before it ran out of augmenting paths or reached the flow limit.</summary>
    public bool Truncated { get; internal set; }
    public long[] EdgeFlow { get; internal set; } = [];
    public List<FlowAugmentation> Augmentations { get; internal set; } = [];

    public string Explain()
    {
        if (Code.Length != 0) return "refused " + Code;
        var text = "flow " + Flow + " from " + Source + " to " + Sink + " at total cost " + Cost + " in " + Augmentations.Count + " augmentations";
        return Truncated ? text + " (stopped at the augmentation limit)" : text;
    }

    /// <summary>Splits the final flow into paths from the source to the sink. Deterministic: at each node the lowest edge id with flow left is taken first. O(paths x path length x degree).</summary>
    public FlowDecomposition Decompose(CsrGraph graph)
    {
        var remaining = (long[])EdgeFlow.Clone();
        var paths = new List<FlowPath>();
        var walkEdges = new List<int>();
        var position = new int[graph.NodeCount];
        Array.Fill(position, -1);
        long cancelled = 0;
        var at = Source;
        position[at] = 0;
        while (true)
        {
            if (at == Sink)
            {
                var amount = long.MaxValue;
                foreach (var e in walkEdges) amount = Math.Min(amount, remaining[e]);
                foreach (var e in walkEdges) remaining[e] -= amount;
                paths.Add(new FlowPath(amount, walkEdges.ToArray()));
                foreach (var e in walkEdges) position[graph.Target(e)] = -1;
                walkEdges.Clear();
                at = Source;
                continue;
            }
            var next = -1;
            for (var e = graph.FirstEdge(at); e < graph.EndEdge(at); e++)
                if (remaining[e] > 0) { next = e; break; }
            if (next < 0) break; // flow conservation: nothing is left to leave this node, so this is the source with nothing left
            var to = graph.Target(next);
            if (to != Source && position[to] >= 0)
            {
                // a cycle: edges from position[to] to the end, then `next`; cancel its bottleneck and walk on from where the cycle began
                var from = position[to];
                var amount = remaining[next];
                for (var i = from; i < walkEdges.Count; i++) amount = Math.Min(amount, remaining[walkEdges[i]]);
                for (var i = from; i < walkEdges.Count; i++) remaining[walkEdges[i]] -= amount;
                remaining[next] -= amount;
                cancelled += amount;
                for (var i = from; i < walkEdges.Count; i++) position[graph.Target(walkEdges[i])] = -1;
                walkEdges.RemoveRange(from, walkEdges.Count - from);
                at = to;
                continue;
            }
            if (to == Source)
            {
                // back at the source: a cycle through the source
                var amount = remaining[next];
                foreach (var e in walkEdges) amount = Math.Min(amount, remaining[e]);
                foreach (var e in walkEdges) remaining[e] -= amount;
                remaining[next] -= amount;
                cancelled += amount;
                foreach (var e in walkEdges) position[graph.Target(e)] = -1;
                walkEdges.Clear();
                at = Source;
                continue;
            }
            walkEdges.Add(next);
            position[to] = walkEdges.Count;
            at = to;
        }
        long leftover = 0;
        foreach (var r in remaining) leftover += r;
        return new FlowDecomposition(paths, cancelled, leftover);
    }
}

/// <summary>
/// Minimum-cost maximum-flow by successive shortest paths with node potentials. Edge weight is the cost per unit (non-negative), edge capacity the limit; a multi-source or multi-sink problem is a single source and
/// sink the caller joins to the others with zero-cost edges of the supply or demand as capacity. Each round runs Dijkstra on reduced costs (ties by node index, as in <see cref="Dijkstra"/>) in the residual graph, stops
/// when the sink is settled, raises every potential by min(distance, distance to the sink) and pushes the bottleneck along the path. Integer capacities give integer flows, and the result is a flow of the largest
/// value (or the given limit) with the least total cost. Residual arcs of a node list its forward arcs before its backward ones, each by ascending edge id, so the run is the same for the same graph.
/// At most <c>maxAugmentations</c> rounds; each saturates an arc or reaches the limit, and the limit makes the worst case a stated bound rather than the capacity values.
/// </summary>
public static class MinCostFlow
{
    public static FlowResult Run(CsrGraph graph, int source, int sink, long maxFlow, int maxAugmentations, GraphScratch scratch)
    {
        var result = new FlowResult { Source = source, Sink = sink };
        if (scratch is null) { result.Code = GraphCodes.ArgumentMissing; return result; }
        if (source < 0 || source >= graph.NodeCount) { result.Code = GraphCodes.SourceRange; return result; }
        if (sink < 0 || sink >= graph.NodeCount) { result.Code = GraphCodes.SinkRange; return result; }
        if (source == sink) { result.Code = GraphCodes.SameSourceSink; return result; }
        if (graph.HasNegativeWeight) { result.Code = GraphCodes.NegativeWeight; return result; }
        if (maxFlow < 0) { result.Code = GraphCodes.FlowLimitRange; return result; }
        if (maxAugmentations < 1 || maxAugmentations > GraphLimits.MaxAugmentations) { result.Code = GraphCodes.AugmentationsRange; return result; }

        var n = graph.NodeCount;
        var m = graph.EdgeCount;
        var start = scratch.Ints(0, n + 1);
        var arcs = scratch.Ints(1, 2 * m);
        var cursor = scratch.Ints(2, n);
        var parentArc = scratch.Ints(3, n);
        var residual = scratch.Longs(0, 2 * m);
        var potential = scratch.Longs(1, n);
        var dist = scratch.Longs(2, n);
        BuildResidual(graph, start, arcs, cursor, residual);
        Array.Fill(potential, 0L, 0, n);

        var heap = scratch.Heap;
        long flow = 0;
        Int128 cost = 0;
        while (flow < maxFlow)
        {
            if (result.Augmentations.Count >= maxAugmentations) { result.Truncated = true; break; }
            heap.Clear();
            Array.Fill(dist, ShortestPaths.Unreached, 0, n);
            Array.Fill(parentArc, -1, 0, n);
            dist[source] = 0;
            heap.Push(0, source);
            while (heap.Count > 0)
            {
                var (d, u) = heap.Pop();
                if (d != dist[u]) continue;
                if (u == sink) break;
                for (var k = start[u]; k < start[u + 1]; k++)
                {
                    var arc = arcs[k];
                    if (residual[arc] <= 0) continue;
                    var v = Head(graph, arc);
                    var through = d + ArcCost(graph, arc) + potential[u] - potential[v];
                    if (through >= dist[v]) continue;
                    dist[v] = through;
                    parentArc[v] = arc;
                    heap.Push(through, v);
                }
            }
            if (dist[sink] == ShortestPaths.Unreached) break;
            var cap = dist[sink];
            for (var v = 0; v < n; v++) potential[v] += Math.Min(dist[v], cap);

            var amount = maxFlow - flow;
            var length = 0;
            for (var v = sink; v != source; v = Tail(graph, parentArc[v]))
            {
                amount = Math.Min(amount, residual[parentArc[v]]);
                length++;
            }
            var path = new int[length];
            long unit = 0;
            var at = sink;
            for (var i = length - 1; i >= 0; i--)
            {
                var arc = parentArc[at];
                path[i] = arc;
                residual[arc] -= amount;
                residual[arc ^ 1] += amount;
                unit += ArcCost(graph, arc);
                at = Tail(graph, arc);
            }
            flow += amount;
            cost += (Int128)amount * unit;
            result.Augmentations.Add(new FlowAugmentation(amount, unit, path));
        }
        var edgeFlow = new long[m];
        for (var e = 0; e < m; e++) edgeFlow[e] = residual[2 * e + 1];
        result.EdgeFlow = edgeFlow;
        result.Flow = flow;
        result.Cost = cost;
        return result;
    }

    private static int Head(CsrGraph graph, int arc) => (arc & 1) == 0 ? graph.Target(arc >> 1) : graph.Source(arc >> 1);
    private static int Tail(CsrGraph graph, int arc) => (arc & 1) == 0 ? graph.Source(arc >> 1) : graph.Target(arc >> 1);
    private static long ArcCost(CsrGraph graph, int arc) => (arc & 1) == 0 ? graph.Weight(arc >> 1) : -graph.Weight(arc >> 1);

    /// <summary>Arc 2e is edge e forward with its capacity, arc 2e + 1 its reverse with none. Each node lists the forward arcs of its out-edges, then the reverse arcs of its in-edges, each in ascending edge id.</summary>
    private static void BuildResidual(CsrGraph graph, int[] start, int[] arcs, int[] cursor, long[] residual)
    {
        var n = graph.NodeCount;
        Array.Clear(start, 0, n + 1);
        for (var e = 0; e < graph.EdgeCount; e++)
        {
            start[graph.Source(e) + 1]++;
            start[graph.Target(e) + 1]++;
            residual[2 * e] = graph.Capacity(e);
            residual[2 * e + 1] = 0;
        }
        for (var v = 0; v < n; v++) start[v + 1] += start[v];
        for (var v = 0; v < n; v++) cursor[v] = start[v];
        for (var e = 0; e < graph.EdgeCount; e++) arcs[cursor[graph.Source(e)]++] = 2 * e;
        for (var e = 0; e < graph.EdgeCount; e++) arcs[cursor[graph.Target(e)]++] = 2 * e + 1;
    }
}
