namespace AIEngine;

/// <summary>The smallest and largest value an expression can take, worked out from the document alone, so arithmetic can be proven not to overflow and results proven to fit.</summary>
public readonly record struct Interval(long Low, long High)
{
    public static Interval Exactly(long value) => new(value, value);
    public static Interval Flag => new(0, 1);

    public bool Within(long low, long high) => Low >= low && High <= high;
}