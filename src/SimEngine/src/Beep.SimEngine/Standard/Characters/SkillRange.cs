namespace Beep.SimEngine.Standard
{
    /// <summary>One skill of an archetype and the level range its characters draw from, up to the skill's own maximum.</summary>
    public sealed class SkillRange
    {
        public SkillRange(CharacterSkillDef skill, int min, int max) { Skill = skill; Min = min; Max = max; }
        public CharacterSkillDef Skill { get; }
        public int Min { get; }
        public int Max { get; }
    }
}
