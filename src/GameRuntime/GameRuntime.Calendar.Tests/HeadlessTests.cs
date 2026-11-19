using GameRuntime.Calendar.Headless;
using Xunit;

namespace GameRuntime.Calendar.Tests;

/// <summary>The headless calendar game: it runs, it replays its own second half from a checkpoint and journal with the same result, and it prints the same thing every time.</summary>
public sealed class HeadlessTests
{
    private static (int Code, string Output, string Error) Play(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = CalendarHeadlessGame.Run(args, output, error);
        return (code, output.ToString().Replace("\r\n", "\n"), error.ToString().Replace("\r\n", "\n"));
    }

    [Fact]
    public void AShortRunReplaysFromItsCheckpointToTheSameStateAndEvents()
    {
        var (code, output, error) = Play("--days", "40", "--checkpoint-day", "15");
        Assert.Equal((0, ""), (code, error));
        Assert.Contains("replayed ticks 21601 to 57600 from the checkpoint at tick 21600: state same, events same", output);
        Assert.Contains("applied storm-mira at tick 3241", output);
        Assert.Contains("applied sleep at tick 6721", output);
        Assert.Contains("refused sleep-too-far at tick 6723: CAL_SKIP", output);
        Assert.Contains("season: spring -> summer on year 1 summer 1", output);
        Assert.Contains("holiday: egg-festival on year 1 spring 13", output);
        Assert.DoesNotContain("DIFFERENT", output);
    }

    [Fact]
    public void TwoRunsPrintTheSameAndTheSeedChangesTheWeather()
    {
        var a = Play("--days", "30", "--checkpoint-day", "10");
        var b = Play("--days", "30", "--checkpoint-day", "10");
        var c = Play("--days", "30", "--checkpoint-day", "10", "--seed", "8");
        Assert.Equal(a.Output, b.Output);
        Assert.NotEqual(a.Output.Split('\n').Last(l => l.StartsWith("output-sha256", StringComparison.Ordinal)), c.Output.Split('\n').Last(l => l.StartsWith("output-sha256", StringComparison.Ordinal)));
        Assert.Equal(0, c.Code);
    }

    [Fact]
    public void ThePrintedHashOfAShortRunIsFrozen()
    {
        var (_, output, _) = Play("--days", "20", "--checkpoint-day", "8");
        Assert.Contains("output-sha256 62CE9648D22172039975EFB782B475E13C35A85DD35CE9A52764CDC5118EA062", output);
    }

    [Theory]
    [InlineData("--days", "0")]
    [InlineData("--days", "3001")]
    [InlineData("--days", "abc")]
    [InlineData("--seed", "-1")]
    [InlineData("--checkpoint-day", "0")]
    [InlineData("--days")]
    [InlineData("--wat")]
    public void ABadOptionIsRefusedWithUsage(params string[] args)
    {
        var (code, output, error) = Play(args);
        Assert.Equal(2, code);
        Assert.Equal("", output);
        Assert.Contains("usage: GameRuntime.Calendar.Headless", error);
    }

    [Fact]
    public void ACheckpointAtOrAfterTheEndIsRefused()
    {
        var (code, _, error) = Play("--days", "10", "--checkpoint-day", "10");
        Assert.Equal(2, code);
        Assert.Contains("--checkpoint-day must be before --days", error);
    }

    [Fact]
    public void HelpPrintsTheUsage()
    {
        var (code, output, _) = Play("--help");
        Assert.Equal(0, code);
        Assert.StartsWith("usage: GameRuntime.Calendar.Headless", output);
    }
}
