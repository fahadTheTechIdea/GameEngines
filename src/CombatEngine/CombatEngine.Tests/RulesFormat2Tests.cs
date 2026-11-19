using CombatEngine;
using Xunit;

namespace CombatEngine.Tests;

/// <summary>The sections format 2 added (limits, morale, terrain, movement, training morale): each is strict, none is defaulted.</summary>
public sealed class RulesFormat2Tests
{
    private static readonly string Standard = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Content", "combat-rules.json"));

    private static string Code(string json) => Assert.Throws<CombatRefusedException>(() => CombatRulesLoader.Load(json)).Code;

    [Fact]
    public void TheStandardRulesCarryTheNewSectionsAndTheOldFormatIsRefused()
    {
        var rules = CombatRulesLoader.Load(Standard);
        Assert.Equal(3600, rules.MaxEngagementSeconds);
        Assert.Equal(1_000_000, rules.MaxShotsPerBattery);
        Assert.Equal(250, rules.Morale.RoutBelow);
        Assert.Equal(["open", "road", "rough", "forest", "urban"], rules.Terrain.Select(t => t.Id).ToArray());
        Assert.Equal(1400, rules.FootSpeedMilliPerSecond);
        Assert.Equal([700, 850, 950, 1000], rules.Training.Select(t => t.MoralePermille).ToArray());
        Assert.Equal(CombatCodes.RulesFormat, Code(Standard.Replace("\"format\": 2,", "\"format\": 1,")));
    }

    [Theory]
    [InlineData("\"limits\": { \"max_engagement_seconds\": 3600, ", "\"limits\": { \"max_engagement_seconds\": 3600, \"extra\": 1, ", CombatCodes.RulesUnknownField)]
    [InlineData("\"max_shots_per_battery\": 1000000", "\"max_shots_per_battery\": 0", CombatCodes.RulesRange)]
    [InlineData("\"max_engagement_seconds\": 3600", "\"max_engagement_seconds\": 90000", CombatCodes.RulesRange)]
    [InlineData("\"rout_below_permille\": 250", "\"rout_below_permille\": 1000", CombatCodes.RulesRange)]
    [InlineData("\"recovery_permille\": 30, ", "", CombatCodes.RulesMissingField)]
    [InlineData("\"foot_speed_milli_per_second\": 1400", "\"foot_speed_milli_per_second\": 0", CombatCodes.RulesRange)]
    [InlineData("\"movement_percent\": 130", "\"movement_percent\": 301", CombatCodes.RulesRange)]
    [InlineData("{ \"id\": \"road\"", "{ \"id\": \"open\"", CombatCodes.RulesDuplicate)]
    [InlineData("\"morale_permille\": 700", "\"morale_permille\": 0", CombatCodes.RulesRange)]
    [InlineData(", \"morale_permille\": 700 }", " }", CombatCodes.RulesMissingField)]
    public void EachNewFieldIsRefusedWithItsCode(string find, string replace, string code)
    {
        Assert.Contains(find, Standard);
        Assert.Equal(code, Code(Standard.Replace(find, replace)));
    }
}
