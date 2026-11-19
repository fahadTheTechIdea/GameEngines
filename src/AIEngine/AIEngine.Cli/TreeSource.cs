/// <summary>A trained tree after the strict read: the declarations and the nodes by id, in file order.</summary>
internal sealed record TreeSource(string Id, long Every, IReadOnlyDictionary<string, TreeFeature> Features, IReadOnlyList<string> Actions, string Root, IReadOnlyList<TreeNode> Nodes)
{
    private readonly Dictionary<string, TreeNode> _byId = Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);

    public TreeNode Node(string id) => _byId[id];

    public bool TryNode(string id, out TreeNode node) => _byId.TryGetValue(id, out node!);
}
