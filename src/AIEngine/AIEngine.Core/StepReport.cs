namespace AIEngine;

/// <summary>What one step did: the intents raised (in the order the agents thought), how many agents thought, the work units spent, how many were due and had to wait, and how late the latest thinker was.</summary>
public sealed record StepReport(long Tick, IReadOnlyList<Intent> Intents, int Thought, long Spent, int Deferred, long LongestWait);