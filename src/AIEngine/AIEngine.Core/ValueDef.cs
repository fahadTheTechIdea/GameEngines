using ScenarioEngine.Contracts;

namespace AIEngine;

/// <summary>A typed slot a document declares: a blackboard key, a percept attribute or an intent argument. Integers carry a range; the others do not.</summary>
public sealed record ValueDef(int Index, string Id, FactKind Kind, long Minimum, long Maximum)
{
    public const long Limit = 1_000_000_000_000;
    public const int TextLimit = 256;

    public FactDefinition Fact => Kind == FactKind.Integer ? new FactDefinition(Kind, Minimum, Maximum) : new FactDefinition(Kind);

    /// <summary>Whether the value belongs to this declaration: the right shape, inside the range, no longer than <see cref="TextLimit"/>.</summary>
    public bool Accepts(FactValue value)
    {
        if (value.Kind != Kind) return false;
        switch (Kind)
        {
            case FactKind.Boolean: return true;
            case FactKind.Integer: return value.Integer >= Minimum && value.Integer <= Maximum;
            case FactKind.String: break;
        }
        return value.Text is { } text && text.Length <= TextLimit;
    }
}