using EngineCommon;

namespace AIEngine;

/// <summary>
/// Chooses one child by weight with a seeded roll and runs it; while the chosen child reports Running it is kept, and a new roll is drawn only after it has
/// finished. A roll is a pure function of the seed, the agent, this node and the agent roll counter.
/// </summary>
public sealed class RandomNode(string id, int slot, IReadOnlyList<RandomOption> options)
    : Node(id, slot, 1 + options.Max(o => o.Child.Bound), options.Any(o => o.Child.MayRun))
{
    private readonly long _total = options.Sum(o => o.Weight);

    internal override Status Execute(ThinkContext c)
    {
        c.Charge(1);
        var slots = c.Agent.Slots;
        var chosen = (int)slots[Slot] - 1;
        if (chosen < 0)
        {
            var roll = DeterministicRoll.Below(c.Seed, "ai:" + c.Agent.Id + ":" + Id, c.Agent.RollCounter++, _total);
            chosen = 0;
            while (roll >= options[chosen].Weight)
            {
                roll -= options[chosen].Weight;
                chosen++;
            }
            c.Trace?.Note("rolled option " + chosen + " of " + options.Count);
        }
        var status = options[chosen].Child.Run(c);
        if (status == Status.Running)
        {
            slots[Slot] = chosen + 1;
            c.Alive[Slot] = true;
        }
        else slots[Slot] = 0;
        return status;
    }
}