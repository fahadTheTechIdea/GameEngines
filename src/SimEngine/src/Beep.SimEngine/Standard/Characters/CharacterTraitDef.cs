using Beep.SimEngine.Content;

namespace Beep.SimEngine.Standard
{
    /// <summary>A named disposition a character may have. The label is a text key for the presenter; the engine only carries the id.</summary>
    public sealed class CharacterTraitDef : Def
    {
        public string Label { get; private set; }

        protected override void ReadCore(JsonReader reader)
        {
            Label = reader.String("label");
            if (string.IsNullOrWhiteSpace(Label)) throw reader.Error("label must not be blank");
        }
    }
}
