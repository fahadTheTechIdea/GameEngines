namespace CombatEngine;

/// <summary>Combat rules or a request were refused. The code is one of <see cref="CombatCodes"/>.</summary>
public sealed class CombatRefusedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
