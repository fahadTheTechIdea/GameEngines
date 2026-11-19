using System.Text.Json;
using EngineCommon;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>Save and open (R11): exact round trip; format, document, ranges before the digest, digest, then what the state says about the document; each with its own code; nothing repaired.</summary>
public sealed class SaveTests
{
    private static readonly byte[] Good = RelationshipSave.Write(Tamper.Played());

    private static string Open(byte[] bytes) => Fixture.Refusal(() => RelationshipSave.Open(Fixture.Village, bytes));

    [Fact]
    public void ASaveOpensToTheSameDigestAndWritesBackTheSameBytes()
    {
        var runtime = Tamper.Played();
        var reopened = RelationshipSave.Open(Fixture.Village, RelationshipSave.Write(runtime));
        Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
        Assert.Equal(Good, RelationshipSave.Write(reopened));
        Assert.Equal((runtime.Step, runtime.Rolls, runtime.EdgeCount), (reopened.Step, reopened.Rolls, reopened.EdgeCount));
    }

    [Fact]
    public void AnOpenedRuntimeGoesOnExactlyAsTheOriginalWouldIncludingTheSeededRollAndTheTraitScaledDecay()
    {
        var original = Tamper.Played();
        var reopened = RelationshipSave.Open(Fixture.Village, RelationshipSave.Write(original));
        foreach (var runtime in new[] { original, reopened })
        {
            runtime.Perform(new InteractionRequest("share-meal", "alice", "cara", [], ""), Fixture.Spring);
            runtime.Advance(runtime.Step + 100, Fixture.Host(season: "winter"));
            runtime.Perform(new InteractionRequest("share-meal", "cara", "alice", [], ""), Fixture.Spring);
            runtime.Advance(runtime.Step + 300, Fixture.Spring);
        }
        Assert.Equal(original.StateDigest(), reopened.StateDigest());
        Assert.Equal(original.Rolls, reopened.Rolls);
    }

    [Fact]
    public void EveryKindOfChangeToTheStateChangesTheDigest()
    {
        var seen = new HashSet<string>();
        void Check(string what, Action<RelationshipRuntime> change)
        {
            var runtime = Tamper.Played();
            var before = runtime.StateDigest();
            change(runtime);
            Assert.True(before != runtime.StateDigest(), what + " did not change the digest");
            seen.Add(runtime.StateDigest());
        }
        Check("a new actor", r => r.AddActor("eve", []));
        Check("a trait", r => { r.RemoveActor("dan"); r.AddActor("dan", ["kind"]); });
        Check("an activity", r => r.SetActivity("cara", "work"));
        Check("a step", r => r.Advance(r.Step + 1, Fixture.Spring));
        Check("a need", r => r.AdjustNeed("cara", "fun", 3));
        Check("a moodlet", r => r.Grant("cara", "ate-well", 1000));
        Check("a memory", r => r.Remember("cara", "alice", "shared-feast", 1000));
        Check("a base value", r => r.AdjustAxis("dan", "cara", "trust", 7));
        Check("a bond", r => r.SetBond("bob", "cara", "sworn-rival", true));
        Check("a cooldown", r => r.Perform(new InteractionRequest("chat", "cara", "dan", [], ""), Fixture.Spring));
        Check("a roll", r => { r.Advance(r.Step + 12, Fixture.Spring); r.Perform(new InteractionRequest("share-meal", "cara", "dan", [], ""), Fixture.Spring); });
        Check("a taste", r => { r.RemoveActor("cara"); r.AddActor("cara", [], new Dictionary<string, int> { ["pie"] = 1 }); });
        Assert.Equal(12, seen.Count);
    }

    [Fact]
    public void TheDigestIsTheSameForTheSameStateWhateverOrderThingsAreDoneIn()
    {
        string Build(bool reversed)
        {
            var runtime = Fixture.Runtime();
            var names = new[] { "a", "b", "c" };
            foreach (var name in reversed ? names.Reverse() : names) runtime.AddActor(name, []);
            var pairs = new[] { ("a", "b", 10), ("b", "c", 20), ("c", "a", 30) };
            foreach (var (from, to, value) in reversed ? pairs.Reverse() : pairs) runtime.AdjustAxis(from, to, "affinity", value);
            return runtime.StateDigest();
        }
        Assert.Equal(Build(false), Build(true));
    }

    // ---- refusals, in the order they are checked ---------------------------------------------------------------------------------------

    [Fact]
    public void NotASaveAnOldFormatAnExtraFieldAndAMissingFieldAreAFormatRefusal()
    {
        Assert.Equal(RelationshipCodes.SaveFormat, Open("[]"u8.ToArray()));
        Assert.Equal(RelationshipCodes.SaveFormat, Open("null"u8.ToArray()));
        Assert.Equal(RelationshipCodes.SaveFormat, Open(Tamper.Forge(Good, i => i.Format = 0)));
        Assert.Equal(RelationshipCodes.SaveFormat, Open(Tamper.Forge(Good, i => i.Format = 3)));
        Assert.Equal(RelationshipCodes.SaveOldFormat, Open(Tamper.Forge(Good, i => i.Format = 1)));
        var withExtra = System.Text.Json.Nodes.JsonNode.Parse(Good)!.AsObject();
        withExtra["Surprise"] = 1;
        Assert.Equal(RelationshipCodes.SaveFormat, Open(System.Text.Encoding.UTF8.GetBytes(withExtra.ToJsonString())));
        foreach (var field in new[] { "Format", "Document", "Id", "Step", "Rolls", "Actors", "Digest" })
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(Good)!.AsObject();
            node.Remove(field);
            Assert.Equal(RelationshipCodes.SaveFormat, Open(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())));
        }
    }

    [Fact]
    public void AMissingFieldDeepInsideAnActorIsRefusedToo()
    {
        foreach (var field in new[] { "Id", "Traits", "Tastes", "Activity", "MoodState", "Joined", "Needs", "Moodlets", "Edges" })
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(Good)!.AsObject();
            node["Actors"]![0]!.AsObject().Remove(field);
            Assert.Equal(RelationshipCodes.SaveFormat, Open(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())));
        }
        foreach (var field in new[] { "To", "Touched", "Base", "Ladder", "Bonds", "Memories", "Cooldowns" })
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(Good)!.AsObject();
            var actor = node["Actors"]!.AsArray().Select(a => a!.AsObject()).First(a => a["Edges"]!.AsArray().Count > 0);
            actor["Edges"]![0]!.AsObject().Remove(field);
            Assert.Equal(RelationshipCodes.SaveFormat, Open(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())));
        }
    }

    [Fact]
    public void ASaveFromAnotherDocumentOrWithAnotherIdIsRefusedBeforeAnythingElseIsLookedAt()
    {
        Assert.Equal(RelationshipCodes.SaveDocument, Open(Tamper.Forge(Good, i => i.Document = "ABC")));
        Assert.Equal(RelationshipCodes.SaveDocument, Open(Tamper.Forge(Good, i => i.Id = "other")));
        Assert.Equal(RelationshipCodes.SaveDocument, Open(Tamper.Forge(Good, i => { i.Document = "ABC"; i.Step = -5; })));   // the document is checked before the ranges
        var other = RelationshipLoader.Load(Edit.Doc(r => r["seed"] = 1));
        Assert.Equal(RelationshipCodes.SaveDocument, Fixture.Refusal(() => RelationshipSave.Open(other, Good)));
    }

    [Fact]
    public void ARangeErrorIsReportedAsAStateEvenWhenTheDigestWasLeftStaleAndAStaleDigestAloneIsADigestError()
    {
        Assert.Equal(RelationshipCodes.SaveState, Open(Tamper.Stale(Good, i => i.Step = -1)));
        Assert.Equal(RelationshipCodes.SaveDigest, Open(Tamper.Stale(Good, i => i.Step += 1)));
        Assert.Equal(RelationshipCodes.SaveDigest, Open(Tamper.Stale(Good, i => i.Digest = "00")));
    }

    [Fact]
    public void EveryRangeAndConsistencyRuleRefusesAForgedStateWhoseDigestWasRecomputed()
    {
        var cases = new (string Name, Action<RelationshipImage> Edit)[]
        {
            ("negative rolls", i => i.Rolls = -1),
            ("actors out of order", i => i.Actors.Reverse()),
            ("actor repeated", i => i.Actors.Add(i.Actors[0])),
            ("actor id not an id", i => i.Actors[0].Id = "Bad Id"),
            ("traits out of order", i => i.Actors.First(a => a.Traits.Count == 2).Traits.Reverse()),
            ("trait not an id", i => i.Actors[0].Traits.Add("NOT AN ID")),
            ("too many tastes", i => { for (var n = 0; n < 40; n++) i.Actors[0].Tastes.Add(new ImageTaste { Tag = "t" + n.ToString("00"), Level = 1 }); }),
            ("taste above love", i => i.Actors.First(a => a.Tastes.Count > 0).Tastes[0].Level = 3),
            ("activity not an id", i => i.Actors[0].Activity = "Not An Id"),
            ("mood state past the last", i => i.Actors[0].MoodState = 99),
            ("joined in the future", i => i.Actors[0].Joined = i.Step + 1),
            ("one need missing", i => i.Actors[0].Needs.RemoveAt(0)),
            ("need above its max", i => i.Actors[0].Needs[0].Level = 1_000_000),
            ("need below zero", i => i.Actors[0].Needs[0].Level = -1),
            ("critical flag set on a high need", i => { i.Actors[0].Needs[1].Level = 24_000; i.Actors[0].Needs[1].Critical = true; }),
            ("low need not marked critical", i => { i.Actors[0].Needs[1].Level = 0; i.Actors[0].Needs[1].Critical = false; }),
            ("moodlet not in the document", i => i.Actors.First(a => a.Moodlets.Count > 0).Moodlets[0].Moodlet = 99),
            ("moodlet from the future", i => i.Actors.First(a => a.Moodlets.Count > 0).Moodlets[0].Start = i.Step + 1),
            ("moodlet scale beyond the limit", i => i.Actors.First(a => a.Moodlets.Count > 0).Moodlets[0].Scale = 10_001),
            ("moodlet source not an id", i => i.Actors.First(a => a.Moodlets.Count > 0).Moodlets[0].Source = "Bad"),
            ("more moodlets than the limit", i => { var a = i.Actors[0]; for (var n = 0; n < 20; n++) a.Moodlets.Add(new ImageMoodlet { Moodlet = 0, Start = 0, Scale = 1000, Source = "" }); }),
            ("edge to itself", i => { var a = i.Actors.First(x => x.Edges.Count > 0); a.Edges[0].To = a.Id; }),
            ("edge to nobody", i => i.Actors.First(a => a.Edges.Count > 0).Edges[0].To = "zzz"),
            ("edge touched in the future", i => i.Actors.First(a => a.Edges.Count > 0).Edges[0].Touched = i.Step + 1),
            ("edge with a missing axis", i => i.Actors.First(a => a.Edges.Count > 0).Edges[0].Base.RemoveAt(0)),
            ("base value outside the axis", i => i.Actors.First(a => a.Edges.Count > 0).Edges[0].Base[0] = 5000),
            ("ladder step past the last", i => i.Actors.First(a => a.Edges.Count > 0).Edges[0].Ladder[0] = 99),
            ("bond not in the document", i => i.Actors.First(a => a.Edges.Count > 0).Edges[0].Bonds.Add(99)),
            ("bonds out of order", i => { var e = i.Actors.First(a => a.Edges.Count > 0).Edges[0]; e.Bonds.Clear(); e.Bonds.Add(1); e.Bonds.Add(0); }),
            ("memory not in the document", i => i.Actors.SelectMany(a => a.Edges).First(e => e.Memories.Count > 0).Memories[0].Memory = 99),
            ("memory from the future", i => i.Actors.SelectMany(a => a.Edges).First(e => e.Memories.Count > 0).Memories[0].Start = i.Step + 1),
            ("memory with five hops", i => i.Actors.SelectMany(a => a.Edges).First(e => e.Memories.Count > 0).Memories[0].Hops = 5),
            ("memory origin not an id", i => i.Actors.SelectMany(a => a.Edges).First(e => e.Memories.Count > 0).Memories[0].Origin = "Bad"),
            ("more memories than the limit", i => { var e = i.Actors.SelectMany(a => a.Edges).First(x => x.Memories.Count > 0); for (var n = 0; n < 10; n++) e.Memories.Add(new ImageMemory { Memory = 0, Start = 0, Scale = 1000, Hops = 0, Origin = "" }); }),
            ("cooldown not in the document", i => i.Actors.SelectMany(a => a.Edges).First(e => e.Cooldowns.Count > 0).Cooldowns[0].Interaction = 99),
            ("cooldown beyond what the interaction allows", i => i.Actors.SelectMany(a => a.Edges).First(e => e.Cooldowns.Count > 0).Cooldowns[0].ReadyAt = i.Step + 100_000),
            ("symmetric bond without the way back", i => { var spouse = i.Actors.First(a => a.Id == "alice").Edges.First(e => e.To == "dan"); i.Actors.First(a => a.Id == "dan").Edges.Clear(); _ = spouse; }),
            ("exclusive bond twice", i => { var a = i.Actors.First(x => x.Id == "alice"); foreach (var other in new[] { "bob", "cara" }) { var edge = a.Edges.FirstOrDefault(e => e.To == other); if (edge is null) { edge = new ImageEdge { To = other, Touched = 0, Base = new List<long> { 0, 0, 0 }, Ladder = new List<int> { 2, 1 } }; a.Edges.Add(edge); a.Edges.Sort((x, y) => string.CompareOrdinal(x.To, y.To)); } edge.Bonds.Add(0); var back = i.Actors.First(x => x.Id == other); if (!back.Edges.Any(e => e.To == "alice")) { back.Edges.Add(new ImageEdge { To = "alice", Touched = 0, Base = new List<long> { 0, 0, 0 }, Ladder = new List<int> { 2, 1 } }); back.Edges.Sort((x, y) => string.CompareOrdinal(x.To, y.To)); } back.Edges.First(e => e.To == "alice").Bonds.Add(0); } }),
        };
        foreach (var (name, edit) in cases)
        {
            byte[] forged;
            try { forged = Tamper.Forge(Good, edit); }
            catch (Exception exception) { throw new Xunit.Sdk.XunitException("the case '" + name + "' could not be built: " + exception.Message); }
            var code = Open(forged);
            Assert.True(code == RelationshipCodes.SaveState, name + ": expected " + RelationshipCodes.SaveState + ", got " + code);
        }
    }

    [Fact]
    public void ASaveIsPlainJsonWithEveryPartOfTheStateInIt()
    {
        using var document = JsonDocument.Parse(Good);
        var root = document.RootElement;
        Assert.Equal(RelationshipSave.Format, root.GetProperty("Format").GetInt32());
        Assert.Equal(Fixture.Village.Digest, root.GetProperty("Document").GetString());
        Assert.Equal(4, root.GetProperty("Actors").GetArrayLength());
        Assert.Equal(64, root.GetProperty("Digest").GetString()!.Length);
    }
}
