using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>The sample document, a plain copy of it the tests edit, and the helpers they share.</summary>
internal static class Fixture
{
    public static string Text(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", name));

    public static readonly string IslesText = Text("isles.diplomacy.json");
    public static readonly DipSet Isles = DipLoader.Load(IslesText);

    public static IReadOnlyDictionary<string, FactValue> Host(long crisis = 2) => new Dictionary<string, FactValue> { ["game.crisis"] = FactValue.From(crisis) };

    /// <summary>The code of the refusal an action ends in; fails the test when it does not refuse.</summary>
    public static string Refusal(Action action) => Assert.Throws<EngineRefusedException>(action).Code;

    public static string Refusal(Action action, string name)
    {
        try { action(); }
        catch (EngineRefusedException refusal) { return refusal.Code; }
        throw new Xunit.Sdk.XunitException(name + " was not refused");
    }

    public static JsonObject Root() => JsonNode.Parse(IslesText)!.AsObject();

    /// <summary>
    /// The sample with nothing happening by itself and every effect isolated: no succession, no decay of standing or knowledge, no drift of contentment or legitimacy, a stable order, no war weariness, no regard from ideology,
    /// leaders, trust or honour, and a balance that never wakes. A test turns on exactly what it examines.
    /// </summary>
    public static JsonObject PlainRoot()
    {
        var root = Root();
        foreach (var realm in root["realms"]!.AsArray())
        {
            realm!["successionPpm"] = 0;
            realm["traits"] = new JsonArray();
        }
        var rules = root["rules"]!.AsObject();
        foreach (var key in new[] { "ideology", "leader", "trust", "honour", "coalition" }) rules["regard"]![key] = 0;
        foreach (var key in new[] { "trustDecayPpm", "fearDecayPpm", "honourDecayPpm", "infamyDecayPpm" }) rules["standing"]![key] = 0;
        rules["war"]!["exhaustPpm"] = 0;
        rules["balance"]!["interval"] = 100000;
        foreach (var key in new[] { "inertiaPpm", "driftPpm", "legitimacyDriftPpm" }) rules["politics"]![key] = 0;
        rules["espionage"]!["intelDecayPpm"] = 0;
        rules["espionage"]!["counterDecayPpm"] = 0;
        return root;
    }

    public static DipSet Plain(Action<JsonObject>? edit = null)
    {
        var root = PlainRoot();
        edit?.Invoke(root);
        return DipLoader.Load(root.ToJsonString());
    }

    public static DipRuntime PlainWorld(Action<JsonObject>? edit = null) => DipRuntime.Create(Plain(edit));

    public static DipRuntime IslesWorld() => DipRuntime.Create(Isles);

    public static DipSet Edited(Action<JsonObject> edit)
    {
        var root = Root();
        edit(root);
        return DipLoader.Load(root.ToJsonString());
    }

    /// <summary>Lives steps up to <paramref name="to"/> one call each on the document's own seasons and returns everything that happened.</summary>
    public static List<DipChange> Live(DipRuntime runtime, long to, IReadOnlyDictionary<string, FactValue>? host = null)
    {
        var changes = new List<DipChange>();
        for (var step = runtime.Step + 1; step <= to; step++) changes.AddRange(runtime.Advance(step, runtime.CycleSeasonAt, host ?? Host()).Items);
        return changes;
    }

    public static List<DipChange> Of(this IEnumerable<DipChange> changes, ChangeKind kind) => changes.Where(c => c.Kind == kind).ToList();

    public static List<DipChange> Of(this DipChanges changes, ChangeKind kind) => changes.Items.Where(c => c.Kind == kind).ToList();

    public static int R(DipSet set, string realm) => set.RealmById[realm].Index;

    public static int Standing(DipRuntime runtime, string realm, string axis)
    {
        var state = runtime.Realms[runtime.Set.RealmById[realm].Index];
        return (int)((axis switch { "trust" => state.Trust, "fear" => state.Fear, "honour" => state.Honour, "infamy" => state.Infamy, _ => 0 }) / 1000);
    }

    /// <summary>Signs a treaty of the type at once (an imposed type, or a mutual one the target accepts) and returns its state.</summary>
    public static TreatyState Treaty(DipRuntime runtime, string from, string to, string type, int days = 0, long amount = 0)
    {
        var def = runtime.Set.TypeById[type];
        var before = runtime.Treaties.Count;
        runtime.Propose(from, to, type, days, amount);
        if (runtime.Treaties.Count == before)
        {
            var proposal = runtime.Proposals.Last();
            if (runtime.Set.Realms[proposal.To].Control == Control.Host) runtime.Respond(proposal.Id, true);
            else
                for (var i = 0; i <= def.Consider && runtime.Treaties.Count == before; i++) Live(runtime, runtime.Step + 1);
        }
        return runtime.Treaties.Last(t => t.Type == def.Index);
    }

    public static JsonObject Type(JsonObject root, string id) => root["types"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);
    public static JsonObject Realm(JsonObject root, string id) => root["realms"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);
    public static JsonObject Cb(JsonObject root, string id) => root["casusBelli"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);
    public static JsonObject Mission(JsonObject root, string id) => root["missions"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);
    public static JsonObject Rules(JsonObject root, string name) => root["rules"]![name]!.AsObject();
    public static JsonObject Deed(JsonObject root, string id) => root["deeds"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);

    /// <summary>Moves the entry with this id to the front of a list of the document, so the index it is referred by is 0.</summary>
    public static void First(JsonObject root, string list, string id)
    {
        var array = root[list]!.AsArray();
        var item = array.First(n => n!["id"]!.GetValue<string>() == id)!;
        array.Remove(item);
        array.Insert(0, item);
    }
    public static JsonNode Copy(JsonNode node) => JsonNode.Parse(node.ToJsonString())!;
    public static JsonObject Cond(string fact, string op, JsonNode value) => new() { ["fact"] = fact, ["op"] = op, ["value"] = value };

    /// <summary>Makes every realm decided by the engine, so that every offer is answered by its acceptance score.</summary>
    public static void AllAi(JsonObject root)
    {
        foreach (var realm in root["realms"]!.AsArray()) realm!["control"] = "ai";
    }

    /// <summary>Gives the type a weight table and bias that make every proposal certain to be accepted (or refused when <paramref name="accept"/> is false).</summary>
    public static void Decided(JsonObject root, string type, bool accept)
    {
        var def = Type(root, type);
        def["bias"] = accept ? 5000 : -5000;
        def["weights"] = new JsonObject();
        def["modifiers"] = new JsonArray();
    }
}

/// <summary>Builds saves that are wrong in one place: with the digest left stale, or recomputed so only the state rules can catch the forgery.</summary>
internal static class Tamper
{
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    /// <summary>A world part way through its life: a vassal treaty whose payment is waiting, an alliance, a war with a call to arms owed, an offer waiting, a mission away, a coalition, a granted casus belli and some deeds.</summary>
    public static DipRuntime Played(out DipSet set)
    {
        set = Fixture.Plain(root =>
        {
            Fixture.Realm(root, "cinder")["control"] = "host";
            Fixture.Rules(root, "balance")["interval"] = 5;
            Fixture.Rules(root, "balance")["threatLine"] = 100;
            Fixture.Rules(root, "balance")["relaxLine"] = 50;
            Fixture.Rules(root, "balance")["joinRegard"] = 1000;
            Fixture.Rules(root, "balance")["bandwagonFear"] = 1000;
            Fixture.Rules(root, "war")["exhaustPpm"] = 5000;
            Fixture.Rules(root, "war")["callBase"] = 0;
            Fixture.Decided(root, "vassalage", true);
            Fixture.Decided(root, "alliance", true);
        });
        var runtime = DipRuntime.Create(set);
        Fixture.Live(runtime, 2);
        Fixture.Treaty(runtime, "brass", "dune", "vassalage", 0, 5000);
        Fixture.Treaty(runtime, "aurel", "ember", "alliance");
        runtime.Remember("aurel", "brass", "gift", 500);
        runtime.GrantCasusBelli("dune", "ember", "border", 400);
        while (!runtime.Treaties.First(t => runtime.Set.Types[t.Type].Id == "vassalage").Awaiting) Fixture.Live(runtime, runtime.Step + 1);
        runtime.Propose("aurel", "cinder", "nap", 50, 0);
        runtime.StartMission("agent-brass-2", "aurel", "spy");
        runtime.DeclareWar("cinder", "ember", "");
        return runtime;
    }

    public static DipState Image(byte[] save) => JsonSerializer.Deserialize<DipState>(save, Options)!;

    /// <summary>The save with the edit applied to its state and the digest recomputed, so only the rules about the state can refuse it.</summary>
    public static byte[] Forge(byte[] save, Action<DipState> edit)
    {
        var image = Image(save);
        edit(image);
        image.Digest = EngineCommon.Digest.Of(DipSave.Canonical(image));
        return JsonSerializer.SerializeToUtf8Bytes(image, Options);
    }

    /// <summary>The save with the edit applied and the old digest left, so only the digest can refuse it.</summary>
    public static byte[] Stale(byte[] save, Action<DipState> edit)
    {
        var image = Image(save);
        edit(image);
        return JsonSerializer.SerializeToUtf8Bytes(image, Options);
    }
}
