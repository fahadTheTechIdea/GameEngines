using System.Text.RegularExpressions;
using GameRuntime.WorldEvents.Headless;
using Xunit;

namespace GameRuntime.WorldEvents.Tests;

/// <summary>The headless year: it prints the same output every run (the hash is printed), the replay from the checkpoint agrees with the live run, the story it tells has the parts the demo promises, and its options are checked.</summary>
public sealed class HeadlessTests
{
    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = ValleyWorldGame.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static string Hash(string output) => Regex.Match(output, @"output-sha256 ([0-9A-F]{64})").Groups[1].Value;

    [Fact]
    public void AYearIsTheSameEveryTimeItIsPlayedAndTheReplayFromTheCheckpointAgrees()
    {
        var first = Run();
        var second = Run();
        Assert.Equal(0, first.Code);
        Assert.Equal("", first.Err);
        Assert.Equal(64, Hash(first.Out).Length);
        Assert.Equal(Hash(first.Out), Hash(second.Out));
        Assert.Equal(first.Out, second.Out);
        Assert.Contains("state same, events same", first.Out);
        Assert.Contains("money conserved: yes", first.Out);
    }

    [Fact]
    public void TheYearHasADroughtAPlagueAStormAndThePacingOfTheDirector()
    {
        var output = Run().Out;
        Assert.Contains("Drought (", output);
        Assert.Matches(@"Plague outbreak in \w+: \d+ ill", output);
        Assert.Contains("Storm (", output);
        Assert.Contains("the director goes from build to peak", output);
        Assert.Contains("the director goes from relax to build", output);
        Assert.Contains("the world asks the economy for drought", output);
        Assert.Contains("economy shock drought on", output);
        Assert.Contains("Plague hospital starts in", output);
        Assert.Contains("Spring begins", output);
        Assert.Contains("director phoebe on hard", output);                              // the player asked for peace on day 200 and for the hard difficulty on day 300
        Assert.Matches(@"ended at tick 8760, day 366: \d+ hazards started", output);
    }

    [Fact]
    public void TheStorytellerAndTheDifficultyCanBeChosenAndTheOutputChangesWithThem()
    {
        var randy = Run("--storyteller", "randy", "--difficulty", "easy", "--days", "120", "--checkpoint-day", "50");
        Assert.Equal(0, randy.Code);
        Assert.Contains("state same, events same", randy.Out);
        var default120 = Run("--days", "120", "--checkpoint-day", "50");
        Assert.NotEqual(Hash(randy.Out), Hash(default120.Out));
    }

    [Fact]
    public void VerbosePrintsTheWeatherAndTheSignals()
    {
        var output = Run("--days", "40", "--checkpoint-day", "10", "--verbose").Out;
        Assert.Contains("turns", output);
        Assert.Matches(@"signal world\.", output);
    }

    [Theory]
    [InlineData("--days", "5")]
    [InlineData("--days", "x")]
    [InlineData("--storyteller")]
    [InlineData("--bogus")]
    [InlineData("--days", "50", "--checkpoint-day", "60")]
    public void AWrongOptionIsRefusedWithTheUsageAndExitTwo(params string[] args)
    {
        var (code, _, error) = Run(args);
        Assert.Equal(2, code);
        Assert.Contains("usage: GameRuntime.WorldEvents.Headless", error);
    }

    [Fact]
    public void AnUnknownStorytellerIsRefusedByItsCodeAndHelpPrintsTheUsage()
    {
        var (code, _, error) = Run("--storyteller", "nobody");
        Assert.Equal(2, code);
        Assert.StartsWith("WE_UNKNOWN_PROFILE:", error);
        var help = Run("--help");
        Assert.Equal(0, help.Code);
        Assert.Contains("usage: GameRuntime.WorldEvents.Headless", help.Out);
    }
}
