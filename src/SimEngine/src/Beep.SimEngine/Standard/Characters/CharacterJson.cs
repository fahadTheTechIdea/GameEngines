using System.Collections.Generic;
using Beep.SimEngine.Content;

namespace Beep.SimEngine.Standard
{
    /// <summary>The strict reading helpers the character definitions share: an array must be present, and every entry is read as an object whose unknown keys fail the load.</summary>
    internal static class CharacterJson
    {
        public static List<JsonReader> Entries(JsonReader reader, string key)
        {
            JsonValue array = reader.Array(key) ?? throw reader.Error("missing '" + key + "'");
            var entries = new List<JsonReader>(array.Count);
            for (int i = 0; i < array.Count; i++) entries.Add(new JsonReader(array[i], reader.Where + "." + key + "[" + i + "]"));
            return entries;
        }

        public static string[] RequiredStrings(JsonReader reader, string key)
        {
            if (!reader.Has(key)) throw reader.Error("missing '" + key + "'");
            return reader.Strings(key);
        }

        public static List<EquipmentLink> Equipment(JsonReader reader, string key)
        {
            var links = new List<EquipmentLink>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (JsonReader entry in Entries(reader, key))
            {
                string item = entry.String("item");
                int count = entry.Int("count");
                entry.End();
                if (string.IsNullOrWhiteSpace(item) || !seen.Add(item)) throw reader.Error("equipment items must be non-blank and unique, but found '" + item + "'");
                if (count < 1) throw reader.Error("equipment '" + item + "' needs a count of at least 1");
                links.Add(new EquipmentLink(item, count));
            }
            return links;
        }
    }
}
