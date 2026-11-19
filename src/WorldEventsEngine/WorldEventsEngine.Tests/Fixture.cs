using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>The sample document, the small document the tests edit, and the helpers they share.</summary>
internal static class Fixture
{
    public static string Text(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", name));

    public static readonly string ValleyText = Text("valley.world.json");
    public static readonly WorldSet Valley = WorldLoader.Load(ValleyText);

    public static IReadOnlyDictionary<string, FactValue> Host(long alert = 0) => new Dictionary<string, FactValue> { ["ext.alert"] = FactValue.From(alert) };

    public static IReadOnlyDictionary<string, FactValue> ValleyHost(long garrison = 30) => new Dictionary<string, FactValue> { ["game.garrison"] = FactValue.From(garrison) };

    /// <summary>The code of the refusal an action ends in; fails the test when it does not refuse.</summary>
    public static string Refusal(Action action) => Assert.Throws<EngineRefusedException>(action).Code;

    /// <summary>The same, naming the case in the failure.</summary>
    public static string Refusal(Action action, string name)
    {
        try { action(); }
        catch (EngineRefusedException refusal) { return refusal.Code; }
        throw new Xunit.Sdk.XunitException(name + " was not refused");
    }

    public static WorldSet Mini(Action<JsonObject>? edit = null)
    {
        var root = Tiny.Root();
        edit?.Invoke(root);
        return WorldLoader.Load(root.ToJsonString());
    }

    public static WorldRuntime MiniWorld(Action<JsonObject>? edit = null) => WorldRuntime.Create(Mini(edit));

    /// <summary>Lives steps <paramref name="from"/> to <paramref name="to"/> one call each on the document's own seasons and returns everything that happened.</summary>
    public static List<WorldChange> Live(WorldRuntime runtime, long to, IReadOnlyDictionary<string, FactValue>? host = null)
    {
        var changes = new List<WorldChange>();
        for (var step = runtime.Step + 1; step <= to; step++) changes.AddRange(runtime.Advance(step, runtime.CycleSeasonAt, host ?? Host()).Items);
        return changes;
    }

    public static List<WorldChange> Of(this List<WorldChange> changes, ChangeKind kind) => changes.Where(c => c.Kind == kind).ToList();

    public static string Season(long step) => step % 20 < 10 ? "warm" : "cold";
}

/// <summary>A small document with every kind of thing in it once (samples/tiny.world.json), read as a tree so a test can change one place.</summary>
internal static class Tiny
{
    public static JsonObject Root() => JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", "tiny.world.json")))!.AsObject();

    public static JsonObject Hazard(JsonObject root) => root["hazards"]!.AsArray()[0]!.AsObject();
    public static JsonObject Disease(JsonObject root) => root["diseases"]!.AsArray()[0]!.AsObject();
    public static JsonObject Card(JsonObject root, string id) => root["cards"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);
    public static JsonObject Region(JsonObject root, string id) => root["regions"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);
    public static JsonObject Rung(JsonObject root, int index) => Hazard(root)["ladder"]!.AsArray()[index]!.AsObject();
    public static JsonObject Teller(JsonObject root, string id) => root["storytellers"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);
    public static JsonObject Zone(JsonObject root) => root["zones"]!.AsArray()[0]!.AsObject();
    public static JsonArray Array(JsonObject root, string name) => root[name]!.AsArray();

    /// <summary>A copy of a node, so a document can repeat an entry.</summary>
    public static JsonNode Copy(JsonNode node) => JsonNode.Parse(node.ToJsonString())!;

    public static JsonObject Cond(string fact, string op, JsonNode value) => new() { ["fact"] = fact, ["op"] = op, ["value"] = value };

    /// <summary>Makes the hazard start by itself every step it can (a rate of certainty), at the first rung unless the tail edits say otherwise.</summary>
    public static void Certain(JsonObject root) => Hazard(root)["ratePpm"] = 1_000_000;
}

/// <summary>Builds saves that are wrong in one place: with the digest left stale, or recomputed so only the state rules can catch the forgery.</summary>
internal static class Tamper
{
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    /// <summary>A world part way through its life on the small document: a hazard going, an outbreak under way with an intervention, an agenda entry and a timed modifier.</summary>
    public static WorldRuntime Played(out WorldSet set)
    {
        set = Fixture.Mini(root =>
        {
            Tiny.Certain(root);
            Tiny.Disease(root)["seedPpm"] = 1_000_000;
            Tiny.Disease(root)["seedCases"] = 20;
        });
        var runtime = WorldRuntime.Create(set);
        Fixture.Live(runtime, 6);
        runtime.PlayCard("c-bad", "a");
        runtime.Intervene("a", "quar", true);
        return runtime;
    }

    public static WorldImage Image(byte[] save) => JsonSerializer.Deserialize<WorldImage>(save, Options)!;

    /// <summary>The save with the edit applied to its state and the digest recomputed, so only the rules about the state can refuse it.</summary>
    public static byte[] Forge(byte[] save, Action<WorldImage> edit)
    {
        var image = Image(save);
        edit(image);
        image.Digest = EngineCommon.Digest.Of(WorldImage.Canonical(image));
        return JsonSerializer.SerializeToUtf8Bytes(image, Options);
    }

    /// <summary>The save with the edit applied and the old digest left, so only the digest can refuse it.</summary>
    public static byte[] Stale(byte[] save, Action<WorldImage> edit)
    {
        var image = Image(save);
        edit(image);
        return JsonSerializer.SerializeToUtf8Bytes(image, Options);
    }
}
