using System.Text.RegularExpressions;
using GameRuntime.Diplomacy.Headless;
using Xunit;

namespace GameRuntime.Diplomacy.Tests;

/// <summary>The headless isles: it prints the same output every run (the hash is printed), the replay from the checkpoint agrees with the live run, the story it tells has the parts the demo promises, and its options are checked.</summary>
public sealed class HeadlessTests
{
    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = IslesDiplomacyGame.Run(args, output, error);
        return (code, output.ToString().Replace(Environment.NewLine, "\n", StringComparison.Ordinal), error.ToString().Replace(Environment.NewLine, "\n", StringComparison.Ordinal));
    }

    private static string Hash(string output) => Regex.Match(output, @"output-sha256 ([0-9A-F]{64})").Groups[1].Value;

    [Fact]
    public void ASeasonOfTheIslesIsTheSameEveryTimeItIsPlayedAndTheReplayFromTheCheckpointAgrees()
    {
        var first = Run();
        var second = Run();
        Assert.Equal(0, first.Code);
        Assert.Equal("", first.Err);
        Assert.Equal(64, Hash(first.Out).Length);
        Assert.Equal(Hash(first.Out), Hash(second.Out));
        Assert.Equal(first.Out, second.Out);
        Assert.Matches(@"state same, events same \(\d+ events\)", first.Out);
        Assert.Contains("money conserved: yes", first.Out);
    }

    [Fact]
    public void TheStoryHasTreatiesABreachAWarAPeaceTributeAnEspionageIncidentACoalitionAndASuccession()
    {
        var output = Run().Out;
        Assert.Contains("the alliance between Aurelia and Brassport is signed", output);
        Assert.Contains("the trade between Dune Clans and Cinderfell is signed", output);
        Assert.Contains("the marriage between Aurelia and Cinderfell is signed", output);
        Assert.Contains("the nap between Emberwatch and Dune Clans is broken", output);
        Assert.Contains("war 0 (Emberwatch against Dune Clans) declared, no casus belli", output);
        Assert.Contains("the peace between Emberwatch and Dune Clans is signed, tribute 200", output);
        Assert.Contains("war 0 (Emberwatch against Dune Clans) ended", output);
        Assert.Matches(@"on a sabotage against Aurelia: failed exposed", output);
        Assert.Contains("agent-cinder-1 of Cinderfell is captured", output);
        Assert.Contains("agent-cinder-1 of Cinderfell is released", output);
        Assert.Contains("against Aurelia, a coalition formed", output);
        Assert.Contains("Emberwatch: succession", output);
        Assert.Contains("the diplomacy asks the economy for embargo on Emberwatch's market at 800 per mille (treaty:embargo)", output);
        Assert.Contains("economy shock embargo on ember-market began", output);
        Assert.Matches(@"ended at tick 5760, day 241: 6 treaties signed, 1 broken, 1 wars declared, 1 ended, 1 tributes paid, 1 agents exposed, 1 coalitions", output);
    }

    [Fact]
    public void AShorterOrLongerRunReplaysToo()
    {
        var shorter = Run("--days", "80", "--checkpoint-day", "30");
        Assert.Equal(0, shorter.Code);
        Assert.Contains("state same, events same", shorter.Out);
        Assert.NotEqual(Hash(shorter.Out), Hash(Run().Out));
    }

    [Fact]
    public void VerbosePrintsTheProposalsTheIntelligenceAndTheSignals()
    {
        var output = Run("--days", "60", "--checkpoint-day", "10", "--verbose").Out;
        Assert.Contains("offers", output);
        Assert.Matches(@"signal \w[\w-]* for ", output);
        Assert.Contains("incident", output);
    }

    [Theory]
    [InlineData("--days", "5")]
    [InlineData("--days", "x")]
    [InlineData("--checkpoint-day")]
    [InlineData("--bogus")]
    [InlineData("--days", "100", "--checkpoint-day", "120")]
    public void AWrongOptionIsRefusedWithTheUsageAndExitTwo(params string[] args)
    {
        var (code, _, error) = Run(args);
        Assert.Equal(2, code);
        Assert.Contains("usage: GameRuntime.Diplomacy.Headless", error);
    }

    [Fact]
    public void HelpPrintsTheUsage()
    {
        var help = Run("--help");
        Assert.Equal(0, help.Code);
        Assert.Contains("usage: GameRuntime.Diplomacy.Headless", help.Out);
    }

    [Fact]
    public void TheScriptIsInDayOrderAndEveryEntryNamesARealRequestKind()
    {
        var days = IslesDiplomacyGame.Script.Select(s => s.Day).ToList();
        Assert.Equal(days.OrderBy(d => d).ToList(), days);
        Assert.All(IslesDiplomacyGame.Script, s => Assert.NotNull(DiplomacyRequestNames.KindOf(s.Kind)));
    }
}
