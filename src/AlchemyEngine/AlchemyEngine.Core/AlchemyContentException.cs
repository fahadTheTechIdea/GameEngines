namespace AlchemyEngine;

/// <summary>Alchemy content or a saved discovery book was refused. The code is one of <see cref="AlchemyCodes"/>.</summary>
public sealed class AlchemyRefusedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
