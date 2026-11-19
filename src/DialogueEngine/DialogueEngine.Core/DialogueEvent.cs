using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>Something a step did that the host may need to hear: a signal to act on, a quest that moved, a variable that changed, a conversation that began, ended, resumed, was preempted or queued.</summary>
public abstract record DialogueEvent;

/// <summary>A signal for the host, with the values its arguments had when it was raised (variables are read at that moment).</summary>
public sealed record SignalEvent : DialogueEvent
{
    public SignalEvent(string name, IReadOnlyList<FactValue>? args = null)
    {
        Name = name;
        Args = args ?? Array.Empty<FactValue>();
    }

    public string Name { get; }
    public IReadOnlyList<FactValue> Args { get; }

    protected override bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("Name = ").Append(Name);
        if (Args.Count > 0) builder.Append(", Args = [").Append(string.Join(", ", Args.Select(a => a.Kind == FactKind.Integer ? a.Integer.ToString(System.Globalization.CultureInfo.InvariantCulture) : a.Kind == FactKind.Boolean ? (a.Boolean ? "true" : "false") : a.Text))).Append("]");
        return true;
    }
}

public sealed record QuestEvent(string Quest, QuestStatus Status, string Stage) : DialogueEvent;

public sealed record VariableEvent(string Id, FactValue Value) : DialogueEvent;

/// <summary>What happened to a conversation in the priority rules.</summary>
public enum ConversationChange
{
    /// <summary>It finished while something followed it (a resumed or queued conversation); <c>Other</c> is its result.</summary>
    Ended,
    /// <summary>It was taken from the queue and started.</summary>
    Started,
    /// <summary>It was broken into by a higher-priority conversation, named in <c>Other</c>.</summary>
    Preempted,
    /// <summary>It was put back where it was after its preempter finished.</summary>
    Resumed,
    /// <summary>It was put in the queue; <c>Position</c> is its place (1 is next).</summary>
    Queued,
}

public sealed record ConversationEvent(ConversationChange Change, string Conversation, string Other, int Position) : DialogueEvent;

/// <summary>The skip-seen rule passed this many already-seen lines.</summary>
public sealed record SkippedEvent(int Count) : DialogueEvent;
