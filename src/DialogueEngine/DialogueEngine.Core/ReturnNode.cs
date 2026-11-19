namespace DialogueEngine;

/// <summary>
/// Ends a subflow and goes back to whoever called or interrupted it. In a conversation that was not called, it ends the conversation with <see cref="Result"/> like <c>end</c>.
/// </summary>
public sealed class ReturnNode(string id, string result) : Node(id)
{
    public string Result { get; } = result;

    public override string Kind => "return";
    public override IEnumerable<string> Targets => [];
    public override bool Waits => false;

    internal override NodeOutcome Enter(StepContext context)
    {
        context.Trace?.Note(context.State.FrameList.Count == 0 ? "ends the conversation with " + Result : "returns");
        return NodeOutcome.Return(Result);
    }
}