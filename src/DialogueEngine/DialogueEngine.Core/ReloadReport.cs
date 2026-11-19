namespace DialogueEngine;

/// <summary>
/// What a hot reload did to the state: what it added, the visit counts of nodes that no longer exist (dropped), and, when the open conversation could not go on and the reload was asked to
/// restart it, where it had been (<c>conversation/node</c>), otherwise null; how many visit counts a <see cref="ReloadMap"/> moved to a new name, how many history entries were dropped (their node is gone, or the new document keeps fewer) and how many queued conversations were dropped when the open conversation was closed.
/// </summary>
public sealed record ReloadReport(IReadOnlyList<string> AddedVariables, IReadOnlyList<string> AddedQuests, int DroppedVisits, string? ClosedConversation = null, int RenamedVisits = 0, int DroppedHistory = 0, int DroppedQueued = 0);