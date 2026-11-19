namespace Beep.SimEngine.Standard
{
    /// <summary>Create <see cref="Count"/> individually simulated characters of one archetype for a company. Character i is drawn from <see cref="Seed"/> and i.</summary>
    public sealed class SpawnIndividualsRequest
    {
        public SpawnIndividualsRequest(string archetype, int count, PlayerId company, ulong seed, SpawnLayout layout)
        {
            Archetype = archetype;
            Count = count;
            Company = company;
            Seed = seed;
            Layout = layout;
        }

        public string Archetype { get; }
        public int Count { get; }
        public PlayerId Company { get; }
        public ulong Seed { get; }
        public SpawnLayout Layout { get; }
    }
}
