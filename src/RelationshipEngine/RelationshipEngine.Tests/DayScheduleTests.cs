using System.Text.Json.Nodes;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>
/// The day's schedule (pass 45): under "spread" the five stages of the day (upkeep, communities, standing, balance, gossip) are lived in the steps 24, 25, 26, 27 and 28 of a 24-step day, each whole in its own
/// step; under "atomic" all of them in step 24. The oracles are the stage timetable written out here, and the atomic mode (the behaviour the other tests pin) as the result a spread pass must reach on a village whose
/// opinions cannot move during the day.
/// </summary>
public sealed class DayScheduleTests
{
    private static RelationshipSet Doc(string schedule, Action<JsonObject>? edit = null) => SocialFixture.Set(root =>
    {
        SocialFixture.Social(root)["schedule"] = schedule;
        edit?.Invoke(root);
    });

    /// <summary>
    /// A village with no drift and no memory but the one gossip is about, so the opinions are the same at every step of the day. Two liking circles joined by nothing (ann, bea, cyd; dov, eli); ann has a neutral opinion
    /// of eli that bea's friendship with eli presses; t tells s a deed to the listeners it trusts.
    /// </summary>
    private static RelationshipRuntime Village(string schedule, Action<JsonObject>? edit = null)
    {
        var runtime = Make(Doc(schedule, edit), 23, "ann", "bea", "cyd", "dov", "eli", "s", "t", "u");
        foreach (var (a, b) in new[] { ("ann", "bea"), ("bea", "ann"), ("bea", "cyd"), ("cyd", "bea"), ("ann", "cyd"), ("cyd", "ann"), ("dov", "eli"), ("eli", "dov") }) SocialFixture.Think(runtime, a, b, 500);
        SocialFixture.Think(runtime, "bea", "eli", 500);
        SocialFixture.Think(runtime, "ann", "eli", 50);
        foreach (var (a, b) in new[] { ("ann", "bea"), ("bea", "cyd"), ("cyd", "ann"), ("dov", "eli"), ("eli", "dov"), ("u", "ann"), ("u", "bea"), ("cyd", "bea") }) SocialFixture.Trust(runtime, a, b, 400);
        runtime.AdjustAxis("t", "u", "trust", 300);
        runtime.Remember("t", "s", "was-gifted", 1000);
        return runtime;
    }

    private static RelationshipRuntime Make(RelationshipSet set, long start, params string[] actors)
    {
        var runtime = RelationshipRuntime.Create(set, start);
        foreach (var actor in actors) runtime.AddActor(actor, []);
        return runtime;
    }

    private static string Snapshot(RelationshipRuntime runtime) =>
        string.Join(';', runtime.Actors.Values.Select(a => a.Id + "=" + a.Community + "/" + a.Standing));

    private static readonly IReadOnlyDictionary<string, FactValue> Host = Fixture.Spring;

    [Fact]
    public void EachStageHappensInItsOwnStepAndInNoOtherUnderTheSpreadSchedule()
    {
        var runtime = Village("spread");
        var pressure = runtime.Set.MemoryById["social-pressure"].Index;
        var start = Snapshot(runtime);
        var rolls = runtime.Rolls;
        var upkeep = runtime.Advance(24, Host);                                          // stage 0: upkeep only
        Assert.Equal((start, rolls), (Snapshot(runtime), runtime.Rolls));
        Assert.Empty(upkeep.Communities); Assert.Empty(upkeep.Standing); Assert.Empty(upkeep.Balance);
        var communities = runtime.Advance(25, Host);                                     // stage 1: communities
        Assert.NotEmpty(communities.Communities); Assert.Empty(communities.Standing); Assert.Empty(communities.Balance);
        Assert.All(runtime.Actors.Values, a => Assert.Equal(1000, a.Standing));
        Assert.Equal("ann", runtime.Actor("cyd").Community);
        Assert.Equal("dov", runtime.Actor("eli").Community);
        var standing = runtime.Advance(26, Host);                                        // stage 2: standing
        Assert.Empty(standing.Communities); Assert.NotEmpty(standing.Standing); Assert.Empty(standing.Balance);
        Assert.DoesNotContain(runtime.Edge("ann", "eli")!.Memories, m => m.Memory == pressure);
        var balance = runtime.Advance(27, Host);                                         // stage 3: balance
        Assert.Empty(balance.Communities); Assert.Empty(balance.Standing); Assert.NotEmpty(balance.Balance);
        Assert.Equal(27, Assert.Single(runtime.Edge("ann", "eli")!.Memories, m => m.Memory == pressure).Start);
        Assert.Equal(rolls, runtime.Rolls);                                              // nothing has been told yet
        runtime.Advance(28, Host);                                          // stage 4: gossip
        Assert.True(runtime.Rolls > rolls, "the night's tellings drew their rolls in step 28");
        var rollsAfter = runtime.Rolls;
        var after = runtime.Advance(47, Host);                                           // nothing further until the next day
        Assert.Equal(rollsAfter, runtime.Rolls);
        Assert.Empty(after.Communities); Assert.Empty(after.Standing); Assert.Empty(after.Balance);
    }

    [Fact]
    public void AtomicDoesAllOfItInTheFirstStepOfTheDay()
    {
        var runtime = Village("atomic");
        var pressure = runtime.Set.MemoryById["social-pressure"].Index;
        var rolls = runtime.Rolls;
        var day = runtime.Advance(24, Host);
        Assert.NotEmpty(day.Communities); Assert.NotEmpty(day.Standing); Assert.NotEmpty(day.Balance);
        Assert.Equal(24, Assert.Single(runtime.Edge("ann", "eli")!.Memories, m => m.Memory == pressure).Start);
        Assert.True(runtime.Rolls > rolls);
        var rest = runtime.Advance(47, Host);
        Assert.Empty(rest.Communities); Assert.Empty(rest.Standing); Assert.Empty(rest.Balance);
    }

    [Fact]
    public void OnAVillageWhoseOpinionsCannotMoveTheSpreadPassReachesWhatTheAtomicPassReaches()
    {
        var spread = Village("spread");
        var atomic = Village("atomic");
        spread.Advance(28, Host);
        atomic.Advance(28, Host);
        Assert.Equal(Snapshot(atomic), Snapshot(spread));                                // the same communities and standing
        Assert.Equal(atomic.Rolls, spread.Rolls);                                        // the same tellings
        var pressure = atomic.Set.MemoryById["social-pressure"].Index;
        foreach (var holder in atomic.Actors.Values)
            foreach (var edge in holder.Edges.Values)
            {
                var other = spread.Edge(holder.Id, edge.To)!;
                Assert.Equal(edge.Memories.Select(m => (m.Memory, m.Scale, m.Origin, m.Hops, m.Told)), other.Memories.Select(m => (m.Memory, m.Scale, m.Origin, m.Hops, m.Told)));
            }
        // The one thing that differs is when the pressure began: the step of its own stage.
        Assert.Equal((24L, 27L), (atomic.Edge("ann", "eli")!.Memories.Single(m => m.Memory == pressure).Start, spread.Edge("ann", "eli")!.Memories.Single(m => m.Memory == pressure).Start));
    }

    [Theory]
    [InlineData(100)]      // communities and standing read the very same opinions: the atomic day builds one graph for both
    [InlineData(300)]      // the same axis at another line: two graphs, as a spread day always has
    public void StandingIsTheSameUnderEitherScheduleWhateverGraphItWasWorkedOnWhenBothPassesReadOneAxis(long standingLine)
    {
        RelationshipRuntime Make(string schedule)
        {
            var runtime = Village(schedule, root =>
            {
                SocialFixture.Block(root, "standing")["axis"] = "affinity";
                SocialFixture.Block(root, "standing")["above"] = standingLine;
            });
            return runtime;
        }
        var spread = Make("spread");
        var atomic = Make("atomic");
        spread.Advance(28, Host);
        atomic.Advance(28, Host);
        Assert.Equal(Snapshot(atomic), Snapshot(spread));
        Assert.Contains(spread.Actors.Values, a => a.Standing != 1000);
    }

    [Fact]
    public void StandingOnAnotherAxisAtTheSameLineIsNotWorkedOnTheCommunityGraph()
    {
        RelationshipRuntime Make(string schedule) => Village(schedule, root =>
        {
            SocialFixture.Block(root, "standing")["axis"] = "trust";
            SocialFixture.Block(root, "standing")["above"] = 100;   // the communities' line (affinity, 100)
        });
        var spread = Make("spread");
        var atomic = Make("atomic");
        spread.Advance(28, Host);
        atomic.Advance(28, Host);
        Assert.Equal(Snapshot(atomic), Snapshot(spread));
        Assert.Contains(spread.Actors.Values, a => a.Standing != 1000);
    }

    [Fact]
    public void TheCommunitySizeReadBeforeAStageIsNotKeptAfterTheStageMovesTheCommunities()
    {
        var runtime = Village("spread");
        runtime.Advance(24, Host);
        Assert.Equal(1, runtime.CommunitySize(runtime.Actor("cyd")));   // reads (and caches) the sizes of the lone actors
        runtime.Advance(25, Host);
        Assert.Equal(3, runtime.CommunitySize(runtime.Actor("cyd")));   // ann, bea, cyd
    }

    [Theory]
    [InlineData(24)] [InlineData(25)] [InlineData(26)] [InlineData(27)] [InlineData(28)] [InlineData(29)]
    public void ASaveTakenBetweenTwoStagesResumesTheSamePass(int cut)
    {
        var straight = Village("spread");
        var first = Village("spread");
        straight.Advance(100, Host);
        first.Advance(cut, Host);
        var reopened = RelationshipSave.Open(first.Set, RelationshipSave.Write(first));
        Assert.Equal(first.StateDigest(), reopened.StateDigest());
        var changes = reopened.Advance(100, Host);
        Assert.Equal(straight.StateDigest(), reopened.StateDigest());
        Assert.Equal(straight.Rolls, reopened.Rolls);
        // What the resumed runtime announced from the cut on is what an uninterrupted one announced from there.
        var tail = Village("spread");
        tail.Advance(cut, Host);
        var expected = tail.Advance(100, Host);
        Assert.Equal((expected.Communities.Count, expected.Standing.Count, expected.Balance.Count, expected.Memories.Count), (changes.Communities.Count, changes.Standing.Count, changes.Balance.Count, changes.Memories.Count));
    }

    [Fact]
    public void OneBigAdvanceAndSingleStepsLiveTheSameSpreadDays()
    {
        var big = Village("spread");
        var steps = Village("spread");
        big.Advance(120, Host);
        for (var step = 24; step <= 120; step++) steps.Advance(step, Host);
        Assert.Equal(steps.StateDigest(), big.StateDigest());
    }

    [Fact]
    public void ADayOfExactlyFiveStepsRunsTheWholePassAndStartsAgain()
    {
        var runtime = Make(Doc("spread", r => r["time"]!["stepsPerDay"] = 5), 4, "ann", "bea", "cyd");
        SocialFixture.Think(runtime, "ann", "bea", 500);
        SocialFixture.Think(runtime, "bea", "ann", 500);
        var steps = new List<long>();
        for (var step = 5; step <= 15; step++)
            if (runtime.Advance(step, Host).Communities.Count > 0) steps.Add(step);
        Assert.Equal([6L], steps);                                                       // stage 1 of the second day (steps 5 to 9): bea joins ann once; the later days change nothing
        Assert.Equal("ann", runtime.Actor("bea").Community);
    }

    [Fact]
    public void ARunStartedMidDayWaitsForTheNextStageItsStepNames()
    {
        var runtime = RelationshipRuntime.Create(Doc("spread"), 26);
        foreach (var name in new[] { "ann", "bea" }) runtime.AddActor(name, []);
        SocialFixture.Think(runtime, "ann", "bea", 500);
        SocialFixture.Think(runtime, "bea", "ann", 500);
        Assert.Empty(runtime.Advance(48, Host).Communities);                             // steps 27 to 48 are past the stages of that day
        Assert.Equal("bea", runtime.Actor("bea").Community);
        Assert.Equal("bea", Assert.Single(runtime.Advance(49, Host).Communities).Actor);   // stage 1 of the next day, step 49
    }

    [Fact]
    public void ARunFromStepZeroLivesTheLaterStagesOfItsFirstDayWithoutAnUpkeep()
    {
        // The stage a step names is lived whenever the step is lived, so a run that begins at step 0 meets stages 1 to 4 in steps 1 to 4 (a day begun before the run); the atomic schedule has no work before step 24.
        var runtime = Make(Doc("spread"), 0, "ann", "bea", "cyd");
        SocialFixture.Think(runtime, "ann", "bea", 500);
        SocialFixture.Think(runtime, "bea", "ann", 500);
        Assert.Equal("bea", Assert.Single(runtime.Advance(1, Host).Communities).Actor);
        SocialFixture.Trust(runtime, "ann", "bea", 400);
        SocialFixture.Trust(runtime, "cyd", "bea", 400);
        Assert.NotEmpty(runtime.Advance(2, Host).Standing);
        var atomic = Make(Doc("atomic"), 0, "ann", "bea", "cyd");
        SocialFixture.Think(atomic, "ann", "bea", 500);
        SocialFixture.Think(atomic, "bea", "ann", 500);
        Assert.Empty(atomic.Advance(23, Host).Communities);
    }

    [Fact]
    public void TheShippedSampleSpreadsTheDay()
    {
        Assert.Equal(DaySchedule.Spread, Fixture.Shipped.Social.Schedule);
        Assert.Equal(DaySchedule.Atomic, Fixture.Village.Social.Schedule);
        Assert.Equal(5, RelationshipRuntime.DayStages);
    }

    [Fact]
    public void TheScheduleIsRequiredNamedAndFitsTheDay()
    {
        Assert.Equal(RelationshipCodes.MissingField, Fixture.Refusal(() => RelationshipLoader.Load(Edit.Doc(r => r["social"]!.AsObject().Remove("schedule"))), "no schedule"));
        Assert.Equal(RelationshipCodes.Format, Fixture.Refusal(() => RelationshipLoader.Load(Edit.Doc(r => r["social"]!["schedule"] = "lazy")), "unknown word"));
        Assert.Equal(RelationshipCodes.Format, Fixture.Refusal(() => RelationshipLoader.Load(Edit.Doc(r => r["social"]!["schedule"] = "Spread")), "word is exact"));
        Assert.Equal(RelationshipCodes.Social, Fixture.Refusal(() => RelationshipLoader.Load(Edit.Doc(r => { r["social"]!["schedule"] = "spread"; r["time"]!["stepsPerDay"] = RelationshipRuntime.DayStages - 1; })), "four steps cannot hold five stages"));
        RelationshipLoader.Load(Edit.Doc(r => { r["social"]!["schedule"] = "spread"; r["time"]!["stepsPerDay"] = RelationshipRuntime.DayStages; }));
        RelationshipLoader.Load(Edit.Doc(r => { r["social"]!["schedule"] = "atomic"; r["time"]!["stepsPerDay"] = 1; }));
    }
}
