using CombatEngine;
using Xunit;

namespace CombatEngine.Tests;

public sealed class RulesLoaderTests
{
    private static readonly string Standard = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Content", "combat-rules.json"));

    private static string Code(string json) => Assert.Throws<CombatRefusedException>(() => CombatRulesLoader.Load(json)).Code;

    [Fact]
    public void TheStandardRulesLoad()
    {
        var rules = CombatRulesLoader.Load(Standard);
        Assert.Equal(100, rules.ArmorConstant);
        Assert.Equal(7, rules.DamageTypes.Count);
        Assert.Equal(["raw", "regular", "veteran", "elite"], rules.Training.Select(t => t.Id).ToArray());
        Assert.Equal(100, rules.RangeBands[^1].UpToPercent);
    }

    [Fact]
    public void NotJsonIsRefused() => Assert.Equal(CombatCodes.RulesFormat, Code("{ not json"));

    [Fact]
    public void UnknownAndMissingFieldsAreRefused()
    {
        Assert.Equal(CombatCodes.RulesUnknownField, Code(Standard.Replace("\"format\": 2,", "\"format\": 2, \"extra\": 1,")));
        Assert.Equal(CombatCodes.RulesMissingField, Code(Standard.Replace("  \"minimum_damage\": 1,\n", "").Replace("  \"minimum_damage\": 1,\r\n", "")));
    }

    [Fact]
    public void ValuesOutsideTheirRangeAreRefused()
    {
        Assert.Equal(CombatCodes.RulesRange, Code(Standard.Replace("\"non_piercing_cut_percent\": 50", "\"non_piercing_cut_percent\": 101")));
        Assert.Equal(CombatCodes.RulesRange, Code(Standard.Replace("\"armor_constant\": 100", "\"armor_constant\": 0")));
        Assert.Equal(CombatCodes.RulesRange, Code(Standard.Replace("\"floor_permille\": 50", "\"floor_permille\": 960")));
        Assert.Equal(CombatCodes.RulesFormat, Code(Standard.Replace("\"minimum_damage\": 1", "\"minimum_damage\": \"one\"")));
        Assert.Equal(CombatCodes.RulesFormat, Code(Standard.Replace("\"minimum_damage\": 1", "\"minimum_damage\": 1.5")));
    }

    [Fact]
    public void DuplicatesAreRefused()
    {
        Assert.Equal(CombatCodes.RulesDuplicate, Code(Standard.Replace("{ \"id\": \"blunt\"", "{ \"id\": \"slash\"")));
        Assert.Equal(CombatCodes.RulesDuplicate, Code(Standard.Replace("{ \"id\": \"light\"", "{ \"id\": \"open\"")));
        Assert.Equal(CombatCodes.RulesDuplicate, Code(Standard.Replace("{ \"id\": \"veteran\"", "{ \"id\": \"raw\"")));
    }

    [Fact]
    public void RangeBandsMustRiseAndReachTheEndOfTheRange()
    {
        Assert.Equal(CombatCodes.RulesBands, Code(Standard.Replace("\"up_to_percent\": 50", "\"up_to_percent\": 20")));
        Assert.Equal(CombatCodes.RulesBands, Code(Standard.Replace("\"up_to_percent\": 100,", "\"up_to_percent\": 90,")));
    }

    [Fact]
    public void AnIdMayOnlyUseLowerCaseLettersDigitsAndHyphens() => Assert.Equal(CombatCodes.RulesFormat, Code(Standard.Replace("\"id\": \"slash\"", "\"id\": \"Slash!\"")));
}
