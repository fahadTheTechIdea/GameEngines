using CombatEngine;
using Xunit;

namespace CombatEngine.Tests;

/// <summary>Groups with a platform pool (vehicles, guns) beside their people: where hits land, what fires, and what crew it takes.</summary>
public sealed class PlatformTests
{
    private static readonly CombatResolver Resolver = new(Scenes.Rules());

    private static string Code(Action action) => Assert.Throws<CombatRefusedException>(action).Code;

    /// <summary>Sixteen people and four tanks of 900 structure each. A tank needs four crew; the gun battery is the tanks, the rifles are the people.</summary>
    private static Group Armoured(string id = "a", int side = 1, long people = 16, long platformHealth = 3600, long crew = 4)
    {
        var gun = new Battery("gun", Scenes.Rifle(damage: 120, penetration: 5, rangeMetres: 2000, accuracyPercent: 100, perMinute: 6), null, 4, 0, 0, 0, Platform: true);
        var rifles = new Battery("rifles", Scenes.Rifle(damage: 20, rangeMetres: 2000, accuracyPercent: 100, perMinute: 6), null, 16, 0, 0, 0);
        return new Group(id, side, "regular", 16, 100, people * 100, 0, false, false, [gun, rifles], 850, 1400,
            PlatformHealth: platformHealth, PlatformUnitHealth: 900, InitialPlatforms: 4, PlatformArmor: 0, CrewPerPlatform: crew);
    }

    /// <summary>A target nothing can empty in one engagement: a huge crowd of people, no platforms.</summary>
    private static Group Crowd(string id = "b", int side = 2) => Scenes.Squad(id, side, members: 100_000);

    private static long Fired(EngagementResult result, string battery) => result.Batteries.Single(b => b.Battery == battery).Fired;

    // ---- the pools ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AGroupCountsPlatformsAndPeopleSeparately()
    {
        var g = Armoured();
        Assert.Equal(16, g.Members);
        Assert.Equal(4, g.Platforms);
        Assert.Equal(4, g.OperablePlatforms);
        Assert.Equal(3, (g with { PlatformHealth = 2700 }).Platforms);
        Assert.Equal(1, (g with { PlatformHealth = 1 }).Platforms);   // a part of a unit is still a unit
        Assert.Equal(0, (g with { PlatformHealth = 0 }).Platforms);
        Assert.Equal(4, (g with { PlatformHealth = 99_999 }).Platforms);   // never more than it started with
        Assert.False(g.Destroyed);
        Assert.True((g with { Health = 0 }).Destroyed);   // nobody left to work the platforms
    }

    [Fact]
    public void HitsOnAMixedGroupLandInBothPoolsByTheShareOfHealthEachHolds()
    {
        var target = Armoured("b", 2);
        // Weak, accurate fire at a long engagement: plenty of hits, none enough to empty a pool.
        var attacker = Scenes.Squad("a", 1, members: 4000, weapon: Scenes.Rifle(damage: 5, penetration: 0, rangeMetres: 2000, accuracyPercent: 100, perMinute: 6));
        var result = Resolver.Fire(Scenes.Request(attacker, target, distanceMetres: 50, seconds: 60));
        var people = target.Health - result.Defender.Health;
        var platforms = target.PlatformHealth - result.Defender.PlatformHealth;
        Assert.True(people > 0 && platforms > 0, $"people lost {people}, platform lost {platforms}");
        // Platforms hold 3600 of the 5200 health: about 69 percent of the hits.
        var share = platforms * 100 / (people + platforms);
        Assert.InRange(share, 60, 78);
    }

    [Fact]
    public void ADefenderWithNoPlatformsTakesEveryHitInThePeoplePool()
    {
        var attacker = Scenes.Squad("a", 1, members: 500, weapon: Scenes.Rifle(damage: 5, penetration: 0, rangeMetres: 2000, accuracyPercent: 100, perMinute: 6));
        var result = Resolver.Fire(Scenes.Request(attacker, Crowd(), distanceMetres: 50, seconds: 60));
        Assert.True(result.Defender.Health < 100_000 * 100);
        Assert.Equal(0, result.Defender.PlatformHealth);
    }

    [Fact]
    public void WhenOnePoolIsEmptyEveryHitLandsInTheOther()
    {
        var attacker = Scenes.Squad("a", 1, members: 2000, weapon: Scenes.Rifle(damage: 5, penetration: 0, rangeMetres: 2000, accuracyPercent: 100, perMinute: 6));
        var noPlatforms = Armoured("b", 2, platformHealth: 0);
        var onlyPlatformsHit = Resolver.Fire(Scenes.Request(attacker, noPlatforms, distanceMetres: 50, seconds: 30));
        Assert.Equal(0, onlyPlatformsHit.Defender.PlatformHealth);
        Assert.True(onlyPlatformsHit.Defender.Health < noPlatforms.Health);
    }

    // ---- firepower and crew -------------------------------------------------------------------------------------------------------

    [Fact]
    public void PlatformBatteriesFollowTheStandingCrewedPlatformsAndPeopleBatteriesFollowThePeople()
    {
        long Gun(Group g) => Fired(Resolver.Fire(Scenes.Request(g, Crowd(), distanceMetres: 50, seconds: 600)), "gun");
        long Rifles(Group g) => Fired(Resolver.Fire(Scenes.Request(g, Crowd(), distanceMetres: 50, seconds: 600)), "rifles");
        var whole = Armoured();
        Assert.Equal(4 * 60, Gun(whole));                                         // six a minute for ten minutes, per tank
        Assert.Equal(16 * 60, Rifles(whole));
        // Half the platforms gone: half the guns; the people are untouched.
        var halfPlatforms = whole with { PlatformHealth = 1800 };
        Assert.Equal(2 * 60, Gun(halfPlatforms));
        Assert.Equal(16 * 60, Rifles(halfPlatforms));
        // Half the people gone: the rifles halve; four crew a tank leaves crew for two tanks, so the guns halve with them.
        var halfPeople = whole with { Health = 800 };
        Assert.Equal(8 * 60, Rifles(halfPeople));
        Assert.Equal(2 * 60, Gun(halfPeople));
        // Six people left: one crew of four, so one tank fires although four stand.
        var sixLeft = whole with { Health = 600 };
        Assert.Equal(4, sixLeft.Platforms);
        Assert.Equal(1, sixLeft.OperablePlatforms);
        Assert.Equal(1 * 60, Gun(sixLeft));
        // Crew that is not needed does not limit anything.
        Assert.Equal(4 * 60, Gun(whole with { CrewPerPlatform = 0, Health = 100 }));
    }

    [Fact]
    public void AGroupWithoutPlatformsFiresExactlyAsItAlwaysDid()
    {
        var plain = Scenes.Squad("a", 1, members: 10);
        var hurt = plain with { Health = 500 };
        var result = Resolver.Fire(Scenes.Request(hurt, Crowd(), distanceMetres: 50, seconds: 60));
        Assert.Equal(5 * 60, Fired(result, "main"));   // ceil(10 x 5 / 10) rifles for a minute, six a minute each... follows the survivors
    }

    [Fact]
    public void PlatformsAreProtectedByPlatformArmor()
    {
        // Almost all of the group health is platforms, so almost every hit lands there.
        var bare = Armoured("b", 2) with { Health = 100, PlatformHealth = 3600 };
        var attacker = Scenes.Squad("a", 1, members: 300, weapon: Scenes.Rifle(damage: 60, penetration: 0, rangeMetres: 2000, accuracyPercent: 100, perMinute: 6));
        long Lost(Group target) => target.PlatformHealth - Resolver.Fire(Scenes.Request(attacker, target, distanceMetres: 50, seconds: 30)).Defender.PlatformHealth;
        var open = Lost(bare);
        var plated = Lost(bare with { PlatformArmor = 300 });
        Assert.True(open > 0);
        Assert.True(plated < open, $"plated {plated}, open {open}");
    }

    // ---- crews of weapons that are not platforms ---------------------------------------------------------------------------------------

    /// <summary>Twenty people with eight machine guns of four crew each: more crew than there are people, so the crew is what limits the guns.</summary>
    private static Group Gunners(long people = 20)
    {
        var guns = new Battery("guns", Scenes.Rifle(damage: 20, rangeMetres: 2000, accuracyPercent: 100, perMinute: 6), null, 8, 0, 0, 0, CrewPerUnit: 4);
        return new Group("g", 1, "regular", 20, 100, people * 100, 0, false, false, [guns], 850, 1400);
    }

    [Fact]
    public void WeaponsThatNeedACrewFireOnlyAsManyAsThePeopleAliveCanCrew()
    {
        long Fired(Group g) => Resolver.Fire(Scenes.Request(g, Crowd(), distanceMetres: 50, seconds: 600)).Batteries.Single().Fired;
        Assert.Equal(5 * 60, Fired(Gunners()));            // twenty people crew five guns of four
        Assert.Equal(2 * 60, Fired(Gunners(people: 10)));  // ten people: two crews, though the share of survivors alone would say four
        Assert.Equal(0, Fired(Gunners(people: 3)));        // fewer than one crew
        // With a crew of one the rule has nothing to say beyond the share of survivors.
        var single = Gunners(10) with { Batteries = [Gunners().Batteries[0] with { CrewPerUnit = 1 }] };
        Assert.Equal(4 * 60, Fired(single));
        // No crew stated: as before.
        var none = Gunners(10) with { Batteries = [Gunners().Batteries[0] with { CrewPerUnit = 0 }] };
        Assert.Equal(4 * 60, Fired(none));
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Fire(Scenes.Request(Gunners() with { Batteries = [Gunners().Batteries[0] with { CrewPerUnit = -1 }] }, Crowd()))));
    }

    // ---- standing, rounds and refusals ----------------------------------------------------------------------------------------------------

    [Fact]
    public void LosingPlatformsCostsMoraleButIsNotAMemberLossAndDoesNotRoutByStrength()
    {
        var target = Armoured("b", 2);
        var attacker = Scenes.Squad("a", 1, members: 3000, weapon: Scenes.Rifle(damage: 40, penetration: 20, rangeMetres: 2000, accuracyPercent: 100, perMinute: 6));
        var round = Resolver.Round([attacker, target], [new RoundOrder("a", "b", 50_000, "open", "open")], 20, 7, 1, 0);
        var after = round.Groups.Single(g => g.Id == "b");
        Assert.True(after.PlatformHealth < target.PlatformHealth);
        Assert.True(after.Morale < target.Morale, "platform damage should shake the group");
    }

    [Fact]
    public void LosingPlatformsAloneLowersMoraleByTheShareOfTheWholeGroupLostAndLosingNothingRestoresIt()
    {
        var rules = Scenes.Rules();
        var group = Armoured();   // 1,600 of people and 3,600 of platforms: 5,200 in all
        var lostNothing = StandingRule.After(rules, group, group.Health, 0);
        Assert.Equal(Math.Min(1000, group.Morale + rules.Morale.Recovery), lostNothing.Morale);
        var lostPlatforms = StandingRule.After(rules, group, group.Health, 0, platformHealthAfter: 1800);   // half the platform pool: 1,800 of 5,200
        Assert.Equal(group.Morale - 1800L * 1000 / 5200 * rules.Morale.CasualtyLossPercent / 100, lostPlatforms.Morale);
        Assert.Equal(1800, lostPlatforms.PlatformHealth);
        Assert.Equal(group.Health, lostPlatforms.Health);
        Assert.False(lostPlatforms.Routed);   // strength is people, and none were lost
    }

    [Fact]
    public void ARoundAddsTheDamageEveryAttackerDoesToEachPoolAndNeverGoesBelowZero()
    {
        var target = Armoured("c", 3, people: 16, platformHealth: 900);
        var a = Scenes.Squad("a", 1, members: 2000, weapon: Scenes.Rifle(damage: 60, penetration: 50, rangeMetres: 2000, accuracyPercent: 100, perMinute: 6));
        var b = Scenes.Squad("b", 2, members: 2000, weapon: Scenes.Rifle(damage: 60, penetration: 50, rangeMetres: 2000, accuracyPercent: 100, perMinute: 6));
        var one = Resolver.Round([a, b, target], [new RoundOrder("a", "c", 50_000, "open", "open")], 10, 7, 1, 0).Groups.Single(g => g.Id == "c");
        var two = Resolver.Round([a, b, target], [new RoundOrder("a", "c", 50_000, "open", "open"), new RoundOrder("b", "c", 50_000, "open", "open")], 10, 7, 1, 0).Groups.Single(g => g.Id == "c");
        Assert.True(one.PlatformHealth >= 0 && two.PlatformHealth >= 0 && two.Health >= 0);
        Assert.True(two.Health <= one.Health && two.PlatformHealth <= one.PlatformHealth);
        Assert.True(two.PlatformHealth < one.PlatformHealth || one.PlatformHealth == 0);
    }

    [Fact]
    public void ThePlatformPoolIsPartOfTheStateHash()
    {
        var g = Armoured();
        Assert.NotEqual(StateHash.Of(g), StateHash.Of(g with { PlatformHealth = 3599 }));
        Assert.Equal(StateHash.Of(g), StateHash.Of(g with { }));
        var plain = Scenes.Squad("a");
        Assert.Equal(StateHash.Of(plain), StateHash.Of(plain with { PlatformHealth = 0 }));   // no platforms: the hash is the one it always was
    }

    [Fact]
    public void ImpossiblePlatformNumbersAreRefusedWhole()
    {
        var attacker = Scenes.Squad("a", 1, members: 5);
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Fire(Scenes.Request(attacker, Armoured("b", 2, platformHealth: 3601)))));   // more than four units hold
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Fire(Scenes.Request(attacker, Armoured("b", 2) with { PlatformUnitHealth = 0 }))));
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Fire(Scenes.Request(attacker, Armoured("b", 2) with { CrewPerPlatform = -1 }))));
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Fire(Scenes.Request(attacker, Scenes.Squad("b", 2) with { PlatformHealth = 5 }))));   // a platform pool with no platforms
    }

    [Fact]
    public void ReinforcingBringsInPeopleAndLeavesPlatformsAlone()
    {
        var hurt = Armoured() with { Health = 800, PlatformHealth = 1800 };
        var result = Logistics.Reinforce(hurt, 5);
        Assert.Equal(5, result.Accepted);
        Assert.Equal(13, result.Group.Members);
        Assert.Equal(1800, result.Group.PlatformHealth);
    }
}
