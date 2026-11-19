using EngineCommon;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>The shared pieces both engines stand on: the seeded roll and the strict reader.</summary>
public sealed class CommonTests
{
    [Fact]
    public void ARollIsAPureFunctionOfSeedStreamAndIndex()
    {
        Assert.Equal(DeterministicRoll.Next(5, "a", 3), DeterministicRoll.Next(5, "a", 3));
        Assert.NotEqual(DeterministicRoll.Next(5, "a", 3), DeterministicRoll.Next(6, "a", 3));
        Assert.NotEqual(DeterministicRoll.Next(5, "a", 3), DeterministicRoll.Next(5, "b", 3));
        Assert.NotEqual(DeterministicRoll.Next(5, "a", 3), DeterministicRoll.Next(5, "a", 4));
        // Asking in another order changes nothing.
        var forward = Enumerable.Range(0, 20).Select(i => DeterministicRoll.Next(9, "s", i)).ToArray();
        var backward = Enumerable.Range(0, 20).Reverse().Select(i => DeterministicRoll.Next(9, "s", i)).Reverse().ToArray();
        Assert.Equal(forward, backward);
    }

    [Fact]
    public void ARollBelowABoundStaysInsideItAndSpreadsOverIt()
    {
        var counts = new int[10];
        for (var i = 0; i < 10_000; i++) counts[DeterministicRoll.Below(1, "spread", i, 10)]++;
        Assert.All(counts, c => Assert.InRange(c, 850, 1150));
        Assert.Throws<ArgumentException>(() => DeterministicRoll.Below(1, "x", 0, 0));
        Assert.All(Enumerable.Range(0, 1000), i => Assert.InRange(DeterministicRoll.Permille(2, "p", i), 0, 999));
    }

    [Fact]
    public void TheDigestIsSha256InUpperCaseHex()
    {
        Assert.Equal("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", Digest.Of(""));
        Assert.Equal(Digest.Of("abc"), Digest.Of("abc"u8));
    }
}