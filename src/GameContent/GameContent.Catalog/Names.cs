using System.Text;

namespace GameContent.Catalog;

/// <summary>The JSON spelling of an enum member: lower-case words joined by hyphens. Parsing accepts exactly that spelling and nothing else.</summary>
public static class Names<T> where T : struct, Enum
{
    private static readonly Dictionary<string, T> ByName = Enum.GetValues<T>().ToDictionary(Spell, StringComparer.Ordinal);
    private static readonly Dictionary<T, string> ByValue = Enum.GetValues<T>().ToDictionary(v => v, Spell);

    public static string Of(T value) => ByValue[value];

    /// <summary>The member spelled this way. An unknown spelling is a caller mistake: schema validation has already admitted only the declared ones.</summary>
    public static T Parse(string name) => ByName[name];

    public static bool TryParse(string name, out T value) => ByName.TryGetValue(name, out value);

    private static string Spell(T value)
    {
        var text = value.ToString();
        var spelled = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsUpper(text[i])) spelled.Append('-');
            spelled.Append(char.ToLowerInvariant(text[i]));
        }
        return spelled.ToString();
    }
}
