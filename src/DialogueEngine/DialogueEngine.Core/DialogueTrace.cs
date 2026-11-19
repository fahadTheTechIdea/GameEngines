using System.Text;

namespace DialogueEngine;

/// <summary>One node a call passed through, in order, with what it decided (the guard that held or failed and the values it read, the pick of a random or a variant, the answers offered).</summary>
public sealed record TraceEntry(string Conversation, string Node, string Kind, string Detail);

/// <summary>
/// Why a call did what it did: every node it entered, in order, and how it ended. At most <see cref="MaxEntries"/> entries (the runtime refuses a longer call) of at most <see cref="MaxDetail"/>
/// characters each; a longer detail is cut and ends with dots. The same document, state, seed and facts give the same trace. Recording changes nothing the call does.
/// </summary>
public sealed class DialogueTrace(IReadOnlyList<TraceEntry> entries, string? refusal)
{
    /// <summary>The most entries a trace keeps: a stretch of a call passes at most <see cref="DialogueRuntime.SilentLimit"/> nodes without waiting and enters one more. A call that goes on into a resumed or queued conversation, or skips lines, has more stretches and keeps the newest entries.</summary>
    public const int MaxEntries = DialogueRuntime.SilentLimit + 1;

    public const int MaxDetail = 512;

    public IReadOnlyList<TraceEntry> Entries { get; } = entries;

    /// <summary>The code the call was refused with, or null when it completed.</summary>
    public string? Refusal { get; } = refusal;

    public string ToText()
    {
        var text = new StringBuilder();
        foreach (var entry in Entries)
        {
            text.Append(entry.Conversation).Append('/').Append(entry.Node).Append(" [").Append(entry.Kind).Append(']');
            if (entry.Detail.Length > 0) text.Append(" - ").Append(entry.Detail);
            text.Append('\n');
        }
        if (Refusal is not null) text.Append("refused ").Append(Refusal).Append('\n');
        return text.ToString();
    }
}

/// <summary>Collects the entries of one call while it runs.</summary>
internal sealed class TraceRecorder
{
    private readonly List<TraceEntry> _entries = new();

    public void Entered(string conversation, Node node)
    {
        if (_entries.Count == DialogueTrace.MaxEntries) _entries.RemoveAt(0);   // a call that resumes, starts a queued conversation or skips keeps the newest entries
        _entries.Add(new TraceEntry(conversation, node.Id, node.Kind, ""));
    }

    /// <summary>What the node just entered decided.</summary>
    public void Note(string detail)
    {
        if (_entries.Count == 0) return;
        var last = _entries[^1];
        _entries[^1] = last with { Detail = detail.Length <= DialogueTrace.MaxDetail ? detail : detail[..(DialogueTrace.MaxDetail - 3)] + "..." };
    }

    public DialogueTrace Finish(string? refusal) => new(_entries.ToArray(), refusal);
}
