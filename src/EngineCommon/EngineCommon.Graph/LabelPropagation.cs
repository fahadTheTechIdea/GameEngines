namespace EngineCommon.Graph;

/// <summary>How well a node fits its community: of the positive-weight edges leaving it, <see cref="Agreeing"/> of <see cref="Neighbours"/> go to a node with the same label, carrying <see cref="Weight"/> of <see cref="Total"/> weight.</summary>
public readonly record struct LabelSupport(int Label, int Agreeing, int Neighbours, long Weight, long Total);

/// <summary>
/// The communities found. <see cref="Labels"/> are the raw labels (the starting label of some node); <see cref="Community"/> names each community by the lowest node id in it, so it does not depend on which labels
/// survived. <see cref="ChangesPerRound"/> is how many nodes changed label in each round that ran; the last round of a converged run changed none.
/// </summary>
public sealed class CommunitiesResult
{
    internal int[] labels = [];
    internal int[] community = [];

    public string Code { get; internal set; } = GraphCodes.Ok;
    public int NodeCount { get; internal set; }
    public int Rounds { get; internal set; }
    public bool Converged { get; internal set; }
    public int CommunityCount { get; internal set; }
    public int[] ChangesPerRound { get; internal set; } = [];
    public ReadOnlySpan<int> Labels => labels.AsSpan(0, NodeCount);
    public ReadOnlySpan<int> Community => community.AsSpan(0, NodeCount);

    /// <summary>The support the node's label has among the nodes it points at (edges of weight 0 or less are not counted).</summary>
    public LabelSupport Support(CsrGraph graph, int node)
    {
        var label = labels[node];
        var agreeing = 0;
        var neighbours = 0;
        long weight = 0;
        long total = 0;
        for (var e = graph.FirstEdge(node); e < graph.EndEdge(node); e++)
        {
            var w = graph.Weight(e);
            if (w <= 0) continue;
            neighbours++;
            total += w;
            if (labels[graph.Target(e)] != label) continue;
            agreeing++;
            weight += w;
        }
        return new LabelSupport(label, agreeing, neighbours, weight, total);
    }

    public string Explain() => Code.Length != 0
        ? "refused " + Code
        : CommunityCount + " communities over " + NodeCount + " nodes after " + Rounds + " rounds" + (Converged ? " (converged)" : " (stopped at the round limit)");
}

/// <summary>
/// Deterministic asynchronous label propagation. A node looks at the edges leaving it that have positive weight and adopts the label with the greatest total weight among the nodes they point at (for an
/// undirected structure add both directions). Nodes are visited in ascending id order within a round and a change is seen at once by the nodes after it. Tie-break, stated: if the node's own label is among the
/// heaviest it keeps it, otherwise it takes the lowest heaviest label; a node with no positive edge keeps its label. There is no random order and no random tie-break (the textbook version shuffles and breaks ties at
/// random, so it has no unique answer). It stops when a round changes nothing or after <c>maxRounds</c>. The same edges added in any order give the same result; a renumbering of the nodes changes the visiting order
/// and so can change the result, which is why the engine must give nodes the ordinal order of its own ids. O(rounds x E).
/// </summary>
public static class LabelPropagation
{
    /// <summary>Runs from <paramref name="initialLabels"/> (empty: each node starts with its own id; otherwise one label in 0 to V - 1 per node).</summary>
    public static CommunitiesResult Run(CsrGraph graph, ReadOnlySpan<int> initialLabels, int maxRounds, GraphScratch scratch, CommunitiesResult? reuse = null)
    {
        var result = reuse ?? new CommunitiesResult();
        var n = graph.NodeCount;
        result.NodeCount = n;
        result.Code = GraphCodes.Ok;
        result.Rounds = 0;
        result.Converged = false;
        result.CommunityCount = 0;
        result.ChangesPerRound = [];
        if (scratch is null) { result.Code = GraphCodes.ArgumentMissing; return result; }
        if (maxRounds < 1 || maxRounds > GraphLimits.MaxRounds) { result.Code = GraphCodes.RoundsRange; return result; }
        if (initialLabels.Length != 0 && initialLabels.Length != n) { result.Code = GraphCodes.LabelsLength; return result; }
        foreach (var label in initialLabels)
            if (label < 0 || label >= n) { result.Code = GraphCodes.LabelRange; return result; }
        if (result.labels.Length < n)
        {
            result.labels = new int[n];
            result.community = new int[n];
        }
        var labels = result.labels;
        for (var i = 0; i < n; i++) labels[i] = initialLabels.Length == 0 ? i : initialLabels[i];

        var weightOf = scratch.Longs(0, n);
        var touched = scratch.Ints(0, n);
        Array.Clear(weightOf, 0, n);
        var changes = new List<int>();
        for (var round = 1; round <= maxRounds; round++)
        {
            var changed = 0;
            for (var u = 0; u < n; u++)
            {
                var touchedCount = 0;
                for (var e = graph.FirstEdge(u); e < graph.EndEdge(u); e++)
                {
                    var w = graph.Weight(e);
                    if (w <= 0) continue;
                    var label = labels[graph.Target(e)];
                    if (weightOf[label] == 0) touched[touchedCount++] = label;
                    weightOf[label] += w;
                }
                if (touchedCount == 0) continue;
                long best = 0;
                var lowest = int.MaxValue;
                for (var i = 0; i < touchedCount; i++)
                {
                    var label = touched[i];
                    if (weightOf[label] > best) { best = weightOf[label]; lowest = label; }
                    else if (weightOf[label] == best && label < lowest) lowest = label;
                }
                var chosen = weightOf[labels[u]] == best ? labels[u] : lowest;
                for (var i = 0; i < touchedCount; i++) weightOf[touched[i]] = 0;
                if (chosen == labels[u]) continue;
                labels[u] = chosen;
                changed++;
            }
            changes.Add(changed);
            result.Rounds = round;
            if (changed == 0) { result.Converged = true; break; }
        }
        result.ChangesPerRound = changes.ToArray();

        var firstNode = scratch.Ints(1, n);
        Array.Fill(firstNode, -1, 0, n);
        var count = 0;
        for (var u = 0; u < n; u++)
        {
            if (firstNode[labels[u]] < 0) { firstNode[labels[u]] = u; count++; }
            result.community[u] = firstNode[labels[u]];
        }
        result.CommunityCount = count;
        return result;
    }
}
