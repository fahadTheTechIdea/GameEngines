using CombatEngine;
using Xunit;

namespace CombatEngine.Tests;

/// <summary>
/// Random valid rosters through every entry point: nothing crashes, ammunition and headcount are conserved, casualties never fall back,
/// and two mirrored sides are treated alike. Rosters come from a fixed-seed generator, so a failure reproduces.
/// </summary>
public sealed class PropertyTests
{
    private static readonly CombatRules Rules = Scenes.Rules();
    private static readonly CombatResolver Resolver = new(Rules);
    private static readonly string[] Types = ["slash", "pierce", "blunt", "ballistic", "blast", "energy", "fire"];
    private static readonly string[] Trainings = ["raw", "regular", "veteran", "elite"];

    private static Group RandomGroup(Random random, string id, int side)
    {
        var batteries = new List<Battery>();
        for (var i = 0; i < random.Next(0, 4); i++)
        {
            var usesAmmo = random.Next(2) == 0;
            var capacity = usesAmmo ? random.Next(0, 500) : 0;
            var weapon = new WeaponSpec("w" + i, Types[random.Next(Types.Length)], random.Next(0, 300), random.Next(0, 200), random.Next(0, 800) * 1000L, random.Next(0, 1001),
                random.Next(0, 120) * 1000L, random.Next(0, 3) == 0 ? random.Next(0, 10) * 1000L : 0);
            var ammo = usesAmmo ? new AmmoSpec("m" + i, random.Next(3) == 0 ? Types[random.Next(Types.Length)] : null, random.Next(0, 50), random.Next(0, 50), random.Next(0, 3) == 0 ? 4000 : 0) : null;
            batteries.Add(new Battery("b" + i, weapon, ammo, random.Next(0, 80), usesAmmo ? random.Next(0, capacity + 1) : 0, random.Next(0, 1000), capacity));
        }
        var members = random.Next(1, 120);
        return new Group(id, side, Trainings[random.Next(Trainings.Length)], members, 100, members * 100L, random.Next(0, 400), false, false, batteries, random.Next(300, 1001), random.Next(0, 20_000));
    }

    private static string RandomCover(Random random) => Rules.Cover[random.Next(Rules.Cover.Count)].Id;
    private static string RandomTerrain(Random random) => Rules.Terrain[random.Next(Rules.Terrain.Count)].Id;

    [Fact]
    public void RandomValidEngagementsNeverCrashAndConserveAmmunitionAndHeadcount()
    {
        var random = new Random(20261027);
        for (var run = 0; run < 400; run++)
        {
            var a = RandomGroup(random, "a", 1);
            var b = RandomGroup(random, "b", 2);
            var result = Resolver.Fire(new EngagementRequest(a, b, random.Next(0, 900) * 1000L, random.Next(1, 120), RandomCover(random), RandomTerrain(random), (ulong)random.Next(), random.Next(0, 1000), 3));

            Assert.InRange(result.Defender.Health, 0, b.Health);
            Assert.True(result.Defender.Members <= b.Members, "headcount fell back up");
            Assert.InRange(result.Defender.Morale, 0, 1000);
            Assert.Equal(a.Health, result.Attacker.Health);                                   // the attacker takes no damage from its own fire
            for (var i = 0; i < a.Batteries.Count; i++)
            {
                var before = a.Batteries[i];
                var after = result.Attacker.Batteries[i];
                var report = result.Batteries[i];
                Assert.Equal(report.Earned, report.Fired + report.OutOfRange + report.NoAmmo + report.Wasted);
                Assert.Equal(report.Fired, report.Hits + report.Misses);
                if (before.Ammo is not null) Assert.Equal(before.Rounds - report.Fired, after.Rounds);   // every round fired is a round spent, none appear from nowhere
                else Assert.Equal(before.Rounds, after.Rounds);
                Assert.InRange(after.Rounds, 0, before.Capacity);
                Assert.InRange(after.CarryMilli, 0, 999);
            }
            Assert.Equal(b.Health - result.Defender.Health, result.Batteries.Sum(r => r.Damage));
        }
    }

    [Fact]
    public void RandomRoundsOfManyGroupsKeepEveryInvariant()
    {
        var random = new Random(77);
        for (var run = 0; run < 200; run++)
        {
            var count = random.Next(2, 6);
            var groups = Enumerable.Range(0, count).Select(i => RandomGroup(random, "g" + i, 1 + i % 3)).ToList();
            var orders = new List<RoundOrder>();
            foreach (var attacker in groups.Where(_ => random.Next(4) != 0))
            {
                var foes = groups.Where(g => g.Side != attacker.Side).ToList();
                if (foes.Count == 0) continue;
                orders.Add(new RoundOrder(attacker.Id, foes[random.Next(foes.Count)].Id, random.Next(0, 900) * 1000L, RandomCover(random), RandomTerrain(random)));
            }
            if (orders.Count == 0) continue;
            var seconds = random.Next(1, 60);
            var seed = (ulong)random.Next();
            var attempt = (long)random.Next(0, 1000);
            var round = Resolver.Round(groups, orders, seconds, seed, attempt, 0);
            Assert.Equal(groups.Count, round.Groups.Count);
            for (var i = 0; i < groups.Count; i++)
            {
                Assert.InRange(round.Groups[i].Health, 0, groups[i].Health);
                Assert.True(round.Groups[i].Members <= groups[i].Members);
                Assert.InRange(round.Groups[i].Morale, 0, 1000);
                Assert.Equal(groups[i].Id, round.Groups[i].Id);
                Assert.True(!groups[i].Routed || round.Groups[i].Routed, "a routed group rallied");
            }
            Assert.Equal(round.Hash, Resolver.Round(groups, orders, seconds, seed, attempt, 0).Hash);
        }
    }

    [Fact]
    public void CasualtiesNeverFallBackOverARepeatedFight()
    {
        var random = new Random(5);
        for (var run = 0; run < 60; run++)
        {
            var a = RandomGroup(random, "a", 1);
            var b = RandomGroup(random, "b", 2);
            var healthA = a.Health;
            var healthB = b.Health;
            for (var round = 0; round < 25; round++)
            {
                var exchange = Resolver.Exchange(a, b, random.Next(0, 300) * 1000L, 10, RandomCover(random), RandomTerrain(random), (ulong)run, round * 2L + 1, 0);
                Assert.True(exchange.First.Health <= healthA && exchange.Second.Health <= healthB, "health rose during a fight");
                (a, b, healthA, healthB) = (exchange.First, exchange.Second, exchange.First.Health, exchange.Second.Health);
            }
        }
    }

    [Fact]
    public void MirroredSidesAreTreatedAlikeOverManySeeds()
    {
        var gun = Scenes.Rifle(damage: 60, rangeMetres: 300, accuracyPercent: 70, perMinute: 30);
        var a = Scenes.Squad("a", members: 40, weapon: gun);
        var b = Scenes.Squad("b", members: 40, weapon: gun);
        long lostA = 0, lostB = 0;
        for (ulong seed = 1; seed <= 600; seed++)
        {
            var exchange = Resolver.Exchange(a, b, 100_000, 4, "open", "open", seed, 1, 0);
            lostA += a.Health - exchange.First.Health;
            lostB += b.Health - exchange.Second.Health;
        }
        var difference = Math.Abs(lostA - lostB) * 1000 / Math.Max(lostA, lostB);
        Assert.True(difference < 40, $"mirrored sides lost {lostA} and {lostB} (differ by {difference} per-mille)");   // within 4 percent over 600 seeds
        Assert.True(lostA > 0 && lostB > 0);
    }

    [Fact]
    public void ARepeatedFightFromTheSameStartIsReproducibleByEveryEntryPoint()
    {
        var random = new Random(3);
        for (var run = 0; run < 50; run++)
        {
            var a = RandomGroup(random, "a", 1);
            var b = RandomGroup(random, "b", 2);
            var request = new EngagementRequest(a, b, random.Next(0, 500) * 1000L, random.Next(1, 60), "light", "rough", 9, 4, 2);
            Assert.Equal(Resolver.Fire(request).Hash, Resolver.Fire(request).Hash);
            Assert.Equal(Resolver.Exchange(a, b, 50_000, 12, "heavy", "urban", 9, 4, 0).Hash, Resolver.Exchange(a, b, 50_000, 12, "heavy", "urban", 9, 4, 0).Hash);
        }
    }
}
