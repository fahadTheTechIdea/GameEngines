using Beep.SimEngine.Content;

namespace Beep.SimEngine.Standard
{
    /// <summary>A number every archetype may give its characters (strength, courage). The legal range is the content's, not the engine's.</summary>
    public sealed class CharacterStatDef : Def
    {
        public int Min { get; private set; }
        public int Max { get; private set; }

        protected override void ReadCore(JsonReader reader)
        {
            Min = reader.Int("min");
            Max = reader.Int("max");
            if (Min > Max) throw reader.Error("min " + Min + " exceeds max " + Max);
        }
    }
}
