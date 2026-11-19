using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>
/// Balance pressure (Heider): a triad is balanced when the sign of A about C is the product of A about B and B about C. The four hand-built triads are worked on paper; the pressure is a memory of 60 on affinity (the
/// sample's), bounded by that figure; everything is planned from the state at the start of the pass.
/// </summary>
public sealed class BalanceTests
{
    private static RelationshipRuntime Triad(long aboutB, long bThinksOfC, long aboutC, Action<JsonObject>? edit = null)
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(root =>
        {
            SocialFixture.Block(root, "communities")["enabled"] = false;
            SocialFixture.Block(root, "standing")["enabled"] = false;
            edit?.Invoke(root);
        }), "ann", "bea", "cyd");
        SocialFixture.Think(runtime, "ann", "bea", aboutB);
        SocialFixture.Think(runtime, "bea", "cyd", bThinksOfC);
        SocialFixture.Think(runtime, "ann", "cyd", aboutC);
        return runtime;
    }

    private static long Pressure(RelationshipRuntime runtime, string holder, string subject) =>
        runtime.Opinion(holder, subject, runtime.Set.AxisById["affinity"].Index) - SocialFixture.Own(runtime, holder, subject, "affinity");

    [Theory]
    [InlineData(500, 500, 50, 60, "a friend of my friend is my friend")]
    [InlineData(500, -500, 50, -60, "an enemy of my friend is my enemy")]
    [InlineData(-500, -500, 50, 60, "an enemy of my enemy is my friend")]
    [InlineData(-500, 500, 50, -60, "a friend of my enemy is my enemy")]
    [InlineData(500, 500, -300, 60, "a friend of my friend whom I dislike is pressed the other way")]
    [InlineData(-500, 500, 300, -60, "a friend of my enemy whom I like is pressed the other way")]
    public void AnUnbalancedTriadIsPressedTowardTheProductOfTheTwoSigns(long aboutB, long bThinksOfC, long aboutC, long expectedPressure, string why)
    {
        var runtime = Triad(aboutB, bThinksOfC, aboutC);
        var changes = SocialFixture.NextDay(runtime);
        Assert.True(expectedPressure == Pressure(runtime, "ann", "cyd"), why + ": pressure " + Pressure(runtime, "ann", "cyd"));
        Assert.Equal(aboutC, SocialFixture.Own(runtime, "ann", "cyd", "affinity"));    // what ann herself thinks is untouched
        var shift = Assert.Single(changes.Balance);
        Assert.Equal(("ann", "cyd", "bea", aboutB, bThinksOfC, (int)(expectedPressure > 0 ? 1000 : -1000)), (shift.Holder, shift.Subject, shift.Via, shift.HolderVia, shift.ViaSubject, shift.Scale));
        Assert.Equal("bea", runtime.Edge("ann", "cyd")!.Memories.Single(m => m.Memory == runtime.Set.MemoryById["social-pressure"].Index).Origin);
    }

    [Theory]
    [InlineData(500, 500, 300)]
    [InlineData(500, -500, -300)]
    [InlineData(-500, -500, 300)]
    [InlineData(-500, 500, -300)]
    public void ABalancedTriadIsLeftAlone(long aboutB, long bThinksOfC, long aboutC)
    {
        var runtime = Triad(aboutB, bThinksOfC, aboutC);
        var changes = SocialFixture.NextDay(runtime);
        Assert.Empty(changes.Balance);
        Assert.Equal(0, Pressure(runtime, "ann", "cyd"));
        Assert.Empty(runtime.Edge("ann", "cyd")!.Memories);
    }

    [Theory]
    [InlineData(100, 500)]    // a neutral opinion of the friend
    [InlineData(500, 100)]    // a neutral opinion of the friend's friend
    [InlineData(199, 500)]    // just under the line of liking
    [InlineData(-199, -500)]  // just over the line of disliking
    public void WhereEitherSideIsNeutralThereIsNoTriadAndNoPressure(long aboutB, long bThinksOfC)
    {
        var runtime = Triad(aboutB, bThinksOfC, 50);
        Assert.Empty(SocialFixture.NextDay(runtime).Balance);
    }

    [Fact]
    public void TheLinesAreInclusiveAtLikeAndDislike()
    {
        Assert.Single(SocialFixture.NextDay(Triad(200, 200, 50)).Balance);          // exactly like
        Assert.Single(SocialFixture.NextDay(Triad(-200, -200, 50)).Balance);        // exactly dislike
    }

    [Fact]
    public void WithoutAnEdgeFromTheHolderToTheSubjectNothingIsPressedAndNoEdgeIsMade()
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(), "ann", "bea", "cyd");
        SocialFixture.Think(runtime, "ann", "bea", 500);
        SocialFixture.Think(runtime, "bea", "cyd", 500);
        Assert.Empty(SocialFixture.NextDay(runtime).Balance);
        Assert.Null(runtime.Edge("ann", "cyd"));
    }

    [Fact]
    public void ThePressureIsHeldAtTheMemorysSizeHoweverManyDaysItIsRenewedAndOnlyTheFirstDayIsNews()
    {
        var runtime = Triad(500, 500, 50);
        var news = 0;
        for (var day = 0; day < 40; day++)
        {
            news += SocialFixture.NextDay(runtime).Balance.Count;
            Assert.Equal(60, Pressure(runtime, "ann", "cyd"));
        }
        Assert.Equal(1, news);
    }

    [Fact]
    public void WhenTheFriendComesRoundTheSamePressureTurnsTheOtherWayAndIsNewsAgain()
    {
        var runtime = Triad(500, 500, 50);
        SocialFixture.NextDay(runtime);
        SocialFixture.Think(runtime, "bea", "cyd", -800);                          // bea now dislikes cyd: an enemy of ann's friend
        var shift = Assert.Single(SocialFixture.NextDay(runtime).Balance);
        Assert.Equal(-1000, shift.Scale);
        Assert.Equal(-60, Pressure(runtime, "ann", "cyd"));
    }

    [Fact]
    public void WhenTheTriadEndsThePressureFadesLinearlyAndIsGoneAtTheMemorysDuration()
    {
        var runtime = Triad(500, 500, 50);
        SocialFixture.NextDay(runtime);                                            // step 24: the memory (duration 72, linear, 60) begins
        SocialFixture.Think(runtime, "bea", "cyd", -450);                          // bea's opinion of cyd is now 50: neutral, no triad
        var left = new List<long>();
        for (var day = 0; day < 3; day++)
        {
            Assert.Empty(SocialFixture.NextDay(runtime).Balance);
            left.Add(Pressure(runtime, "ann", "cyd"));
        }
        Assert.Equal([40L, 20, 0], left);                                          // 60 x (72 - 24) / 72, 60 x (72 - 48) / 72, expired at age 72
        Assert.Empty(runtime.Edge("ann", "cyd")!.Memories);
    }

    [Fact]
    public void TheStrongestPullWinsWhenTwoFriendsDisagreeAboutTheSameSubject()
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(r => { SocialFixture.Block(r, "communities")["enabled"] = false; SocialFixture.Block(r, "standing")["enabled"] = false; }), "ann", "bea", "cyd", "dee");
        SocialFixture.Think(runtime, "ann", "bea", 900);     // a close friend of bea...
        SocialFixture.Think(runtime, "ann", "dee", 300);     // ...and a mild friend of dee
        SocialFixture.Think(runtime, "bea", "cyd", 400);     // bea likes cyd (pull 900 x 400)
        SocialFixture.Think(runtime, "dee", "cyd", -900);    // dee dislikes cyd (pull 300 x 900)
        SocialFixture.Think(runtime, "ann", "cyd", 50);
        var changes = SocialFixture.NextDay(runtime);
        var shift = Assert.Single(changes.Balance);
        Assert.Equal(("bea", 1000), (shift.Via, shift.Scale));               // 360,000 beats 270,000
    }

    [Fact]
    public void AnActorTakesAtMostItsDailyAllowanceStrongestFirstAndOnePerSubject()
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(r =>
        {
            SocialFixture.Block(r, "communities")["enabled"] = false;
            SocialFixture.Block(r, "standing")["enabled"] = false;
            SocialFixture.Block(r, "balance")["maxPerActor"] = 2;
        }), "ann", "bea", "c1", "c2", "c3");
        SocialFixture.Think(runtime, "ann", "bea", 500);
        foreach (var (subject, strength) in new[] { ("c1", 300), ("c2", 900), ("c3", 600) })
        {
            SocialFixture.Think(runtime, "bea", subject, strength);
            SocialFixture.Think(runtime, "ann", subject, 50);
        }
        var changes = SocialFixture.NextDay(runtime);
        Assert.Equal(["c2", "c3"], changes.Balance.Select(b => b.Subject).Order(StringComparer.Ordinal));
        Assert.Equal(0, Pressure(runtime, "ann", "c1"));
    }

    [Fact]
    public void TheOrderOfTheActorsDoesNotChangeWhatIsPlannedBecauseEveryPushComesFromTheStateAtTheStart()
    {
        // ann presses cyd through bea; cyd presses ann through dee in the same pass: each reads opinions as they were before any push.
        RelationshipRuntime Build(params string[] order)
        {
            var runtime = SocialFixture.Village(SocialFixture.Set(r => { SocialFixture.Block(r, "communities")["enabled"] = false; SocialFixture.Block(r, "standing")["enabled"] = false; }), order);
            SocialFixture.Think(runtime, "ann", "bea", 500); SocialFixture.Think(runtime, "bea", "cyd", 500); SocialFixture.Think(runtime, "ann", "cyd", 50);
            SocialFixture.Think(runtime, "cyd", "dee", 500); SocialFixture.Think(runtime, "dee", "ann", 500); SocialFixture.Think(runtime, "cyd", "ann", 50);
            return runtime;
        }
        var one = Build("ann", "bea", "cyd", "dee");
        var other = Build("dee", "cyd", "bea", "ann");
        SocialFixture.NextDay(one);
        SocialFixture.NextDay(other);
        Assert.Equal(one.StateDigest(), other.StateDigest());
        Assert.Equal(60, Pressure(one, "ann", "cyd"));
        Assert.Equal(60, Pressure(one, "cyd", "ann"));
    }

    [Fact]
    public void OnlyTheTopKStrongestOpinionsOfTheHolderAndOfTheFriendCount()
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(r =>
        {
            SocialFixture.Block(r, "communities")["enabled"] = false;
            SocialFixture.Block(r, "standing")["enabled"] = false;
            SocialFixture.Block(r, "balance")["topK"] = 1;
        }), "ann", "bea", "bob", "cyd");
        SocialFixture.Think(runtime, "ann", "bea", 900);     // ann's strongest friend
        SocialFixture.Think(runtime, "ann", "bob", 400);     // second: not counted with topK 1
        SocialFixture.Think(runtime, "bob", "cyd", 800);
        SocialFixture.Think(runtime, "bea", "cyd", 50);
        SocialFixture.Think(runtime, "ann", "cyd", 50);
        Assert.Empty(SocialFixture.NextDay(runtime).Balance);
        var explanation = RelationshipExplain.Balance(runtime, "ann", "cyd");
        Assert.Contains("via bob", explanation);
        Assert.Contains("unbalanced, not among the strongest", explanation);
    }

    [Fact]
    public void TheSeededChanceDecidesEachPushExactlyAsTheDocumentedRollsDo()
    {
        const int groups = 12;
        var holders = Enumerable.Range(0, groups).Select(i => "a" + i.ToString("00")).ToArray();
        var set = SocialFixture.Set(r =>
        {
            SocialFixture.Block(r, "communities")["enabled"] = false;
            SocialFixture.Block(r, "standing")["enabled"] = false;
            SocialFixture.Block(r, "balance")["chancePermille"] = 500;
        });
        var runtime = RelationshipRuntime.Create(set);
        foreach (var holder in holders) { runtime.AddActor(holder, []); runtime.AddActor("b" + holder[1..], []); runtime.AddActor("c" + holder[1..], []); }
        foreach (var holder in holders)
        {
            SocialFixture.Think(runtime, holder, "b" + holder[1..], 500);
            SocialFixture.Think(runtime, "b" + holder[1..], "c" + holder[1..], 500);
            SocialFixture.Think(runtime, holder, "c" + holder[1..], 50);
        }
        var changes = SocialFixture.NextDay(runtime);
        // One roll per candidate in holder order (a00..a11): the k-th holder is pressed when roll k is under the chance. No other roll is drawn in this village (no gossip).
        var expected = holders.Where((_, k) => DeterministicRoll.Below(set.Seed, "balance", k, 1000) < 500).ToArray();
        Assert.Equal(expected, changes.Balance.Select(b => b.Holder).Order(StringComparer.Ordinal));
        Assert.Equal(groups, runtime.Rolls);
        Assert.InRange(expected.Length, 1, groups - 1);                                 // the sample seed gives some of each, so the test can fail either way
    }


    [Fact]
    public void APushTheChanceTurnsAwayDoesNotUseTheDailyAllowanceAndTheRollsFollowTheStrongestFirst()
    {
        var set = SocialFixture.Set(r =>
        {
            SocialFixture.Block(r, "communities")["enabled"] = false;
            SocialFixture.Block(r, "standing")["enabled"] = false;
            SocialFixture.Block(r, "balance")["maxPerActor"] = 1;
            SocialFixture.Block(r, "balance")["chancePermille"] = 500;
        });
        var runtime = SocialFixture.Village(set, "ann", "bea", "c1", "c2", "c3");
        SocialFixture.Think(runtime, "ann", "bea", 500);
        foreach (var (subject, strength) in new[] { ("c1", 300), ("c2", 900), ("c3", 600) })
        {
            SocialFixture.Think(runtime, "bea", subject, strength);
            SocialFixture.Think(runtime, "ann", subject, 50);
        }
        var changes = SocialFixture.NextDay(runtime);
        // Strongest first: c2, c3, c1 draw rolls 0, 1, 2; the first that passes is the one push, and no roll is drawn after it.
        var order = new[] { "c2", "c3", "c1" };
        var winner = Enumerable.Range(0, 3).Where(k => DeterministicRoll.Below(set.Seed, "balance", k, 1000) < 500).Select(k => (int?)k).FirstOrDefault();
        Assert.Equal(winner is null ? [] : [order[winner.Value]], changes.Balance.Select(b => b.Subject));
        Assert.Equal(winner is null ? 3 : winner.Value + 1, runtime.Rolls);
    }
    [Fact]
    public void WithChanceCertainNoRollIsDrawn()
    {
        var runtime = Triad(500, 500, 50);
        SocialFixture.NextDay(runtime);
        Assert.Equal(0, runtime.Rolls);
    }

    [Fact]
    public void WithBalanceOffNothingIsPressed()
    {
        var runtime = Triad(500, 500, 50, r => SocialFixture.Block(r, "balance")["enabled"] = false);
        Assert.Empty(SocialFixture.NextDay(runtime).Balance);
        Assert.Empty(runtime.Edge("ann", "cyd")!.Memories);
    }

    [Fact]
    public void OverAYearInARandomVillageNoOpinionIsEverMovedByMoreThanTheMemoryAllows()
    {
        foreach (var seed in new ulong[] { 3, 17, 90210 })
        {
            var rng = seed;
            long Next() { rng = rng * 6364136223846793005UL + 1442695040888963407UL; return (long)(rng >> 33); }
            var names = Enumerable.Range(0, 40).Select(i => "v" + i.ToString("00")).ToArray();
            var runtime = RelationshipRuntime.Create(SocialFixture.Set(r => { r["limits"]!["maxEdgesPerActor"] = 12; }));
            foreach (var name in names) runtime.AddActor(name, []);
            for (var i = 0; i < names.Length; i++)
                for (var k = 0; k < 8; k++)
                {
                    var j = (int)(Next() % names.Length);
                    if (j != i) SocialFixture.Think(runtime, names[i], names[j], Next() % 1600 - 800);
                }
            var announced = 0;
            for (var day = 0; day < 365; day++)
            {
                announced += SocialFixture.NextDay(runtime).Balance.Count;
                if (day % 20 == 0)   // the village keeps living: a few opinions change by hand
                    for (var k = 0; k < 10; k++)
                    {
                        var i = (int)(Next() % names.Length);
                        var j = (int)(Next() % names.Length);
                        if (i != j) SocialFixture.Think(runtime, names[i], names[j], Next() % 600 - 300);
                    }
                foreach (var actor in runtime.Actors.Values)
                    foreach (var edge in actor.Edges.Values)
                        Assert.InRange(Pressure(runtime, actor.Id, edge.To), -60, 60);
            }
            Assert.True(announced > 0);
            Assert.True(announced < 40 * 365 * 2, "announced " + announced);       // never more than the daily allowance
        }
    }
}
