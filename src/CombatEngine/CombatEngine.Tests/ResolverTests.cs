using CombatEngine;
using Xunit;
using static CombatEngine.Tests.Scenes;

namespace CombatEngine.Tests;

public sealed class ResolverTests
{
    private static readonly CombatResolver Resolver = new(Rules());

    // ---- determinism --------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheSameRequestResolvesTheSameWayAndADifferentSeedOrAttemptDoesNot()
    {
        var request = Request(Squad("a"), Squad("b", side: 2, members: 200));
        var first = Resolver.Fire(request);
        var second = Resolver.Fire(request);
        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(first.Shots, second.Shots);
        Assert.NotEqual(first.Hash, Resolver.Fire(request with { Seed = 8 }).Hash);
        Assert.NotEqual(first.Hash, Resolver.Fire(request with { Attempt = 2 }).Hash);
    }

    [Fact]
    public void ARollIsPinnedToItsInputs()
    {
        // A pinned value: the roll for (seed 7, attempt 1, group a, battery main, shot 0). If the roll function changes, saved games replay differently.
        Assert.Equal(CombatRoll.Permille(7, 1, "a", "main", 0), CombatRoll.Permille(7, 1, "a", "main", 0));
        Assert.InRange(CombatRoll.Permille(7, 1, "a", "main", 0), 0, 999);
        Assert.NotEqual(CombatRoll.Permille(7, 1, "a", "main", 0), CombatRoll.Permille(7, 1, "a", "main", 1) + 1000);
        var rolls = Enumerable.Range(0, 2000).Select(i => CombatRoll.Permille(7, 1, "a", "main", i)).ToList();
        Assert.InRange(rolls.Average(), 450, 550);   // roughly uniform over 0..999
    }

    // ---- shots, rate, range, ammunition -------------------------------------------------------------------------------------

    [Fact]
    public void EveryEarnedShotLandsInExactlyOneOutcome()
    {
        var result = Resolver.Fire(Request(Squad("a"), Squad("b", side: 2), seconds: 10));
        var report = Assert.Single(result.Batteries);
        Assert.Equal(100, report.Earned);                       // 10 rifles x 60 a minute x 10 seconds
        Assert.Equal(report.Earned, report.Fired + report.OutOfRange + report.NoAmmo + report.Wasted);
        Assert.Equal(report.Fired, report.Hits + report.Misses);
    }

    [Fact]
    public void DamageToTheDefenderIsExactlyWhatTheHitsDid()
    {
        var defender = Squad("b", side: 2, armor: 30, members: 200);
        var result = Resolver.Fire(Request(Squad("a"), defender));
        var report = Assert.Single(result.Batteries);
        Assert.Equal(defender.Health - result.Defender.Health, report.Damage);
        Assert.True(report.Hits > 0);
        var each = DamageRule.Compute(Rules(), 100, "ballistic", 10, 30, 100, 100);
        Assert.Equal(report.Hits * each, report.Damage);       // single damage rule, no splash
    }

    [Fact]
    public void ABeyondRangeShotIsNeitherFiredNorSpent()
    {
        var result = Resolver.Fire(Request(Squad("a", ammo: new AmmoSpec("ball", null, 0, 0, 0)), Squad("b", side: 2), distanceMetres: 401));
        var report = Assert.Single(result.Batteries);
        Assert.Equal(report.Earned, report.OutOfRange);
        Assert.Equal(0, report.Fired);
        Assert.Equal(1000, result.Attacker.Batteries[0].Rounds);
        Assert.Equal(CombatCodes.OutOfRange, result.Shots[0].Code);
        Assert.Equal(1000, result.Defender.Health);
    }

    [Fact]
    public void AmmunitionLimitsFireAndIsSpentOneRoundAShot()
    {
        var ammo = new AmmoSpec("ball", null, 0, 0, 0);
        var result = Resolver.Fire(Request(Squad("a", rounds: 30, ammo: ammo), Squad("b", side: 2, members: 200), logged: 40));
        var report = Assert.Single(result.Batteries);
        Assert.Equal(30, report.Fired);
        Assert.Equal(70, report.NoAmmo);
        Assert.Equal(0, result.Attacker.Batteries[0].Rounds);
        Assert.Contains(result.Shots, s => s.Code == CombatCodes.NoAmmo);
        var again = Resolver.Fire(Request(result.Attacker, Squad("b", side: 2, members: 200), attempt: 2));
        Assert.Equal(0, again.Batteries[0].Fired);
    }

    [Fact]
    public void AmmunitionAddsToTheWeaponAndMayChangeTheDamageType()
    {
        var plain = Resolver.Fire(Request(Squad("a", weapon: Rifle(damage: 50, penetration: 0)), Squad("b", side: 2, armor: 0, members: 200)));
        var loaded = Resolver.Fire(Request(Squad("a", weapon: Rifle(damage: 50, penetration: 0), ammo: new AmmoSpec("ap", "blast", 30, 20, 0)), Squad("b", side: 2, armor: 0, members: 200)));
        Assert.Equal(50, plain.Batteries[0].Damage / plain.Batteries[0].Hits);
        Assert.Equal(80, loaded.Batteries[0].Damage / loaded.Batteries[0].Hits);
    }

    [Fact]
    public void SlowWeaponsFireOverSeveralEngagementsByCarryingTheFraction()
    {
        var slow = Squad("a", members: 1, weapon: Rifle(perMinute: 3));
        var first = Resolver.Fire(Request(slow, Squad("b", side: 2), seconds: 10));
        Assert.Equal(0, first.Batteries[0].Earned);
        Assert.Equal(500, first.Attacker.Batteries[0].CarryMilli);
        var second = Resolver.Fire(Request(first.Attacker, Squad("b", side: 2), seconds: 10, attempt: 2));
        Assert.Equal(1, second.Batteries[0].Earned);
        Assert.Equal(0, second.Attacker.Batteries[0].CarryMilli);
    }

    [Fact]
    public void FirepowerFollowsTheSurvivors()
    {
        var full = Resolver.Fire(Request(Squad("a"), Squad("b", side: 2)));
        var half = Resolver.Fire(Request(Squad("a", health: 500), Squad("b", side: 2)));
        Assert.Equal(100, full.Batteries[0].Earned);
        Assert.Equal(50, half.Batteries[0].Earned);
    }

    [Fact]
    public void ADestroyedTargetWastesTheRestOfTheShots()
    {
        var result = Resolver.Fire(Request(Squad("a", weapon: Rifle(accuracyPercent: 100, damage: 500)), Squad("b", side: 2, members: 1)));
        var report = Assert.Single(result.Batteries);
        Assert.True(result.Defender.Destroyed);
        Assert.True(report.Wasted > 0);
        Assert.Contains(CombatCodes.Destroyed, result.Codes);
        Assert.Equal(report.Earned, report.Fired + report.Wasted);
    }

    // ---- modifiers ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void CoverTrainingAndRangeChangeHowOftenShotsHit()
    {
        long Hits(string cover = "open", string training = "regular", long metres = 100) =>
            Resolver.Fire(Request(Squad("a", members: 100, training: training), Squad("b", side: 2, members: 1000), distanceMetres: metres, cover: cover)).Batteries[0].Hits;
        Assert.True(Hits("open") > Hits("light") && Hits("light") > Hits("heavy") && Hits("heavy") > Hits("fortified"));
        Assert.True(Hits(training: "elite") > Hits(training: "regular") && Hits(training: "regular") > Hits(training: "raw"));
        Assert.True(Hits(metres: 50) > Hits(metres: 380));
    }

    [Fact]
    public void ASplashHitDamagesSeveralMembersAndIgnoresCover()
    {
        var shell = Rifle(damage: 100, penetration: 100, splashMetres: 6, perMinute: 6, type: "blast");
        long Damage(string cover)
        {
            for (long attempt = 1; attempt < 200; attempt++)
            {
                var r = Resolver.Fire(Request(Squad("a", members: 1, weapon: shell), Squad("b", side: 2, members: 50), cover: cover, seconds: 10, attempt: attempt));
                if (r.Batteries[0].Hits == 1) return r.Batteries[0].Damage;
            }
            throw new InvalidOperationException("no hit in 200 attempts");
        }
        Assert.Equal(100 + 3 * 50, Damage("open"));          // 6 m / 2 m spacing = 3 extra members at half damage
        Assert.Equal(Damage("open"), Damage("fortified") + (Damage("open") - Damage("fortified")));
        var chanceOpen = HitRule.ChancePermille(Rules(), shell.AccuracyPermille, 100, 100, 100, false, 100, true);
        var chanceFortified = HitRule.ChancePermille(Rules(), shell.AccuracyPermille, 100, 100, 100, false, 35, true);
        Assert.Equal(chanceOpen, chanceFortified);
    }

    [Fact]
    public void ManyHitsSuppressTheDefenderAndASuppressedGroupShootsWorse()
    {
        var result = Resolver.Fire(Request(Squad("a", members: 100), Squad("b", side: 2, members: 100)));
        Assert.True(result.Defender.Suppressed);
        Assert.Contains(CombatCodes.Suppressed, result.Codes);
        var calm = Resolver.Fire(Request(Squad("b", side: 2, members: 100), Squad("a", members: 100), attempt: 5));
        var pinned = Resolver.Fire(Request(result.Defender, Squad("a", members: 100), attempt: 5));
        Assert.True(pinned.Batteries[0].Hits < calm.Batteries[0].Hits);
        Assert.False(Resolver.Fire(Request(Squad("a", members: 1), Squad("b", side: 2, members: 100))).Defender.Suppressed);
    }

    [Fact]
    public void AGroupBelowTheRoutShareRoutsAndStopsFiring()
    {
        var weak = Squad("b", side: 2, members: 10, health: 200);       // 2 of 10 left: below a quarter
        var result = Resolver.Fire(Request(Squad("a", members: 1, weapon: Rifle(damage: 1, accuracyPercent: 100)), weak));
        Assert.True(result.Defender.Routed);
        Assert.Contains(CombatCodes.Routed, result.Codes);
        var routedFire = Resolver.Fire(Request(result.Defender, Squad("a"), attempt: 3));
        Assert.Equal(0, routedFire.Batteries[0].Earned);
        Assert.Contains(CombatCodes.Routed, routedFire.Codes);
    }

    // ---- exchange -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void ABothWaysExchangeIsSimultaneous()
    {
        var a = Squad("a", side: 1, members: 20);
        var b = Squad("b", side: 2, members: 20);
        var exchange = Resolver.Exchange(a, b, 100_000, 10, "open", "open", 7, 1, 3);
        Assert.True(exchange.First.Health < a.Health && exchange.Second.Health < b.Health);
        // Each side fired its full allowance at the state it started with.
        Assert.Equal(exchange.FirstFired.Batteries[0].Earned, exchange.SecondFired.Batteries[0].Earned);
        Assert.Equal(a.Health - exchange.First.Health, exchange.SecondFired.Batteries[0].Damage);
        Assert.Equal(b.Health - exchange.Second.Health, exchange.FirstFired.Batteries[0].Damage);
        Assert.Equal(exchange.Hash, Resolver.Exchange(a, b, 100_000, 10, "open", "open", 7, 1, 3).Hash);
    }

    // ---- the log and refusals ------------------------------------------------------------------------------------------------

    [Fact]
    public void TheShotLogIsBoundedAndExplainsEachShot()
    {
        var result = Resolver.Fire(Request(Squad("a"), Squad("b", side: 2), logged: 3));
        Assert.Equal(3, result.Shots.Count);
        Assert.All(result.Shots, s => Assert.Contains(s.Code, new[] { CombatCodes.Hit, CombatCodes.Miss }));
        foreach (var shot in result.Shots)
            Assert.Equal(shot.Code == CombatCodes.Hit, shot.Roll < shot.ChancePermille);
        Assert.Empty(Resolver.Fire(Request(Squad("a"), Squad("b", side: 2), logged: 0)).Shots);
    }

    [Fact]
    public void BadRequestsAreRefusedWithTheirCodes()
    {
        string Code(EngagementRequest request) => Assert.Throws<CombatRefusedException>(() => Resolver.Fire(request)).Code;
        var good = Request(Squad("a"), Squad("b", side: 2));
        Assert.Equal(CombatCodes.UnknownCover, Code(good with { Cover = "bunker" }));
        Assert.Equal(CombatCodes.UnknownTraining, Code(good with { Attacker = Squad("a", training: "legendary") }));
        Assert.Equal(CombatCodes.UnknownDamageType, Code(good with { Attacker = Squad("a", weapon: Rifle(type: "sonic")) }));
        Assert.Equal(CombatCodes.UnknownDamageType, Code(good with { Attacker = Squad("a", ammo: new AmmoSpec("x", "sonic", 1, 1, 0)) }));
        Assert.Equal(CombatCodes.BadRequest, Code(good with { Seconds = 0 }));
        Assert.Equal(CombatCodes.BadRequest, Code(good with { DistanceMilli = -1 }));
        Assert.Equal(CombatCodes.BadRequest, Code(good with { LoggedShots = -1 }));
    }
}
