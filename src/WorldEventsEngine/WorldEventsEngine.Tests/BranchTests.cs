using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>
/// Branching a world under another seed (<see cref="WorldRuntime.Branch"/>): the same state at the same step, a future that comes from the new seed alone. The rare-event sampler restarts many futures from one checkpoint this way. The
/// state of a live runtime must also be one a save accepts, or a checkpoint taken after a seeding would be refused.
/// </summary>
public sealed class BranchTests
{
    private static WorldSet Reseeded(long seed)
    {
        var root = JsonNode.Parse(Fixture.ValleyText)!.AsObject();
        root["seed"] = seed;
        return WorldLoader.Load(root.ToJsonString());
    }

    private static WorldRuntime Lived(long to)
    {
        var runtime = WorldRuntime.Create(Fixture.Valley);
        Fixture.Live(runtime, to, Fixture.ValleyHost());
        return runtime;
    }

    private static long DeadTotal(WorldRuntime runtime) => runtime.Regions.Sum(r => r.Dead.Sum());

    [Fact]
    public void CasesSeededByACallLeaveAStateTheSaveAccepts()
    {
        var runtime = Lived(10);
        runtime.SeedOutbreak("hillfort", "flu", 4);
        var reopened = WorldSave.Open(Fixture.Valley, WorldSave.Write(runtime));
        Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
        Assert.True(reopened.Regions[1].Peak[Fixture.Valley.Diseases.First(d => d.Id == "flu").Index] >= 4);
    }

    [Fact]
    public void ABranchUnderTheSameSeedLivesExactlyTheFutureTheOriginalLives()
    {
        var original = Lived(60);
        var branch = original.Branch(Fixture.Valley);
        Assert.Equal(original.StateDigest(), branch.StateDigest());
        Fixture.Live(original, 200, Fixture.ValleyHost());
        Fixture.Live(branch, 200, Fixture.ValleyHost());
        Assert.Equal(original.StateDigest(), branch.StateDigest());
    }

    [Fact]
    public void ABranchUnderAnotherSeedKeepsThePastAndChangesTheFuture()
    {
        var original = Lived(60);
        var before = original.StateDigest();
        var other = Reseeded((long)Fixture.Valley.Seed + 1);
        var branch = original.Branch(other);
        Assert.Equal(original.Step, branch.Step);
        Assert.Equal(DeadTotal(original), DeadTotal(branch));
        Assert.Equal(original.Stress, branch.Stress);
        Assert.Equal(original.Draws, branch.Draws);
        Assert.Equal(before, original.StateDigest());                       // the original is untouched by being branched
        Fixture.Live(original, 365, Fixture.ValleyHost());
        Fixture.Live(branch, 365, Fixture.ValleyHost());
        var same = original.Regions.Select((r, i) => r.Weather == branch.Regions[i].Weather && r.Precip == branch.Regions[i].Precip && r.Temp == branch.Regions[i].Temp).All(x => x);
        Assert.False(same, "the future of a branch under another seed should differ from the original's");
        Assert.NotEqual(original.StateDigest(), branch.StateDigest());
    }

    [Fact]
    public void ABranchCanBeBranchedAgainAndAgain()
    {
        var original = Lived(30);
        var first = original.Branch(Reseeded(11));
        var second = first.Branch(Reseeded(12));
        var third = original.Branch(Reseeded(12));
        Fixture.Live(second, 120, Fixture.ValleyHost());
        Fixture.Live(third, 120, Fixture.ValleyHost());
        Assert.Equal(second.StateDigest(), third.StateDigest());             // the same state under the same seed gives the same future, however the branch was reached
    }

    [Fact]
    public void AnotherWorldOrAnotherShapeIsRefused()
    {
        var original = Lived(5);
        Assert.Equal(WorldCodes.BranchDocument, Fixture.Refusal(() => original.Branch(Fixture.Mini()), "another world"));
        var smaller = Fixture.Mini(root =>
        {
            root["id"] = Fixture.Valley.Id;
            root["diseases"] = new JsonArray();
            root["interventions"] = new JsonArray();
            root["cards"] = new JsonArray();
            foreach (var region in root["regions"]!.AsArray()) region!["immune"] = new JsonArray();
        });
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => original.Branch(smaller), "another shape"));
    }
}
