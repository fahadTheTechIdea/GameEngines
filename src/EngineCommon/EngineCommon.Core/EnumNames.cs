using System.Text;

namespace EngineCommon;

/// <summary>
/// The names of the members of a closed set (a kind, a phase, a state) in documents, facts, journals and events: the member in snake case (<c>NonAggression</c> is <c>non_aggression</c>), so a document, a fact and an
/// event cannot disagree. One place turns a member into its name and a name into its member; a name that is none of the members is no member (null) and the caller refuses it by name.
/// </summary>
public static class EnumNames
{
    public static string Name<T>(T value) where T : struct, Enum
    {
        var text = value.ToString();
        var name = new StringBuilder(text.Length + 4);
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsUpper(text[i]) && i > 0) name.Append('_');
            name.Append(char.ToLowerInvariant(text[i]));
        }
        return name.ToString();
    }

    public static T? Parse<T>(string name) where T : struct, Enum
    {
        foreach (var member in Enum.GetValues<T>())
            if (Name(member) == name) return member;
        return null;
    }

    public static string Choices<T>() where T : struct, Enum => string.Join(", ", Enum.GetValues<T>().Select(Name));
}
