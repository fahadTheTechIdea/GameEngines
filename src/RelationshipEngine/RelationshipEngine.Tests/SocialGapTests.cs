using System.Text.Json.Nodes;
using EngineCommon;
using EngineCommon.Graph;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>Boundaries of the social block and of the three passes that the first mutation round showed untested: the lines of the axis, the exact event delta, the weight line of standing, the ordering of the strongest opinions, the memory at index 0, the explanation's empty cases.</summary>
public sealed class SocialGapTests
{
    private static string Code(Action<JsonObject> edit) => Fixture.Refusal(() => RelationshipLoader.Load(Edit.Doc(edit)));

    [Fact]
    public void TheLinesOfTheAxisAreInclusiveWhereTheyMayBeAndRefusedWhereTheyMayNot()
    {
        RelationshipLoader.Load(Edit.Doc(r => SocialFixture.Block(r, "standing")["above"] = -1000));
        RelationshipLoader.Load(Edit.Doc(r => SocialFixture.Block(r, "communities")["above"] = -1000));
        RelationshipLoader.Load(Edit.Doc(r => SocialFixture.Block(r, "balance")["dislike"] = -1000));
        RelationshipLoader.Load(Edit.Doc(r => SocialFixture.Block(r, "balance")["like"] = 1000));
        Assert.Equal(RelationshipCodes.Range, Code(r => SocialFixture.Block(r, "standing")["above"] = 1000));
        Assert.Equal(RelationshipCodes.Range, Code(r => SocialFixture.Block(r, "communities")["above"] = 1000));
        Assert.Equal(RelationshipCodes.Range, Code(r => SocialFixture.Block(r, "balance")["dislike"] = 1000));
        Assert.Equal(RelationshipCodes.Range, Code(r => SocialFixture.Block(r, "balance")["like"] = -1000));
    }

    [Fact]
    public void ThePressureMemoryIsCheckedWhateverItsPlaceInTheDocument()
    {
        // "was-gifted" is memory 0 and spreads by gossip.
        Assert.Equal(RelationshipCodes.Social, Code(r => SocialFixture.Block(r, "balance")["memory"] = "was-gifted"));
        // Made not to spread, it is accepted at index 0 and the explanation reads its pressure.
        var set = SocialFixture.Set(r =>
        {
            Edit.Memory(r, "was-gifted")["spread"] = null;
            SocialFixture.Block(r, "balance")["memory"] = "was-gifted";
            SocialFixture.Block(r, "communities")["enabled"] = false;
            SocialFixture.Block(r, "standing")["enabled"] = false;
        });
        Assert.Equal(0, set.Social.Balance.Memory);
        var runtime = SocialFixture.Village(set, "ann", "bea", "cyd");
        SocialFixture.Think(runtime, "ann", "bea", 500);
        SocialFixture.Think(runtime, "bea", "cyd", 500);
        SocialFixture.Think(runtime, "ann", "cyd", 50);
        SocialFixture.NextDay(runtime);
        Assert.Contains("pressure now: ", RelationshipExplain.Balance(runtime, "ann", "cyd"));
    }

    [Fact]
    public void TheGraphEdgeCeilingIsInclusive()
    {
        RelationshipLoader.Load(Edit.Doc(r => { r["limits"]!["maxActors"] = 20000; r["limits"]!["maxEdgesPerActor"] = 100; }));            // exactly 2,000,000
        Assert.Equal(RelationshipCodes.Social, Code(r => { r["limits"]!["maxActors"] = 20001; r["limits"]!["maxEdgesPerActor"] = 100; }));
    }

    private static long StandingMove(long delta)
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(r => { SocialFixture.Block(r, "standing")["preTrustTrait"] = ""; SocialFixture.Block(r, "standing")["eventDelta"] = delta; }), "p0", "p1", "p2", "p3");
        foreach (var id in new[] { "p1", "p2", "p3" }) SocialFixture.Trust(runtime, id, "p0", 800);
        var changes = SocialFixture.NextDay(runtime);
        return changes.Standing.Count(c => c.Actor == "p0") == 1 ? runtime.Actor("p0").Standing - 1000 : -1;
    }

    [Fact]
    public void AStandingChangeOfExactlyTheEventDeltaIsAnnounced()
    {
        var moved = StandingMove(1_000_000_000) == -1 ? 0 : -2;                 // a huge delta announces nothing
        Assert.Equal(0, moved);
        var runtime = SocialFixture.Village(SocialFixture.Set(r => SocialFixture.Block(r, "standing")["preTrustTrait"] = ""), "p0", "p1", "p2", "p3");
        foreach (var id in new[] { "p1", "p2", "p3" }) SocialFixture.Trust(runtime, id, "p0", 800);
        SocialFixture.NextDay(runtime);
        var size = runtime.Actor("p0").Standing - 1000;
        Assert.True(size > 0);
        Assert.Equal(size, StandingMove(size));          // announced at exactly its own size
        Assert.Equal(-1, StandingMove(size + 1));        // not one unit beyond
    }

    [Fact]
    public void TheStandingGraphWeighsOpinionsByTheirExcessOverTheLine()
    {
        const long above = 250;
        var names = Enumerable.Range(0, 8).Select(i => "q" + i).ToArray();
        var runtime = SocialFixture.Village(SocialFixture.Set(r => { SocialFixture.Block(r, "standing")["preTrustTrait"] = ""; SocialFixture.Block(r, "standing")["above"] = above; }), names);
        var seed = 5UL;
        long Next() { seed = seed * 6364136223846793005UL + 1442695040888963407UL; return (long)(seed >> 33); }
        for (var i = 0; i < names.Length; i++)
            for (var k = 0; k < 4; k++)
            {
                var j = (int)(Next() % names.Length);
                if (j != i) SocialFixture.Trust(runtime, names[i], names[j], 100 + Next() % 800);
            }
        SocialFixture.NextDay(runtime);
        Assert.Equal(string.Empty, GraphBuilder.Create(names.Length, out var builder));
        var trust = runtime.Set.AxisById["trust"].Index;
        for (var i = 0; i < names.Length; i++)
            for (var j = 0; j < names.Length; j++)
            {
                if (i == j || runtime.Edge(names[i], names[j]) is null) continue;
                var weight = runtime.Opinion(names[i], names[j], trust) - above;
                if (weight > 0) Assert.Equal(string.Empty, builder!.Add(i, j, weight));
            }
        var direct = TrustIteration.Run(builder!.Build().Graph!, [], 150 * (TrustIteration.Scale / 1000), 12, TrustIteration.Scale / 1_000_000, new GraphScratch());
        for (var i = 0; i < names.Length; i++) Assert.Equal(RelationshipRuntime.StandingOf(direct.Scores[i], names.Length), runtime.Actor(names[i]).Standing);
    }

    [Fact]
    public void TheStrongestOpinionsAreChosenByStrengthWhateverTheSignAndTiesGoToTheLowestId()
    {
        var set = SocialFixture.Set(r => SocialFixture.Block(r, "balance")["topK"] = 3);
        var runtime = SocialFixture.Village(set, "ann", "b1", "b2", "b3", "b4", "b5", "b6");
        foreach (var (other, opinion) in new[] { ("b1", 300), ("b2", -900), ("b3", 300), ("b4", 700), ("b5", -300), ("b6", 50) }) SocialFixture.Think(runtime, "ann", other, opinion);
        var strong = runtime.StrongOpinions(runtime.Actor("ann"), set.Social.Balance);
        // -900, 700, then the 300 / -300 / 300 tie: all three have strength 300; the lowest id (b1) comes first.
        Assert.Equal(["b2", "b4", "b1"], strong.Select(s => s.To));
        Assert.Equal([-900L, 700, 300], strong.Select(s => s.Opinion));
    }

    [Fact]
    public void TheStrongestProductOfTheTwoOpinionsIsPressedFirst()
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(r =>
        {
            SocialFixture.Block(r, "communities")["enabled"] = false;
            SocialFixture.Block(r, "standing")["enabled"] = false;
            SocialFixture.Block(r, "balance")["maxPerActor"] = 1;
        }), "ann", "bea", "dee", "eve", "c1", "c2", "c3");
        foreach (var (via, first, subject, second) in new[] { ("bea", 900, "c1", 100), ("dee", 200, "c2", 800), ("eve", 500, "c3", 400) })
        {
            SocialFixture.Think(runtime, "ann", via, first);
            SocialFixture.Think(runtime, via, subject, second);
            SocialFixture.Think(runtime, "ann", subject, 50);
        }
        var shift = Assert.Single(SocialFixture.NextDay(runtime).Balance);
        Assert.Equal(("c3", "eve"), (shift.Subject, shift.Via));         // 500 x 400 = 200,000 beats 160,000 and 90,000
    }

    [Fact]
    public void ARarerWeakerFriendCanHaveTheStrongerPullAndEqualPullsGoToTheLowerSubjectThenTheLowerFriend()
    {
        RelationshipRuntime Build(params (string Via, int First, string Subject, int Second)[] triads)
        {
            var runtime = SocialFixture.Village(SocialFixture.Set(r =>
            {
                SocialFixture.Block(r, "communities")["enabled"] = false;
                SocialFixture.Block(r, "standing")["enabled"] = false;
                SocialFixture.Block(r, "balance")["maxPerActor"] = 1;
            }), "ann", "ba", "bz", "c1", "c2", "c3", "c4");
            foreach (var (via, first, subject, second) in triads)
            {
                SocialFixture.Think(runtime, "ann", via, first);
                SocialFixture.Think(runtime, via, subject, second);
                SocialFixture.Think(runtime, "ann", subject, 50);
            }
            return runtime;
        }
        // ann's strongest friend (ba, 900) has the weaker pull (900 x 100 = 90,000); bz (600 x 900 = 540,000) wins.
        var weaker = Assert.Single(SocialFixture.NextDay(Build(("ba", 900, "c1", 100), ("bz", 600, "c2", 900))).Balance);
        Assert.Equal(("c2", "bz"), (weaker.Subject, weaker.Via));
        // Equal pulls (400 x 400): the lower subject id wins even though its friend has the higher id.
        var tie = Assert.Single(SocialFixture.NextDay(Build(("ba", 400, "c2", 400), ("bz", 400, "c1", 400))).Balance);
        Assert.Equal(("c1", "bz"), (tie.Subject, tie.Via));
        // The same subject through two friends with equal pulls: the lower friend id is named.
        var same = Assert.Single(SocialFixture.NextDay(Build(("bz", 400, "c1", 400), ("ba", 400, "c1", 400))).Balance);
        Assert.Equal(("c1", "ba"), (same.Subject, same.Via));
    }

    [Fact]
    public void TheSeededChanceIsExclusiveAtTheRoll()
    {
        // A chance equal to the very first roll lets no push through: the roll must be below the chance.
        RelationshipSet SetWith(int chance) => SocialFixture.Set(r =>
        {
            SocialFixture.Block(r, "communities")["enabled"] = false;
            SocialFixture.Block(r, "standing")["enabled"] = false;
            SocialFixture.Block(r, "balance")["chancePermille"] = chance;
        });
        var probe = SetWith(1000);
        var first = (int)DeterministicRoll.Below(probe.Seed, "balance", 0, 1000);
        Assert.InRange(first, 1, 999);
        foreach (var (chance, pushed) in new[] { (first, 0), (first + 1, 1) })
        {
            var runtime = SocialFixture.Village(SetWith(chance), "ann", "bea", "cyd");
            SocialFixture.Think(runtime, "ann", "bea", 500);
            SocialFixture.Think(runtime, "bea", "cyd", 500);
            SocialFixture.Think(runtime, "ann", "cyd", 50);
            Assert.Equal(pushed, SocialFixture.NextDay(runtime).Balance.Count);
        }
    }

    [Fact]
    public void TheExplanationsSayWhenThereIsNothingToExplain()
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(), "ann", "bea", "cyd");
        SocialFixture.Think(runtime, "ann", "bea", 100);                 // exactly on the line of liking for communities: not a liking
        SocialFixture.NextDay(runtime);
        var community = RelationshipExplain.Community(runtime, "ann");
        Assert.DoesNotContain("likes bea", community);
        Assert.Contains("0 of 0 liked actors", community);
        Assert.Contains("nobody trusts ann above 0 on trust: only the pre-trust share is left", RelationshipExplain.Standing(runtime, "ann"));
        Assert.Contains("no third actor both ann and cyd hold an opinion about, or ann has no opinion of cyd", RelationshipExplain.Balance(runtime, "ann", "cyd"));
        Assert.DoesNotContain("pressure now", RelationshipExplain.Balance(runtime, "ann", "bea"));
    }
}
