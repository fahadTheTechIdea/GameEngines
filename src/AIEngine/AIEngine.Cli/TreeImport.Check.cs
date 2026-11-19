internal sealed partial class TreeImport
{
    /// <summary>Checks that the nodes form one tree from the root: every name resolves, no node is reached twice (a shared subtree or a cycle), none is left over, and every feature and action is declared.</summary>
    private void Check()
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        Visit(source.Root, "the root", visited);
        foreach (var node in source.Nodes)
            if (!visited.Contains(node.Id))
                report.Refuse(TreeCodes.NotATree, "node " + node.Id, "it cannot be reached from the root '" + source.Root + "'; a node that nothing uses would be silently dropped, so remove it from the file");
    }

    private void Visit(string id, string from, HashSet<string> visited)
    {
        if (!source.TryNode(id, out var node))
        {
            report.Refuse(TreeCodes.Reference, from, "names the node '" + id + "', which the file does not define");
            return;
        }
        if (!visited.Add(id))
        {
            report.Refuse(TreeCodes.NotATree, "node " + id, "is reached a second time from " + from + ": a node shared by two parents or a cycle is a graph, not a tree");
            return;
        }
        if (node.IsLeaf)
        {
            if (!source.Actions.Contains(node.Action)) report.Refuse(TreeCodes.Reference, "node " + id, "its action '" + node.Action + "' is not in the 'actions' list");
            return;
        }
        if (!source.Features.ContainsKey(node.Feature)) report.Refuse(TreeCodes.Reference, "node " + id, "its feature '" + node.Feature + "' is not in the 'features' table");
        Visit(node.Left, "node " + id + " (left)", visited);
        Visit(node.Right, "node " + id + " (right)", visited);
    }
}
