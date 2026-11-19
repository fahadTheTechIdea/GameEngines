namespace DialogueEngine;

/// <summary>Takes the first case whose condition holds; the otherwise target is required, so no path ends by falling off.</summary>
public sealed class BranchNode(string id, IReadOnlyList<BranchCase> cases, string otherwise) : Node(id)
{
    public IReadOnlyList<BranchCase> Cases { get; } = cases;
    public string Otherwise { get; } = otherwise;

    public override string Kind => "branch";
    public override IEnumerable<string> Targets => Cases.Select(c => c.Next).Append(Otherwise);
    public override bool Waits => false;

    internal override NodeOutcome Enter(StepContext context)
    {
        var tried = context.Trace is null ? null : new List<string>();
        for (var i = 0; i < Cases.Count; i++)
        {
            if (context.Test(Cases[i].When))
            {
                tried?.Add("case " + i + " held: " + context.Explain(Cases[i].When));
                context.Trace?.Note(string.Join("; ", tried!));
                return NodeOutcome.Go(Cases[i].Next);
            }
            tried?.Add("case " + i + " failed: " + context.Explain(Cases[i].When));
        }
        tried?.Add("otherwise");
        context.Trace?.Note(string.Join("; ", tried!));
        return NodeOutcome.Go(Otherwise);
    }
}