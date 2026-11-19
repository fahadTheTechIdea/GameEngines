namespace Beep.SimEngine.Standard
{
    /// <summary>One stat of an archetype and the range its characters draw from, inside the stat's own limits.</summary>
    public sealed class StatRange
    {
        public StatRange(CharacterStatDef stat, int min, int max) { Stat = stat; Min = min; Max = max; }
        public CharacterStatDef Stat { get; }
        public int Min { get; }
        public int Max { get; }
    }
}
