using System.Collections.Generic;
using Beep.SimEngine.Kernel;

namespace Beep.SimEngine.Standard
{
    /// <summary>
    /// The identity of one individually simulated character: who it is, drawn from its archetype by a seed and an index, and which
    /// squad slot it was materialized from (if any). Everything here is saved and hashed.
    /// </summary>
    public interface ICharacter : IEntityCapability
    {
        /// <summary>The archetype id, or empty before the factory has assigned one (only inside the factory's own command).</summary>
        string Archetype { get; }
        string Name { get; }
        ulong Seed { get; }
        int Index { get; }
        IReadOnlyList<int> Stats { get; }
        IReadOnlyList<int> Skills { get; }
        IReadOnlyList<string> Traits { get; }
        /// <summary>The squad this character stands in for one slot of, or none.</summary>
        EntityId Squad { get; }
        int Slot { get; }
    }
}
