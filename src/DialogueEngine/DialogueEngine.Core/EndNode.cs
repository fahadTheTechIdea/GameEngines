namespace DialogueEngine;

/// <summary>Ends the conversation with a result the host can act on.</summary>
public sealed class EndNode(string id, string result) : Node(id)
{
    public string Result { get; } = result;

    public override string Kind => "end";
    public override IEnumerable<string> Targets => [];
    public override bool Waits => false;

    internal override NodeOutcome Enter(StepContext context) => NodeOutcome.End(Result);
}