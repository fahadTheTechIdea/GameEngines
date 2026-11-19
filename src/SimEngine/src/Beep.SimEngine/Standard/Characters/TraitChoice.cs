namespace Beep.SimEngine.Standard
{
    /// <summary>A trait an archetype may give, with the weight of its draw (a higher weight is likelier).</summary>
    public sealed class TraitChoice
    {
        public TraitChoice(CharacterTraitDef trait, int weight) { Trait = trait; Weight = weight; }
        public CharacterTraitDef Trait { get; }
        public int Weight { get; }
    }
}
