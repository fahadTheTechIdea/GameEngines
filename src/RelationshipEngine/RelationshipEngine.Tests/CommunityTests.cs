using EngineCommon;
using EngineCommon.Graph;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>Communities (derived at the day boundary by deterministic label propagation over the edges that like): worked answers, the graph library's own result for the same edges, order independence, identity from day to day.</summary>
public sealed class CommunityTests
{
    private static readonly string[] Six = ["ann", "ben", "cat", "dov", "eli", "fay"];

    /// <summary>Two circles of three who like each other (affinity 500) joined by one weak liking (cat about dov: 150, only 50 above the line of 100).</summary>
    private static RelationshipRuntime TwoCircles(IReadOnlyList<string> order)
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(), order.ToArray());
        foreach (var circle in new[] { new[] { "ann", "ben", "cat" }, new[] { "dov", "eli", "fay" } })
            foreach (var a in circle)
                foreach (var b in circle.Where(b => b != a))
                    SocialFixture.Think(runtime, a, b, 500);
        SocialFixture.Think(runtime, "cat", "dov", 150);
        return runtime;
    }

    [Fact]
    public void TwoCirclesWithAWeakLinkAreTwoCommunitiesNamedByTheirLowestMember()
    {
        var runtime = TwoCircles(Six);
        foreach (var actor in runtime.Actors.Values) Assert.Equal(actor.Id, actor.Community);   // before the first day boundary everyone is alone
        var changes = SocialFixture.NextDay(runtime);
        Assert.Equal(["ann", "ann", "ann", "dov", "dov", "dov"], Six.Select(id => runtime.Actor(id).Community));
        // The changes name each actor that moved, in id order, with where it came from and went to.
        Assert.Equal(["ben:ben>ann", "cat:cat>ann", "eli:eli>dov", "fay:fay>dov"], changes.Communities.Select(c => c.Actor + ":" + c.From + ">" + c.To));
    }

    [Fact]
    public void TheCommunityOfEachActorIsWhatTheGraphLibraryFindsOnTheSameEdges()
    {
        var runtime = TwoCircles(Six);
        SocialFixture.NextDay(runtime);
        // Build the same graph by hand from the opinions the runtime reports, and run the library directly.
        Assert.Equal(string.Empty, GraphBuilder.Create(Six.Length, out var builder));
        for (var i = 0; i < Six.Length; i++)
            for (var j = 0; j < Six.Length; j++)
            {
                if (i == j || runtime.Edge(Six[i], Six[j]) is null) continue;
                var weight = runtime.Opinion(Six[i], Six[j], runtime.Set.AxisById["affinity"].Index) - 100;
                if (weight > 0) Assert.Equal(string.Empty, builder!.Add(i, j, weight));
            }
        var direct = LabelPropagation.Run(builder!.Build().Graph!, [], 8, new GraphScratch());
        Assert.Equal(string.Empty, direct.Code);
        Assert.Equal(Six.Select(id => Six[direct.Community[Array.IndexOf(Six, id)]]), Six.Select(id => runtime.Actor(id).Community));
    }

    [Fact]
    public void EveryActorHoldsTheCommunityOfTheHeaviestLikingOrOneTiedWithIt()
    {
        // A fixed point, checked from the runtime's own numbers and not from the library: no actor would change its community on another sweep.
        var runtime = SocialFixture.Village(SocialFixture.Set(), Enumerable.Range(0, 30).Select(i => "p" + i.ToString("00")).ToArray());
        var seed = 99UL;
        long Next() { seed = seed * 6364136223846793005UL + 1442695040888963407UL; return (long)(seed >> 33); }
        for (var i = 0; i < 30; i++)
            for (var k = 0; k < 6; k++)
            {
                var j = (int)(Next() % 30);
                if (j != i) SocialFixture.Think(runtime, "p" + i.ToString("00"), "p" + j.ToString("00"), 150 + Next() % 800);
            }
        SocialFixture.NextDay(runtime);
        var affinity = runtime.Set.AxisById["affinity"].Index;
        foreach (var actor in runtime.Actors.Values)
        {
            var support = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var edge in actor.Edges.Values)
            {
                var weight = runtime.OpinionOf(edge, affinity) - 100;
                if (weight > 0) support[runtime.Actor(edge.To).Community] = support.GetValueOrDefault(runtime.Actor(edge.To).Community) + weight;
            }
            if (support.Count == 0) continue;
            Assert.Equal(support.Values.Max(), support.GetValueOrDefault(actor.Community));
        }
        // Every community is named by its lowest member, who belongs to it.
        foreach (var actor in runtime.Actors.Values)
        {
            Assert.True(string.CompareOrdinal(actor.Community, actor.Id) <= 0);
            Assert.Equal(actor.Community, runtime.Actor(actor.Community).Community);
        }
    }

    [Fact]
    public void TheOrderActorsAndOpinionsWereAddedInDoesNotChangeTheResult()
    {
        var one = TwoCircles(Six);
        var other = TwoCircles(Six.Reverse().ToArray());
        SocialFixture.NextDay(one);
        SocialFixture.NextDay(other);
        Assert.Equal(one.StateDigest(), other.StateDigest());
    }

    [Fact]
    public void ACommunityKeepsItsIdentityFromDayToDayAndAnActorWhoseLinksFadeLeaves()
    {
        var runtime = TwoCircles(Six);
        SocialFixture.NextDay(runtime);
        var digest = runtime.StateDigest();
        var again = SocialFixture.NextDay(runtime);
        Assert.Empty(again.Communities);                                  // nothing moved, nothing announced
        Assert.NotEqual(digest, runtime.StateDigest());                   // (the step moved on)
        Assert.Equal("ann", runtime.Actor("cat").Community);
        // Fay stops liking the others and they stop liking fay: fay is alone again.
        foreach (var other in new[] { "dov", "eli" })
        {
            SocialFixture.Think(runtime, "fay", other, -600);
            SocialFixture.Think(runtime, other, "fay", -600);
        }
        var changes = SocialFixture.NextDay(runtime);
        Assert.Equal("fay", runtime.Actor("fay").Community);
        Assert.Contains(changes.Communities, c => c is { Actor: "fay", From: "dov", To: "fay" });
        Assert.Equal("dov", runtime.Actor("eli").Community);
    }

    [Fact]
    public void RemovingTheActorThatNamesACommunityRenamesItAfterTheNextLowestMember()
    {
        var runtime = TwoCircles(Six);
        SocialFixture.NextDay(runtime);
        runtime.RemoveActor("ann");
        Assert.Equal("ben", runtime.Actor("ben").Community);
        Assert.Equal("ben", runtime.Actor("cat").Community);
        Assert.Equal("dov", runtime.Actor("eli").Community);
        runtime.RemoveActor("fay");                                       // a member that names nothing changes nothing
        Assert.Equal("dov", runtime.Actor("dov").Community);
        // The save of that state is accepted (the naming rule holds after the rename).
        var opened = RelationshipSave.Open(runtime.Set, RelationshipSave.Write(runtime));
        Assert.Equal(runtime.StateDigest(), opened.StateDigest());
    }

    [Fact]
    public void ANewActorStartsAloneUntilTheNextBoundaryFindsItCompany()
    {
        var runtime = TwoCircles(Six);
        SocialFixture.NextDay(runtime);
        runtime.AddActor("gus", []);
        Assert.Equal("gus", runtime.Actor("gus").Community);
        foreach (var other in new[] { "dov", "eli", "fay" })
        {
            SocialFixture.Think(runtime, "gus", other, 600);
            SocialFixture.Think(runtime, other, "gus", 600);
        }
        SocialFixture.NextDay(runtime);
        Assert.Equal("dov", runtime.Actor("gus").Community);
    }

    [Fact]
    public void TheFactsSayTheCommunityItsSizeAndWhetherTwoActorsShareOne()
    {
        var runtime = TwoCircles(Six);
        SocialFixture.NextDay(runtime);
        var facts = RelationshipFacts.Read(runtime, "ben", "eli");
        Assert.Equal("ann", facts["actor.community"].Text);
        Assert.Equal(3, facts["actor.community_size"].Integer);
        Assert.Equal("dov", facts["target.community"].Text);
        Assert.False(facts["edge.same_community"].Boolean);
        Assert.True(RelationshipFacts.Read(runtime, "ben", "cat")["edge.same_community"].Boolean);
        var declared = runtime.Set.Facts["actor.community_size"];
        Assert.Equal((1, runtime.Set.Limits.MaxActors), (declared.Minimum, declared.Maximum));
    }

    [Fact]
    public void WithCommunitiesOffEveryoneStaysAlone()
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(r => SocialFixture.Block(r, "communities")["enabled"] = false), Six);
        foreach (var circle in new[] { new[] { "ann", "ben", "cat" } })
            foreach (var a in circle) foreach (var b in circle.Where(b => b != a)) SocialFixture.Think(runtime, a, b, 500);
        var changes = SocialFixture.NextDay(runtime);
        Assert.Empty(changes.Communities);
        Assert.All(runtime.Actors.Values, a => Assert.Equal(a.Id, a.Community));
    }
}
