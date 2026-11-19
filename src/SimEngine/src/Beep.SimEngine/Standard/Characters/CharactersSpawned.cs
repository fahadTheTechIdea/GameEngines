namespace Beep.SimEngine.Standard
{
    /// <summary>Individually simulated characters of one archetype were created.</summary>
    public readonly struct CharactersSpawned
    {
        public const string EventId = "sim.characters.spawned";

        public CharactersSpawned(PlayerId company, string archetype, int count, EntityId first)
        {
            Company = company;
            Archetype = archetype;
            Count = count;
            First = first;
        }

        public PlayerId Company { get; }
        public string Archetype { get; }
        public int Count { get; }
        public EntityId First { get; }
    }
}
