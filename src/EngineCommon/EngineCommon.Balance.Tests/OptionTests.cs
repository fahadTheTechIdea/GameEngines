using Xunit;

namespace EngineCommon.Balance.Tests;

/// <summary>Every bad balance command line is refused with its own code before anything runs.</summary>
public sealed class OptionTests
{
    [Fact]
    public void AGoodCommandLineIsRead()
    {
        var options = SweepOptions.Parse(["--seeds", "30", "--from-seed", "5", "--metrics", "a,b", "--rows", "--days", "12"]);
        Assert.Equal(30, options.Seeds);
        Assert.Equal(5, options.FromSeed);
        Assert.Equal(12, options.Days);
        Assert.Null(options.Horizon);
        Assert.Equal(["a", "b"], options.Metrics);
        Assert.True(options.Rows);
        var positional = SweepOptions.Parse(["365", "--seeds", "1"]);
        Assert.Equal(365, positional.Horizon);
        Assert.Equal(0, positional.FromSeed);
        Assert.Empty(positional.Metrics);
    }

    [Theory]
    [InlineData(BalanceCodes.SeedsMissing, "10")]
    [InlineData(BalanceCodes.SeedsRange, "--seeds", "0", "10")]
    [InlineData(BalanceCodes.SeedsRange, "--seeds", "100001", "10")]
    [InlineData(BalanceCodes.OptionValue, "--seeds", "many", "10")]
    [InlineData(BalanceCodes.OptionValue, "--seeds", "-3", "10")]
    [InlineData(BalanceCodes.OptionValue, "--seeds")]
    [InlineData(BalanceCodes.OptionTwice, "--seeds", "2", "--seeds", "3", "10")]
    [InlineData(BalanceCodes.FromSeedRange, "--seeds", "2", "--from-seed", "9000000000000000", "10")]
    [InlineData(BalanceCodes.FromSeedRange, "--seeds", "2", "--from-seed", "9000000000000001", "10")]
    [InlineData(BalanceCodes.DaysRange, "--seeds", "2", "--days", "0")]
    [InlineData(BalanceCodes.HorizonMissing, "--seeds", "2")]
    [InlineData(BalanceCodes.HorizonTwice, "--seeds", "2", "10", "--days", "5")]
    [InlineData(BalanceCodes.HorizonTwice, "--seeds", "2", "10", "11")]
    [InlineData(BalanceCodes.OptionUnknown, "--seeds", "2", "10", "--save", "x")]
    [InlineData(BalanceCodes.MetricName, "--seeds", "2", "10", "--metrics", "a,,b")]
    [InlineData(BalanceCodes.MetricName, "--seeds", "2", "10", "--metrics", "a,a")]
    [InlineData(BalanceCodes.OptionTwice, "--seeds", "2", "10", "--rows", "--rows")]
    public void ABadCommandLineIsRefusedWithItsCode(string code, params string[] args) => Assert.Equal(code, Assert.Throws<EngineRefusedException>(() => SweepOptions.Parse(args)).Code);

    [Fact]
    public void TheLastAllowedSeedIsAccepted() => Assert.Equal(9_000_000_000_000_000 - 1, SweepOptions.Parse(["--seeds", "2", "--from-seed", "8999999999999999", "5"]).FromSeed);
}
