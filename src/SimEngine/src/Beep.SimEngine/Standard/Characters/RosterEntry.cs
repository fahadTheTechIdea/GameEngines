namespace Beep.SimEngine.Standard
{
    /// <summary>How many characters of one archetype a formation holds.</summary>
    public sealed class RosterEntry
    {
        public RosterEntry(CharacterArchetypeDef archetype, int count) { Archetype = archetype; Count = count; }
        public CharacterArchetypeDef Archetype { get; }
        public int Count { get; }
    }
}
