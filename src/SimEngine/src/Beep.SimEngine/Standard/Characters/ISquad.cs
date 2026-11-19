using Beep.SimEngine.Kernel;

namespace Beep.SimEngine.Standard
{
    /// <summary>
    /// A body of characters represented by one entity: the formation it was raised from, a seed, how many slots it had, which are lost
    /// and which are stood in for by individual entities. A slot that is neither lost nor materialized is an unnamed member whose
    /// identity is a pure function of the seed and the slot, so it costs nothing to keep.
    /// </summary>
    public interface ISquad : IEntityCapability
    {
        string Formation { get; }
        ulong Seed { get; }
        /// <summary>Slots the squad was raised with.</summary>
        int Total { get; }
        int Lost { get; }
        int Materialized { get; }
        /// <summary>Members still alive, materialized or not.</summary>
        int Headcount { get; }
        /// <summary>Alive members that exist only as a count.</summary>
        int Aggregate { get; }
        bool IsLost(int slot);
        bool TryMaterialized(int slot, out EntityId character);
    }
}
