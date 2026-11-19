namespace AIEngine;

/// <summary>Runs its children in order and fails at the first that fails; succeeds when all have.</summary>
public sealed class SequenceNode(string id, int slot, IReadOnlyList<Node> children, bool resume) : CompositeNode(id, slot, children, resume, Status.Failure, Status.Success);