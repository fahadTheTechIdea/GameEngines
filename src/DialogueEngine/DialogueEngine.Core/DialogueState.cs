using ScenarioEngine.Contracts;

namespace DialogueEngine;

public enum Waiting { Nothing, Line, Choice }

/// <summary>Where a quest stands: its status and the stage it is at ("" when it has not started).</summary>
public readonly record struct QuestProgress(QuestStatus Status, string Stage);

/// <summary>
/// Everything a dialogue session remembers, and nothing the world owns: variables, where each quest stands, how often each node was entered,
/// the conversation cursor with the calls, interruptions and preemptions it is inside, the conversations waiting in the queue, the kept history, and the roll counter. Plain values, so a save is a
/// copy of it. Only the runtime changes it.
/// </summary>
public sealed class DialogueState
{
    internal SortedDictionary<string, FactValue> VariableValues { get; } = new(StringComparer.Ordinal);
    internal SortedDictionary<string, QuestProgress> QuestValues { get; } = new(StringComparer.Ordinal);
    internal SortedDictionary<string, int> VisitCounts { get; } = new(StringComparer.Ordinal);
    internal List<DialogueFrame> FrameList { get; } = new();
    internal List<string> QueueList { get; } = new();
    internal HistoryLog HistoryList { get; } = new();

    public ulong Seed { get; internal set; }
    public long RollCounter { get; internal set; }

    /// <summary>How many history entries have ever been recorded; the next entry's turn.</summary>
    public long Recorded { get; internal set; }
    public string? Conversation { get; internal set; }
    public string? Node { get; internal set; }
    public Waiting WaitingFor { get; internal set; }

    public IReadOnlyDictionary<string, FactValue> Variables => VariableValues;
    public IReadOnlyDictionary<string, QuestProgress> Quests => QuestValues;
    public IReadOnlyDictionary<string, int> Visits => VisitCounts;

    /// <summary>The places the conversation will come back to, oldest first: the calls, interruptions and preemptions it is inside.</summary>
    public IReadOnlyList<DialogueFrame> Frames => FrameList;

    /// <summary>The conversations waiting for the open one to finish, in the order they were queued (they start by priority, then by this order).</summary>
    public IReadOnlyList<string> Queue => QueueList;

    /// <summary>What the player was shown, oldest first, as many as the document keeps.</summary>
    public IReadOnlyList<HistoryEntry> History => HistoryList;

    /// <summary>The state of a new session: every variable at its initial value, every quest not started, nothing visited, no conversation.</summary>
    public static DialogueState Initial(DialogueSet set, ulong seed)
    {
        var state = new DialogueState { Seed = seed };
        foreach (var variable in set.Variables.Values) state.VariableValues[variable.Id] = variable.Initial;
        foreach (var quest in set.Quests.Values) state.QuestValues[quest.Id] = new QuestProgress(QuestStatus.NotStarted, "");
        return state;
    }
}
