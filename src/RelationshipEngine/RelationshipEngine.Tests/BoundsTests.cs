using EngineCommon;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>Bounds (R9) and consistency: after any sequence of calls nothing exceeds its limit, every opinion is inside its axis, bonds mirror, and the state saves and opens to the same digest.</summary>
public sealed class BoundsTests
{
    private static readonly string[] Names = ["n0", "n1", "n2", "n3", "n4", "n5", "n6", "n7", "n8", "n9"];

    private static RelationshipSet Small() => RelationshipLoader.Load(Edit.Doc(r =>
    {
        r["limits"] = System.Text.Json.Nodes.JsonNode.Parse("{\"maxActors\":8,\"maxEdgesPerActor\":3,\"maxMemoriesPerEdge\":2,\"maxMoodlets\":3}");
    }));

    private static void CheckInvariants(RelationshipRuntime runtime)
    {
        var set = runtime.Set;
        Assert.True(runtime.Actors.Count <= set.Limits.MaxActors);
        foreach (var actor in runtime.Actors.Values)
        {
            Assert.True(actor.Edges.Count <= set.Limits.MaxEdgesPerActor, actor.Id + " has " + actor.Edges.Count + " edges");
            Assert.True(actor.Moodlets.Count <= set.Limits.MaxMoodlets, actor.Id + " has " + actor.Moodlets.Count + " moodlets");
            for (var n = 0; n < set.Needs.Count; n++) Assert.InRange(actor.Need[n], 0, set.Needs[n].Max * set.StepsPerDay);
            Assert.InRange(runtime.MoodValue(actor), set.Mood.Min, set.Mood.Max);
            foreach (var edge in actor.Edges.Values)
            {
                Assert.True(runtime.Actors.ContainsKey(edge.To), actor.Id + " has an edge to the missing " + edge.To);
                Assert.NotEqual(actor.Id, edge.To);
                Assert.True(edge.Memories.Count <= set.Limits.MaxMemoriesPerEdge, actor.Id + "->" + edge.To + " has " + edge.Memories.Count + " memories");
                foreach (var axis in set.Axes)
                {
                    Assert.InRange(edge.Base[axis.Index], axis.Min, axis.Max);
                    Assert.InRange(runtime.OpinionOf(edge, axis.Index), axis.Min, axis.Max);
                }
                foreach (var bond in edge.Bonds)
                    if (set.Bonds[bond].Symmetric) Assert.True(runtime.Actors[edge.To].Edges.TryGetValue(actor.Id, out var back) && back.Bonds.Contains(bond), "asymmetric symmetric bond " + actor.Id + "->" + edge.To);
            }
            foreach (var bond in set.Bonds.Where(b => b.Exclusive)) Assert.True(actor.Edges.Values.Count(e => e.Bonds.Contains(bond.Index)) <= 1);
        }
    }

    [Fact]
    public void AfterEverySingleCallOfThreeHundredRandomSequencesNothingExceedsItsLimitAndTheStateSavesAndOpensIdentically()
    {
        var set = Small();
        var interactions = set.Interactions.Where(i => !i.Gift).Select(i => i.Id).ToArray();
        var gifts = set.Gifts.Select(g => g.Id).ToArray();
        var moodlets = set.Moodlets.Select(m => m.Id).ToArray();
        var memories = set.Memories.Select(m => m.Id).ToArray();
        var needs = set.Needs.Select(n => n.Id).ToArray();
        var bonds = set.Bonds.Select(b => b.Id).ToArray();
        var activities = new[] { "", "eat", "sleep", "idle", "celebrate" };
        var evictions = 0;
        var calls = 0;
        for (var run = 0; run < 300; run++)
        {
            var seed = (ulong)run * 7919 + 17;
            int Next(int bound) { seed = seed * 6364136223846793005UL + 1442695040888963407UL; return (int)((seed >> 33) % (ulong)bound); }
            var runtime = RelationshipRuntime.Create(set);
            for (var op = 0; op < 80; op++)
            {
                string Any() => Names[Next(Names.Length)];
                string Present() => runtime.Actors.Count == 0 ? Names[0] : runtime.Actors.Keys.ElementAt(Next(runtime.Actors.Count));
                RelationshipChanges? changes = null;
                try
                {
                    switch (Next(11))
                    {
                        case 0: runtime.AddActor(Any(), Next(2) == 0 ? ["kind"] : ["grumpy", "stoic"]); break;
                        case 1: if (Next(5) == 0 && runtime.Actors.Count > 0) runtime.RemoveActor(Present()); break;
                        case 2: changes = runtime.Advance(runtime.Step + 1 + Next(70), Fixture.Host(season: Next(2) == 0 ? "winter" : "spring")); break;
                        case 3: changes = runtime.Perform(new InteractionRequest(interactions[Next(interactions.Length)], Present(), Present(), [], ""), Fixture.Spring).Changes; break;
                        case 4: changes = runtime.Perform(new InteractionRequest("give-gift", Present(), Present(), [Present()], gifts[Next(gifts.Length)]), Fixture.Spring).Changes; break;
                        case 5: changes = runtime.Grant(Present(), moodlets[Next(moodlets.Length)], Next(2000) - 500); break;
                        case 6: changes = runtime.Remember(Present(), Present(), memories[Next(memories.Length)], Next(2000) - 500); break;
                        case 7: changes = runtime.AdjustAxis(Present(), Present(), set.Axes[Next(set.Axes.Count)].Id, Next(2400) - 1200); break;
                        case 8: changes = runtime.SetBond(Present(), Present(), bonds[Next(bonds.Length)], Next(3) != 0); break;
                        case 9: changes = runtime.AdjustNeed(Present(), needs[Next(needs.Length)], Next(1400) - 700); break;
                        case 10: runtime.SetActivity(Present(), activities[Next(activities.Length)]); break;
                    }
                }
                catch (EngineRefusedException refusal) when (refusal.Code is RelationshipCodes.DuplicateActor or RelationshipCodes.UnknownActor or RelationshipCodes.Self or RelationshipCodes.Cooldown or RelationshipCodes.Precondition or RelationshipCodes.Witness or RelationshipCodes.Capacity)
                {
                    // a refusal the sequence expects: random names are not always actors, pairs are not always two actors, cooldowns run
                }
                calls++;
                if (changes is not null) evictions += changes.Evictions.Count;
                CheckInvariants(runtime);
                if (op % 20 == 19)
                {
                    var reopened = RelationshipSave.Open(set, RelationshipSave.Write(runtime));
                    Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
                    CheckInvariants(reopened);
                }
            }
        }
        Assert.True(calls >= 24_000);
        Assert.True(evictions > 100, "the limits were small so evictions should be common; saw " + evictions);
    }

    [Fact]
    public void ARefusedCallInTheMiddleOfASequenceLeavesTheStateExactlyAsItWas()
    {
        var runtime = RelationshipRuntime.Create(Small());
        foreach (var name in Names.Take(4)) runtime.AddActor(name, []);
        runtime.Perform(new InteractionRequest("chat", "n0", "n1", [], ""), Fixture.Spring);
        var digest = runtime.StateDigest();
        foreach (var bad in new Action[]
        {
            () => runtime.Perform(new InteractionRequest("chat", "n0", "n1", [], ""), Fixture.Spring),
            () => runtime.Perform(new InteractionRequest("chat", "n0", "n0", [], ""), Fixture.Spring),
            () => runtime.Perform(new InteractionRequest("steal", "n0", "n1", ["n0"], ""), Fixture.Spring),
            () => runtime.Grant("n0", "nope", 1000),
            () => runtime.Remember("n0", "n0", "was-gifted", 1000),
            () => runtime.SetBond("n0", "n9", "spouse", true),
            () => runtime.Advance(-1, Fixture.Spring),
        })
        {
            Assert.Throws<EngineRefusedException>(bad);
            Assert.Equal(digest, runtime.StateDigest());
        }
    }

    [Fact]
    public void ThousandsOfActorsAreHeldUpToTheLimitAndNoFurther()
    {
        var set = RelationshipLoader.Load(Edit.Doc(r => r["limits"]!["maxActors"] = 2000));
        var runtime = RelationshipRuntime.Create(set);
        for (var i = 0; i < 2000; i++) runtime.AddActor("p" + i, []);
        Assert.Equal(RelationshipCodes.Capacity, Fixture.Refusal(() => runtime.AddActor("one-too-many", [])));
        runtime.RemoveActor("p17");
        runtime.AddActor("one-more", []);
        Assert.Equal(2000, runtime.Actors.Count);
    }
}
