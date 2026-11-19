namespace EngineCommon.Graph;

/// <summary>The graph a build produced, or the code that refused it.</summary>
public readonly record struct GraphBuild(CsrGraph? Graph, string Code)
{
    public bool Ok => Code.Length == 0;
}

/// <summary>Collects edges and builds the one <see cref="CsrGraph"/>. Every add is checked against the limits; nothing is accepted and then ignored.</summary>
public sealed class GraphBuilder
{
    private readonly int nodeCount;
    private int[] from = new int[16];
    private int[] to = new int[16];
    private long[] weight = new long[16];
    private long[] capacity = new long[16];
    private int count;

    private GraphBuilder(int nodeCount) { this.nodeCount = nodeCount; }

    /// <summary>Starts a builder for a graph of <paramref name="nodeCount"/> nodes; a count outside 0 to <see cref="GraphLimits.MaxNodes"/> is refused.</summary>
    public static string Create(int nodeCount, out GraphBuilder? builder)
    {
        builder = null;
        if (nodeCount < 0 || nodeCount > GraphLimits.MaxNodes) return GraphCodes.NodeCount;
        builder = new GraphBuilder(nodeCount);
        return GraphCodes.Ok;
    }

    public int EdgeCount => count;

    /// <summary>Adds one directed edge. Weight is within +-MaxWeight; capacity within 0 to MaxCapacity. Returns a refusal code, or the empty text.</summary>
    public string Add(int source, int target, long edgeWeight, long edgeCapacity = 0)
    {
        if (source < 0 || source >= nodeCount || target < 0 || target >= nodeCount) return GraphCodes.NodeRange;
        if (edgeWeight < -GraphLimits.MaxWeight || edgeWeight > GraphLimits.MaxWeight) return GraphCodes.WeightRange;
        if (edgeCapacity < 0 || edgeCapacity > GraphLimits.MaxCapacity) return GraphCodes.CapacityRange;
        if (count >= GraphLimits.MaxEdges) return GraphCodes.EdgeCount;
        if (count == from.Length)
        {
            var size = Math.Min(count * 2, GraphLimits.MaxEdges);
            Array.Resize(ref from, size);
            Array.Resize(ref to, size);
            Array.Resize(ref weight, size);
            Array.Resize(ref capacity, size);
        }
        from[count] = source;
        to[count] = target;
        weight[count] = edgeWeight;
        capacity[count] = edgeCapacity;
        count++;
        return GraphCodes.Ok;
    }

    /// <summary>Adds the two directed edges of an undirected link, both with the same weight and capacity. If either is refused nothing is added.</summary>
    public string AddUndirected(int a, int b, long edgeWeight, long edgeCapacity = 0)
    {
        if (count + 2 > GraphLimits.MaxEdges) return GraphCodes.EdgeCount;
        var first = Add(a, b, edgeWeight, edgeCapacity);
        if (first.Length != 0) return first;
        return Add(b, a, edgeWeight, edgeCapacity);
    }

    /// <summary>Sorts the edges by (from, to, weight, capacity) and builds the graph. Edges equal in all four are interchangeable, so the order they were added in cannot show.</summary>
    public GraphBuild Build()
    {
        var order = new int[count];
        for (var i = 0; i < count; i++) order[i] = i;
        var f = from; var t = to; var w = weight; var c = capacity;
        Array.Sort(order, (x, y) =>
        {
            var r = f[x].CompareTo(f[y]);
            if (r != 0) return r;
            r = t[x].CompareTo(t[y]);
            if (r != 0) return r;
            r = w[x].CompareTo(w[y]);
            return r != 0 ? r : c[x].CompareTo(c[y]);
        });
        var offsets = new int[nodeCount + 1];
        var sources = new int[count];
        var targets = new int[count];
        var weights = new long[count];
        var capacities = new long[count];
        var inputOf = new int[count];
        for (var i = 0; i < count; i++)
        {
            var e = order[i];
            sources[i] = f[e];
            targets[i] = t[e];
            weights[i] = w[e];
            capacities[i] = c[e];
            inputOf[i] = e;
            offsets[f[e] + 1]++;
        }
        for (var n = 0; n < nodeCount; n++) offsets[n + 1] += offsets[n];
        return new GraphBuild(new CsrGraph(nodeCount, offsets, sources, targets, weights, capacities, inputOf), GraphCodes.Ok);
    }
}
