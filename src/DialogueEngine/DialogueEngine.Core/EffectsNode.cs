namespace DialogueEngine;

/// <summary>Applies effects and goes on.</summary>
public sealed class EffectsNode(string id, IReadOnlyList<Effect> effects, string next) : Node(id)
{
    public IReadOnlyList<Effect> Effects { get; } = effects;
    public string Next { get; } = next;

    public override string Kind => "effects";
    public override IEnumerable<string> Targets => [Next];
    public override bool Waits => false;

    internal override NodeOutcome Enter(StepContext context)
    {
        foreach (var effect in Effects) effect.Apply(context);
        context.Trace?.Note(Effects.Count + " effects applied");
        return NodeOutcome.Go(Next);
    }
}