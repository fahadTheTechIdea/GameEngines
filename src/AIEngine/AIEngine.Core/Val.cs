using ScenarioEngine.Contracts;

namespace AIEngine;

/// <summary>
/// One value of a blackboard key, a percept attribute or an argument, without an allocation: a number (a boolean is 0 or 1) or a string. Its
/// kind is not stored, because whoever holds it knows the declaration it belongs to.
/// </summary>
public readonly record struct Val(long Number, string? Text)
{
    public static Val Of(long number) => new(number, null);
    public static Val Of(bool value) => new(value ? 1 : 0, null);
    public static Val Of(string text) => new(0, text);

    public bool Flag => Number != 0;

    // Facts are immutable, so the ones a think asks for again and again (true, false and the small numbers) are made once and shared.
    private static readonly FactValue True = FactValue.From(true);
    private static readonly FactValue False = FactValue.From(false);
    private static readonly FactValue[] Small = Enumerable.Range(0, 1024).Select(n => FactValue.From((long)n)).ToArray();

    public FactValue ToFact(FactKind kind)
    {
        switch (kind)
        {
            case FactKind.Boolean: return Number != 0 ? True : False;
            case FactKind.Integer: return Number is >= 0 and < 1024 ? Small[Number] : FactValue.From(Number);
            case FactKind.String: break;
        }
        return FactValue.From(Text!);
    }

    public static Val From(FactValue value)
    {
        switch (value.Kind)
        {
            case FactKind.Boolean: return Of(value.Boolean);
            case FactKind.Integer: return Of(value.Integer);
            case FactKind.String: break;
        }
        return Of(value.Text!);
    }
}