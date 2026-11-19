using EngineCommon;
namespace DialogueEngine;

public enum HistoryKind { Line, Answer }

/// <summary>
/// One thing the player was shown, kept by the engine for a history-log screen. A line carries who said it, its key and the values it showed; an answer carries the choice node, the id taken and the
/// key of its text. <see cref="Turn"/> counts entries since the session began (it never repeats, and gaps show that older entries were pushed out); <see cref="Tick"/> is the number the host gave the
/// runtime for the call that recorded it. <see cref="Seen"/> says a line had been shown before; <see cref="Skipped"/> says it was passed by the skip-seen rule instead of being read;
/// <see cref="Replaced"/> says its key is a content-warning alternative.
/// </summary>
public sealed record HistoryEntry(long Turn, long Tick, HistoryKind Kind, string Conversation, string Node, string Speaker, string Key, IReadOnlyList<MessageArg> Args, string Chosen, bool Seen, bool Skipped, bool Replaced);
