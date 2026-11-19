/// <summary>A node of the source tree that has no node in the emitted document, and why: its test was decided by the feature's range (collapsed) or it sat under a branch that can never be taken (unreachable).</summary>
internal sealed record TreePruned(string Source, string Why);
