namespace DialogueEngine;

/// <summary>
/// Runs another conversation as a subflow and comes back: the callee ends with a <c>return</c> node, and this conversation goes on at <see cref="Next"/>. The call does not wait; a callee that
/// ends with <c>end</c> ends the whole conversation. Conversations may not call each other in a circle, and calls nest to <see cref="DialogueRuntime.CallDepthLimit"/>.
/// </summary>
public sealed class CallNode(string id, string conversation, string next) : Node(id)
{
    public string Conversation { get; } = conversation;
    public string Next { get; } = next;

    public override string Kind => "call";
    public override IEnumerable<string> Targets => [Next];
    public override bool Waits => false;

    internal override NodeOutcome Enter(StepContext context)
    {
        context.Trace?.Note("calls " + Conversation + ", comes back to " + Next);
        return NodeOutcome.Call(this);
    }
}