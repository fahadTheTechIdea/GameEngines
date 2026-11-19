using EngineCommon;

namespace DialogueEngine;

/// <summary>Takes one outcome by weight from the seeded roll: the same seed and the same count of earlier rolls give the same outcome, in any order of asking.</summary>
public sealed class RandomNode(string id, IReadOnlyList<RandomOutcome> outcomes) : Node(id)
{
    public IReadOnlyList<RandomOutcome> Outcomes { get; } = outcomes;

    public override string Kind => "random";
    public override IEnumerable<string> Targets => Outcomes.Select(o => o.Next);
    public override bool Waits => false;

    internal override NodeOutcome Enter(StepContext context)
    {
        var total = Outcomes.Sum(o => (long)o.Weight);
        var pick = DeterministicRoll.Below(context.State.Seed, "random:" + context.Conversation!.Id + "." + Id, context.NextRoll(), total);
        foreach (var outcome in Outcomes)
        {
            if (pick < outcome.Weight)
            {
                context.Trace?.Note("rolled " + (total - Outcomes.Skip(Outcomes.ToList().IndexOf(outcome)).Sum(o => (long)o.Weight) + pick) + " of " + total + " -> " + outcome.Next);
                return NodeOutcome.Go(outcome.Next);
            }
            pick -= outcome.Weight;
        }
        throw new InvalidOperationException("a seeded pick fell outside the total weight");
    }
}