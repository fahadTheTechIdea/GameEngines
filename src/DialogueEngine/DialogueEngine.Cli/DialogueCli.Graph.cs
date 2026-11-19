using System.Text;
using DialogueEngine;
using EngineCommon;

public static partial class DialogueCli
{
    /// <summary>
    /// The conversations as a Graphviz digraph: a node for each node of the document (its kind and, for lines, the speaker), an edge for each way on (labelled with the answer, the case, the
    /// outcome weight or the call), and the start marked. Every order is fixed, so the same document gives the same text.
    /// </summary>
    private static int Graph(string document, string? only, TextWriter output)
    {
        try
        {
            var set = Load(document);
            if (only is not null && !set.Conversations.ContainsKey(only)) return Refused(output, new EngineRefusedException(DialogueCodes.NoConversation, "there is no conversation '" + only + "'"));
            var text = new StringBuilder("digraph \"").Append(set.Id).Append("\" {\n  rankdir=LR;\n  node [fontname=\"Helvetica\"];\n");
            foreach (var (id, conversation) in set.Conversations.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                if (only is not null && id != only) continue;
                text.Append("  subgraph \"cluster_").Append(id).Append("\" {\n    label=\"").Append(id).Append("\";\n");
                foreach (var node in conversation.Nodes.Values.OrderBy(n => n.Id, StringComparer.Ordinal))
                {
                    var detail = node is LineNode line ? "\\n" + line.Speaker : node is CallNode call ? "\\n-> " + call.Conversation : node is VariantNode variant ? "\\n" + variant.Mode.ToString().ToLowerInvariant() : "";
                    var shape = node switch { ChoiceNode => "diamond", EndNode or ReturnNode => "doublecircle", BranchNode or RandomNode or VariantNode => "hexagon", _ => "box" };
                    text.Append("    \"").Append(id).Append('/').Append(node.Id).Append("\" [label=\"").Append(node.Id).Append("\\n[").Append(node.Kind).Append(']').Append(detail).Append("\", shape=").Append(shape)
                        .Append(node.Id == conversation.Start ? ", style=bold" : "").Append("];\n");
                }
                text.Append("  }\n");
                foreach (var node in conversation.Nodes.Values.OrderBy(n => n.Id, StringComparer.Ordinal))
                    foreach (var (target, label) in Edges(node))
                        text.Append("  \"").Append(id).Append('/').Append(node.Id).Append("\" -> \"").Append(id).Append('/').Append(target).Append("\" [label=\"").Append(label).Append("\"];\n");
            }
            text.Append("}\n");
            output.Write(text.ToString());
            return 0;
        }
        catch (EngineRefusedException refusal)
        {
            return Refused(output, refusal);
        }
    }

    private static IEnumerable<(string Target, string Label)> Edges(Node node)
    {
        switch (node)
        {
            case LineNode line: yield return (line.Next, ""); break;
            case EffectsNode effects: yield return (effects.Next, ""); break;
            case CallNode call: yield return (call.Next, "returns here"); break;
            case ChoiceNode choice:
                foreach (var option in choice.Options) yield return (option.Next, option.Id + (option.When is null ? "" : " ?"));
                if (choice.Fallback is not null) yield return (choice.Fallback, "fallback");
                break;
            case BranchNode branch:
                for (var i = 0; i < branch.Cases.Count; i++) yield return (branch.Cases[i].Next, "case " + i);
                yield return (branch.Otherwise, "otherwise");
                break;
            case RandomNode random:
                foreach (var outcome in random.Outcomes) yield return (outcome.Next, "weight " + outcome.Weight);
                break;
            case VariantNode variant:
                for (var i = 0; i < variant.Options.Count; i++) yield return (variant.Options[i], variant.Mode.ToString().ToLowerInvariant() + " " + i);
                if (variant.Exhausted is not null) yield return (variant.Exhausted, "exhausted");
                break;
        }
    }
}
