namespace Beep.SimEngine.Standard
{
    /// <summary>A squad slot became an individual entity.</summary>
    public readonly struct CharacterMaterialized
    {
        public const string EventId = "sim.characters.materialized";

        public CharacterMaterialized(EntityId squad, int slot, EntityId character)
        {
            Squad = squad;
            Slot = slot;
            Character = character;
        }

        public EntityId Squad { get; }
        public int Slot { get; }
        public EntityId Character { get; }
    }
}
