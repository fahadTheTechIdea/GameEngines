internal sealed partial class TreeImport
{
    /// <summary>
    /// The tree to emit. Walking down, each feature's range narrows by what the splits above say about it; a test that no value in the narrowed range can pass (or fail) is decided, so only the
    /// branch that can be taken is kept. That changes nothing a host that keeps its promise (every feature inside its declared range) can observe, and it is reported as a reduction.
    /// </summary>
    private TreePlan Build(string id, Dictionary<string, TreeSpan> ranges, List<TreePruned> pruned)
    {
        var node = source.Node(id);
        if (node.IsLeaf) return new TreeLeaf(node.Id, node.Action, node.Weight);
        var feature = source.Features[node.Feature];
        var range = ranges[feature.Id];
        var whenTrue = TreeOps.Holds(range, node.Op, node.Threshold);
        var whenFalse = TreeOps.Holds(range, TreeOps.Negate(node.Op), node.Threshold);
        var test = feature.Id + " " + TreeOps.Name(node.Op) + " " + node.Threshold;
        if (whenTrue is null) return Decided(node, node.Right, node.Left, whenFalse!.Value, ranges, pruned, feature, range, test, "never passes");
        if (whenFalse is null) return Decided(node, node.Left, node.Right, whenTrue.Value, ranges, pruned, feature, range, test, "always passes");
        return new TreeSplit(node.Id, feature.Key!, node.Op, node.Threshold,
            Build(node.Left, With(ranges, feature.Id, whenTrue.Value), pruned),
            Build(node.Right, With(ranges, feature.Id, whenFalse.Value), pruned));
    }

    /// <summary>The test is decided: keep the live branch, drop the split and the other branch, and say so.</summary>
    private TreePlan Decided(TreeNode node, string live, string dead, TreeSpan narrowed, Dictionary<string, TreeSpan> ranges, List<TreePruned> pruned, TreeFeature feature, TreeSpan range, string test, string verdict)
    {
        var dropped = Subtree(dead);
        var reason = "the test '" + test + "' " + verdict + " while '" + feature.Id + "' is within " + range.Min + " to " + range.Max;
        report.Reduce(TreeCodes.BranchUnreachable, "node " + node.Id, reason + "; the split is replaced by its branch '" + live + "' and " + dropped.Count + " node(s) under '" + dead + "' are left out");
        pruned.Add(new TreePruned(node.Id, "collapsed: " + reason));
        foreach (var id in dropped) pruned.Add(new TreePruned(id, "unreachable: under '" + dead + "', which the test of " + node.Id + " never selects"));
        return Build(live, With(ranges, feature.Id, narrowed), pruned);
    }

    private static Dictionary<string, TreeSpan> With(Dictionary<string, TreeSpan> ranges, string feature, TreeSpan span)
    {
        var copy = new Dictionary<string, TreeSpan>(ranges, StringComparer.Ordinal) { [feature] = span };
        return copy;
    }

    /// <summary>Every node id under <paramref name="id"/>, itself included, in the order a depth-first walk meets them.</summary>
    private List<string> Subtree(string id)
    {
        var found = new List<string>();
        var stack = new Stack<string>();
        stack.Push(id);
        while (stack.Count > 0)
        {
            var node = source.Node(stack.Pop());
            found.Add(node.Id);
            if (node.IsLeaf) continue;
            stack.Push(node.Right);
            stack.Push(node.Left);
        }
        return found;
    }

    private static int Depth(TreePlan plan) => plan is TreeSplit split ? 1 + Math.Max(Depth(split.Left), Depth(split.Right)) : 0;
}
