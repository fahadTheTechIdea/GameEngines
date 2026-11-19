using Beep.SimEngine.Content;

namespace Beep.SimEngine.Standard
{
    /// <summary>A trained ability with levels from zero to <see cref="MaxLevel"/>.</summary>
    public sealed class CharacterSkillDef : Def
    {
        public int MaxLevel { get; private set; }

        protected override void ReadCore(JsonReader reader)
        {
            MaxLevel = reader.Int("max_level");
            if (MaxLevel < 1) throw reader.Error("max_level must be at least 1");
        }
    }
}
