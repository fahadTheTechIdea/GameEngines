namespace EngineCommon.Graph;

/// <summary>
/// An immutable directed graph in compressed sparse row form. Nodes are the integers 0 to NodeCount - 1 (the engine sorts its own ids ordinally and maps them). Edge ids are positions in the CSR order, which
/// is sorted by (from, to, weight, capacity): the same set of edges gives the same graph whatever order they were added in. Parallel edges and self-loops are allowed. An undirected link is two edges.
/// The topology (nodes, sources, targets, capacities) never changes; the weights can be replaced as a set by <see cref="Reweight"/>, which leaves exactly the graph a fresh build with those weights would give.
/// Not thread-safe while it is being reweighted.
/// </summary>
public sealed class CsrGraph
{
    private readonly int[] offsets;
    private readonly int[] sources;
    private readonly int[] targets;
    private readonly long[] weights;
    private readonly long[] capacities;
    private readonly int[] inputOf;

    internal CsrGraph(int nodeCount, int[] offsets, int[] sources, int[] targets, long[] weights, long[] capacities, int[] inputOf)
    {
        NodeCount = nodeCount;
        this.offsets = offsets;
        this.sources = sources;
        this.targets = targets;
        this.weights = weights;
        this.capacities = capacities;
        this.inputOf = inputOf;
        EdgeCount = targets.Length;
        Summarize();
    }

    public int NodeCount { get; }
    public int EdgeCount { get; }
    public bool HasNegativeWeight { get; private set; }
    /// <summary>The largest absolute weight on any edge (0 for no edges).</summary>
    public long MaxAbsWeight { get; private set; }

    /// <summary>The first edge id leaving the node; its edges are FirstEdge(node) up to, not including, EndEdge(node), in ascending target order.</summary>
    public int FirstEdge(int node) => offsets[node];
    public int EndEdge(int node) => offsets[node + 1];
    public int Source(int edge) => sources[edge];
    public int Target(int edge) => targets[edge];
    public long Weight(int edge) => weights[edge];
    public long Capacity(int edge) => capacities[edge];
    public int OutDegree(int node) => offsets[node + 1] - offsets[node];

    private void Summarize()
    {
        var negative = false;
        var largest = 0L;
        foreach (var w in weights)
        {
            if (w < 0) negative = true;
            var a = w < 0 ? -w : w;
            if (a > largest) largest = a;
        }
        HasNegativeWeight = negative;
        MaxAbsWeight = largest;
    }

    /// <summary>
    /// Replaces every weight. <paramref name="weightsByInput"/> holds one weight per edge, in the order the edges were added to the builder (the n-th edge added is entry n), whatever the edge ids have since become.
    /// Afterwards the graph is exactly the one a fresh build with these weights would give: parallel edges are put back in (weight, capacity) order, so edge ids, tie-breaks and results are the same, and nothing is
    /// allocated. A wrong length or a weight outside +-MaxWeight is refused and changes nothing. Edge ids held from before a reweight are not valid after it.
    /// </summary>
    public string Reweight(ReadOnlySpan<long> weightsByInput)
    {
        if (weightsByInput.Length != EdgeCount) return GraphCodes.WeightsLength;
        foreach (var w in weightsByInput)
            if (w < -GraphLimits.MaxWeight || w > GraphLimits.MaxWeight) return GraphCodes.WeightRange;
        for (var e = 0; e < EdgeCount; e++) weights[e] = weightsByInput[inputOf[e]];
        for (var node = 0; node < NodeCount; node++)
        {
            var end = offsets[node + 1];
            var run = offsets[node];
            while (run < end)
            {
                var stop = run + 1;
                while (stop < end && targets[stop] == targets[run]) stop++;
                for (var i = run + 1; i < stop; i++) SinkIntoPlace(run, i);
                run = stop;
            }
        }
        Summarize();
        return GraphCodes.Ok;
    }

    /// <summary>Insertion step for a run of parallel edges: moves the edge at <paramref name="at"/> down to its place by (weight, capacity), carrying its input position with it.</summary>
    private void SinkIntoPlace(int first, int at)
    {
        var weight = weights[at];
        var capacity = capacities[at];
        var input = inputOf[at];
        var i = at;
        while (i > first && (weights[i - 1] > weight || (weights[i - 1] == weight && capacities[i - 1] > capacity)))
        {
            weights[i] = weights[i - 1];
            capacities[i] = capacities[i - 1];
            inputOf[i] = inputOf[i - 1];
            i--;
        }
        weights[i] = weight;
        capacities[i] = capacity;
        inputOf[i] = input;
    }
}
