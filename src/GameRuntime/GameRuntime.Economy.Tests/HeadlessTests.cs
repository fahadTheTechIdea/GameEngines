using GameRuntime.Economy.Headless;
using Xunit;

namespace GameRuntime.Economy.Tests;

/// <summary>The town demo: it prints the same thing every run, it replays its own second part to the same state and events, and its options are checked.</summary>
public sealed class HeadlessTests
{
    private static (int Exit, string[] Lines, string Error) Play(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = TownHeadlessGame.Run(args, output, error);
        return (exit, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), error.ToString());
    }

    [Fact]
    public void TwoRunsPrintExactlyTheSameAndTheHashLineIsLast()
    {
        var one = Play("--days", "20", "--checkpoint-day", "7");
        var two = Play("--days", "20", "--checkpoint-day", "7");
        Assert.Equal(0, one.Exit);
        Assert.Equal(one.Lines, two.Lines);
        Assert.Matches("^output-sha256 [0-9A-F]{64}$", one.Lines[^1]);
    }

    [Fact]
    public void TheReplayOfTheSecondPartEndsInTheSameStateAndEvents()
    {
        var run = Play("--days", "20", "--checkpoint-day", "7").Lines;
        Assert.Contains(run, l => l.StartsWith("replayed ticks 10111 to 28800 from the checkpoint at tick 10110: state same, events same (", StringComparison.Ordinal));
        Assert.Contains(run, l => l.StartsWith("ended at tick 28800: period 20, 3 towns, 22 prices,", StringComparison.Ordinal));
        Assert.Contains("money conserved: yes", run);
    }

    [Fact]
    public void TheTownsLiveWhatTheDocumentsAndTheCalendarSayTheyDo()
    {
        var run = Play("--days", "90").Lines;
        Assert.Equal(90, run.Count(l => l.StartsWith("day ", StringComparison.Ordinal)));
        Assert.Contains(run, l => l.Contains("shock drought on riverton began", StringComparison.Ordinal));
        Assert.Contains(run, l => l.Contains("shock devaluation on seaport began", StringComparison.Ordinal));
        Assert.Contains(run, l => l.Contains("shock devaluation on seaport ended", StringComparison.Ordinal));
        Assert.Contains(run, l => l.Contains("caravan bought", StringComparison.Ordinal));
        Assert.Contains(run, l => l.Contains("caravan sold", StringComparison.Ordinal));
        Assert.Contains(run, l => l.Contains("hillfort tax luxury-duty", StringComparison.Ordinal));      // the decree of the 15th
        Assert.Contains(run, l => l.Contains("loan relief", StringComparison.Ordinal) || l.Contains("hillfort loan relief", StringComparison.Ordinal));
        Assert.Contains(run, l => l.StartsWith("day ", StringComparison.Ordinal) && l.Contains("(friday)", StringComparison.Ordinal));
        Assert.Contains(run, l => l.Contains("recipe alloy-steel was discovered", StringComparison.Ordinal));       // the story unlocks the locked recipe on day 35
        Assert.Contains(run, l => l.Contains("hillfort smelt-steel is short of coal", StringComparison.Ordinal));   // the chain runs short, and says what of
        Assert.Contains(run, l => l.Contains("line mill: ", StringComparison.Ordinal));
        Assert.DoesNotContain(run, l => l.StartsWith("refused ", StringComparison.Ordinal));
    }

    [Fact]
    public void TheDefaultRunPrintsTheRecordedHashAndSoDoesAShorterOne()
    {
        // These are the hashes of docs/ECONOMY_ENGINE.md section 15.9 (format 4: margin throttles, shocks on recipe output, noise 20; sections 10.3, 11.5 and 13.4 have the earlier values they replaced); a change in any engine, document or rule that moves a price moves them.
        Assert.Equal("output-sha256 10CCBD044E511BA5C85523C233E46BD22502522C2F6782618470B7C83E8B3A83", Play().Lines[^1]);
        Assert.Equal("output-sha256 A65672F88F44A05BD4464054B795AE319E1DA964739FB82CB3DF55BD7647C5C8", Play("--days", "20", "--checkpoint-day", "7").Lines[^1]);
    }

    [Fact]
    public void AnotherSeedGivesAnotherCaravanAndSoAnotherRun()
    {
        var a = Play("--days", "20", "--seed", "5", "--checkpoint-day", "5").Lines[^1];
        var b = Play("--days", "20", "--seed", "6", "--checkpoint-day", "5").Lines[^1];
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ABadOptionExitsWithTwoAndSaysWhatIsWrong()
    {
        foreach (var args in new[] { new[] { "--days" }, ["--days", "0"], ["--days", "9999"], ["--days", "3001"], ["--days", "x"], ["--seed", "-1"], ["--nonsense"], ["--checkpoint-day", "0"], ["--days", "10", "--checkpoint-day", "10"] })
        {
            var (exit, _, error) = Play(args);
            Assert.Equal(2, exit);
            Assert.Contains("usage: GameRuntime.Economy.Headless", error, StringComparison.Ordinal);
        }
        Assert.Equal(0, Play("--help").Exit);
    }

    [Fact]
    public void VerboseAddsTheEventsAndChangesNothingElse()
    {
        var quiet = Play("--days", "8", "--checkpoint-day", "2");
        var loud = Play("--days", "8", "--checkpoint-day", "2", "--verbose");
        Assert.Equal(0, loud.Exit);
        Assert.True(loud.Lines.Length > quiet.Lines.Length);
        Assert.Contains(loud.Lines, l => l.Contains(" left by ", StringComparison.Ordinal));
        Assert.All(quiet.Lines.Where(l => l.StartsWith("day ", StringComparison.Ordinal)), l => Assert.Contains(l, loud.Lines));
    }
}
