namespace EngineCommon.Graph;

/// <summary>
/// Reusable working memory. An algorithm asks for an array by slot and length and gets one at least that long, grown only when a bigger graph comes; the contents are not cleared, so the algorithm fills what it
/// reads. Reusing one scratch for repeated calls on graphs of one size allocates nothing after the first call. Not thread-safe: one scratch per thread.
/// </summary>
public sealed class GraphScratch
{
    private readonly int[]?[] ints = new int[12][];
    private readonly long[]?[] longs = new long[8][];
    private readonly Int128[]?[] wides = new Int128[2][];
    internal readonly Heap Heap = new();
    internal readonly RemainderOrder Order = new();

    internal int[] Ints(int slot, int length)
    {
        var a = ints[slot];
        if (a is null || a.Length < length) ints[slot] = a = new int[Math.Max(length, 16)];
        return a;
    }

    internal long[] Longs(int slot, int length)
    {
        var a = longs[slot];
        if (a is null || a.Length < length) longs[slot] = a = new long[Math.Max(length, 16)];
        return a;
    }

    internal Int128[] Wides(int slot, int length)
    {
        var a = wides[slot];
        if (a is null || a.Length < length) wides[slot] = a = new Int128[Math.Max(length, 16)];
        return a;
    }
}

/// <summary>A binary min-heap on (cost, node). The node index is the tie-break, so the pop order is a total order and never depends on insertion order.</summary>
internal sealed class Heap
{
    private long[] costs = new long[64];
    private int[] nodes = new int[64];
    public int Count { get; private set; }

    public void Clear() => Count = 0;

    public void Push(long cost, int node)
    {
        if (Count == costs.Length)
        {
            Array.Resize(ref costs, Count * 2);
            Array.Resize(ref nodes, Count * 2);
        }
        var i = Count++;
        while (i > 0)
        {
            var parent = (i - 1) >> 1;
            if (!Less(cost, node, costs[parent], nodes[parent])) break;
            costs[i] = costs[parent];
            nodes[i] = nodes[parent];
            i = parent;
        }
        costs[i] = cost;
        nodes[i] = node;
    }

    public (long Cost, int Node) Pop()
    {
        var topCost = costs[0];
        var topNode = nodes[0];
        Count--;
        if (Count > 0)
        {
            var cost = costs[Count];
            var node = nodes[Count];
            var i = 0;
            while (true)
            {
                var child = 2 * i + 1;
                if (child >= Count) break;
                if (child + 1 < Count && Less(costs[child + 1], nodes[child + 1], costs[child], nodes[child])) child++;
                if (!Less(costs[child], nodes[child], cost, node)) break;
                costs[i] = costs[child];
                nodes[i] = nodes[child];
                i = child;
            }
            costs[i] = cost;
            nodes[i] = node;
        }
        return (topCost, topNode);
    }

    private static bool Less(long costA, int nodeA, long costB, int nodeB) => costA < costB || (costA == costB && nodeA < nodeB);
}

/// <summary>Orders indices by a remainder, largest first, the lowest index first among equal remainders: the one rule that hands out the units a rounding left over.</summary>
internal sealed class RemainderOrder
{
    private long[] remainders = [];
    private readonly Comparison<int> comparison;

    public RemainderOrder() { comparison = Compare; }

    private int Compare(int a, int b)
    {
        var r = remainders[b].CompareTo(remainders[a]);
        return r != 0 ? r : a.CompareTo(b);
    }

    /// <summary>
    /// Adds one unit to each of the <paramref name="deficit"/> entries (of the first <paramref name="count"/>) with the largest remainder; ties go to the lowest index. <paramref name="order"/> is scratch of at least
    /// <paramref name="count"/>. The deficit is at most the count, which holds whenever the values are floors of shares of an exact total.
    /// </summary>
    public void Distribute(int[] order, long[] remainderValues, long[] values, int count, long deficit)
    {
        if (deficit <= 0) return;
        for (var i = 0; i < count; i++) order[i] = i;
        remainders = remainderValues;
        order.AsSpan(0, count).Sort(comparison); // one cached delegate: the Array.Sort overload that takes a comparer allocates a delegate on every call
        for (var i = 0; i < deficit; i++) values[order[i]]++;
    }
}
