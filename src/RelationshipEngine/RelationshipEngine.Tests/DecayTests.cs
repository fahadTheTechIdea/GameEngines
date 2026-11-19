using System.Numerics;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>Decay is exact and does not depend on how often or in what order it is read (R2), and urgency and the step ladders are exact.</summary>
public sealed class DecayTests
{
    private static readonly long[] Magnitudes = [1000, -1000, 7, -7, 1, -1, 123_457, -999_999, 1_000_000];
    private static readonly int[] Durations = [1, 2, 5, 24, 100, 1000];
    private static readonly int[] HalfLives = [1, 3, 7, 24, 333];

    /// <summary>The same shapes in rational arithmetic on big integers, written separately from the engine.</summary>
    private static long Oracle(DecayKind kind, long m, long age, int duration, int halfLife)
    {
        if (age < 0 || age >= duration) return 0;
        BigInteger magnitude = m;
        switch (kind)
        {
            case DecayKind.Fixed: return m;
            case DecayKind.Linear: return (long)BigInteger.Divide(magnitude * (duration - age), duration);
            case DecayKind.HalfLife: break;
        }
        var k = (int)(age / halfLife);
        var r = age % halfLife;
        var lo = BigInteger.Divide(magnitude, BigInteger.Pow(2, k));
        var hi = BigInteger.Divide(magnitude, BigInteger.Pow(2, k + 1));
        return (long)(lo - BigInteger.Divide((lo - hi) * r, halfLife));
    }

    [Fact]
    public void TheThreeShapesEqualTheRationalOracleAtEveryAgeOfEveryParameterSet()
    {
        var checkedValues = 0;
        foreach (var m in Magnitudes)
            foreach (var duration in Durations)
            {
                for (var age = -2L; age <= duration + 3; age++)
                {
                    Assert.Equal(Oracle(DecayKind.Fixed, m, age, duration, 0), DecayMath.Value(m, age, duration, new DecayDef(DecayKind.Fixed, 0)));
                    Assert.Equal(Oracle(DecayKind.Linear, m, age, duration, 0), DecayMath.Value(m, age, duration, new DecayDef(DecayKind.Linear, 0)));
                    checkedValues += 2;
                }
                foreach (var halfLife in HalfLives.Where(h => h <= duration))
                    for (var age = -2L; age <= duration + 3; age++)
                    {
                        var got = DecayMath.Value(m, age, duration, new DecayDef(DecayKind.HalfLife, halfLife));
                        Assert.True(Oracle(DecayKind.HalfLife, m, age, duration, halfLife) == got, $"m={m} duration={duration} halfLife={halfLife} age={age}: engine {got}, oracle {Oracle(DecayKind.HalfLife, m, age, duration, halfLife)}");
                        checkedValues++;
                    }
            }
        Assert.True(checkedValues > 8000, "the sweep checked only " + checkedValues + " values");
    }

    [Fact]
    public void EveryShapeIsZeroFromTheDurationOnAndBeforeTheStart()
    {
        foreach (var kind in new[] { DecayKind.Fixed, DecayKind.Linear, DecayKind.HalfLife })
        {
            var decay = new DecayDef(kind, kind == DecayKind.HalfLife ? 3 : 0);
            Assert.NotEqual(0, DecayMath.Value(1000, 0, 10, decay));
            Assert.NotEqual(0, DecayMath.Value(1000, 9, 10, decay));
            Assert.Equal(0, DecayMath.Value(1000, 10, 10, decay));
            Assert.Equal(0, DecayMath.Value(1000, 1_000_000, 10, decay));
            Assert.Equal(0, DecayMath.Value(1000, -1, 10, decay));
        }
    }

    [Fact]
    public void AtEachWholeHalfLifeTheValueIsExactlyTheMagnitudeOverAPowerOfTwo()
    {
        foreach (var m in new long[] { 1024, -1024, 1000, -1000, 999_999 })
            for (var k = 0; k <= 8; k++)
                Assert.Equal(m / (1L << k), DecayMath.Value(m, k * 24L, 10_000, new DecayDef(DecayKind.HalfLife, 24)));
    }

    [Fact]
    public void BetweenWholeHalfLivesTheValueLiesOnTheChordAboveTheTrueCurveWithinAboutSixPercent()
    {
        foreach (var m in new long[] { 1_000_000, -1_000_000 })
            for (var age = 0; age < 240; age++)
            {
                var value = Math.Abs(DecayMath.Value(m, age, 1000, new DecayDef(DecayKind.HalfLife, 24)));
                var truth = Math.Abs(m) * Math.Pow(2, -age / 24.0);
                Assert.True(value >= truth - 1, $"age {age}: {value} is below the true curve {truth}");
                Assert.True(value <= truth * 1.0615 + 1, $"age {age}: {value} is more than 6.15 percent above the true curve {truth}");
            }
    }

    [Fact]
    public void TheMagnitudeNeverGrowsAndNeverChangesSignAsTheAgeRises()
    {
        foreach (var kind in new[] { DecayKind.Fixed, DecayKind.Linear, DecayKind.HalfLife })
            foreach (var m in new long[] { 500, -500 })
            {
                var decay = new DecayDef(kind, kind == DecayKind.HalfLife ? 5 : 0);
                long previous = Math.Abs(m);
                for (var age = 0; age < 120; age++)
                {
                    var value = DecayMath.Value(m, age, 100, decay);
                    Assert.True(Math.Abs(value) <= previous, $"{kind} grew at age {age}");
                    Assert.True(value == 0 || Math.Sign(value) == Math.Sign(m), $"{kind} changed sign at age {age}");
                    previous = Math.Abs(value);
                }
            }
    }

    [Fact]
    public void ScalingByAPermilleTruncatesTowardZero()
    {
        Assert.Equal(1, DecayMath.Scale(3, 500));
        Assert.Equal(-1, DecayMath.Scale(-3, 500));
        Assert.Equal(0, DecayMath.Scale(1, 999));
        Assert.Equal(-300, DecayMath.Scale(300, -1000));
        Assert.Equal(3000, DecayMath.Scale(1500, 2000));
    }

    // ---- urgency ----------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(100, 900)]
    [InlineData(200, 800)]
    [InlineData(400, 500)]
    [InlineData(600, 200)]
    [InlineData(700, 150)]
    [InlineData(1000, 0)]
    [InlineData(1500, 0)]
    [InlineData(-5, 1000)]
    public void UrgencyIsLinearBetweenThePointsOfTheCurve(long level, long urgency) => Assert.Equal(urgency, DecayMath.Urgency(Fixture.Village.NeedById["hunger"], level));

    [Fact]
    public void UrgencyNeverRisesWithTheLevelOnTheSampleCurves()
    {
        foreach (var need in Fixture.Village.Needs)
        {
            long previous = long.MaxValue;
            for (var level = 0L; level <= need.Max; level++)
            {
                var urgency = DecayMath.Urgency(need, level);
                Assert.InRange(urgency, 0, 1000);
                Assert.True(urgency <= previous, need.Id + " urgency rose at " + level);
                previous = urgency;
            }
        }
    }

    // ---- steps ------------------------------------------------------------------------------------------------------------------------

    private static readonly IReadOnlyList<StepDef> Steps = [new("low", "k", 0), new("mid", "k", 100), new("high", "k", 200)];

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(99, 0)]
    [InlineData(100, 1)]
    [InlineData(199, 1)]
    [InlineData(200, 2)]
    [InlineData(10_000, 2)]
    public void ThePlainStepIsTheLastOneWhoseStartTheValueReaches(long value, int step) => Assert.Equal(step, DecayMath.PlainStep(Steps, value));

    [Theory]
    [InlineData(1, 99, 10, 1)]      // below the start of the step it is in, but 100 - 10 = 90 is the line down and 99 is above it, so it stays
    [InlineData(1, 90, 10, 1)]      // on the line
    [InlineData(1, 89, 10, 0)]      // past the line
    [InlineData(1, 200, 10, 2)]     // up: no margin
    [InlineData(2, 195, 10, 2)]
    [InlineData(2, 189, 10, 1)]
    [InlineData(2, 50, 10, 0)]      // a long way down passes through
    [InlineData(0, 100, 10, 1)]
    [InlineData(0, 99, 10, 0)]
    public void ASmallFallIsNotEnoughToLeaveAStepButAnyRiseToTheNextStartIs(int current, long value, long hysteresis, int expected) => Assert.Equal(expected, DecayMath.Resolve(Steps, current, value, hysteresis));

    [Fact]
    public void ValuesJumpingSeveralStepsUpOrDownLandOnTheRightStep()
    {
        Assert.Equal(2, DecayMath.Resolve(Steps, 0, 500, 50));
        Assert.Equal(0, DecayMath.Resolve(Steps, 2, -500, 50));
    }

    [Fact]
    public void ARandomWalkAroundAThresholdChangesStepFarLessOftenWithMarginThanWithout()
    {
        var seed = 12345L;
        long Next() { seed = (seed * 6364136223846793005L + 1442695040888963407L) & long.MaxValue; return (seed >> 33) % 21 - 10; }
        for (var margin = 0; margin <= 20; margin += 20)
        {
            var value = 100L;
            var step = DecayMath.PlainStep(Steps, value);
            var changes = 0;
            for (var i = 0; i < 1000; i++)
            {
                value = Math.Clamp(value + Next(), 80, 120);
                var next = DecayMath.Resolve(Steps, step, value, margin);
                if (next != step) changes++;
                step = next;
            }
            if (margin == 0) Assert.True(changes > 100, "the walk should cross the line often without a margin, crossed " + changes);
            else Assert.True(changes < 3, "with a margin the walk (80 to 120 around 100, margin 20) should settle, changed " + changes);
        }
    }
}
