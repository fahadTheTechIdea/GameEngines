namespace GameContent.Catalog;

/// <summary>A catalog item or formation cannot be used in combat as it stands. The code is one of <see cref="CombatAdaptCodes"/>.</summary>
public sealed class CombatAdaptRefusedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
