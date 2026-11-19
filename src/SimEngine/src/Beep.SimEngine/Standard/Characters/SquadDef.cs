using Beep.SimEngine.Content;

namespace Beep.SimEngine.Standard
{
    /// <summary>Marks an entity definition as a squad and states the largest squad it can hold.</summary>
    public sealed class SquadDef : ComponentDef<SquadComponent>, ISquadDefinition
    {
        public const int Ceiling = 1 << 24;

        public int MaxMembers { get; private set; }

        protected override void ReadCore(JsonReader reader)
        {
            MaxMembers = reader.Int("max_members");
            if (MaxMembers < 1 || MaxMembers > Ceiling) throw reader.Error("max_members must be 1 to " + Ceiling + ", not " + MaxMembers);
        }
    }
}
