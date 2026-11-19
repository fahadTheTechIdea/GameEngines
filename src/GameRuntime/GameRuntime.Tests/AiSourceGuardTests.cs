using Xunit;

namespace GameRuntime.Tests;

/// <summary>One owner for how an agent decides: the games' own code senses and translates; thresholds, cadences and target rules live in the AI documents.</summary>
public sealed class AiSourceGuardTests
{
    private static string Sources(string project)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "GameRuntime", project))) directory = directory.Parent;
        Assert.NotNull(directory);
        return string.Join('\n', Directory.GetFiles(Path.Combine(directory!.FullName, "src", "GameRuntime", project), "*.cs").Select(File.ReadAllText));
    }

    [Theory]
    [InlineData("GameRuntime.Field.Headless")]
    [InlineData("GameRuntime.Combat.Headless")]
    [InlineData("GameRuntime.Briefing.Headless")]
    [InlineData("GameRuntime.Headless")]
    public void NoThresholdCadenceOrTargetRuleIsWrittenInTheGameCode(string project)
    {
        var text = Sources(project);
        foreach (var forbidden in new[] { "% 15", "% 30", "% ActEveryTicks", "BelowPercent", "* 100 <", "* 100 >=", "ISkirmishPolicy", "AggressorPolicy", "MeleePolicy", "IPlayerPolicy", "CooldownTicks", "OrderBy(g => g.Health)", "Math.Clamp(pair" })
            Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOldHandWrittenDecisionClassesAreGone()
    {
        var tycoon = Sources("GameRuntime.Headless");
        foreach (var name in new[] { "class GreedyPolicy", "class CautiousPolicy", "class PlayerPolicies" }) Assert.DoesNotContain(name, tycoon, StringComparison.Ordinal);
        var combat = Sources("GameRuntime.Combat.Headless");
        foreach (var name in new[] { "class AggressorPolicy", "class DefenderPolicy", "class SkirmisherPolicy", "class MeleePolicy", "class SkirmishPolicies" })
            Assert.DoesNotContain(name, combat, StringComparison.Ordinal);
    }
}
