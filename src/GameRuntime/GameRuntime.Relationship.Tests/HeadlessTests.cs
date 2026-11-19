using GameRuntime.Relationship.Headless;
using Xunit;

namespace GameRuntime.Relationship.Tests;

/// <summary>The village demo: it prints the same thing every run, it replays its own second half to the same state and events, and its options are checked.</summary>
public sealed class HeadlessTests
{
    private static (int Exit, string[] Lines, string Error) Play(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = VillageHeadlessGame.Run(args, output, error);
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
        Assert.Contains(run, l => l.StartsWith("ended at tick 28800: step 480, 6 villagers,", StringComparison.Ordinal));
    }

    [Fact]
    public void TheVillageLivesWhatTheDocumentsAndTheCalendarSayItDoes()
    {
        var run = Play("--days", "30").Lines;
        Assert.Equal(30, run.Count(l => l.StartsWith("day ", StringComparison.Ordinal)));   // one line at each midnight after the first morning, the last at the end of the thirtieth day
        Assert.Contains(run, l => l.Contains("about", StringComparison.Ordinal) && l.Contains("friendship", StringComparison.Ordinal) && l.Contains("->", StringComparison.Ordinal));   // someone's opinion of someone crossed a line
        Assert.Contains(run, l => l.StartsWith("day ", StringComparison.Ordinal) && l.Contains("(sunday)", StringComparison.Ordinal));
        Assert.Contains(run, l => l.StartsWith("alice: mood ", StringComparison.Ordinal));
        Assert.DoesNotContain(run, l => l.StartsWith("refused ", StringComparison.Ordinal));
    }

    [Fact]
    public void TheDefaultRunPrintsTheRecordedHashAndSoDoesAShorterOne()
    {
        // These are the hashes of docs/RELATIONSHIP_ENGINE.md section 10.5; a change in any engine, document or rule that moves a villager moves them.
        Assert.Equal("output-sha256 D3987D2841CCF236859AB6AF08C6A3C3C724BEF19C341E8DF40DE462019656D3", Play().Lines[^1]);
        Assert.Equal("output-sha256 84900AF4609AA265E4A1B917475219628FE7B76687025C61A7002B3A258FB205", Play("--days", "20", "--checkpoint-day", "7").Lines[^1]);
    }

    [Fact]
    public void AnotherSeedGivesAnotherWeatherAndSoAnotherRun()
    {
        var a = Play("--days", "20", "--seed", "11", "--checkpoint-day", "5").Lines[^1];
        var b = Play("--days", "20", "--seed", "12", "--checkpoint-day", "5").Lines[^1];
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ABadOptionExitsWithTwoAndSaysWhatIsWrong()
    {
        foreach (var args in new[] { new[] { "--days" }, ["--days", "0"], ["--days", "9999"], ["--days", "x"], ["--seed", "-1"], ["--nonsense"], ["--checkpoint-day", "0"], ["--days", "10", "--checkpoint-day", "10"] })
        {
            var (exit, _, error) = Play(args);
            Assert.Equal(2, exit);
            Assert.Contains("usage: GameRuntime.Relationship.Headless", error, StringComparison.Ordinal);
        }
        Assert.Equal(0, Play("--help").Exit);
    }

    [Fact]
    public void VerboseAddsTheEventsAndChangesNothingElse()
    {
        var quiet = Play("--days", "6", "--checkpoint-day", "2");
        var loud = Play("--days", "6", "--checkpoint-day", "2", "--verbose");
        Assert.Equal(0, loud.Exit);
        Assert.True(loud.Lines.Length > quiet.Lines.Length);
        Assert.Contains(loud.Lines, l => l.Contains("accepted", StringComparison.Ordinal) || l.Contains("refused", StringComparison.Ordinal));
        Assert.All(quiet.Lines.Where(l => l.StartsWith("day ", StringComparison.Ordinal)), l => Assert.Contains(l, loud.Lines));
    }
}
