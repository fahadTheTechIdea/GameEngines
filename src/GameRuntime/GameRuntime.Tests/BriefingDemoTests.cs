using GameRuntime.Briefing.Headless;
using GameRuntime.Field.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>The combined demo: a dialogue-driven briefing and quest, then squads led by AI agents. It prints the same thing every run, and the answer changes the game.</summary>
public sealed class BriefingDemoTests
{
    private static readonly string[] Field =
        ["--pack-a", "arms-modern", "--formation-a", "arms-modern.rifle-company", "--squads-a", "2", "--pack-b", "arms-modern", "--formation-b", "arms-modern.rifle-company", "--squads-b", "2",
         "--frame-ms", "100", "--seconds", "20", "--distance", "250"];

    private static (int Exit, string[] Lines, string Error) Play(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = BriefingHeadlessGame.Run(args, output, error);
        return (exit, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), error.ToString());
    }

    [Fact]
    public void TwoRunsPrintExactlyTheSameAndTheHashLineIsLast()
    {
        var one = Play(["--answer", "go", .. Field]);
        var two = Play(["--answer", "go", .. Field]);
        Assert.Equal(0, one.Exit);
        Assert.Equal(one.Lines, two.Lines);
        Assert.StartsWith("output-sha256=", one.Lines[^1]);
    }

    [Fact]
    public void AnsweringGoStartsTheQuestLetsTheSquadsFireAndCompletesTheQuestWhenNoFoeStands()
    {
        var run = Play(["--answer", "go", .. Field]).Lines;
        var order = new[] { "says ", "choices go:", "quest hold active order", "signal fire-at-will", "briefing ended briefed", "round 1 headcount", "quest hold completed done", "ended side-1-wins" };
        var at = -1;
        foreach (var expected in order)
        {
            var next = Array.FindIndex(run, at + 1, l => l.Contains(expected, StringComparison.Ordinal));
            Assert.True(next > at, "'" + expected + "' not found after line " + at);
            at = next;
        }
        Assert.Contains("quest hold=completed stage=done answer=go", run);
        Assert.Contains(run, l => l.Contains("2 squads are formed up. Orders?", StringComparison.Ordinal));
        Assert.Contains(run, l => l.Contains("says arms-modern.rifle-company.member/", StringComparison.Ordinal));   // the speaker is a character of the catalog formation
    }

    [Fact]
    public void TheFightTheAgentsLeadAfterAGoIsTheFightTheFieldGameAlonePlays()
    {
        var demo = Play(["--answer", "go", .. Field]).Lines.Single(l => l.StartsWith("final ", StringComparison.Ordinal));
        var output = new StringWriter();
        var directory = Directory.CreateTempSubdirectory("briefing-");
        try
        {
            Assert.Equal(0, FieldHeadlessGame.Run([.. Field, "--audit", Path.Combine(directory.FullName, "a.jsonl")], output, new StringWriter()));
        }
        finally { directory.Delete(true); }
        Assert.Equal(output.ToString().Split('\n').Single(l => l.StartsWith("final ", StringComparison.Ordinal)).Trim(), demo);
    }

    [Fact]
    public void TheBriefingCountsTheSquadsOfTheCommandersOwnSideAndTheTextFollowsThePluralRule()
    {
        string[] uneven = ["--pack-a", "arms-modern", "--formation-a", "arms-modern.rifle-company", "--squads-a", "3", "--pack-b", "arms-modern", "--formation-b", "arms-modern.rifle-company", "--squads-b", "1",
            "--frame-ms", "100", "--seconds", "5", "--distance", "250"];
        Assert.Contains(Play(["--answer", "wait", .. uneven]).Lines, l => l.Contains("3 squads are formed up. Orders?", StringComparison.Ordinal));
        string[] single = ["--pack-a", "arms-modern", "--formation-a", "arms-modern.rifle-company", "--squads-a", "1", "--pack-b", "arms-modern", "--formation-b", "arms-modern.rifle-company", "--squads-b", "3",
            "--frame-ms", "100", "--seconds", "5", "--distance", "250"];
        Assert.Contains(Play(["--answer", "wait", .. single]).Lines, l => l.Contains("1 squad is formed up. Orders?", StringComparison.Ordinal));
    }

    [Fact]
    public void AnsweringWaitHoldsTheSquadsAndTheQuestNeverStarts()
    {
        var run = Play(["--answer", "wait", .. Field]).Lines;
        Assert.Contains(run, l => l.Contains("signal hold-fire", StringComparison.Ordinal));
        Assert.DoesNotContain(run, l => l.Contains("round 1", StringComparison.Ordinal));
        Assert.Contains("final a=200/200 b=200/200 rounds=0", run);
        Assert.Contains("quest hold=not-started stage= answer=wait", run);
        Assert.Contains(run, l => l.StartsWith("result state=Running", StringComparison.Ordinal));
    }

    [Fact]
    public void ABadAnswerIsRefusedWithAMessageAndNoGameIsPlayed()
    {
        var run = Play(["--answer", "maybe", .. Field]);
        Assert.Equal(BriefingHeadlessGame.Refused, run.Exit);
        Assert.Contains("--answer must be go or wait", run.Error, StringComparison.Ordinal);
        Assert.Empty(run.Lines);
    }
}
