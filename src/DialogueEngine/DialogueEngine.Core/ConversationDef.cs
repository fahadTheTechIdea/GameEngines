namespace DialogueEngine;

/// <summary>What starting this conversation does while another one is open.</summary>
public enum BusyPolicy
{
    /// <summary>Refuse the start (<see cref="DialogueCodes.Busy"/>).</summary>
    Refuse,
    /// <summary>Wait in the queue until everything open has finished, then start (highest priority first, then first come first served).</summary>
    Queue,
    /// <summary>Break into the open conversation when this one has a strictly higher priority and the open one allows it; the open one resumes when this one finishes.</summary>
    Preempt,
}

/// <summary>A conversation: where it starts, its nodes, its priority (0 to 1000), what starting it does while another is open, and whether a preempter may break into it.</summary>
public sealed record ConversationDef(string Id, string Start, IReadOnlyDictionary<string, Node> Nodes, int Priority = 0, BusyPolicy OnBusy = BusyPolicy.Refuse, bool Interruptible = true);
