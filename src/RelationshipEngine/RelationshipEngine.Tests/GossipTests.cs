using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>Gossip (R8): bounded per night, once per instance, hop-limited, seeded, only to trusted neighbours, planned from the state at the start of the night.</summary>
public sealed class GossipTests
{
    private const int Trust = 1;

    private static RelationshipRuntime Make(Action<JsonObject>? edit, params string[] names)
    {
        var set = edit is null ? Fixture.Village : RelationshipLoader.Load(Edit.Doc(edit));
        var runtime = RelationshipRuntime.Create(set);
        foreach (var name in names) runtime.AddActor(name, []);
        return runtime;
    }

    private static int Heard(RelationshipRuntime runtime, string listener, string subject, string memory = "heard-kindness") => runtime.MemoryCount(listener, subject, runtime.Set.MemoryById[memory].Index);

    private static void Night(RelationshipRuntime runtime) => runtime.Advance((runtime.Step / 24 + 1) * 24, Fixture.Spring);

    private static void ChainOfSelf(JsonObject r) => Edit.Memory(r, "heard-kindness")["spread"] = JsonNode.Parse("{\"as\":\"heard-kindness\",\"permille\":800,\"chance\":1000,\"hops\":1}");

    [Fact]
    public void ATellerTellsItsMostTrustedListenersUpToTheLimitAndTheSeededRollDecidesEach()
    {
        var runtime = Make(null, "t", "s", "h1", "h2", "h3", "h4");
        runtime.Remember("t", "s", "was-gifted", 1000);
        foreach (var (name, trust) in new[] { ("h1", 500), ("h2", 400), ("h3", 300), ("h4", 200) }) runtime.AdjustAxis("t", name, "trust", trust);
        var rollsBefore = runtime.Rolls;
        var changes = runtime.Advance(24, Fixture.Spring);
        Assert.Equal(rollsBefore + 3, runtime.Rolls);                                   // three listeners, three rolls
        var expected = new[] { "h1", "h2", "h3" }.Where((_, i) => DeterministicRoll.Below(Fixture.Village.Seed, "gossip", rollsBefore + i, 1000) < 700).ToList();
        Assert.Equal(expected, new[] { "h1", "h2", "h3" }.Where(h => Heard(runtime, h, "s") == 1));
        Assert.Equal(0, Heard(runtime, "h4", "s"));                                     // the fourth most trusted is beyond the limit
        Assert.Equal(expected.Count, changes.Memories.Count(m => m.Origin == "t"));
        foreach (var listener in expected)
        {
            var heard = runtime.Edge(listener, "s")!.Memories.Single();
            Assert.Equal(("t", 600, 1), (heard.Origin, heard.Scale, heard.Hops));       // 600 permille of the teller's 1000, one hop less
        }
        Assert.NotEmpty(expected);
    }

    [Fact]
    public void AnInstanceIsToldOnceSoALaterNightTellsNothingMore()
    {
        var runtime = Make(null, "t", "s", "h1");
        runtime.Remember("t", "s", "was-gifted", 1000);
        runtime.AdjustAxis("t", "h1", "trust", 500);
        Night(runtime);
        var after = runtime.Rolls;
        Assert.True(runtime.Edge("t", "s")!.Memories.Single().Told);
        for (var i = 0; i < 5; i++) Night(runtime);
        Assert.Equal(after, runtime.Rolls);
    }

    [Fact]
    public void OnlyListenersAtOrAboveTheTrustLineHearAndTheSubjectNeverHearsItself()
    {
        var runtime = Make(r => r["gossip"]!["minTrust"] = 8, "t", "s", "dist", "ok", "near");
        runtime.Remember("t", "s", "was-gifted", 1000);
        runtime.AdjustAxis("t", "dist", "trust", -100);
        runtime.AdjustAxis("t", "s", "trust", 900);
        runtime.AdjustAxis("t", "ok", "trust", 10);           // drifts to 8 by the night: exactly the line, allowed
        runtime.AdjustAxis("t", "near", "trust", 9);          // drifts to 7: just under
        var before = runtime.Rolls;
        Night(runtime);
        Assert.Equal(before + 1, runtime.Rolls);            // only "ok": the distrusted and the subject are skipped
        Assert.Equal(0, Heard(runtime, "dist", "s"));
        Assert.Null(runtime.Edge("s", "s"));
    }

    [Fact]
    public void NewsTravelsOneHopPerNightAndStopsWhenTheHopsRunOut()
    {
        // The first telling is made sure (chance 1000) so the test does not depend on a roll; the memory it becomes tells itself on, surely, with one hop.
        var sure = Make(r => { ChainOfSelf(r); Edit.Memory(r, "was-gifted")["spread"]!["chance"] = 1000; }, "t", "s", "u", "v", "w");
        sure.Remember("t", "s", "was-gifted", 1000);
        sure.AdjustAxis("t", "u", "trust", 500);
        sure.AdjustAxis("u", "v", "trust", 500);
        sure.AdjustAxis("v", "w", "trust", 500);
        Night(sure);
        Assert.Equal((1, 0), (Heard(sure, "u", "s"), Heard(sure, "v", "s")));          // not both in one night: the plan is made first
        Night(sure);
        Assert.Equal((1, 1, 0), (Heard(sure, "u", "s"), Heard(sure, "v", "s"), Heard(sure, "w", "s")));
        Night(sure);
        Assert.Equal(0, Heard(sure, "w", "s"));                                        // v's copy has no hops left
        Assert.Equal(2, sure.Edge("u", "s")!.Memories.Single().Hops + 1);
        Assert.Equal(0, sure.Edge("v", "s")!.Memories.Single().Hops);
        Assert.Equal("u", sure.Edge("v", "s")!.Memories.Single().Origin);
    }

    [Fact]
    public void ATellerTellsOnlyItsNewestMemoriesUpToTheLimitOfTellings()
    {
        var runtime = Make(r => Edit.Memory(r, "was-gifted")["spread"]!["chance"] = 1000, "t", "h", "s1", "s2", "s3", "s4");
        runtime.AdjustAxis("t", "h", "trust", 500);
        for (var i = 1; i <= 4; i++)
        {
            runtime.Advance(i, Fixture.Spring);
            runtime.Remember("t", "s" + i, "was-gifted", 1000);
        }
        Night(runtime);
        Assert.Equal((0, 0, 1, 1), (Heard(runtime, "h", "s1"), Heard(runtime, "h", "s2"), Heard(runtime, "h", "s3"), Heard(runtime, "h", "s4")));   // the newest two of four
        Assert.True(runtime.Edge("t", "s1")!.Memories.Single().Told == false);
    }

    [Fact]
    public void AMemoryWithoutSpreadOrWithNoHopsLeftIsNeverTold()
    {
        var runtime = Make(null, "t", "s", "h");
        runtime.Remember("t", "s", "shared-feast", 1000);       // no spread
        runtime.AdjustAxis("t", "h", "trust", 500);
        var before = runtime.Rolls;
        for (var i = 0; i < 3; i++) Night(runtime);
        Assert.Equal(before, runtime.Rolls);
    }

    [Fact]
    public void GossipCanBeSwitchedOffByAZeroLimit()
    {
        var runtime = Make(r => r["gossip"]!["maxTellings"] = 0, "t", "s", "h");
        runtime.Remember("t", "s", "was-gifted", 1000);
        runtime.AdjustAxis("t", "h", "trust", 500);
        Night(runtime);
        Assert.Equal(0, runtime.Rolls);
        Assert.False(runtime.Edge("t", "s")!.Memories.Single().Told);
    }

    [Fact]
    public void AnInsultTravelsAsAnInsultWithItsOwnScaleAndSign()
    {
        var runtime = Make(r => Edit.Memory(r, "was-insulted")["spread"]!["chance"] = 1000, "t", "s", "h");
        runtime.Remember("t", "s", "was-insulted", 1000);
        runtime.AdjustAxis("t", "h", "trust", 500);
        Night(runtime);
        Assert.Equal(1, Heard(runtime, "h", "s", "heard-insult"));
        Assert.Equal(500, runtime.Edge("h", "s")!.Memories.Single().Scale);
        Assert.True(runtime.Opinion("h", "s", 0) < 0);
    }

    [Fact]
    public void DifferentSeedsGiveDifferentGossipAndTheSameSeedTheSame()
    {
        string Run(long seed)
        {
            var runtime = RelationshipRuntime.Create(RelationshipLoader.Load(Edit.Doc(r => { r["seed"] = seed; r["limits"]!["maxEdgesPerActor"] = 24; })));
            var names = Enumerable.Range(0, 20).Select(i => "n" + i.ToString("00")).ToList();
            foreach (var name in names) runtime.AddActor(name, []);
            for (var i = 0; i < 20; i++)
            {
                runtime.AdjustAxis(names[i], names[(i + 1) % 20], "trust", 400);
                runtime.AdjustAxis(names[i], names[(i + 2) % 20], "trust", 300);
                runtime.AdjustAxis(names[i], names[(i + 3) % 20], "trust", 200);
                runtime.Remember(names[i], names[(i + 7) % 20], "was-gifted", 1000);
            }
            Night(runtime);
            return runtime.StateDigest();
        }
        Assert.Equal(Run(5), Run(5));
        Assert.NotEqual(Run(5), Run(6));
    }

    [Fact]
    public void WhateverTheVillageTheMemoriesAGossipNightMakesStayWithinTellingsTimesListenersTimesActors()
    {
        var seed = 31UL;
        int Next(int bound) { seed = seed * 6364136223846793005UL + 1442695040888963407UL; return (int)((seed >> 33) % (ulong)bound); }
        for (var village = 0; village < 12; village++)
        {
            var runtime = Make(r => { r["limits"]!["maxEdgesPerActor"] = 64; r["limits"]!["maxMemoriesPerEdge"] = 16; }, Enumerable.Range(0, 40).Select(i => "n" + i.ToString("00")).ToArray());
            var names = runtime.Actors.Keys.ToList();
            for (var i = 0; i < 400; i++)
            {
                var a = names[Next(40)];
                var b = names[Next(40)];
                if (a == b) continue;
                if (Next(2) == 0) runtime.AdjustAxis(a, b, "trust", Next(900) - 200);
                else runtime.Remember(a, b, new[] { "was-gifted", "was-insulted", "saw-theft" }[Next(3)], 1000);
            }
            for (var night = 0; night < 6; night++)
            {
                var changes = runtime.Advance((runtime.Step / 24 + 1) * 24, Fixture.Spring);
                var told = changes.Memories.Count(m => m.Origin.Length > 0);
                Assert.True(told <= 40 * runtime.Set.Gossip.MaxTellings * runtime.Set.Gossip.MaxListeners, "a night made " + told + " tellings");
            }
        }
    }

    [Fact]
    public void TheTrustAxisTheGossipUsesIsTheDocumentsOwn()
    {
        Assert.Equal(Trust, Fixture.Village.Gossip.TrustAxis);
        Assert.Equal((0L, 2, 3), (Fixture.Village.Gossip.MinTrust, Fixture.Village.Gossip.MaxTellings, Fixture.Village.Gossip.MaxListeners));
    }
}
