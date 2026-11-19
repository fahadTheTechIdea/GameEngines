using ScenarioEngine.Contracts;

namespace DialogueEngine;

public sealed partial class DialogueRuntime
{
    /// <summary>
    /// What the player was shown, oldest first, for a history-log screen: the newest <paramref name="count"/> entries the document keeps (<see cref="DialogueSet.HistoryLimit"/>). With
    /// <paramref name="newOnly"/> the entries of lines that had been shown before (which includes every line the skip rule passed) are left out (a log of what was actually new to the player).
    /// </summary>
    public IReadOnlyList<HistoryEntry> History(int count = int.MaxValue, bool newOnly = false)
    {
        if (count < 0) throw new EngineCommon.EngineRefusedException(DialogueCodes.Range, "a history request asks for 0 or more entries");
        IReadOnlyList<HistoryEntry> list = newOnly ? State.HistoryList.Where(e => !e.Seen).ToList() : State.HistoryList;
        return count >= list.Count ? list.ToArray() : list.Skip(list.Count - count).ToArray();
    }

    /// <summary>
    /// Passes the line that is showing and then every following line the player has already seen (Ren'Py's "skip read text"; it never passes a choice, a line shown for the first time or the end),
    /// up to <see cref="SkipLimit"/> lines, and returns what it stopped at. Each line it passed stays in the history, marked skipped. Refused when no line is showing.
    /// </summary>
    public DialogueStep SkipSeen(IReadOnlyDictionary<string, FactValue> external)
    {
        var (def, node) = Cursor(Waiting.Line);
        return Execute(external, def, context =>
        {
            var step = Run(context, def, ((LineNode)node).Next);
            var skipped = 0;
            while (step.Kind == StepKind.Line && step.Line!.Seen && skipped < SkipLimit)
            {
                context.MarkLastSkipped(step.Line.Conversation, step.Line.Node);
                skipped++;
                var current = Set.Conversations[context.State.Conversation!];
                step = Run(context, current, ((LineNode)current.Nodes[context.State.Node!]).Next);
            }
            if (skipped > 0) context.Events.Add(new SkippedEvent(skipped));
            return step;
        });
    }
}
