namespace AIEngine;

/// <summary>One thing a utility node may choose: its optional gate, its considerations, a weight in permille and the subtree it runs when chosen.</summary>
public sealed class UtilityOption
{
    private readonly Cond? _when;
    private readonly IReadOnlyList<Consideration> _considerations;
    private readonly long _weight;

    internal UtilityOption(string id, Cond? when, IReadOnlyList<Consideration> considerations, long weight, Node child)
    {
        Id = id;
        _when = when;
        _considerations = considerations;
        _weight = weight;
        Child = child;
    }

    public string Id { get; }
    public Node Child { get; }
    public int ConsiderationCount => _considerations.Count;

    /// <summary>
    /// The score in permille: the gate (false scores 0), then each consideration score is made up a little for there being several (a product of several numbers below one is small
    /// just because there are several: with n considerations each score s becomes s + (1 - s) x (1 - 1/n) x s), the made-up scores are multiplied, then the weight is applied.
    /// This is the compensation factor of Dave Mark's IAUS; the formula is checked against the nine published values for a score of one half (see the tests), not against his slides.
    /// </summary>
    internal long Score(ThinkContext c)
    {
        if (_when is not null && !_when.Test(c)) return 0;
        var n = _considerations.Count;
        var modification = 1000 - 1000 / n;
        long product = 1000;
        foreach (var consideration in _considerations)
        {
            var score = consideration.Score(c);
            if (n > 1) score += (1000 - score) * modification / 1000 * score / 1000;
            product = product * score / 1000;
        }
        return product * _weight / 1000;
    }
}