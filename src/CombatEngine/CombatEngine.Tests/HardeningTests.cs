using System.Globalization;
using CombatEngine;
using Xunit;

namespace CombatEngine.Tests;

/// <summary>Determinism hazards, numeric limits, validation of every number a request carries, and the code raised for each.</summary>
public sealed class HardeningTests
{
    private static readonly CombatRules Rules = Scenes.Rules();
    private static readonly CombatResolver Resolver = new(Rules);

    private static string Code(Action action) => Assert.Throws<CombatRefusedException>(action).Code;

    // ---- determinism --------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("sv-SE")]   // writes a real minus sign (U+2212) instead of a hyphen
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    [InlineData("fa-IR")]
    [InlineData("de-DE")]
    [InlineData("th-TH")]
    public void TheSameEngagementHasTheSameHashUnderAnyCulture(string culture)
    {
        var request = Scenes.Request(Scenes.Squad("a", members: 30, ammo: new AmmoSpec("ball", null, 5, 2, 0)), Scenes.Squad("b", members: 30, armor: 40), distanceMetres: 120, seconds: 30, seed: 123456789);
        var expected = Resolver.Fire(request).Hash;
        var exchangeExpected = Resolver.Exchange(request.Attacker, request.Defender, 120_000, 30, "light", "forest", 77, 1, 4).Hash;
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            Assert.Equal(expected, Resolver.Fire(request).Hash);
            Assert.Equal(exchangeExpected, Resolver.Exchange(request.Attacker, request.Defender, 120_000, 30, "light", "forest", 77, 1, 4).Hash);
            Assert.Equal(CombatRoll.Permille(7, 1, "a", "main", 0), CombatRoll.Permille(7, 1, "a", "main", 0));
            Assert.Equal(StateHash.Of(request.Attacker, request.Defender), StateHash.Of(request.Attacker, request.Defender));
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Fact]
    public void ARollIsTheSameWhateverTheCultureAndNegativeNumbersCannotChangeIt()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            var invariant = CombatRoll.Permille(ulong.MaxValue, 0, "a", "main", 5);
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
            Assert.Equal(invariant, CombatRoll.Permille(ulong.MaxValue, 0, "a", "main", 5));
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Fact]
    public void ThePinnedEngagementStillResolvesToTheSameStateOnEveryPlatform()
    {
        // Pinned on 2026-10-27. If the model, the roll or the hash changes this changes, and saved games and journals stop replaying: change it on purpose only.
        var result = Resolver.Fire(Scenes.Request(Scenes.Squad("a", members: 20), Scenes.Squad("b", members: 20, armor: 20), distanceMetres: 150, seconds: 20, seed: 42));
        Assert.Equal("B8E87DBFC5E37C0D00A30A5780EFB3EB7986713784CC8E62700D07233EAC8F0B", result.Hash);
    }

    // ---- numeric limits -----------------------------------------------------------------------------------------------------

    [Fact]
    public void HugeCountsAreRefusedAsNumericRangeNotWrapped()
    {
        var weapon = Scenes.Rifle(perMinute: 600);
        var huge = Scenes.Squad("a", members: 1, weapon: weapon) with { Batteries = [new Battery("main", weapon, null, long.MaxValue / 4, 0, 0, 0)] };
        Assert.Equal(CombatCodes.NumericRange, Code(() => Resolver.Fire(Scenes.Request(huge with { InitialMembers = 1_000_000_000_000, Health = 100_000_000_000_000 }, Scenes.Squad("b")))));
        var hugeDamage = Scenes.Squad("a", weapon: Scenes.Rifle(damage: long.MaxValue / 2));
        Assert.Equal(CombatCodes.NumericRange, Code(() => Resolver.Fire(Scenes.Request(hugeDamage, Scenes.Squad("b"), seconds: 600))));
    }

    [Fact]
    public void MoreShotsThanTheLimitAreRefusedBeforeAnyAreFired()
    {
        var gun = Scenes.Rifle(perMinute: 6_000_000);
        var many = Scenes.Squad("a", members: 100, weapon: gun);
        Assert.Equal(CombatCodes.TooManyShots, Code(() => Resolver.Fire(Scenes.Request(many, Scenes.Squad("b"), seconds: 3600))));
    }

    [Fact]
    public void AnEngagementOutsideTheTimeLimitsIsRefused()
    {
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Fire(Scenes.Request(Scenes.Squad("a"), Scenes.Squad("b"), seconds: Rules.MaxEngagementSeconds + 1))));
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Fire(Scenes.Request(Scenes.Squad("a"), Scenes.Squad("b"), seconds: 0))));
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Fire(Scenes.Request(Scenes.Squad("a"), Scenes.Squad("b"), seconds: -5))));
    }

    [Fact]
    public void ZeroStatsAreLegalAndCannotDivideByZeroOrCrash()
    {
        var inert = Scenes.Squad("a", weapon: Scenes.Rifle(damage: 0, penetration: 0, rangeMetres: 0, accuracyPercent: 0, perMinute: 0));
        var result = Resolver.Fire(Scenes.Request(inert, Scenes.Squad("b"), distanceMetres: 0));
        Assert.Equal(Scenes.Squad("b").Health, result.Defender.Health);
        var zeroRange = Scenes.Squad("a", weapon: Scenes.Rifle(rangeMetres: 0));
        Assert.Equal(0, Resolver.Fire(Scenes.Request(zeroRange, Scenes.Squad("b"), distanceMetres: 0, seconds: 30)).Batteries[0].OutOfRange);
        Assert.Equal(CombatCodes.OutOfRange, Resolver.Fire(Scenes.Request(zeroRange, Scenes.Squad("b"), distanceMetres: 1, seconds: 30)).Shots[0].Code);
    }

    [Theory]
    [InlineData("health")]
    [InlineData("armor")]
    [InlineData("speed")]
    [InlineData("morale-low")]
    [InlineData("morale-high")]
    [InlineData("damage")]
    [InlineData("penetration")]
    [InlineData("range")]
    [InlineData("accuracy")]
    [InlineData("rate")]
    [InlineData("splash")]
    [InlineData("count")]
    [InlineData("rounds")]
    [InlineData("carry")]
    [InlineData("capacity")]
    [InlineData("over-capacity")]
    [InlineData("ammo-damage")]
    public void EveryNegativeOrImpossibleNumberInAGroupIsRefused(string which)
    {
        var good = Scenes.Squad("a", ammo: new AmmoSpec("ball", null, 5, 2, 0));
        var battery = good.Batteries[0];
        var weapon = battery.Weapon;
        var bad = which switch
        {
            "health" => good with { Health = -1 },
            "armor" => good with { ArmorRating = -1 },
            "speed" => good with { SpeedMilli = -1 },
            "morale-low" => good with { Morale = -1 },
            "morale-high" => good with { Morale = 1001 },
            "damage" => good with { Batteries = [battery with { Weapon = weapon with { Damage = -1 } }] },
            "penetration" => good with { Batteries = [battery with { Weapon = weapon with { Penetration = -1 } }] },
            "range" => good with { Batteries = [battery with { Weapon = weapon with { RangeMilli = -1 } }] },
            "accuracy" => good with { Batteries = [battery with { Weapon = weapon with { AccuracyPermille = -1 } }] },
            "rate" => good with { Batteries = [battery with { Weapon = weapon with { RatePerMinuteMilli = -1 } }] },
            "splash" => good with { Batteries = [battery with { Weapon = weapon with { SplashMilli = -1 } }] },
            "count" => good with { Batteries = [battery with { Count = -1 }] },
            "rounds" => good with { Batteries = [battery with { Rounds = -1 }] },
            "carry" => good with { Batteries = [battery with { CarryMilli = -1 }] },
            "capacity" => good with { Batteries = [battery with { Capacity = -1 }] },
            "over-capacity" => good with { Batteries = [battery with { Rounds = battery.Capacity + 1 }] },
            "ammo-damage" => good with { Batteries = [battery with { Ammo = new AmmoSpec("ball", null, -1, 2, 0) }] },
            _ => throw new InvalidOperationException(which),
        };
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Fire(Scenes.Request(bad, Scenes.Squad("b")))));
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Fire(Scenes.Request(Scenes.Squad("b"), bad with { Id = "c", Side = 3 }))));
    }

    [Fact]
    public void EveryRequestCodeIsRaisedByItsCause()
    {
        var a = Scenes.Squad("a");
        var b = Scenes.Squad("b");
        Assert.Equal(CombatCodes.UnknownDamageType, Code(() => Resolver.Fire(Scenes.Request(Scenes.Squad("a", weapon: Scenes.Rifle(type: "plasma-ish")), b))));
        Assert.Equal(CombatCodes.UnknownCover, Code(() => Resolver.Fire(Scenes.Request(a, b, cover: "fortress"))));
        Assert.Equal(CombatCodes.UnknownTraining, Code(() => Resolver.Fire(Scenes.Request(Scenes.Squad("a", training: "godlike"), b))));
        Assert.Equal(CombatCodes.UnknownTerrain, Code(() => Resolver.Fire(Scenes.Request(a, b) with { Terrain = "lava" })));
        Assert.Equal(CombatCodes.SameSide, Code(() => Resolver.Fire(Scenes.Request(a, b with { Side = a.Side }))));
        Assert.Equal(CombatCodes.UnknownBattery, Code(() => Logistics.Resupply(a, "nothing", 5)));
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Fire(Scenes.Request(a, b, attempt: -1))));
    }

    // ---- friend and foe -----------------------------------------------------------------------------------------------------

    [Fact]
    public void GroupsOfOneSideNeverFightEachOtherNotEvenInARound()
    {
        var a = Scenes.Squad("a", side: 1);
        var c = Scenes.Squad("c", side: 1);
        var order = new RoundOrder("a", "c", 100_000, "open", "open");
        Assert.Equal(CombatCodes.SameSide, Code(() => Resolver.Round([a, c], [order], 10, 1, 1, 0)));
    }

    // ---- standing and morale ------------------------------------------------------------------------------------------------

    [Fact]
    public void MoraleFallsWithCasualtiesAndSuppressionRecoversWhenUnharmedAndRoutsBelowTheLine()
    {
        var group = Scenes.Squad("b", members: 10);
        var unharmed = StandingRule.After(Rules, group with { Morale = 500 }, group.Health, 0);
        Assert.Equal(500 + Rules.Morale.Recovery, unharmed.Morale);
        var hurt = StandingRule.After(Rules, group, group.Health - 200, 3);   // a fifth of the health: 200 per-mille, times 100 percent
        Assert.Equal(850 - 200, hurt.Morale);
        Assert.False(hurt.Routed);
        var suppressed = StandingRule.After(Rules, group, group.Health - 100, Rules.SuppressionHits);
        Assert.Equal(850 - 100 - Rules.Morale.SuppressionLoss, suppressed.Morale);
        Assert.True(suppressed.Suppressed);
        var broken = StandingRule.After(Rules, group, group.Health - 700, 5);   // 850 - 700 = 150 < 250, though 30 percent of the strength remains
        Assert.True(broken.Routed);
        Assert.True(broken.Members * 100 >= group.InitialMembers * Rules.RoutStrengthPercent);
        Assert.True(StandingRule.After(Rules, broken, broken.Health, 0).Routed);   // once routed, always routed
        Assert.False(StandingRule.After(Rules, group, 0, 3).Routed);   // a destroyed group is destroyed, not routed
        Assert.Equal(0, StandingRule.After(Rules, group with { Morale = 10 }, group.Health - 900, 1).Morale);   // floored at zero
    }

    [Fact]
    public void TerrainChangesHowOftenShotsHitAndHowFastAGroupMoves()
    {
        var a = Scenes.Squad("a", members: 20);
        var b = Scenes.Squad("b", members: 2000, armor: 300);
        var open = Resolver.Fire(Scenes.Request(a, b, distanceMetres: 100, seconds: 30) with { Terrain = "open" }).Batteries[0].Hits;
        var forest = Resolver.Fire(Scenes.Request(a, b, distanceMetres: 100, seconds: 30) with { Terrain = "forest" }).Batteries[0].Hits;
        Assert.True(forest < open, $"forest {forest} should hit less than open {open}");

        Assert.Equal(100_000 - 1400 * 20, MovementRule.Advance(Rules, 100_000, 1400, 20, "open"));
        Assert.Equal(100_000 - 1400 * 20 * 130 / 100, MovementRule.Advance(Rules, 100_000, 1400, 20, "road"));
        Assert.Equal(100_000 - 1400 * 20 * 50 / 100, MovementRule.Advance(Rules, 100_000, 1400, 20, "forest"));
        Assert.Equal(0, MovementRule.Advance(Rules, 1_000, 1400, 20, "open"));   // never past the other group
        Assert.Equal(CombatCodes.UnknownTerrain, Code(() => MovementRule.Advance(Rules, 1, 1, 1, "lava")));
        Assert.Equal(CombatCodes.BadRequest, Code(() => MovementRule.Advance(Rules, -1, 1, 1, "open")));
        Assert.Equal(CombatCodes.BadRequest, Code(() => MovementRule.Advance(Rules, 1, 1, 0, "open")));
        Assert.Equal(CombatCodes.NumericRange, Code(() => MovementRule.Advance(Rules, 1, long.MaxValue / 2, 3600, "open")));
    }

    // ---- logistics ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void AResupplyFillsToCapacityAndNoFurther()
    {
        var ammo = new AmmoSpec("ball", null, 5, 2, 0);
        var group = Scenes.Squad("a", rounds: 100, ammo: ammo);
        var used = group with { Batteries = [group.Batteries[0] with { Rounds = 30 }] };
        var result = Logistics.Resupply(used, "main", 50);
        Assert.Equal(50, result.Accepted);
        Assert.Equal(80, result.Group.Batteries[0].Rounds);
        var topped = Logistics.Resupply(result.Group, "main", 500);
        Assert.Equal(20, topped.Accepted);
        Assert.Equal(100, topped.Group.Batteries[0].Rounds);
        Assert.Equal(0, Logistics.Resupply(topped.Group, "main", 10).Accepted);
        Assert.Equal(30, used.Batteries[0].Rounds);   // the group given is untouched
        Assert.Equal(CombatCodes.BadRequest, Code(() => Logistics.Resupply(used, "main", -1)));
        Assert.Equal(CombatCodes.BadRequest, Code(() => Logistics.Resupply(Scenes.Squad("a"), "main", 5)));   // a melee battery uses no ammunition
    }

    [Fact]
    public void AReinforcementRestoresMembersUpToTheStartingStrengthAtFullHealthAndChangesNothingElse()
    {
        var group = Scenes.Squad("a", members: 10, health: 430);   // four full members and a third of one are left: five members
        Assert.Equal(5, group.Members);
        var result = Logistics.Reinforce(group, 3);
        Assert.Equal(3, result.Accepted);
        Assert.Equal(8, result.Group.Members);
        Assert.Equal(730, result.Group.Health);
        Assert.Equal(group.Morale, result.Group.Morale);
        Assert.Equal(group.Suppressed, result.Group.Suppressed);
        Assert.Equal(group.Batteries, result.Group.Batteries);   // people, not rounds
        var topped = Logistics.Reinforce(result.Group, 50);
        Assert.Equal(2, topped.Accepted);   // only back to the ten it started with
        Assert.Equal(10, topped.Group.Members);
        Assert.Equal(0, Logistics.Reinforce(topped.Group, 5).Accepted);
        Assert.Equal(0, Logistics.Reinforce(group, 0).Accepted);
        Assert.Equal(430, group.Health);   // the group given is untouched
        Assert.Equal(CombatCodes.BadRequest, Code(() => Logistics.Reinforce(group, -1)));
    }

    [Fact]
    public void ADestroyedOrRoutedGroupTakesInNoOne()
    {
        var group = Scenes.Squad("a", members: 10);
        Assert.Equal(CombatCodes.GroupWithdrawn, Code(() => Logistics.Reinforce(group with { Health = 0 }, 1)));
        Assert.Equal(CombatCodes.GroupWithdrawn, Code(() => Logistics.Reinforce(group with { Routed = true, Health = 300 }, 1)));
    }

    // ---- n-way rounds -------------------------------------------------------------------------------------------------------

    private static Group Side(string id, int side, int members = 100) => Scenes.Squad(id, side: side, members: members);

    [Fact]
    public void ARoundLetsManyGroupsOnManySidesFireAtOnceAndAddsTheDamageEachTakes()
    {
        var a = Side("a", 1);
        var b = Side("b", 2);
        var c = Side("c", 3, 1000);
        var orders = new[] { new RoundOrder("a", "c", 100_000, "open", "open"), new RoundOrder("b", "c", 100_000, "open", "open"), new RoundOrder("c", "a", 100_000, "open", "open") };
        var round = Resolver.Round([a, b, c], orders, 2, 9, 1, 0);
        var fromA = round.Fires[0].Defender.Health;
        var fromB = round.Fires[1].Defender.Health;
        Assert.Equal(Math.Max(0, c.Health - (c.Health - fromA) - (c.Health - fromB)), round.Groups[2].Health);
        Assert.True(round.Groups[2].Health < Math.Min(fromA, fromB));   // two attackers hurt c more than either alone
        Assert.Equal(b.Health, round.Groups[1].Health);                   // nobody fired at b
        Assert.Equal(a.Health - (a.Health - round.Fires[2].Defender.Health), round.Groups[0].Health);
        Assert.Equal(StateHash.Of(round.Groups), round.Hash);
        Assert.Equal(round.Hash, Resolver.Round([a, b, c], orders, 2, 9, 1, 0).Hash);
    }

    [Fact]
    public void ARoundRefusesTwoOrdersFromOneGroupUnknownGroupsAndRepeatedIds()
    {
        var a = Side("a", 1);
        var b = Side("b", 2);
        var order = new RoundOrder("a", "b", 100_000, "open", "open");
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Round([a, b], [order, order], 5, 1, 1, 0)));
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Round([a, b], [order with { Defender = "z" }], 5, 1, 1, 0)));
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Round([a, a], [order], 5, 1, 1, 0)));
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Round([a, b], [], 5, 1, 1, 0)));
        Assert.Equal(CombatCodes.BadRequest, Code(() => Resolver.Round([], [order], 5, 1, 1, 0)));
    }

    [Fact]
    public void AnExchangeIsARoundOfTwoOrders()
    {
        var a = Side("a", 1);
        var b = Side("b", 2);
        var exchange = Resolver.Exchange(a, b, 80_000, 15, "light", "rough", 3, 4, 0);
        var round = Resolver.Round([a, b], [new RoundOrder("a", "b", 80_000, "light", "rough"), new RoundOrder("b", "a", 80_000, "light", "rough")], 15, 3, 4, 0);
        Assert.Equal(round.Hash, exchange.Hash);
        Assert.Equal((round.Groups[0].Health, round.Groups[0].Morale, round.Groups[0].Standing), (exchange.First.Health, exchange.First.Morale, exchange.First.Standing));
        Assert.Equal((round.Groups[1].Health, round.Groups[1].Morale, round.Groups[1].Standing), (exchange.Second.Health, exchange.Second.Morale, exchange.Second.Standing));
    }

    [Fact]
    public void AShotAtADestroyedTargetIsLoggedAsWastedWithItsCode()
    {
        var gun = Scenes.Rifle(damage: 5000, perMinute: 600);
        var result = Resolver.Fire(Scenes.Request(Scenes.Squad("a", members: 20, weapon: gun), Scenes.Squad("b", members: 1), seconds: 10, logged: 100));
        Assert.Contains(result.Shots, s => s.Code == CombatCodes.TargetDestroyed);
        Assert.Contains(CombatCodes.Destroyed, result.Codes);
        Assert.True(result.Batteries[0].Wasted > 0);
    }

    // ---- the guard: every code is raised and tested -------------------------------------------------------------------------

    [Fact]
    public void EveryCodeTheEngineDeclaresIsRaisedInTheEngineAndNamedInATest()
    {
        var core = SourceGuardTests.CoreSources();
        var tests = Directory.GetFiles(Path.Combine(SourceGuardTests.ProjectRoot(), "CombatEngine.Tests"), "*.cs")
            .Where(f => !f.Contains("obj", StringComparison.Ordinal)).Select(File.ReadAllText).ToList();
        var codes = typeof(CombatCodes).GetFields().Where(f => f.IsLiteral).Select(f => f.Name).ToList();
        Assert.True(codes.Count >= 23, "found " + codes.Count + " codes");
        foreach (var name in codes)
        {
            var raisers = core.Where(s => s.Key != "CombatCodes.cs" && s.Value.Contains("CombatCodes." + name, StringComparison.Ordinal)).ToList();
            Assert.True(raisers.Count > 0, name + " is declared but nothing in the engine raises it");
            Assert.True(tests.Any(t => t.Contains("CombatCodes." + name, StringComparison.Ordinal)), name + " is raised but no test names it");
        }
    }
}
