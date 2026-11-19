using Beep.SimEngine.Content;

namespace Beep.SimEngine.Standard
{
    /// <summary>Marks an entity definition as a character: an entity the factory fills with a name, stats, skills and traits. It takes no settings.</summary>
    public sealed class CharacterDef : ComponentDef<CharacterComponent>, ICharacterDefinition
    {
        protected override void ReadCore(JsonReader reader) { }
    }
}
