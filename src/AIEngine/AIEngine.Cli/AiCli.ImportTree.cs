using System.Text;
using EngineCommon;

public static partial class AiCli
{
    /// <summary>
    /// <c>import-tree &lt;tree.json&gt; --id &lt;document-id&gt; --out &lt;ai-document.json&gt; [--trace &lt;map.json&gt;]</c>: turns a trained integer decision tree (the interchange of docs/AI_ENGINE.md section 8)
    /// into an ordinary AI document and, when asked, the map from each emitted node to its source node. Nothing is written unless the whole tree is carried over; every reduction and refusal is
    /// printed with its code.
    /// </summary>
    private static int ImportTree(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (!TryOptions(args, out var id, out var outPath, out var tracePath)) return Wrong(error, Usage);
        TreeImportResult result;
        try { result = TreeImport.Run(File.ReadAllText(args[1]), id); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        foreach (var note in result.Report.Notes.Where(n => n.Level == TreeLevel.Refused))
            output.WriteLine("refused " + note.Code + ": " + note.Subject + ": " + note.Detail);
        if (result.Document is null)
        {
            output.WriteLine($"not written: {result.Report.Notes.Count(n => n.Level == TreeLevel.Refused)} refused, {result.Report.Notes.Count(n => n.Level == TreeLevel.Reduced)} reduced");
            return 1;
        }
        var utf8 = new UTF8Encoding(false);
        File.WriteAllText(outPath, result.Document, utf8);
        if (tracePath is not null) File.WriteAllText(tracePath, result.TraceMap!, utf8);
        var set = result.Set!;
        output.WriteLine($"ok {set.Id} digest {set.Digest} splits {result.Splits} leaves {result.Leaves} depth {result.Depth} nodes {result.Nodes} budget a step needs at least {set.MaxCost}");
        foreach (var note in result.Report.Notes.Where(n => n.Level == TreeLevel.Reduced))
            output.WriteLine("reduced " + note.Code + ": " + note.Subject + ": " + note.Detail);
        foreach (var (construct, count) in result.Report.Supported)
            output.WriteLine("supported " + construct + " " + count);
        return 0;
    }

    /// <summary>The flags of <c>import-tree</c>: <c>--id</c> and <c>--out</c> once each, <c>--trace</c> at most once, nothing else.</summary>
    private static bool TryOptions(IReadOnlyList<string> args, out string id, out string outPath, out string? tracePath)
    {
        id = outPath = "";
        tracePath = null;
        string? idValue = null, outValue = null;
        for (var i = 2; i < args.Count; i += 2)
        {
            if (i + 1 >= args.Count) return false;
            switch (args[i])
            {
                case "--id" when idValue is null: idValue = args[i + 1]; break;
                case "--out" when outValue is null: outValue = args[i + 1]; break;
                case "--trace" when tracePath is null: tracePath = args[i + 1]; break;
                default: return false;
            }
        }
        if (idValue is null || outValue is null) return false;
        id = idValue;
        outPath = outValue;
        return true;
    }
}
