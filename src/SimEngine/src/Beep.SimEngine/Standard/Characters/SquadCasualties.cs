namespace Beep.SimEngine.Standard
{
    /// <summary>A squad lost members. <see cref="Remaining"/> is its headcount afterwards, individuals included.</summary>
    public readonly struct SquadCasualties
    {
        public const string EventId = "sim.characters.casualties";

        public SquadCasualties(EntityId squad, int lost, int remaining)
        {
            Squad = squad;
            Lost = lost;
            Remaining = remaining;
        }

        public EntityId Squad { get; }
        public int Lost { get; }
        public int Remaining { get; }
    }
}
