using Xunit;

namespace AIEngine.Tests;

/// <summary>The comparisons and the range arithmetic behind the reductions, checked against brute force on every small range.</summary>
public sealed class TreeOpsTests
{
    // The oracle, written out by hand and indexed by the enum's value (Lt, Lte, Gt, Gte, Eq, Ne), not taken from the code under test.
    private static readonly Func<long, long, bool>[] Oracle =
    [
        (v, t) => v < t, (v, t) => v <= t, (v, t) => v > t, (v, t) => v >= t, (v, t) => v == t, (v, t) => v != t,
    ];

    private static bool Test(long value, TreeOp op, long threshold) => Oracle[(int)op](value, threshold);

    [Fact]
    public void EveryNameParsesBackToItsOpAndAnythingElseIsNotAnOp()
    {
        foreach (var op in Enum.GetValues<TreeOp>())
        {
            Assert.True(TreeOps.TryParse(TreeOps.Name(op), out var parsed));
            Assert.Equal(op, parsed);
        }
        Assert.Equal(new[] { "lt", "lte", "gt", "gte", "eq", "ne" }, Enum.GetValues<TreeOp>().Select(TreeOps.Name));
        Assert.False(TreeOps.TryParse("in", out _));
        Assert.False(TreeOps.TryParse("LTE", out _));
    }

    [Fact]
    public void TheOppositeOfAnOpHoldsExactlyWhereTheOpDoesNot()
    {
        foreach (var op in Enum.GetValues<TreeOp>())
            foreach (var threshold in Enumerable.Range(-3, 7))
                foreach (var value in Enumerable.Range(-5, 11))
                    Assert.NotEqual(Test(value, op, threshold), Test(value, TreeOps.Negate(op), threshold));
    }

    [Fact]
    public void TheNarrowedRangeIsNeverSmallerThanTheValuesThatPassAndIsEmptyOnlyWhenNoneDo()
    {
        foreach (var op in Enum.GetValues<TreeOp>())
            foreach (var threshold in Enumerable.Range(-4, 9))
                for (var low = -3; low <= 3; low++)
                    for (var high = low; high <= 3; high++)
                    {
                        var passing = Enumerable.Range(low, high - low + 1).Where(v => Test(v, op, threshold)).ToList();
                        var span = TreeOps.Holds(new TreeSpan(low, high), op, threshold);
                        if (passing.Count == 0)
                        {
                            Assert.Null(span);
                            continue;
                        }
                        Assert.NotNull(span);
                        Assert.True(span!.Value.Min <= passing.Min() && span.Value.Max >= passing.Max(), $"{op} {threshold} on {low}..{high} lost a value");
                        // The ends are exact for every op, a not-equal included; only a hole strictly inside the range is left in (the range stays a superset there).
                        Assert.Equal(passing.Min(), span.Value.Min);
                        Assert.Equal(passing.Max(), span.Value.Max);
                    }
    }
}
