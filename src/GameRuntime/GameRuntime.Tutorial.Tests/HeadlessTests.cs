using System.Globalization;
using GameRuntime.Tutorial.Headless;
using Xunit;

namespace GameRuntime.Tutorial.Tests;

/// <summary>The demo game: it runs, its output is the same on every run, and the replay from its checkpoint ends where the live run ended.</summary>
public sealed class HeadlessTests
{
    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = ValleyHeadlessGame.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void TheDemoRunsAndItsReplayMatches()
    {
        var (code, output, error) = Run();
        Assert.Equal(ValleyHeadlessGame.Ok, code);
        Assert.Equal("", error);
        Assert.Contains("state same, events same", output);
        Assert.Contains("tick 1: journey language -> story, page 1, language fr", output);
        Assert.Contains("tick 3: journey story -> language", output);
        Assert.Contains("tick 640: hint 1 (nudge) for look-mouse", output);
        Assert.Contains("tick 3350: hint 3 (show) for build", output);
        Assert.Contains("tick 3600: step \"Connect it with a road\" active -> skipped", output);
        Assert.Contains("mayor says:", output);
        Assert.Contains("tick 3711: step \"Meet the mayor\" active -> done", output);
        Assert.Contains("tick 4100: tutorial running -> finished", output);
        Assert.Contains("tick 4600: tutorial running -> skipped", output);
        Assert.Contains("[expert] step \"Open the menu\" pending -> mastered", output);
        Assert.Contains("refused p4310-observe: TUT_UNKNOWN_EVENT", output);
        Assert.Contains("[ghost] refused p4320-touch: TUT_CHANNEL_UNKNOWN", output);
        Assert.Contains("first save reopened: journey story, language fr, page 1", output);
        Assert.Contains("output-sha256 ", output);
    }

    [Fact]
    public void TheSameRunPrintsTheSameHashTwice()
    {
        var first = Run();
        var second = Run();
        Assert.Equal(first.Out, second.Out);
    }

    [Theory]
    [InlineData(1000, 400)]
    [InlineData(5000, 1)]
    [InlineData(5000, 4999)]
    [InlineData(5000, 4300)]
    public void TheReplayMatchesFromOtherCheckpoints(int ticks, int checkpoint)
    {
        var (code, output, _) = Run("--ticks", ticks.ToString(CultureInfo.InvariantCulture), "--checkpoint-tick", checkpoint.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(ValleyHeadlessGame.Ok, code);
        Assert.Contains("state same, events same", output);
    }

    [Fact]
    public void VerbosePrintsMoreAndStillMatches()
    {
        var quiet = Run();
        var verbose = Run("--verbose");
        Assert.Equal(ValleyHeadlessGame.Ok, verbose.Code);
        Assert.True(verbose.Out.Length > quiet.Out.Length);
        Assert.Contains(": saved (state ", verbose.Out);
    }

    [Theory]
    [InlineData("--ticks", "999")]
    [InlineData("--ticks", "x")]
    [InlineData("--checkpoint-tick", "0")]
    [InlineData("--checkpoint-tick", "6000")]
    [InlineData("--ticks", "1000", "--checkpoint-tick", "1000")]
    [InlineData("--what")]
    [InlineData("--ticks")]
    public void BadOptionsAreRefusedWithTheUsage(params string[] args)
    {
        var (code, _, error) = Run(args);
        Assert.Equal(ValleyHeadlessGame.Refused, code);
        Assert.Contains("usage: GameRuntime.Tutorial.Headless", error);
    }

    [Fact]
    public void HelpPrintsTheUsage()
    {
        var (code, output, _) = Run("--help");
        Assert.Equal(ValleyHeadlessGame.Ok, code);
        Assert.StartsWith("usage: GameRuntime.Tutorial.Headless", output);
    }
}
