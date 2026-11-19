namespace DialogueEngine;

/// <summary>
/// A node of a conversation. Each kind is its own class that reads itself, names the nodes it can lead to and executes itself; the runtime knows
/// only this contract.
/// </summary>
public abstract class Node(string id)
{
    public string Id { get; } = id;

    /// <summary>The name of the kind in a document.</summary>
    public abstract string Kind { get; }

    /// <summary>Every node this one can lead to, for the checks on reachability and loops.</summary>
    public abstract IEnumerable<string> Targets { get; }

    /// <summary>True when the player has to act before the conversation goes on.</summary>
    public abstract bool Waits { get; }

    internal abstract NodeOutcome Enter(StepContext context);
}