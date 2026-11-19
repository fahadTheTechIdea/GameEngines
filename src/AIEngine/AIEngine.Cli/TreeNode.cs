/// <summary>
/// One node of the trained tree as the interchange writes it. A leaf has an action and a weight; a split has a feature, a comparison, a threshold and two children. A field the engine cannot
/// run (a float, a category set, a missing-value route) is recorded in the report when the node is read and leaves <see cref="Usable"/> false.
/// </summary>
internal sealed record TreeNode(string Id, bool IsLeaf, string Action, long Weight, string Feature, TreeOp Op, long Threshold, string Left, string Right, bool Usable);
