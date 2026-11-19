using ScenarioEngine.Contracts;

namespace AIEngine;

/// <summary>
/// A value worked out during a think. Its kind, the range of whole numbers it can take and the longest string it can be are known when the document
/// loads, so what it is assigned to is checked then and a think never overflows or refuses.
/// </summary>
public abstract class Expr(FactKind kind, Interval range, int maxLength)
{
    public FactKind Kind { get; } = kind;
    public Interval Range { get; } = range;
    public int MaxLength { get; } = maxLength;

    internal abstract Val Eval(ThinkContext c);
}