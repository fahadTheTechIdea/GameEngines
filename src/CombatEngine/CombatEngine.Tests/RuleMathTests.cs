using CombatEngine;
using Xunit;

namespace CombatEngine.Tests;

/// <summary>The damage rule and the hit rule with worked numbers (K = 100, cut 50 percent, floor 1).</summary>
public sealed class RuleMathTests
{
    private static readonly CombatRules Rules = Scenes.Rules();

    [Theory]
    [InlineData(100, "ballistic", 10, 0, 100, 100, 100)]    // no armor: full damage
    [InlineData(100, "ballistic", 10, 50, 100, 100, 35)]    // armor 50 less penetration 10 = 40: 100*100/140 = 71, not pierced so cut by half = 35
    [InlineData(100, "ballistic", 25, 20, 100, 100, 100)]   // penetration beats armor: no mitigation, no cut
    [InlineData(100, "blunt", 0, 100, 100, 100, 55)]        // blunt counts 80 percent of armor, is not armor-checked: 100*100/180
    [InlineData(5, "ballistic", 0, 1000, 100, 100, 1)]      // never below the floor
    [InlineData(100, "slash", 0, 0, 110, 90, 99)]           // elite training 110 percent and fortified 90 percent
    [InlineData(100, "energy", 0, 100, 100, 100, 66)]       // energy counts half the armor: 100*100/150
    [InlineData(0, "slash", 0, 0, 100, 100, 0)]             // a zero-damage hit stays zero
    public void DamageFollowsTheOneRule(long baseDamage, string type, long penetration, long armor, int training, int cover, long expected) =>
        Assert.Equal(expected, DamageRule.Compute(Rules, baseDamage, type, penetration, armor, training, cover));

    [Fact]
    public void MoreArmorNeverMeansMoreDamageAndMorePenetrationNeverMeansLess()
    {
        long last = long.MaxValue;
        for (var armor = 0; armor <= 400; armor += 20)
        {
            var damage = DamageRule.Compute(Rules, 200, "slash", 0, armor, 100, 100);
            Assert.True(damage <= last, "armor " + armor + " did " + damage + " after " + last);
            last = damage;
        }
        long previous = 0;
        for (var penetration = 0; penetration <= 400; penetration += 20)
        {
            var damage = DamageRule.Compute(Rules, 200, "slash", penetration, 200, 100, 100);
            Assert.True(damage >= previous, "penetration " + penetration);
            previous = damage;
        }
    }

    [Fact]
    public void AnUnknownDamageTypeIsRefused() =>
        Assert.Equal(CombatCodes.UnknownDamageType, Assert.Throws<CombatRefusedException>(() => DamageRule.Compute(Rules, 10, "sonic", 0, 0, 100, 100)).Code);

    [Theory]
    [InlineData(100, 400, 100)]    // 25 percent of the range: first band
    [InlineData(101, 400, 100)]    // 25.25 percent still reads as 25
    [InlineData(120, 400, 90)]
    [InlineData(250, 400, 75)]
    [InlineData(400, 400, 55)]
    [InlineData(0, 400, 100)]
    public void RangeBandsPickTheAccuracyOfTheShareOfTheRange(long metres, long rangeMetres, int expected) =>
        Assert.Equal(expected, HitRule.BandPercent(Rules, metres * 1000, rangeMetres * 1000));

    [Fact]
    public void HitChanceScalesAndClamps()
    {
        Assert.Equal(800, HitRule.ChancePermille(Rules, 800, 100, 100, 100, false, 100, false));
        Assert.Equal(920, HitRule.ChancePermille(Rules, 800, 100, 115, 100, false, 100, false));       // veteran
        Assert.Equal(950, HitRule.ChancePermille(Rules, 800, 100, 130, 100, false, 100, false));       // clamped to the ceiling
        Assert.Equal(480, HitRule.ChancePermille(Rules, 800, 100, 100, 100, true, 100, false));        // suppressed to 60 percent
        Assert.Equal(640, HitRule.ChancePermille(Rules, 800, 100, 100, 100, false, 80, false));        // light cover
        Assert.Equal(800, HitRule.ChancePermille(Rules, 800, 100, 100, 100, false, 35, true));         // an area weapon ignores cover
        Assert.Equal(50, HitRule.ChancePermille(Rules, 10, 100, 100, 100, false, 100, false));         // clamped to the floor
    }
}
