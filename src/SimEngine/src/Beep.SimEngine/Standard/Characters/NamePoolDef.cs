using System;
using System.Collections.Generic;
using Beep.SimEngine.Content;

namespace Beep.SimEngine.Standard
{
    /// <summary>The names one archetype draws from. Order is part of the content: the same seed picks the same name only while the list is unchanged.</summary>
    public sealed class NamePoolDef : Def
    {
        public IReadOnlyList<string> Names { get; private set; }

        protected override void ReadCore(JsonReader reader)
        {
            string[] names = reader.Strings("names");
            if (names.Length == 0) throw reader.Error("a name pool needs at least one name");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in names)
                if (string.IsNullOrWhiteSpace(name) || !seen.Add(name)) throw reader.Error("names must be non-blank and unique, but found '" + name + "'");
            Names = Array.AsReadOnly(names);
        }
    }
}
