namespace AIEngine;

/// <summary>
/// A node of a behaviour tree. Its id is unique in its behaviour. <see cref="Bound"/> is the most work units one run of it can cost (one for the
/// node itself, one for each percept a query looks at, plus the nodes below it) and <see cref="MayRun"/> says whether it can ever report Running.
/// </summary>
public abstract class Node(string id, int slot, long bound, bool mayRun)
{
    public string Id { get; } = id;

    /// <summary>The memory slot this node keeps its state in, or -1 when it keeps none.</summary>
    public int Slot { get; } = slot;

    public long Bound { get; } = bound;
    public bool MayRun { get; } = mayRun;

    /// <summary>The name of this kind of node in a document and in a trace: <c>sequence</c>, <c>forEach</c>, <c>utility</c>.</summary>
    public string Kind => KindOf(GetType().Name);

    private static string KindOf(string typeName)
    {
        var name = typeName[..^"Node".Length];
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>Runs the node. When the think is being explained, the node and what it reported are recorded; the record changes nothing the node does.</summary>
    internal Status Run(ThinkContext c)
    {
        if (c.Trace is not { } trace) return Execute(c);
        var entry = trace.Enter(this);
        var status = Execute(c);
        trace.Leave(entry, status);
        return status;
    }

    internal abstract Status Execute(ThinkContext c);
}