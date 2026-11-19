namespace Beep.SimEngine.Standard
{
    /// <summary>Raise <see cref="Squads"/> squads of a formation for a company. Each squad has its own seed derived from <see cref="Seed"/> and its position in the batch.</summary>
    public sealed class SpawnFormationRequest
    {
        public SpawnFormationRequest(string formation, int squads, PlayerId company, ulong seed, SpawnLayout layout)
        {
            Formation = formation;
            Squads = squads;
            Company = company;
            Seed = seed;
            Layout = layout;
        }

        public string Formation { get; }
        public int Squads { get; }
        public PlayerId Company { get; }
        public ulong Seed { get; }
        public SpawnLayout Layout { get; }
    }
}
