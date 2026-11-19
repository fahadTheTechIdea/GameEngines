namespace EngineCommon.Graph;

/// <summary>What a degree table can rank nodes by.</summary>
public enum DegreeMeasure
{
    OutDegree,
    InDegree,
    OutWeight,
    InWeight,
}

/// <summary>The nodes with the largest value of a measure, largest first, the lowest node id first among equals; or the code that refused the request.</summary>
public readonly record struct TopNodes(string Code, int[] Nodes, long[] Values);

/// <summary>Degree and weighted-degree centrality: for each node the number and the total weight of the edges leaving and entering it (a self-loop counts on both sides). Weights are summed as given, negatives included. O(V + E).</summary>
public sealed class DegreeTable
{
    internal long[] outDegree = [];
    internal long[] inDegree = [];
    internal long[] outWeight = [];
    internal long[] inWeight = [];
    private long[][] byMeasure = [];

    public int NodeCount { get; internal set; }
    public ReadOnlySpan<long> OutDegree => outDegree.AsSpan(0, NodeCount);
    public ReadOnlySpan<long> InDegree => inDegree.AsSpan(0, NodeCount);
    public ReadOnlySpan<long> OutWeight => outWeight.AsSpan(0, NodeCount);
    public ReadOnlySpan<long> InWeight => inWeight.AsSpan(0, NodeCount);

    /// <summary>Fills the table for the graph; pass a table from an earlier run to reuse its arrays.</summary>
    public static DegreeTable Run(CsrGraph graph, DegreeTable? reuse = null)
    {
        var table = reuse ?? new DegreeTable();
        var n = graph.NodeCount;
        table.NodeCount = n;
        if (table.outDegree.Length < n || table.byMeasure.Length == 0)
        {
            table.outDegree = new long[n];
            table.inDegree = new long[n];
            table.outWeight = new long[n];
            table.inWeight = new long[n];
            table.byMeasure = [table.outDegree, table.inDegree, table.outWeight, table.inWeight];
        }
        Array.Clear(table.outDegree, 0, n);
        Array.Clear(table.inDegree, 0, n);
        Array.Clear(table.outWeight, 0, n);
        Array.Clear(table.inWeight, 0, n);
        for (var e = 0; e < graph.EdgeCount; e++)
        {
            var from = graph.Source(e);
            var to = graph.Target(e);
            table.outDegree[from]++;
            table.inDegree[to]++;
            table.outWeight[from] += graph.Weight(e);
            table.inWeight[to] += graph.Weight(e);
        }
        return table;
    }

    /// <summary>The <paramref name="k"/> highest nodes by a measure (k from 1 to <see cref="GraphLimits.MaxTop"/>); fewer if the graph has fewer nodes. O(V x k) by insertion into a sorted window.</summary>
    public TopNodes Top(DegreeMeasure measure, int k)
    {
        if (k < 1 || k > GraphLimits.MaxTop) return new TopNodes(GraphCodes.TopRange, [], []);
        var values = byMeasure[(int)measure]; // the enum values are the positions in byMeasure; a value outside the enum fails here and is not answered
        var size = Math.Min(k, NodeCount);
        var nodes = new int[size];
        var found = new long[size];
        var filled = 0;
        for (var node = 0; node < NodeCount; node++)
        {
            var value = values[node];
            // ascending node order means an equal value never outranks one already placed, so only a strictly larger value moves ahead
            var at = filled;
            while (at > 0 && value > found[at - 1]) at--;
            if (at >= size) continue;
            var last = Math.Min(filled, size - 1);
            for (var i = last; i > at; i--)
            {
                nodes[i] = nodes[i - 1];
                found[i] = found[i - 1];
            }
            nodes[at] = node;
            found[at] = value;
            if (filled < size) filled++;
        }
        return new TopNodes(GraphCodes.Ok, nodes, found);
    }
}
