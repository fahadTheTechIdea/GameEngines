/// <summary>The tree as it will be emitted, after unreachable branches were left out: a leaf, or a split with both children.</summary>
internal abstract record TreePlan(string Source);

internal sealed record TreeLeaf(string Source, string Action, long Weight) : TreePlan(Source);

internal sealed record TreeSplit(string Source, string Key, TreeOp Op, long Threshold, TreePlan Left, TreePlan Right) : TreePlan(Source);
