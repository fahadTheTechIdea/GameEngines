namespace DialogueEngine;

/// <summary>
/// One place a conversation will come back to. A <c>call</c> pushes the call node (its callee ends with <c>return</c> and the conversation goes on at the call's next);
/// an interruption pushes the node that was waiting (<paramref name="Reenter"/>), and the conversation shows it again when the interrupter returns.
/// A preemption (<paramref name="Preempted"/>, always with <paramref name="Reenter"/>) is a higher-priority conversation breaking into a lower one: unlike an interruption it
/// survives the preempter's own <c>end</c>, so the lower conversation resumes whichever way the preempter finishes.
/// </summary>
public readonly record struct DialogueFrame(string Conversation, string Node, bool Reenter, bool Preempted = false);
