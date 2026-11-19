using System.Reflection;
using System.Text.Json.Nodes;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>Every refusal code is reached by at least one input, and the set of codes the matrix reaches is exactly the constants of <see cref="RelationshipCodes"/>.</summary>
public sealed class RefusalMatrixTests
{
    private static JsonArray Array(JsonObject root, string name) => root[name]!.AsArray();

    /// <summary>Documents that are wrong in one place: the code each is refused with, a name for the failure message, and the document.</summary>
    private static IEnumerable<(string Code, string Name, Func<string> Text)> Documents()
    {
        yield return (RelationshipCodes.Format, "format 4", () => Edit.Doc(r => r["format"] = 4));
        yield return (RelationshipCodes.FormatTwo, "format 2", () => Edit.Doc(r => r["format"] = 2));
        yield return (RelationshipCodes.OldFormat, "format 1", () => Edit.Doc(r => r["format"] = 1));
        yield return (RelationshipCodes.Format, "not JSON", () => "{ nope");
        yield return (RelationshipCodes.Format, "axes is not an array", () => Edit.Doc(r => r["axes"] = 5));
        yield return (RelationshipCodes.Format, "decay kind unknown", () => Edit.Doc(r => Edit.Moodlet(r, "insulted")["decay"]!["kind"] = "curvy"));
        yield return (RelationshipCodes.Format, "condition operator", () => Edit.Doc(r => Edit.Interaction(r, "chat")["accept"]![0]!["when"]!["op"] = "like"));
        yield return (RelationshipCodes.Format, "effect side unknown", () => Edit.Doc(r => Edit.Interaction(r, "chat")["effects"]![0]!["edge"] = "sideways"));
        yield return (RelationshipCodes.Format, "host fact type", () => Edit.Doc(r => Array(r, "facts")[0]!["type"] = "float"));
        yield return (RelationshipCodes.Format, "taste is text", () => Edit.Doc(r => Array(r, "tastes")[0] = "hate"));
        yield return (RelationshipCodes.UnknownField, "extra top-level field", () => Edit.Doc(r => r["extra"] = 1));
        yield return (RelationshipCodes.UnknownField, "extra axis field", () => Edit.Doc(r => Edit.Obj(r, "axes", 0)["colour"] = "red"));
        yield return (RelationshipCodes.UnknownField, "condition with an unknown shape", () => Edit.Doc(r => Edit.Interaction(r, "chat")["accept"]![0]!["when"] = new JsonObject { ["fact"] = "target.mood" }));
        yield return (RelationshipCodes.MissingField, "no tastes", () => Edit.Doc(r => r.Remove("tastes")));
        yield return (RelationshipCodes.MissingField, "need without decayModifiers", () => Edit.Doc(r => Edit.Need(r, "hunger").Remove("decayModifiers")));
        yield return (RelationshipCodes.MissingField, "effect without kind", () => Edit.Doc(r => Edit.Interaction(r, "chat")["effects"]![0]!.AsObject().Remove("kind")));
        yield return (RelationshipCodes.MissingField, "int host fact without range", () => Edit.Doc(r => Array(r, "facts").Add(new JsonObject { ["id"] = "story.act", ["type"] = "int" })));
        yield return (RelationshipCodes.Range, "no steps in a day", () => Edit.Doc(r => r["time"]!["stepsPerDay"] = 0));
        yield return (RelationshipCodes.Range, "negative seed", () => Edit.Doc(r => r["seed"] = -1));
        yield return (RelationshipCodes.Range, "axis min not below max", () => Edit.Doc(r => Edit.Obj(r, "axes", 0)["min"] = 1000));
        yield return (RelationshipCodes.Range, "moodlet without duration", () => Edit.Doc(r => Edit.Moodlet(r, "insulted")["duration"] = 0));
        yield return (RelationshipCodes.Range, "memory magnitude 0", () => Edit.Doc(r => Edit.Memory(r, "was-gifted")["effects"]![0]!["magnitude"] = 0));
        yield return (RelationshipCodes.Range, "tastes fall", () => Edit.Doc(r => { Array(r, "tastes")[4] = -9; }));
        yield return (RelationshipCodes.Range, "document too long", () => new string(' ', RelationshipLoader.MaxCharacters + 1));
        yield return (RelationshipCodes.Range, "tastes too wide", () => Edit.Doc(r => Array(r, "tastes")[0] = -6000));
        yield return (RelationshipCodes.Duplicate, "axis twice", () => Edit.Doc(r => Array(r, "axes").Add(Edit.Copy(Array(r, "axes")[0]!))));
        yield return (RelationshipCodes.Duplicate, "host fact twice", () => Edit.Doc(r => Array(r, "facts").Add(Edit.Copy(Array(r, "facts")[0]!))));
        yield return (RelationshipCodes.Duplicate, "memory twice", () => Edit.Doc(r => Array(r, "memories").Add(Edit.Copy(Array(r, "memories")[0]!))));
        yield return (RelationshipCodes.Duplicate, "source twice", () => Edit.Doc(r => Array(r, "sources").Add(Edit.Copy(Array(r, "sources")[0]!))));
        yield return (RelationshipCodes.Duplicate, "tag twice", () => Edit.Doc(r => Edit.Moodlet(r, "insulted")["tags"] = new JsonArray("insult", "insult")));
        yield return (RelationshipCodes.Reference, "mood weighs an unknown need", () => Edit.Doc(r => r["mood"]!["needWeights"]![0]!["need"] = "thirst"));
        yield return (RelationshipCodes.Reference, "effect on an unknown axis", () => Edit.Doc(r => Edit.Interaction(r, "chat")["effects"]![0]!["axis"] = "charm"));
        yield return (RelationshipCodes.Reference, "spread as an unknown memory", () => Edit.Doc(r => Edit.Memory(r, "was-gifted")["spread"]!["as"] = "gossiped"));
        yield return (RelationshipCodes.Reference, "condition reads an unknown fact", () => Edit.Doc(r => Edit.Interaction(r, "chat")["accept"]![0]!["when"]!["fact"] = "target.nerve"));
        yield return (RelationshipCodes.Reference, "decay modifier reads an actor fact", () => Edit.Doc(r => Edit.Need(r, "hunger")["decayModifiers"]![0]!["when"] = new JsonObject { ["fact"] = "actor.mood", ["op"] = "lt", ["value"] = 0 }));
        yield return (RelationshipCodes.Reference, "gossip trust axis unknown", () => Edit.Doc(r => r["gossip"]!["trustAxis"] = "esteem"));
        yield return (RelationshipCodes.Reference, "witness memory unknown", () => Edit.Doc(r => Edit.Interaction(r, "give-gift")["witness"]!["memory"] = "forgot"));
        yield return (RelationshipCodes.Curve, "urgency starts above 0", () => Edit.Doc(r => Edit.Need(r, "hunger")["urgency"]![0]![0] = 10));
        yield return (RelationshipCodes.Curve, "urgency levels do not rise", () => Edit.Doc(r => Edit.Need(r, "hunger")["urgency"]![1]![0] = 0));
        yield return (RelationshipCodes.Curve, "urgency pair of three", () => Edit.Doc(r => Edit.Need(r, "hunger")["urgency"]![1] = new JsonArray(200, 800, 1)));
        yield return (RelationshipCodes.Ladder, "first step above the bottom", () => Edit.Doc(r => r["ladders"]![0]!["steps"]![0]!["from"] = -900));
        yield return (RelationshipCodes.Ladder, "steps do not ascend", () => Edit.Doc(r => r["ladders"]![0]!["steps"]![2]!["from"] = -450));
        yield return (RelationshipCodes.Ladder, "mood state first above the bottom", () => Edit.Doc(r => r["mood"]!["states"]![0]!["from"] = -999));
        yield return (RelationshipCodes.Decay, "half-life without a half-life", () => Edit.Doc(r => Edit.Moodlet(r, "gift-received")["decay"]!.AsObject().Remove("halfLife")));
        yield return (RelationshipCodes.Decay, "linear with a half-life", () => Edit.Doc(r => Edit.Moodlet(r, "insulted")["decay"]!.AsObject().Add("halfLife", 5)));
        yield return (RelationshipCodes.Stack, "refresh with room for two", () => Edit.Doc(r => Edit.Moodlet(r, "insulted")["stack"]!["max"] = 2));
        yield return (RelationshipCodes.Effect, "taste on an interaction that is not a gift", () => Edit.Doc(r => Edit.Interaction(r, "chat")["effects"]![0]!["byTaste"] = true));
        yield return (RelationshipCodes.Effect, "axis delta 0", () => Edit.Doc(r => Edit.Interaction(r, "chat")["effects"]![0]!["delta"] = 0));
        yield return (RelationshipCodes.Effect, "effect kind unknown", () => Edit.Doc(r => Edit.Interaction(r, "chat")["effects"]![0]!["kind"] = "teleport"));
        yield return (RelationshipCodes.Reserved, "host fact in the engine's prefix", () => Edit.Doc(r => Array(r, "facts").Add(new JsonObject { ["id"] = "actor.zed", ["type"] = "bool" })));
        yield return (RelationshipCodes.Reserved, "axis named like an edge fact", () => Edit.Doc(r => Edit.Obj(r, "axes", 2)["id"] = "known"));
        yield return (RelationshipCodes.Reserved, "axis named like the community fact", () => Edit.Doc(r => Edit.Obj(r, "axes", 2)["id"] = "same_community"));
        yield return (RelationshipCodes.Social, "balance dislike not below like", () => Edit.Doc(r => r["social"]!["balance"]!["dislike"] = 200));
        yield return (RelationshipCodes.Social, "balance on without a memory", () => Edit.Doc(r => r["social"]!["balance"]!["memory"] = ""));
        yield return (RelationshipCodes.Social, "balance memory with no effect on the axis", () => Edit.Doc(r => r["social"]!["balance"]!["memory"] = "saw-theft"));
        yield return (RelationshipCodes.Social, "balance memory that spreads by gossip", () => Edit.Doc(r => { Edit.Memory(r, "social-pressure")["spread"] = JsonNode.Parse("{ \"as\": \"heard-kindness\", \"permille\": 500, \"chance\": 500, \"hops\": 1 }"); }));
        yield return (RelationshipCodes.Social, "graph larger than the library takes", () => Edit.Doc(r => { r["limits"]!["maxActors"] = 100000; r["limits"]!["maxEdgesPerActor"] = 256; }));
    }

    [Fact]
    public void EveryBadDocumentIsRefusedWithItsCode()
    {
        foreach (var (code, name, text) in Documents())
        {
            var found = Fixture.Refusal(() => RelationshipLoader.Load(text()), name);
            Assert.True(code == found, name + ": expected " + code + " but got " + found);
        }
    }

    [Fact]
    public void ADocumentWithNoEditLoads() => Assert.Equal("village", Fixture.Village.Id);

    // ---- calls ------------------------------------------------------------------------------------------------------------------------

    private static IEnumerable<(string Code, string Name, Action<RelationshipRuntime> Act)> Calls()
    {
        var spring = Fixture.Spring;
        yield return (RelationshipCodes.UnknownActor, "actor", r => r.Actor("zed"));
        yield return (RelationshipCodes.DuplicateActor, "add twice", r => r.AddActor("alice", []));
        yield return (RelationshipCodes.UnknownInteraction, "perform", r => r.Perform(new InteractionRequest("hug", "alice", "bob", [], ""), spring));
        yield return (RelationshipCodes.UnknownGift, "gift", r => r.Perform(new InteractionRequest("give-gift", "alice", "bob", [], "rock"), spring));
        yield return (RelationshipCodes.UnknownNeed, "adjust need", r => r.AdjustNeed("alice", "thirst", 1));
        yield return (RelationshipCodes.UnknownMoodlet, "grant", r => r.Grant("alice", "elation", 1000));
        yield return (RelationshipCodes.UnknownMemory, "remember", r => r.Remember("alice", "bob", "dream", 1000));
        yield return (RelationshipCodes.UnknownAxis, "adjust axis", r => r.AdjustAxis("alice", "bob", "charm", 1));
        yield return (RelationshipCodes.UnknownAxis, "explain opinion", r => RelationshipExplain.Opinion(r, "alice", "bob", "charm"));
        yield return (RelationshipCodes.UnknownBond, "set bond", r => r.SetBond("alice", "bob", "pact", true));
        yield return (RelationshipCodes.Self, "perform on self", r => r.Perform(new InteractionRequest("chat", "alice", "alice", [], ""), spring));
        yield return (RelationshipCodes.Self, "offers to self", r => r.Offers("alice", "alice", spring));
        yield return (RelationshipCodes.Precondition, "share a meal at night", r => r.Perform(new InteractionRequest("share-meal", "alice", "bob", [], ""), Fixture.Host(day: false)));
        yield return (RelationshipCodes.Cooldown, "chat twice", r =>
        {
            r.Perform(new InteractionRequest("chat", "alice", "bob", [], ""), spring);
            r.Perform(new InteractionRequest("chat", "alice", "bob", [], ""), spring);
        });
        yield return (RelationshipCodes.GiftRequired, "gift with no item", r => r.Perform(new InteractionRequest("give-gift", "alice", "bob", [], ""), spring));
        yield return (RelationshipCodes.GiftUnexpected, "chat with a gift", r => r.Perform(new InteractionRequest("chat", "alice", "bob", [], "pie"), spring));
        yield return (RelationshipCodes.Witness, "witness on an interaction with none", r => r.Perform(new InteractionRequest("chat", "alice", "bob", ["cara"], ""), spring));
        yield return (RelationshipCodes.Witness, "a party as witness", r => r.Perform(new InteractionRequest("give-gift", "alice", "bob", ["bob"], "pie"), spring));
        yield return (RelationshipCodes.Taste, "taste above love", r => r.AddActor("eve", [], new Dictionary<string, int> { ["pie"] = 3 }));
        yield return (RelationshipCodes.FactMissing, "no host facts", r => r.Advance(1, new Dictionary<string, FactValue>()));
        yield return (RelationshipCodes.FactInvalid, "season as a number", r => r.Advance(1, new Dictionary<string, FactValue>(Fixture.Spring) { ["cal.season"] = FactValue.From(3L) }));
        yield return (RelationshipCodes.Capacity, "more actors than the limit", r =>
        {
            var small = RelationshipLoader.Load(Edit.Doc(d => d["limits"]!["maxActors"] = 1));
            var runtime = RelationshipRuntime.Create(small);
            runtime.AddActor("one", []);
            runtime.AddActor("two", []);
        });
        yield return (RelationshipCodes.StepOrder, "back in time", r => { r.Advance(10, spring); r.Advance(5, spring); });
        yield return (RelationshipCodes.StepOrder, "start before 0", _ => RelationshipRuntime.Create(Fixture.Village, -1));
        yield return (RelationshipCodes.UnknownName, "actor id with a space", r => r.AddActor("Bad Id", []));
        yield return (RelationshipCodes.Range, "scale beyond the limit", r => r.Grant("alice", "insulted", 20_000));
    }

    private static RelationshipRuntime Pair()
    {
        var runtime = Fixture.Runtime();
        runtime.AddActor("alice", []);
        runtime.AddActor("bob", []);
        runtime.AddActor("cara", []);
        return runtime;
    }

    [Fact]
    public void EveryBadCallIsRefusedWithItsCodeAndChangesNothing()
    {
        foreach (var (code, name, act) in Calls())
        {
            var runtime = Pair();
            var before = runtime.StateDigest();
            var found = Fixture.Refusal(() => act(runtime), name);
            Assert.True(code == found, name + ": expected " + code + " but got " + found);
            if (name is "chat twice" or "back in time" or "more actors than the limit") continue;   // the first call of these changed something on purpose
            Assert.True(before == runtime.StateDigest(), name + " changed the state although it was refused");
        }
    }

    // ---- saves ------------------------------------------------------------------------------------------------------------------------

    private static IEnumerable<(string Code, string Name, Func<byte[]> Save)> Saves()
    {
        var good = RelationshipSave.Write(Tamper.Played());
        yield return (RelationshipCodes.SaveFormat, "garbage", () => "not json"u8.ToArray());
        yield return (RelationshipCodes.SaveFormat, "format 3", () => Tamper.Forge(good, i => i.Format = 3));
        yield return (RelationshipCodes.SaveOldFormat, "format 1", () => Tamper.Forge(good, i => i.Format = 1));
        yield return (RelationshipCodes.SaveFormat, "missing field", () => System.Text.Encoding.UTF8.GetBytes(JsonNode.Parse(good)!.AsObject().Also(o => o.Remove("Rolls")).ToJsonString()));
        yield return (RelationshipCodes.SaveDocument, "other document", () => Tamper.Forge(good, i => i.Document = "0000"));
        yield return (RelationshipCodes.SaveDigest, "stale digest", () => Tamper.Stale(good, i => i.Rolls += 1));
        yield return (RelationshipCodes.SaveState, "negative step", () => Tamper.Forge(good, i => i.Step = -1));
        yield return (RelationshipCodes.SaveState, "symmetric bond one way", () => Tamper.Forge(good, i => i.Actors.First(a => a.Id == "dan").Edges.Clear()));
    }

    [Fact]
    public void EveryBadSaveIsRefusedWithItsCode()
    {
        foreach (var (code, name, save) in Saves())
        {
            var bytes = save();
            var found = Fixture.Refusal(() => RelationshipSave.Open(Fixture.Village, bytes), name);
            Assert.True(code == found, name + ": expected " + code + " but got " + found);
        }
    }

    [Fact]
    public void TheMatrixReachesExactlyTheCodesTheEngineDeclares()
    {
        var declared = typeof(RelationshipCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f is { IsLiteral: true } && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!).ToHashSet();
        var reached = Documents().Select(d => d.Code).Concat(Calls().Select(c => c.Code)).Concat(Saves().Select(s => s.Code)).ToHashSet();
        Assert.Equal(declared.OrderBy(c => c, StringComparer.Ordinal), reached.OrderBy(c => c, StringComparer.Ordinal));
    }
}

internal static class JsonObjectExtensions
{
    public static JsonObject Also(this JsonObject node, Action<JsonObject> act)
    {
        act(node);
        return node;
    }
}
