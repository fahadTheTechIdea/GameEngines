namespace Beep.SimEngine.Standard
{
    /// <summary>Squads of a formation were raised. <see cref="First"/> is the entity id of the first squad; the others follow it in batch order.</summary>
    public readonly struct FormationSpawned
    {
        public const string EventId = "sim.characters.formation-spawned";

        public FormationSpawned(PlayerId company, string formation, int squads, int members, EntityId first)
        {
            Company = company;
            Formation = formation;
            Squads = squads;
            Members = members;
            First = first;
        }

        public PlayerId Company { get; }
        public string Formation { get; }
        public int Squads { get; }
        /// <summary>Characters raised in all: squads times the roster strength.</summary>
        public int Members { get; }
        public EntityId First { get; }
    }
}
