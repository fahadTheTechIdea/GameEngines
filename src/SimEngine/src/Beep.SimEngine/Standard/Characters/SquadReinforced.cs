namespace Beep.SimEngine.Standard
{
    /// <summary>A squad took in new members. <see cref="Remaining"/> is its headcount afterwards, individuals included.</summary>
    public readonly struct SquadReinforced
    {
        public const string EventId = "sim.characters.reinforced";

        public SquadReinforced(EntityId squad, int added, int remaining)
        {
            Squad = squad;
            Added = added;
            Remaining = remaining;
        }

        public EntityId Squad { get; }
        public int Added { get; }
        public int Remaining { get; }
    }
}
