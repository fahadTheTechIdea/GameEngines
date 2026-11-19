using EngineCommon;
namespace DialogueEngine;

/// <summary>
/// A speaker says a line, given by a localisation key and the facts it shows. The conversation waits until the host asks for the next step.
/// A line that carries a content-warning tag is checked against the host's policy when it is reached: a blocked line is passed without being shown (its warning says <c>skip</c>) or shown with its
/// declared alternative key (<c>replace</c>).
/// </summary>
public sealed class LineNode(string id, string speaker, string portrait, string key, IReadOnlyList<KeyValuePair<string, string>> args, string next,
    IReadOnlyList<string> tags, string? voice, string? emotion, int autoAdvanceMs, bool glue = false, string? alternative = null, IReadOnlyList<ContentWarningDef>? warnings = null) : Node(id)
{
    public string Speaker { get; } = speaker;
    public string Portrait { get; } = portrait;
    public string Key { get; } = key;

    /// <summary>The name a placeholder has in the text and the fact that fills it.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Args { get; } = args;

    public string Next { get; } = next;
    public IReadOnlyList<string> Tags { get; } = tags;
    public string? Voice { get; } = voice;
    public string? Emotion { get; } = emotion;
    public int AutoAdvanceMs { get; } = autoAdvanceMs;

    /// <summary>The next line shown continues this one on the same output line (Ink's glue); the host joins them.</summary>
    public bool Glue { get; } = glue;

    /// <summary>The localisation key shown instead of <see cref="Key"/> when a <see cref="WarningAction.Replace"/> warning of this line is blocked.</summary>
    public string? Alternative { get; } = alternative;

    /// <summary>The declared content warnings this line's tags name.</summary>
    public IReadOnlyList<ContentWarningDef> Warnings { get; } = warnings ?? Array.Empty<ContentWarningDef>();

    public override string Kind => "line";
    public override IEnumerable<string> Targets => [Next];

    /// <summary>A line waits for the player unless a content warning can pass it by; then it may not wait, which the loader's checks for loops and runs of silent nodes take into account.</summary>
    public override bool Waits => !Warnings.Any(w => w.Action == WarningAction.Skip);

    internal override NodeOutcome Enter(StepContext context)
    {
        var replaced = false;
        if (Warnings.Count > 0)
        {
            foreach (var warning in Warnings)
            {
            if (!context.Fact("ext." + warning.Fact).Boolean) continue;
            if (warning.Action == WarningAction.Skip)
            {
                context.Trace?.Note("blocked by content warning '" + warning.Tag + "': passed without being shown");
                if (!context.Reentering) context.Unvisit(context.Conversation!.Id, Id);
                return NodeOutcome.Go(Next);
            }
            replaced = true;
            context.Trace?.Note("blocked by content warning '" + warning.Tag + "': shown with its alternative");
            }
        }
        var shown = new List<MessageArg>(Args.Count);
        foreach (var (name, fact) in Args) shown.Add(new MessageArg(name, context.Fact(fact)));
        var seen = context.Reentering || context.EnteredBefore > 0;
        var key = replaced ? Alternative! : Key;
        var line = new LineStep(context.Conversation!.Id, Id, Speaker, Portrait, key, shown, Tags, Voice, Emotion, AutoAdvanceMs, seen, Glue, replaced);
        context.SetCursor(context.Conversation.Id, Id, Waiting.Line);
        if (context.RecordsHistory && !context.Reentering) context.Record(new HistoryEntry(context.NextTurn, context.Tick, HistoryKind.Line, context.Conversation.Id, Id, Speaker, key, shown, "", seen, false, replaced));
        return NodeOutcome.Stop(new DialogueStep(StepKind.Line, line, null, null, Array.Empty<DialogueEvent>()));
    }
}
