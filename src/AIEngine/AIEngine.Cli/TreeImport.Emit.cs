using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AIEngine;

internal sealed partial class TreeImport
{
    /// <summary>One emitted AI node and the source node it stands for: <c>select</c> and <c>branch</c> and <c>test</c> are the three nodes of a split, <c>leaf</c> the act of a leaf.</summary>
    private sealed record Emitted(string Node, string Source, string Role);

    private TreeImportResult Write(TreePlan plan, string documentId, int depth, List<TreePruned> pruned)
    {
        var emitted = new List<Emitted>();
        var leaves = new List<TreeLeaf>();
        var splits = 0;
        var tree = new StringBuilder();
        WriteNode(tree, plan, 3, emitted, leaves, ref splits);
        foreach (var leaf in leaves) report.Support("leaf");
        var document = new StringBuilder();
        document.Append("{ \"format\": 1, \"id\": \"").Append(documentId).Append("\",\n  \"keys\": [\n");
        document.Append(string.Join(",\n", source.Features.Values.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f =>
            "    { \"id\": \"" + f.Key + "\", \"type\": \"int\", \"initial\": " + Number(f.Initial) + ", \"min\": " + Number(f.Min) + ", \"max\": " + Number(f.Max) + ", \"source\": \"host\" }")));
        document.Append(" ],\n  \"percepts\": [],\n  \"intents\": [\n");
        var low = leaves.Min(l => l.Weight);
        var high = leaves.Max(l => l.Weight);
        document.Append(string.Join(",\n", source.Actions.OrderBy(a => a, StringComparer.Ordinal).Select(a =>
            "    { \"id\": \"" + a + "\", \"args\": [ { \"id\": \"weight\", \"type\": \"int\", \"min\": " + Number(low) + ", \"max\": " + Number(high) + " } ] }")));
        document.Append(" ],\n  \"behaviors\": [\n    { \"id\": \"").Append(source.Id).Append("\", \"every\": ").Append(Number(source.Every)).Append(", \"root\":\n");
        document.Append(tree).Append(" } ]\n}\n");
        var text = document.ToString();
        var set = AiLoader.Load(text);
        return new TreeImportResult(report, text, TraceMapText(documentId, emitted, pruned), set, splits, leaves.Count, depth, emitted.Count);
    }

    private void WriteNode(StringBuilder text, TreePlan plan, int indent, List<Emitted> emitted, List<TreeLeaf> leaves, ref int splits)
    {
        var pad = new string(' ', indent * 2);
        switch (plan)
        {
            case TreeLeaf leaf:
                emitted.Add(new Emitted(leaf.Source, leaf.Source, "leaf"));
                leaves.Add(leaf);
                text.Append(pad).Append("{ \"id\": \"").Append(leaf.Source).Append("\", \"kind\": \"act\", \"intent\": \"").Append(leaf.Action)
                    .Append("\", \"args\": { \"weight\": ").Append(Number(leaf.Weight)).Append(" } }");
                break;
            case TreeSplit split:
                splits++;
                report.Support("split-" + TreeOps.Name(split.Op));
                emitted.Add(new Emitted(split.Source, split.Source, "select"));
                emitted.Add(new Emitted(split.Source + ".left", split.Source, "branch"));
                emitted.Add(new Emitted(split.Source + ".test", split.Source, "test"));
                text.Append(pad).Append("{ \"id\": \"").Append(split.Source).Append("\", \"kind\": \"selector\", \"resume\": false, \"children\": [\n");
                text.Append(pad).Append("  { \"id\": \"").Append(split.Source).Append(".left\", \"kind\": \"sequence\", \"resume\": false, \"children\": [\n");
                text.Append(pad).Append("    { \"id\": \"").Append(split.Source).Append(".test\", \"kind\": \"condition\", \"when\": { \"fact\": \"key.").Append(split.Key)
                    .Append("\", \"op\": \"").Append(TreeOps.Name(split.Op)).Append("\", \"value\": ").Append(Number(split.Threshold)).Append(" } },\n");
                WriteNode(text, split.Left, indent + 2, emitted, leaves, ref splits);
                text.Append(" ] },\n");
                WriteNode(text, split.Right, indent + 1, emitted, leaves, ref splits);
                text.Append(" ] }");
                break;
        }
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>The trace map: every emitted node with the source node and role it stands for, then every source node that has no emitted node and why. Together they name each source node once or more.</summary>
    private string TraceMapText(string documentId, List<Emitted> emitted, List<TreePruned> pruned)
    {
        var text = new StringBuilder();
        text.Append("{ \"format\": 1, \"document\": \"").Append(documentId).Append("\", \"behavior\": \"").Append(source.Id).Append("\",\n  \"nodes\": [\n");
        text.Append(string.Join(",\n", emitted.Select(e => "    { \"node\": \"" + e.Node + "\", \"source\": \"" + e.Source + "\", \"role\": \"" + e.Role + "\" }")));
        text.Append(" ],\n  \"pruned\": [\n");
        text.Append(string.Join(",\n", pruned.Select(p => "    { \"source\": \"" + p.Source + "\", \"why\": \"" + JsonEncodedText.Encode(p.Why, JavaScriptEncoder.UnsafeRelaxedJsonEscaping) + "\" }")));
        text.Append(" ]\n}\n");
        return text.ToString();
    }
}
