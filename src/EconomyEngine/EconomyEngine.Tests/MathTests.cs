using System.Numerics;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>The rounding rules of money and the annuity formula, against independent computations.</summary>
public sealed class MathTests
{
    [Theory]
    [InlineData(7, 2, 1, 14)]
    [InlineData(5, 1, 2, 3)]       // 2.5 -> 3 (ties away from zero)
    [InlineData(-5, 1, 2, -3)]     // -2.5 -> -3
    [InlineData(5, -1, 2, -3)]
    [InlineData(-5, -1, 2, 3)]
    [InlineData(7, 1, 3, 2)]       // 2.33 -> 2
    [InlineData(8, 1, 3, 3)]       // 2.67 -> 3
    [InlineData(-7, 1, 3, -2)]
    [InlineData(-8, 1, 3, -3)]
    [InlineData(1, 1, 2, 1)]
    [InlineData(0, 5, 3, 0)]
    [InlineData(3, 1, -2, -2)]     // -1.5 -> -2
    public void NearestRoundsToTheNearestWholeNumberWithTiesAwayFromZero(long a, long b, long d, long expected)
    {
        Assert.Equal(expected, EconomyMath.Nearest(a, b, d));
    }

    [Theory]
    [InlineData(7, 1, 2, 3)]
    [InlineData(-7, 1, 2, -3)]
    [InlineData(7, -1, 2, -3)]
    [InlineData(1, 1, 3, 0)]
    [InlineData(-1, 1, 3, 0)]
    [InlineData(1_000_000_000_000, 1_000_000_000_000, 1_000_000_000_000, 1_000_000_000_000)]
    public void TruncDividesTowardZeroAndDoesNotOverflowInTheProduct(long a, long b, long d, long expected) => Assert.Equal(expected, EconomyMath.Trunc(a, b, d));

    [Fact]
    public void ClampKeepsAValueInsideItsBounds()
    {
        Assert.Equal(5, EconomyMath.Clamp(5, 0, 10));
        Assert.Equal(0, EconomyMath.Clamp(-3, 0, 10));
        Assert.Equal(10, EconomyMath.Clamp(30, 0, 10));
        Assert.Equal(0, EconomyMath.Clamp(0, 0, 10));
        Assert.Equal(10, EconomyMath.Clamp(10, 0, 10));
    }

    [Fact]
    public void ANonRepresentableProductThrowsInsteadOfWrapping()
    {
        Assert.Throws<OverflowException>(() => EconomyMath.Trunc(long.MaxValue, long.MaxValue, 1));
    }

    /// <summary>The exact payment as a rational (numerator, denominator): P i (1+i)^n / ((1+i)^n - 1) with i = r / 1,000,000.</summary>
    private static (BigInteger Numerator, BigInteger Denominator) Exact(long principal, long ratePpm, int periods)
    {
        var s = BigInteger.Pow(1_000_000 + ratePpm, periods);
        var t = BigInteger.Pow(1_000_000, periods);
        return ((BigInteger)principal * ratePpm * s, 1_000_000 * (s - t));
    }

    [Theory]
    [InlineData(10_000, 10_000, 4)]
    [InlineData(1_000_000, 8_000, 12)]
    [InlineData(123_456, 33_333, 36)]
    [InlineData(5, 500_000, 3)]
    [InlineData(999_999_999, 1, 1200)]
    [InlineData(100, 1_000_000, 10)]
    [InlineData(1, 1, 1)]
    public void AnnuityIsTheExactPaymentRoundedToTheNearestMinorUnit(long principal, long ratePpm, int periods)
    {
        var payment = EconomyMath.Annuity(principal, ratePpm, periods);
        var (n, d) = Exact(principal, ratePpm, periods);
        // |payment - n/d| <= 1/2  <=>  |2 payment d - 2 n| <= d
        Assert.True(BigInteger.Abs(2 * payment * d - 2 * n) <= d, "payment " + payment + " is not the nearest to the exact one");
    }

    [Theory]
    [InlineData(1000, 4, 250)]
    [InlineData(1001, 4, 250)]
    [InlineData(1002, 4, 251)]
    [InlineData(7, 1, 7)]
    public void WithNoInterestThePaymentIsThePrincipalOverThePeriods(long principal, int periods, long expected) => Assert.Equal(expected, EconomyMath.Annuity(principal, 0, periods));

    [Theory]
    [InlineData(100_000, 12_000, 24)]
    [InlineData(50_000, 100_000, 6)]
    [InlineData(777, 3_000, 5)]
    public void PayingTheAnnuityEveryPeriodAndTheRemainderAtTheEndRepaysTheLoanExactly(long principal, long ratePpm, int periods)
    {
        var payment = EconomyMath.Annuity(principal, ratePpm, periods);
        var balance = principal;
        for (var i = 1; i <= periods; i++)
        {
            var interest = EconomyMath.Nearest(balance, ratePpm, 1_000_000);
            var instalment = i == periods ? balance : payment - interest;
            Assert.InRange(instalment, 0, balance);
            balance -= instalment;
        }
        Assert.Equal(0, balance);
        // The last payment (interest plus what is left) is within a few units of the fixed one: the rounding of each interest is all that differs.
        var rest = principal;
        for (var i = 1; i < periods; i++) rest -= payment - EconomyMath.Nearest(rest, ratePpm, 1_000_000);
        Assert.InRange(rest + EconomyMath.Nearest(rest, ratePpm, 1_000_000) - payment, -periods, periods);
    }
}
