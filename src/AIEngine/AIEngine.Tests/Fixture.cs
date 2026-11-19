using AIEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace AIEngine.Tests;

/// <summary>One document that uses every kind of node, and helpers to change one thing in it or to build a small tree on the same declarations.</summary>
internal static class Fixture
{
    public const string Keys = """
      [ { "id": "ready", "type": "bool", "initial": false, "source": "host" },
        { "id": "side", "type": "int", "initial": 1, "min": 0, "max": 9, "source": "host" },
        { "id": "distance", "type": "int", "initial": 100, "min": 0, "max": 100000, "source": "host" },
        { "id": "hunger", "type": "int", "initial": 0, "min": 0, "max": 100, "source": "host" },
        { "id": "foes", "type": "int", "initial": 0, "min": 0, "max": 64, "source": "agent" },
        { "id": "target", "type": "string", "initial": "", "source": "agent" },
        { "id": "phase", "type": "int", "initial": 0, "min": 0, "max": 14, "source": "agent" },
        { "id": "due", "type": "bool", "initial": false, "source": "agent" },
        { "id": "mode", "type": "string", "initial": "idle", "source": "agent" },
        { "id": "small", "type": "int", "initial": 0, "min": 0, "max": 3, "source": "agent" },
        { "id": "spare", "type": "int", "initial": 0, "min": 0, "max": 1000000, "source": "agent" } ]
    """;

    public const string Percepts = """
      [ { "kind": "unit", "capacity": 16, "memory": 50, "owned": false, "attrs": [
            { "id": "hostile", "type": "bool", "unknown": false },
            { "id": "standing", "type": "bool", "unknown": false },
            { "id": "health", "type": "int", "min": 0, "max": 1000, "unknown": 0 } ] },
        { "kind": "stock", "capacity": 8, "ownerCapacity": 4, "memory": 100, "owned": true, "attrs": [
            { "id": "held", "type": "int", "min": 0, "max": 100000, "unknown": 7 } ] } ]
    """;

    public const string Intents = """
      [ { "id": "fire", "args": [ { "id": "attacker", "type": "string" }, { "id": "defender", "type": "string" }, { "id": "metres", "type": "int", "min": 0, "max": 100000 } ] },
        { "id": "rest", "args": [] },
        { "id": "order", "args": [ { "id": "what", "type": "string" }, { "id": "amount", "type": "int", "min": 0, "max": 100000 } ] } ]
    """;

    public const string FoeFilter = """{ "all": [ { "fact": "item.hostile", "op": "eq", "value": true }, { "fact": "item.standing", "op": "eq", "value": true } ] }""";
    public const string OwnFilter = """{ "all": [ { "fact": "item.hostile", "op": "eq", "value": false }, { "fact": "item.standing", "op": "eq", "value": true } ] }""";

    /// <summary>Every think the nth own standing unit fires at the nth hostile standing unit (round the end), but only on a beat of 15 ticks that waits until the host says ready.</summary>
    public static readonly string Commander = $$"""
      { "id": "main", "kind": "sequence", "resume": false, "children": [
        { "id": "set-phase", "kind": "let", "key": "phase", "value": { "op": "mod", "args": [ { "op": "tick" }, 15 ] } },
        { "id": "arm", "kind": "succeed", "child": { "id": "arm-seq", "kind": "sequence", "resume": false, "children": [
            { "id": "on-beat", "kind": "condition", "when": { "fact": "key.phase", "op": "eq", "value": 0 } },
            { "id": "mark-due", "kind": "let", "key": "due", "value": true } ] } },
        { "id": "is-ready", "kind": "condition", "when": { "fact": "key.ready", "op": "eq", "value": true } },
        { "id": "is-due", "kind": "condition", "when": { "fact": "key.due", "op": "eq", "value": true } },
        { "id": "clear-due", "kind": "let", "key": "due", "value": false },
        { "id": "count-foes", "kind": "count", "from": "unit", "into": "foes", "where": {{FoeFilter}} },
        { "id": "has-foes", "kind": "condition", "when": { "fact": "key.foes", "op": "gt", "value": 0 } },
        { "id": "each-own", "kind": "forEach", "from": "unit", "limit": 16, "where": {{OwnFilter}}, "child":
          { "id": "shoot", "kind": "sequence", "resume": false, "children": [
            { "id": "pick-foe", "kind": "select", "from": "unit", "into": "target", "where": {{FoeFilter}},
              "pick": { "nth": { "op": "mod", "args": [ { "item": "index" }, { "op": "max", "args": [ 1, { "key": "foes" } ] } ] } } },
            { "id": "fire-at", "kind": "act", "intent": "fire", "args": { "attacker": { "item": "id" }, "defender": { "key": "target" }, "metres": { "key": "distance" } } } ] } } ] }
    """;

    public const string Worker = """
      { "id": "choose", "kind": "utility", "momentum": 150, "options": [
        { "id": "eat", "weight": 1000, "considerations": [ { "input": { "key": "hunger" }, "min": 0, "max": 100, "curve": { "kind": "linear" } } ],
          "child": { "id": "do-eat", "kind": "act", "intent": "order", "args": { "what": "food", "amount": 1 } } },
        { "id": "work", "weight": 1000, "considerations": [ { "input": { "key": "hunger" }, "min": 0, "max": 100, "curve": { "kind": "linear", "invert": true } } ],
          "child": { "id": "do-work", "kind": "act", "intent": "order", "args": { "what": "work", "amount": 1 } } } ] }
    """;

    /// <summary>A waiting errand, a seeded choice, a cooldown and a belief that changes after acting on it.</summary>
    public const string Scout = """
      { "id": "top", "kind": "selector", "resume": false, "children": [
        { "id": "errand", "kind": "sequence", "resume": true, "children": [
          { "id": "go", "kind": "act", "intent": "order", "args": { "what": "go", "amount": 1 },
            "until": { "fact": "key.ready", "op": "eq", "value": true }, "failWhen": { "fact": "key.hunger", "op": "gte", "value": 90 }, "timeout": 5 },
          { "id": "back", "kind": "act", "intent": "order", "args": { "what": "back", "amount": 1 } } ] },
        { "id": "nap", "kind": "cooldown", "ticks": 20, "child": { "id": "sleep", "kind": "act", "intent": "rest", "args": { } } },
        { "id": "dice", "kind": "random", "options": [
          { "weight": 1, "child": { "id": "heads", "kind": "act", "intent": "order", "args": { "what": "heads", "amount": 1 } } },
          { "weight": 1, "child": { "id": "tails", "kind": "act", "intent": "order", "args": { "what": "tails", "amount": 1 } } } ] },
        { "id": "ration", "kind": "adjust", "from": "stock", "target": "depot-a", "attr": "held", "by": -5 } ] }
    """;

    public static readonly string Document = $$"""
    { "format": 1, "id": "demo.ai",
      "keys": {{Keys}},
      "percepts": {{Percepts}},
      "intents": {{Intents}},
      "behaviors": [
        { "id": "commander", "every": 1, "root": {{Commander}} },
        { "id": "worker", "every": 10, "root": {{Worker}} },
        { "id": "scout", "every": 1, "root": {{Scout}} } ] }
    """;

    /// <summary>The fixture declarations with one behaviour of the given root and think interval, for testing a node on its own.</summary>
    public static string Mini(string root, int every = 1) => $$"""
    { "format": 1, "id": "mini", "keys": {{Keys}}, "percepts": {{Percepts}}, "intents": {{Intents}},
      "behaviors": [ { "id": "t", "every": {{every}}, "root": {{root}} } ] }
    """;

    public static AiSet Load(string? document = null) => AiLoader.Load(document ?? Document);

    public static AiSet LoadMini(string root, int every = 1) => AiLoader.Load(Mini(root, every));

    /// <summary>The fixture with one piece of text replaced; the replacement must happen, so a test cannot pass by changing nothing.</summary>
    public static string With(string old, string replacement, string? document = null)
    {
        var text = document ?? Document;
        Assert.Contains(old, text, StringComparison.Ordinal);
        return text.Replace(old, replacement, StringComparison.Ordinal);
    }

    public static string Refusal(Action action) => Assert.Throws<EngineCommon.EngineRefusedException>(action).Code;

    public static IReadOnlyDictionary<string, FactValue> Unit(bool hostile, bool standing, long health = 100) => new Dictionary<string, FactValue>
    {
        ["hostile"] = FactValue.From(hostile),
        ["standing"] = FactValue.From(standing),
        ["health"] = FactValue.From(health),
    };

    public static void Tell(AiRuntime runtime, string agent, string id, bool hostile, bool standing = true, long health = 100, long tick = 0) =>
        runtime.Perceive(agent, "unit", id, "", Unit(hostile, standing, health), tick);

    /// <summary>Everything a step raised, one line each, for comparing runs.</summary>
    public static string Lines(StepReport report) => string.Join('\n', report.Intents.Select(i => $"{i.Tick} {i.Agent} {i.Node} {i.Name} " + string.Join(',', i.Args.Select(a => a.Name + "=" + Show(a.Value)))));

    public static string Show(FactValue value)
    {
        if (value.Kind == FactKind.Boolean) return value.Boolean ? "true" : "false";
        if (value.Kind == FactKind.Integer) return value.Integer.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return "\"" + value.Text + "\"";
    }
}