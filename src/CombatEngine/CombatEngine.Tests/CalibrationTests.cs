using CombatEngine;
using Xunit;

namespace CombatEngine.Tests;

/// <summary>
/// Sanity calibration, not historical calibration. These tests check that the model has the shape combat is known to have (bigger forces
/// win more than proportionally, armor beats weak hits, range costs accuracy, training and cover matter) within wide tolerances. They do
/// not claim that any number matches a real weapon or battle; the numbers are plausible, set by the rules file and the catalog.
/// </summary>
public sealed class CalibrationTests
{
    private static readonly CombatRules Rules = Scenes.Rules();
    private static readonly CombatResolver Resolver = new(Rules);
    private static readonly WeaponSpec Gun = Scenes.Rifle(damage: 40, penetration: 5, rangeMetres: 300, accuracyPercent: 60, perMinute: 20);

    /// <summary>Exchanges of five seconds until one side is routed or destroyed. Returns the survivors' share of strength for each side.</summary>
    private static (double A, double B, bool AWon) Fight(Group a, Group b, ulong seed, int roundSeconds = 5)
    {
        long attempt = 1;
        for (var round = 0; round < 2000 && !a.Routed && !b.Routed && !a.Destroyed && !b.Destroyed; round++)
        {
            var exchange = Resolver.Exchange(a, b, 100_000, roundSeconds, "open", "open", seed, attempt, 0);
            attempt += 2;
            (a, b) = (exchange.First, exchange.Second);
        }
        var shareA = (double)a.Health / (a.InitialMembers * a.MemberHealth);
        var shareB = (double)b.Health / (b.InitialMembers * b.MemberHealth);
        return (shareA, shareB, a.Health > b.Health);
    }

    private static Group Force(string id, int members, WeaponSpec? weapon = null, long armor = 0, string training = "regular") =>
        Scenes.Squad(id, members: members, weapon: weapon ?? Gun, armor: armor, training: training);

    [Fact]
    public void EqualForcesWinAboutHalfTheTimeEach()
    {
        var wins = 0;
        for (ulong seed = 1; seed <= 200; seed++) if (Fight(Force("a", 30), Force("b", 30), seed).AWon) wins++;
        Assert.InRange(wins, 70, 130);   // a fair coin over 200 fights, with room for the ties the simultaneous exchange can produce
    }

    [Fact]
    public void TheSquareLawTrendBiggerForcesWinAndLoseLittle()
    {
        // Lanchester's square law: with aimed fire the stronger side's loss is far less than its advantage. A 2:1 force should win nearly
        // always and keep most of its strength; 1.5:1 should do worse than 2:1 and 1:1 worst of all. Tolerances are wide: this is a shape, not a fit.
        double Mean(int big, int small) => Enumerable.Range(1, 60).Average(seed => Fight(Force("a", big), Force("b", small), (ulong)seed).A);
        double Wins(int big, int small) => Enumerable.Range(1, 60).Count(seed => Fight(Force("a", big), Force("b", small), (ulong)seed).AWon) / 60.0;

        var two = Mean(60, 30);
        var oneHalf = Mean(45, 30);
        var even = Mean(30, 30);
        Assert.True(Wins(60, 30) > 0.95, "a 2:1 force should almost always win");
        Assert.True(two > oneHalf && oneHalf > even - 0.05, $"survivors should rise with the ratio: 2:1 {two:F2}, 1.5:1 {oneHalf:F2}, 1:1 {even:F2}");
        Assert.True(two > 0.55, $"a 2:1 force should keep more than half its strength, kept {two:F2}");
    }

    [Fact]
    public void ArmorBeatsWeakHitsAndPenetrationBeatsArmor()
    {
        var weak = Scenes.Rifle(damage: 20, penetration: 0, rangeMetres: 300, accuracyPercent: 80, perMinute: 60);
        var strong = Scenes.Rifle(damage: 20, penetration: 400, rangeMetres: 300, accuracyPercent: 80, perMinute: 60);
        long Damage(WeaponSpec weapon, long armor) => Enumerable.Range(1, 30).Sum(seed =>
        {
            var target = Scenes.Squad("b", members: 400, armor: armor);
            return target.Health - Resolver.Fire(Scenes.Request(Force("a", 30, weapon), target, distanceMetres: 50, seconds: 10, seed: (ulong)seed)).Defender.Health;
        });
        long previous = long.MaxValue;
        foreach (var armor in new long[] { 0, 50, 100, 200, 400 })
        {
            var damage = Damage(weak, armor);
            Assert.True(damage <= previous, $"armor {armor} should not let more damage through ({damage} after {previous})");
            previous = damage;
        }
        Assert.True(Damage(weak, 400) * 5 < Damage(weak, 0), "heavy armor should stop most of a weak hit");
        Assert.True(Damage(strong, 400) > Damage(weak, 400) * 2, "penetration should beat armor");
    }

    [Fact]
    public void RangeCostsAccuracyAndBeyondRangeNothingLands()
    {
        long Hits(long metres) => Enumerable.Range(1, 30).Sum(seed => Resolver.Fire(Scenes.Request(Force("a", 40), Scenes.Squad("b", members: 400, armor: 300), distanceMetres: metres, seconds: 20, seed: (ulong)seed)).Batteries[0].Hits);
        var near = Hits(60);
        var middle = Hits(180);
        var far = Hits(290);
        Assert.True(near > middle && middle > far, $"hits should fall with distance: {near}, {middle}, {far}");
        Assert.Equal(0, Hits(400));
    }

    [Fact]
    public void TrainingAndCoverEachChangeTheOutcomeInTheRightDirection()
    {
        double Remaining(string training, string defenderCover)
        {
            var total = 0.0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var defender = Scenes.Squad("b", members: 300, armor: 100);
                var result = Resolver.Fire(Scenes.Request(Force("a", 40, training: training), defender, distanceMetres: 100, seconds: 20, cover: defenderCover, seed: seed));
                total += (double)result.Defender.Health / defender.Health;
            }
            return total / 30;
        }
        Assert.True(Remaining("elite", "open") < Remaining("regular", "open") && Remaining("regular", "open") < Remaining("raw", "open"));
        Assert.True(Remaining("regular", "fortified") > Remaining("regular", "heavy") && Remaining("regular", "heavy") > Remaining("regular", "light") && Remaining("regular", "light") > Remaining("regular", "open"));
    }

    [Fact]
    public void MoraleBreaksAForceBeforeItIsDestroyedAndRoutedForcesStopFighting()
    {
        var broken = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var result = Fight(Force("a", 80), Force("b", 30), seed, 1);
            if (result.B > 0.2) broken++;
        }
        Assert.True(broken > 30, "a beaten force should usually break while a fifth or more of it still stands, not fight to the last: " + broken + " of 40");
    }
}
