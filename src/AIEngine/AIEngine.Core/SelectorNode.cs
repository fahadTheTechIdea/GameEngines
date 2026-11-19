namespace AIEngine;

/// <summary>Runs its children in order and succeeds at the first that succeeds; fails when none has.</summary>
public sealed class SelectorNode(string id, int slot, IReadOnlyList<Node> children, bool resume) : CompositeNode(id, slot, children, resume, Status.Success, Status.Failure);