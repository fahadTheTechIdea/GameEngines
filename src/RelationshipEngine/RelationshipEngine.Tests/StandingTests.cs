using EngineCommon.Graph;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>Standing (EigenTrust-style power iteration over trust): worked answers, the library's own result on the same edges, and the property that matters in a village: who trusts you counts, not only how many.</summary>
public sealed class StandingTests
{
    private static string[] Names(int n) => Enumerable.Range(0, n).Select(i => "p" + i.ToString("00")).ToArray();

    [Fact]
    public void WithNoTrustAtAllTheWholeStandingGoesToTheActorsOfThePreTrustTraitEqually()
    {
        // Every row is empty, so each actor trusts "as the pre-trust says": the fixed point is the pre-trust vector itself.
        var set = SocialFixture.Set();
        var runtime = RelationshipRuntime.Create(set);
        foreach (var id in Names(5)) runtime.AddActor(id, id is "p01" or "p03" ? ["kind"] : []);
        SocialFixture.NextDay(runtime);
        Assert.Equal([0L, 2500, 0, 2500, 0], Names(5).Select(id => runtime.Actor(id).Standing));
    }

    [Fact]
    public void WithoutThePreTrustTraitEveryoneStartsEqualAndNobodyTrustedMeansEveryoneIsAverage()
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(r => SocialFixture.Block(r, "standing")["preTrustTrait"] = ""), Names(4));
        SocialFixture.NextDay(runtime);
        Assert.All(runtime.Actors.Values, a => Assert.Equal(1000, a.Standing));
    }

    [Fact]
    public void ATrustedHubOutranksTheRestAndNobodyFallsBelowTheShareThePreTrustGivesEveryone()
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(r => SocialFixture.Block(r, "standing")["preTrustTrait"] = ""), Names(6));
        foreach (var id in Names(6).Skip(1)) SocialFixture.Trust(runtime, id, "p00", 800);
        SocialFixture.NextDay(runtime);
        var standing = Names(6).Select(id => runtime.Actor(id).Standing).ToArray();
        Assert.Equal(standing.Max(), standing[0]);
        Assert.True(standing[0] > 1000);
        Assert.All(standing.Skip(1), s => Assert.Equal(standing[1], s));
        // alpha is 150 permille and the pre-trust uniform: every actor keeps at least 150 permille of an average share (floors can cost one unit).
        Assert.All(standing, s => Assert.True(s >= 149, "standing " + s));
        // The scores floor, so the standings add to the village size times 1000 less at most one unit each.
        Assert.InRange(standing.Sum(), 6 * 1000 - 6, 6 * 1000);
    }

    [Fact]
    public void TheStandingIsTheLibrarysResultOnTheEdgesTheOpinionsMake()
    {
        var names = Names(12);
        var runtime = SocialFixture.Village(SocialFixture.Set(r => SocialFixture.Block(r, "standing")["preTrustTrait"] = ""), names);
        var seed = 7UL;
        long Next() { seed = seed * 6364136223846793005UL + 1442695040888963407UL; return (long)(seed >> 33); }
        for (var i = 0; i < names.Length; i++)
            for (var k = 0; k < 4; k++)
            {
                var j = (int)(Next() % names.Length);
                if (j != i) SocialFixture.Trust(runtime, names[i], names[j], 50 + Next() % 900);
            }
        SocialFixture.NextDay(runtime);
        Assert.Equal(string.Empty, GraphBuilder.Create(names.Length, out var builder));
        var trust = runtime.Set.AxisById["trust"].Index;
        for (var i = 0; i < names.Length; i++)
            for (var j = 0; j < names.Length; j++)
            {
                if (i == j || runtime.Edge(names[i], names[j]) is null) continue;
                var weight = runtime.Opinion(names[i], names[j], trust);
                if (weight > 0) Assert.Equal(string.Empty, builder!.Add(i, j, weight));
            }
        var direct = TrustIteration.Run(builder!.Build().Graph!, [], 150 * (TrustIteration.Scale / 1000), 12, TrustIteration.Scale / 1_000_000, new GraphScratch());
        Assert.Equal(string.Empty, direct.Code);
        for (var i = 0; i < names.Length; i++)
            Assert.Equal((long)((System.Int128)direct.Scores[i] * names.Length * 1000 / TrustIteration.Scale), runtime.Actor(names[i]).Standing);
    }

    [Fact]
    public void StandingFollowsWhoTrustsYouNotOnlyHowManyDo()
    {
        // The elder is trusted by five villagers. The elder trusts Xena and an outcast trusts Yuri, both with trust 600: the same direct edge. Xena stands higher than Yuri.
        var names = new[] { "a1", "a2", "a3", "a4", "a5", "elder", "outcast", "xena", "yuri" };
        var runtime = SocialFixture.Village(SocialFixture.Set(r => SocialFixture.Block(r, "standing")["preTrustTrait"] = ""), names);
        foreach (var villager in new[] { "a1", "a2", "a3", "a4", "a5" }) SocialFixture.Trust(runtime, villager, "elder", 700);
        SocialFixture.Trust(runtime, "elder", "xena", 600);
        SocialFixture.Trust(runtime, "outcast", "yuri", 600);
        SocialFixture.NextDay(runtime);
        Assert.True(runtime.Actor("xena").Standing > runtime.Actor("yuri").Standing, runtime.Actor("xena").Standing + " vs " + runtime.Actor("yuri").Standing);
        // The pairwise reading cannot tell them apart: both are trusted at 600 by exactly one actor.
        Assert.Equal(runtime.Opinion("elder", "xena", runtime.Set.AxisById["trust"].Index), runtime.Opinion("outcast", "yuri", runtime.Set.AxisById["trust"].Index));
    }

    [Fact]
    public void ABigChangeIsAnnouncedAndASmallOneIsNot()
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(r => { SocialFixture.Block(r, "standing")["preTrustTrait"] = ""; SocialFixture.Block(r, "standing")["eventDelta"] = 400; }), Names(4));
        foreach (var id in Names(4).Skip(1)) SocialFixture.Trust(runtime, id, "p00", 800);
        var first = SocialFixture.NextDay(runtime);
        Assert.Contains(first.Standing, c => c.Actor == "p00" && c.From == 1000 && c.To > 1400);
        var again = SocialFixture.NextDay(runtime);
        Assert.Empty(again.Standing);                                       // nothing moved
        var small = SocialFixture.Village(SocialFixture.Set(r => { SocialFixture.Block(r, "standing")["preTrustTrait"] = ""; SocialFixture.Block(r, "standing")["eventDelta"] = 1_000_000; }), Names(4));
        foreach (var id in Names(4).Skip(1)) SocialFixture.Trust(small, id, "p00", 800);
        Assert.Empty(SocialFixture.NextDay(small).Standing);
        Assert.True(small.Actor("p00").Standing > 1400);                    // the value is stored even when it is not announced
    }

    [Fact]
    public void ANewActorIsAverageUntilTheNextBoundaryAndTheFactSaysSo()
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(), Names(3));
        runtime.AddActor("p99", []);
        Assert.Equal(1000, RelationshipFacts.Read(runtime, "p99")["actor.standing"].Integer);
        var declared = runtime.Set.Facts["actor.standing"];
        Assert.Equal((0L, RelationshipRuntime.StandingMax), (declared.Minimum, declared.Maximum));
    }

    [Fact]
    public void WithStandingOffNobodyMovesAndNothingIsAnnounced()
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(r => SocialFixture.Block(r, "standing")["enabled"] = false), Names(4));
        foreach (var id in Names(4).Skip(1)) SocialFixture.Trust(runtime, id, "p00", 800);
        Assert.Empty(SocialFixture.NextDay(runtime).Standing);
        Assert.All(runtime.Actors.Values, a => Assert.Equal(1000, a.Standing));
    }
}
