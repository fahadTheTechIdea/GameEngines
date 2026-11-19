using EngineCommon;

namespace DialogueEngine;

/// <summary>What a <c>variant</c> node does the n-th time it is reached.</summary>
public enum VariantMode
{
    /// <summary>Each in turn, then the last again and again (Ink sequence).</summary>
    Sequence,
    /// <summary>Each in turn, then round to the first (Ink cycle).</summary>
    Cycle,
    /// <summary>Each in turn once, then the <c>exhausted</c> node (Ink once).</summary>
    Once,
    /// <summary>Each once in a seeded order, a new order every round (Ink shuffle).</summary>
    Shuffle,
}

/// <summary>
/// Content that changes by how often it has been seen, the way Ink's sequences, cycles, once-only and shuffled content do. The choice depends on the seed and the visit count of this node
/// alone, both of which a save keeps, so a saved game continues with the same variant and a replay shows the same.
/// </summary>
public sealed class VariantNode(string id, VariantMode mode, IReadOnlyList<string> options, string? exhausted) : Node(id)
{
    public VariantMode Mode { get; } = mode;
    public IReadOnlyList<string> Options { get; } = options;
    public string? Exhausted { get; } = exhausted;

    public override string Kind => "variant";
    public override IEnumerable<string> Targets => Exhausted is null ? Options : Options.Append(Exhausted);
    public override bool Waits => false;

    /// <summary>The index of the option for the given number of earlier visits, or -1 for the exhausted node.</summary>
    internal int Pick(ulong seed, string conversation, long earlier)
    {
        var n = Options.Count;
        switch (Mode)
        {
            case VariantMode.Sequence: return (int)Math.Min(earlier, n - 1);
            case VariantMode.Cycle: return (int)(earlier % n);
            case VariantMode.Once: return earlier < n ? (int)earlier : -1;
            case VariantMode.Shuffle: return Permutation(seed, conversation, earlier / n)[(int)(earlier % n)];
        }
        throw new InvalidOperationException("unknown variant mode");
    }

    /// <summary>The order of one round of a shuffle: Fisher-Yates over the seeded roll of this node and round, so it needs no state beyond the visit count.</summary>
    private int[] Permutation(ulong seed, string conversation, long round)
    {
        var order = Enumerable.Range(0, Options.Count).ToArray();
        for (var i = order.Length - 1; i > 0; i--)
        {
            var j = (int)DeterministicRoll.Below(seed, "shuffle:" + conversation + "." + Id + ":" + round, i, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        return order;
    }

    internal override NodeOutcome Enter(StepContext context)
    {
        var pick = Pick(context.State.Seed, context.Conversation!.Id, context.EnteredBefore);
        var target = pick < 0 ? Exhausted! : Options[pick];
        context.Trace?.Note(Mode.ToString().ToLowerInvariant() + ", entered " + context.EnteredBefore + " times before -> " + target);
        return NodeOutcome.Go(target);
    }
}