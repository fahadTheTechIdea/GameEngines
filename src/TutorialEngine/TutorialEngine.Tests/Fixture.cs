using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace TutorialEngine.Tests;

/// <summary>The sample document, the small document the tests edit, and the helpers they share.</summary>
internal static class Fixture
{
    public static string Text(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", name));

    public static readonly string ValleyText = Text("valley.tutorial.json");
    public static readonly TutorialSet Valley = TutorialLoader.Load(ValleyText);

    public static IReadOnlyDictionary<string, FactValue> Host(bool flag = false, long level = 0) => new Dictionary<string, FactValue>
    {
        ["flag"] = FactValue.From(flag), ["level"] = FactValue.From(level),
    };

    public static IReadOnlyDictionary<string, FactValue> ValleyHost(bool gamepad = false, bool menu = false, bool paused = false, long population = 0) => new Dictionary<string, FactValue>
    {
        ["input.gamepad"] = FactValue.From(gamepad), ["ui.menu_open"] = FactValue.From(menu), ["game.paused"] = FactValue.From(paused), ["game.population"] = FactValue.From(population),
    };

    /// <summary>The code of the refusal an action ends in; fails the test when it does not refuse.</summary>
    public static string Refusal(Action action) => Assert.Throws<EngineRefusedException>(action).Code;

    /// <summary>The same, naming the case in the failure.</summary>
    public static string Refusal(Action action, string name)
    {
        try { action(); }
        catch (EngineRefusedException refusal) { return refusal.Code; }
        throw new Xunit.Sdk.XunitException(name + " was not refused");
    }

    public static TutorialSet Mini(Action<JsonObject>? edit = null)
    {
        var root = Tiny.Root();
        edit?.Invoke(root);
        return TutorialLoader.Load(root.ToJsonString());
    }

    /// <summary>A runtime whose first-run journey is done and whose tutorial is running, on the small document.</summary>
    public static TutorialRuntime Ready(TutorialSet? set = null, long step = 0)
    {
        var runtime = TutorialRuntime.Create(set ?? Mini(), step);
        runtime.SelectLanguage("en");
        while (runtime.Journey.State == JourneyState.Story) runtime.StoryNext();
        runtime.ChooseEntry("host");
        runtime.Start();
        return runtime;
    }

    public static string Status(TutorialRuntime runtime, string step) => TutorialFacts.StatusName(runtime.StateOf(step).Status);

    public static StepState StateOf(this TutorialRuntime runtime, string step) => runtime.StepState(step);
}

/// <summary>A small document with every kind of thing in it once, written as a tree so a test can change one place.</summary>
internal static class Tiny
{
    public static JsonObject Root() => JsonNode.Parse("""
    { "format": 1, "id": "tiny", "time": { "stepsPerDay": 100 }, "limits": { "maxActiveSteps": 1, "maxVisibleTips": 2, "tipGap": 0 },
      "facts": [ { "id": "flag", "type": "bool" }, { "id": "level", "type": "int", "min": 0, "max": 100 } ],
      "events": [ { "id": "e1" }, { "id": "e2" }, { "id": "e3" }, { "id": "e4" } ],
      "inputs": [ { "action": "press.one", "event": "e1" } ],
      "skills": [ { "id": "sk", "key": "k.sk", "mastered": 3, "evidence": [ { "event": "e1", "points": 1 } ] } ],
      "journey": { "languages": [ "en", "fr" ], "defaultLanguage": "en",
        "pages": [ { "id": "p1", "key": "k.p1", "body": "k.p1b", "art": "k.p1a" }, { "id": "p2", "key": "k.p2", "body": "k.p2b", "art": "k.p2a" }, { "id": "p3", "key": "k.p3", "body": "k.p3b", "art": "k.p3a" } ],
        "entries": [ "sign-in", "host", "join" ] },
      "steps": [
        { "id": "s1", "key": "k.s1", "body": "k.s1b", "target": "t1", "motion": "pulse", "skippable": true, "skill": "sk", "requires": [], "requiresAny": [],
          "complete": { "fact": "event.e1.since_step", "op": "gte", "value": 1 },
          "hints": [ { "idle": 10, "kind": "nudge", "key": "k.h1" }, { "idle": 30, "kind": "explain", "key": "k.h2" }, { "idle": 60, "kind": "show", "key": "k.h3" } ] },
        { "id": "s2", "key": "k.s2", "body": "k.s2b", "target": "", "motion": "slide", "skippable": false, "skill": "", "requires": [ "s1" ], "requiresAny": [],
          "complete": { "fact": "event.e2.since_step", "op": "gte", "value": 2 }, "hints": [ { "idle": 5, "kind": "nudge", "key": "k.h4" } ] },
        { "id": "s3", "key": "k.s3", "body": "k.s3b", "target": "", "motion": "none", "skippable": true, "skill": "", "requires": [ "s2" ], "requiresAny": [],
          "complete": { "fact": "flag", "op": "eq", "value": true }, "hints": [] } ],
      "tips": [
        { "id": "t1", "key": "k.t1", "when": { "fact": "flag", "op": "eq", "value": true }, "cooldown": 20, "maxShows": 2, "duration": 10, "priority": 1, "skill": "", "afterStep": "", "motion": "pulse" } ] }
    """)!.AsObject();

    public static JsonObject Step(JsonObject root, string id) => root["steps"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);

    public static JsonObject Tip(JsonObject root, string id) => root["tips"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);

    public static JsonArray Array(JsonObject root, string name) => root[name]!.AsArray();

    /// <summary>A copy of a node, so a document can repeat an entry.</summary>
    public static JsonNode Copy(JsonNode node) => JsonNode.Parse(node.ToJsonString())!;

    public static JsonObject Cond(string fact, string op, JsonNode value) => new() { ["fact"] = fact, ["op"] = op, ["value"] = value };
}

/// <summary>Builds saves that are wrong in one place: with the digest left stale, or recomputed so only the state rules can catch the forgery.</summary>
internal static class Tamper
{
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    /// <summary>A player part way through the small tutorial: journey done, a step done, one being taught with a hint, a tip showing, a skill with points, settings changed.</summary>
    public static TutorialRuntime Played(out TutorialSet set)
    {
        set = Fixture.Mini();
        var runtime = Fixture.Ready(set);
        runtime.Advance(1, Fixture.Host());
        runtime.Observe("e1");
        runtime.Advance(5, Fixture.Host());
        runtime.Advance(40, Fixture.Host(flag: true));       // s2 taught since step 5, idle 35 steps: hint 1; the tip shows
        runtime.SetTextScale(1500);
        runtime.SetReducedMotion(true);
        return runtime;
    }

    public static TutorialImage Image(byte[] save) => JsonSerializer.Deserialize<TutorialImage>(save, Options)!;

    /// <summary>The save with the edit applied to its state and the digest recomputed, so only the rules about the state can refuse it.</summary>
    public static byte[] Forge(byte[] save, Action<TutorialImage> edit)
    {
        var image = Image(save);
        edit(image);
        image.Digest = EngineCommon.Digest.Of(TutorialImage.Canonical(image));
        return JsonSerializer.SerializeToUtf8Bytes(image, Options);
    }

    /// <summary>The save with the edit applied and the old digest left, so only the digest can refuse it.</summary>
    public static byte[] Stale(byte[] save, Action<TutorialImage> edit)
    {
        var image = Image(save);
        edit(image);
        return JsonSerializer.SerializeToUtf8Bytes(image, Options);
    }
}
