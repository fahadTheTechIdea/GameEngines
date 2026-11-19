namespace AIEngine;

/// <summary>
/// One input of a utility option: an expression read between <see cref="Minimum"/> and <see cref="Maximum"/> (clamped), turned into 0 to 1000 and shaped
/// by a curve. <see cref="At"/> is the threshold of a step curve. An inverted curve scores 1000 minus the shape.
/// </summary>
public sealed class Consideration
{
    private readonly Expr _input;
    private readonly long _minimum;
    private readonly long _maximum;
    private readonly CurveKind _curve;
    private readonly bool _invert;
    private readonly long _at;

    internal Consideration(Expr input, long minimum, long maximum, CurveKind curve, bool invert, long at)
    {
        _input = input;
        _minimum = minimum;
        _maximum = maximum;
        _curve = curve;
        _invert = invert;
        _at = at;
    }

    internal long Score(ThinkContext c)
    {
        var raw = Math.Clamp(_input.Eval(c).Number, _minimum, _maximum);
        var x = (raw - _minimum) * 1000 / (_maximum - _minimum);
        var y = Shape(_curve, x, _at);
        return _invert ? 1000 - y : y;
    }

    /// <summary>The curve of a normalised input, both 0 to 1000.</summary>
    public static long Shape(CurveKind curve, long x, long at)
    {
        switch (curve)
        {
            case CurveKind.Linear: return x;
            case CurveKind.Quadratic: return x * x / 1000;
            case CurveKind.Cubic: return x * x / 1000 * x / 1000;
            case CurveKind.Smooth: return x * x / 1000 * (3000 - 2 * x) / 1000;
            case CurveKind.Step: break;
        }
        return x >= at ? 1000 : 0;
    }
}