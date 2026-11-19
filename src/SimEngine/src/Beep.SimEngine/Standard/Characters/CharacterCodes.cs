namespace Beep.SimEngine.Standard
{
    /// <summary>Why the character factory refused a request. A refusal changes nothing in the world.</summary>
    public static class CharacterCodes
    {
        public static readonly Code UnknownFormation = new Code("characters.unknown-formation");
        public static readonly Code UnknownArchetype = new Code("characters.unknown-archetype");
        public static readonly Code UnknownSquad = new Code("characters.unknown-squad");
        public static readonly Code BadCount = new Code("characters.bad-count");
        public static readonly Code TooMany = new Code("characters.too-many");
        public static readonly Code BadLayout = new Code("characters.bad-layout");
        public static readonly Code NoPlacement = new Code("characters.no-placement");
        public static readonly Code UnexpectedPlacement = new Code("characters.unexpected-placement");
        public static readonly Code UnsupportedSpace = new Code("characters.unsupported-space");
        public static readonly Code IdentitiesExhausted = new Code("characters.identities-exhausted");
        public static readonly Code BadSlot = new Code("characters.bad-slot");
        public static readonly Code SlotLost = new Code("characters.slot-lost");
        public static readonly Code SlotMaterialized = new Code("characters.slot-materialized");
        public static readonly Code NotMaterialized = new Code("characters.not-materialized");
        public static readonly Code NotEnoughMembers = new Code("characters.not-enough-members");
        public static readonly Code BadRosterEntry = new Code("characters.bad-roster-entry");
        public static readonly Code Blocking = new Code("characters.blocking-individual");
    }
}
