using AIEngine;

/// <summary>What one import produced: the report, and, when nothing was refused, the AI document text, the trace map text, the document as the engine loaded it and the figures of the emitted tree.</summary>
internal sealed record TreeImportResult(TreeReport Report, string? Document, string? TraceMap, AiSet? Set, int Splits, int Leaves, int Depth, int Nodes);

/// <summary>
/// The one owner of turning a trained integer decision tree into an AI document. A split becomes a selector of two children: a sequence of the test and the left subtree, then the right subtree;
/// a leaf becomes an act that raises the leaf's action with its weight. Because every leaf succeeds, the selector falls to the right subtree exactly when the test fails, so the document runs
/// the tree and nothing else. The file is read strictly (<see cref="TreeReader"/>), checked to be a tree, reduced where the declared ranges decide a test, bounded by what the engine
/// accepts, written, and finally loaded by the engine's own loader, so a document that leaves here is one the game would accept. Nothing is rounded, guessed or defaulted.
/// This is the part of the work in <c>TreeImport.Check.cs</c> (the tree is a tree), <c>TreeImport.Plan.cs</c> (reductions and bounds) and <c>TreeImport.Emit.cs</c> (the text).
/// </summary>
internal sealed partial class TreeImport(TreeSource source, TreeReport report)
{
    /// <summary>
    /// The most splits on any path. A split costs two levels of the behaviour (the selector, then the sequence that holds the test), the root is at level 1 and the engine allows 12
    /// (<c>BehaviorContext.MaxDepth</c>; a test pins this number to it), so the deepest act sits at 2 x splits + 1 and the most is (12 - 1) / 2 = 5, which is 32 leaves.
    /// </summary>
    public const int MaxSplitDepth = 5;

    public static TreeImportResult Run(string treeText, string documentId)
    {
        new EngineCommon.StrictJson(TreeCodes.ForDocument).CheckId(documentId, "the document id");
        var report = new TreeReport();
        var source = new TreeReader(report).Read(treeText);
        return new TreeImport(source, report).Import(documentId);
    }

    private TreeImportResult Import(string documentId)
    {
        Check();
        if (report.HasRefusals) return Refused();
        var pruned = new List<TreePruned>();
        var plan = Build(source.Root, source.Features.ToDictionary(f => f.Key, f => new TreeSpan(f.Value.Min, f.Value.Max), StringComparer.Ordinal), pruned);
        var depth = Depth(plan);
        if (depth > MaxSplitDepth)
        {
            report.Refuse(TreeCodes.TooDeep, "tree " + source.Id, "after unreachable branches are left out the longest path has " + depth + " splits; the engine's behaviour depth allows " + MaxSplitDepth +
                " (two levels a split), so retrain with a shallower tree (for example max depth " + MaxSplitDepth + ")");
            return Refused();
        }
        return Write(plan, documentId, depth, pruned);
    }

    private TreeImportResult Refused() => new(report, null, null, null, 0, 0, 0, 0);
}
