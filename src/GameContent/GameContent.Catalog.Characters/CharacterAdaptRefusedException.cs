namespace GameContent.Catalog;

/// <summary>A catalog formation or a template cannot be used as it stands. The code is one of <see cref="CharacterAdaptCodes"/>.</summary>
public sealed class CharacterAdaptRefusedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
