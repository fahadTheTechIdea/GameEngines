using EngineCommon;
using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>What a call produced: <see cref="Line"/> a line to show, <see cref="Choices"/> answers to offer, <see cref="Ended"/> the end of the conversation, or <see cref="Queued"/> a start that was put in the queue.</summary>
public enum StepKind { Line, Choices, Ended, Queued }


/// <summary>
/// A line to show: who says it (and with which portrait and emotion), the localisation key and the values it shows, the tags the author put on it (for the host to act on: a sound, a camera),
/// the id of the recorded voice line if there is one, how long a host that advances by itself should wait, and whether the player has been shown this line before (<paramref name="Seen"/>: the node was
/// entered earlier, or this is the line shown again after an interruption), which is what a host needs to offer skipping text already read. <paramref name="Glue"/> says the next line continues this
/// one on the same output line (Ink's glue). <paramref name="Replaced"/> says the key is the content-warning alternative of the line, not its own text. None of these is text; the text is the locale table's.
/// </summary>
public sealed record LineStep(string Conversation, string Node, string Speaker, string Portrait, string Key, IReadOnlyList<MessageArg> Args,
    IReadOnlyList<string>? Tags = null, string? Voice = null, string? Emotion = null, int AutoAdvanceMs = 0, bool Seen = false, bool Glue = false, bool Replaced = false)
{
    public IReadOnlyList<string> Tags { get; init; } = Tags ?? Array.Empty<string>();
}

public sealed record ChoiceOption(string Id, string Key, bool Enabled, IReadOnlyList<string>? Tags = null, bool Replaced = false)
{
    public IReadOnlyList<string> Tags { get; init; } = Tags ?? Array.Empty<string>();
}

/// <summary>The answers offered. A choice with a timeout names the answer taken if the host calls <see cref="DialogueRuntime.Timeout"/> after <paramref name="TimeoutMs"/>; the engine has no clock, the host keeps the timer.</summary>
public sealed record ChoiceStep(string Conversation, string Node, IReadOnlyList<ChoiceOption> Options, int TimeoutMs = 0, string? TimeoutOption = null);

/// <summary>What a call produced: a line to show, choices to offer, the end of the conversation or a queued start, plus the events raised on the way.</summary>
public sealed record DialogueStep(StepKind Kind, LineStep? Line, ChoiceStep? Choices, string? Result, IReadOnlyList<DialogueEvent> Events)
{
    /// <summary>For an ended step: the conversation that ended last (the one that was started or that preempted, not a callee), so a host that queued or preempted knows which one the end belongs to.</summary>
    public string? EndedConversation { get; init; }
}
