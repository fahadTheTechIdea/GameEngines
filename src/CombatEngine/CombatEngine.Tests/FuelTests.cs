using CombatEngine;
using Xunit;

namespace CombatEngine.Tests;

/// <summary>The march rule with platforms: crew and fuel decide whether a group rides or walks, and the fuel it burns riding.</summary>
public sealed class FuelTests
{
    private static readonly CombatRules Rules = Scenes.Rules();

    private static string Code(Action action) => Assert.Throws<CombatRefusedException>(action).Code;

    /// <summary>Sixteen people and four tanks (900 structure each, four crew each) that ride at 5 m/s and burn 100 thousandths of a unit a second all together. The tanks hold 400,000.</summary>
    private static Group Tanks(long fuel = 400_000) => new("t", 1, "regular", 16, 100, 1600, 0, false, false, [], 850, 5000,
        PlatformHealth: 3600, PlatformUnitHealth: 900, InitialPlatforms: 4, PlatformArmor: 0, CrewPerPlatform: 4,
        Fuel: fuel, FuelCapacity: 400_000, BurnPerHourMilli: 360_000);

    private static long Foot => Rules.FootSpeedMilliPerSecond;

    // ---- the march ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AGroupWithNoPlatformsMarchesExactlyAsItAlwaysDid()
    {
        var plain = Scenes.Squad("a");
        var march = MovementRule.March(Rules, plain, 100_000, 20, "open");
        Assert.Equal(MovementRule.Advance(Rules, 100_000, plain.SpeedMilli, 20, "open"), march.DistanceMilli);
        Assert.Same(plain, march.Group);
        Assert.Equal((0L, 0), (march.FuelBurnedMilli, march.MotorSeconds));
    }

    [Fact]
    public void AFuelledGroupRidesWhileTheFuelLastsAndBurnsFuelForEverySecondItRides()
    {
        var march = MovementRule.March(Rules, Tanks(), 1_000_000, 10, "open");
        Assert.Equal(MovementRule.Advance(Rules, 1_000_000, 5000, 10, "open"), march.DistanceMilli);
        Assert.Equal(10, march.MotorSeconds);
        Assert.Equal(1000, march.FuelBurnedMilli);                  // 360,000 an hour is 100 a second
        Assert.Equal(399_000, march.Group.Fuel);
    }

    [Fact]
    public void WhenTheFuelRunsOutMidMarchTheGroupRidesWhatItCanAndWalksTheRest()
    {
        var march = MovementRule.March(Rules, Tanks(fuel: 600), 1_000_000, 10, "open");   // six seconds of fuel
        Assert.Equal(6, march.MotorSeconds);
        var ridden = MovementRule.Advance(Rules, 1_000_000, 5000, 6, "open");
        Assert.Equal(MovementRule.Advance(Rules, ridden, Foot, 4, "open"), march.DistanceMilli);
        Assert.Equal(600, march.FuelBurnedMilli);
        Assert.Equal(0, march.Group.Fuel);
        // With nothing left it walks all of it and burns nothing.
        var dry = MovementRule.March(Rules, march.Group, 1_000_000, 10, "open");
        Assert.Equal(MovementRule.Advance(Rules, 1_000_000, Foot, 10, "open"), dry.DistanceMilli);
        Assert.Equal((0L, 0), (dry.FuelBurnedMilli, dry.MotorSeconds));
    }

    [Fact]
    public void AGroupWithoutCrewForAnyPlatformWalksAndBurnsNothing()
    {
        var crewless = Tanks() with { Health = 300 };   // three people, and a tank needs four
        Assert.Equal(0, crewless.OperablePlatforms);
        var march = MovementRule.March(Rules, crewless, 1_000_000, 10, "open");
        Assert.Equal(MovementRule.Advance(Rules, 1_000_000, Foot, 10, "open"), march.DistanceMilli);
        Assert.Equal(0, march.FuelBurnedMilli);
    }

    [Fact]
    public void LostTanksBurnLessFuelAndTheirFuelGoesDownWithThem()
    {
        var half = Tanks(fuel: 200_000) with { PlatformHealth = 1800 };   // two tanks stand and hold what they carried
        Assert.Equal(200_000, half.FuelRoom);
        var march = MovementRule.March(Rules, half, 1_000_000, 10, "open");
        Assert.Equal(500, march.FuelBurnedMilli);              // two tanks burn half as much
        Assert.Equal(10, march.MotorSeconds);
        var march2 = MovementRule.March(Rules, half, 1_000_000, 3600, "open");
        Assert.Equal(3600, march2.MotorSeconds);               // 200,000 at 50 a second lasts four thousand seconds
        var tiny = MovementRule.March(Rules, half with { Fuel = 50_000, PlatformHealth = 900 }, 1_000_000, 3600, "open");   // one tank: 25 a second, 50,000 in the tank
        Assert.Equal(2000, tiny.MotorSeconds);
    }

    [Fact]
    public void WhenPlatformsAreLostTheFuelTheyCarriedIsLostWithThemAndNeverComesBack()
    {
        var rules = Rules;
        var whole = Tanks();
        var hit = StandingRule.After(rules, whole, whole.Health, 3, platformHealthAfter: 1800);   // two of four tanks are gone
        Assert.Equal(200_000, hit.Fuel);
        Assert.Equal(200_000, hit.FuelRoom);
        var again = StandingRule.After(rules, hit, hit.Health, 1, platformHealthAfter: 900);
        Assert.Equal(100_000, again.Fuel);
        Assert.Equal(whole.Fuel, StandingRule.After(rules, whole, whole.Health, 0).Fuel);          // no platform lost, no fuel lost
        // Tanks that were half empty lose only what they held.
        var low = Tanks(fuel: 60_000);
        Assert.Equal(60_000, StandingRule.After(rules, low, low.Health, 1, platformHealthAfter: 2700).Fuel);   // three tanks hold 300,000: nothing lost
        Assert.Equal(60_000, StandingRule.After(rules, low, low.Health, 1, platformHealthAfter: 900).Fuel);    // one tank holds 100,000: still nothing lost
        // Fuel above what the standing tanks hold is not a state the engine accepts.
        var resolver = new CombatResolver(rules);
        Assert.Equal(CombatCodes.BadRequest, Code(() => resolver.Fire(Scenes.Request(Scenes.Squad("b", 2), Tanks() with { PlatformHealth = 1800 }))));
    }

    [Fact]
    public void APlatformGroupThatBurnsNoFuelRidesAllTheTimeAndBurnsNone()
    {
        var horses = Tanks() with { Fuel = 0, FuelCapacity = 0, BurnPerHourMilli = 0 };
        var march = MovementRule.March(Rules, horses, 1_000_000, 10, "open");
        Assert.Equal(10, march.MotorSeconds);
        Assert.Equal(0, march.FuelBurnedMilli);
        Assert.Equal(MovementRule.Advance(Rules, 1_000_000, 5000, 10, "open"), march.DistanceMilli);
    }

    [Fact]
    public void ABadMarchIsRefusedLikeAnyOther()
    {
        Assert.Equal(CombatCodes.BadRequest, Code(() => MovementRule.March(Rules, Tanks(), -1, 10, "open")));
        Assert.Equal(CombatCodes.BadRequest, Code(() => MovementRule.March(Rules, Tanks(), 1000, 0, "open")));
        Assert.Equal(CombatCodes.UnknownTerrain, Code(() => MovementRule.March(Rules, Tanks(), 1000, 10, "lava")));
    }

    // ---- pace of a column that has lost platforms ------------------------------------------------------------------------------------

    /// <summary>The harmonic mean of the platform pace and the foot pace by share of people, written out the long way.</summary>
    private static long Expected(long people, long riding, long motor, long foot) =>
        people * motor * foot / (riding * foot + (people - riding) * motor);

    [Fact]
    public void ARiderlessShareOfThePeopleWalksAndTheColumnMovesAtTheMeanPaceOfTheTwo()
    {
        var whole = Tanks();
        Assert.Equal(5000, whole.MarchSpeedMilli(Foot));                                                   // all four crewed tanks stand: everyone rides
        var half = Tanks() with { PlatformHealth = 1800 };                                                  // two tanks carry eight of sixteen
        Assert.Equal(Expected(16, 8, 5000, Foot), half.MarchSpeedMilli(Foot));
        var one = Tanks() with { PlatformHealth = 900 };
        Assert.Equal(Expected(16, 4, 5000, Foot), one.MarchSpeedMilli(Foot));
        // Eighteen people do not divide over four platforms: each carries five, the last one fewer.
        Assert.Equal(Expected(18, 10, 5000, Foot), (Tanks() with { InitialMembers = 18, Health = 1800, PlatformHealth = 1800 }).MarchSpeedMilli(Foot));
        Assert.True(half.MarchSpeedMilli(Foot) < whole.MarchSpeedMilli(Foot) && one.MarchSpeedMilli(Foot) < half.MarchSpeedMilli(Foot));
        Assert.True(one.MarchSpeedMilli(Foot) > Foot);
        // No platform, or none crewed: they all walk.
        Assert.Equal(Foot, (Tanks() with { PlatformHealth = 0 }).MarchSpeedMilli(Foot));
        Assert.Equal(Foot, (Tanks() with { Health = 300 }).MarchSpeedMilli(Foot));
        // Fewer people than seats: the people still ride in full while platforms stand to carry them.
        Assert.Equal(5000, (Tanks() with { Health = 800 }).MarchSpeedMilli(Foot));
    }

    [Fact]
    public void AStructureNeverMovesAndAGroupWithoutPlatformsKeepsItsOwnPace()
    {
        Assert.Equal(0, (Tanks() with { SpeedMilli = 0 }).MarchSpeedMilli(Foot));
        var plain = Scenes.Squad("a");
        Assert.Equal(plain.SpeedMilli, plain.MarchSpeedMilli(Foot));
    }

    [Fact]
    public void AMarchCoversOnlyAsFarAsTheColumnPaceAllows()
    {
        var half = Tanks() with { PlatformHealth = 1800 };
        var march = MovementRule.March(Rules, half, 1_000_000, 10, "open");
        Assert.Equal(MovementRule.Advance(Rules, 1_000_000, Expected(16, 8, 5000, Foot), 10, "open"), march.DistanceMilli);
        Assert.True(march.DistanceMilli > MovementRule.March(Rules, Tanks(), 1_000_000, 10, "open").DistanceMilli);   // slower than the whole column
    }

    // ---- refuelling ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void RefuellingFillsTheTanksOfTheStandingPlatformsAndNoFurther()
    {
        var low = Tanks(fuel: 100_000) with { PlatformHealth = 1800 };   // two tanks stand: room for 200,000 in all
        var first = Logistics.Refuel(low, 50_000);
        Assert.Equal(50_000, first.Accepted);
        Assert.Equal(150_000, first.Group.Fuel);
        var topped = Logistics.Refuel(first.Group, 1_000_000);
        Assert.Equal(50_000, topped.Accepted);
        Assert.Equal(200_000, topped.Group.Fuel);
        Assert.Equal(0, Logistics.Refuel(topped.Group, 5).Accepted);
        Assert.Equal(100_000, low.Fuel);   // the group given is untouched
    }

    [Fact]
    public void ARefuellingThatMakesNoSenseIsRefused()
    {
        Assert.Equal(CombatCodes.BadRequest, Code(() => Logistics.Refuel(Tanks(), -1)));
        Assert.Equal(CombatCodes.BadRequest, Code(() => Logistics.Refuel(Scenes.Squad("a"), 5)));                          // burns no fuel
        Assert.Equal(CombatCodes.GroupWithdrawn, Code(() => Logistics.Refuel(Tanks() with { Routed = true }, 5)));
        Assert.Equal(CombatCodes.GroupWithdrawn, Code(() => Logistics.Refuel(Tanks() with { Health = 0 }, 5)));
    }

    [Fact]
    public void FuelIsPartOfTheStateHashAndImpossibleTanksAreRefused()
    {
        var group = Tanks();
        Assert.NotEqual(StateHash.Of(group), StateHash.Of(group with { Fuel = group.Fuel - 1 }));
        var resolver = new CombatResolver(Rules);
        var foe = Scenes.Squad("b", 2);
        Assert.Equal(CombatCodes.BadRequest, Code(() => resolver.Fire(Scenes.Request(foe, group with { Fuel = 400_001 }))));
        Assert.Equal(CombatCodes.BadRequest, Code(() => resolver.Fire(Scenes.Request(foe, group with { Fuel = -1 }))));
        Assert.Equal(CombatCodes.BadRequest, Code(() => resolver.Fire(Scenes.Request(foe, group with { InitialPlatforms = 0, PlatformHealth = 0 }))));   // tanks of nothing
    }
}
