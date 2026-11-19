namespace AIEngine;

/// <summary>
/// Scores its options (in id order) and runs the best. The option chosen last think gets <c>momentum</c> permille added, so a small change in an input does not
/// make the agent flip between two near options; an option that scores 0 is never chosen, and a node with nothing worth doing fails. Ties go to the lower id.
/// </summary>
public sealed class UtilityNode(string id, int slot, long momentum, IReadOnlyList<UtilityOption> options)
    : Node(id, slot, 1 + options.Sum(o => 1 + o.ConsiderationCount) + options.Max(o => o.Child.Bound), options.Any(o => o.Child.MayRun))
{
    public const int MaxOptions = 16;

    internal override Status Execute(ThinkContext c)
    {
        c.Charge(1 + options.Sum(o => 1 + o.ConsiderationCount));
        var slots = c.Agent.Slots;
        var current = (int)slots[Slot] - 1;
        var best = -1;
        long bestScore = 0;
        var scores = c.Trace is null ? null : new System.Text.StringBuilder();
        for (var i = 0; i < options.Count; i++)
        {
            var score = options[i].Score(c);
            scores?.Append(i > 0 ? ", " : "").Append(options[i].Id).Append('=').Append(score);
            if (score <= 0) continue;
            if (i == current) score += momentum;
            if (score > bestScore)
            {
                best = i;
                bestScore = score;
            }
        }
        slots[Slot] = best + 1;
        if (scores is not null) c.Trace!.Note(scores + (best < 0 ? "; none scores above 0" : "; chose " + options[best].Id + (best == current ? " (kept, momentum " + momentum + ")" : "")));
        return best < 0 ? Status.Failure : options[best].Child.Run(c);
    }
}