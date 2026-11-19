using DialogueEngine;
using ScenarioEngine.Contracts;

namespace DialogueEngine.Tests;

/// <summary>A document that uses the features added in format 2: subflows, interruptions, variants, tags, voice and emotion ids, and a timed choice.</summary>
internal static class FeatureFixture
{
    public static readonly IReadOnlyDictionary<string, FactValue> NoFacts = new Dictionary<string, FactValue>();

    public const string Document = """
    {
     "format": 3, "id": "demo.features",
     "variables": [ { "id": "mood", "type": "int", "initial": 0, "min": 0, "max": 9 } ],
     "externalFacts": [ { "id": "alarm", "type": "bool" } ],
     "speakers": [ { "id": "cmdr", "portraits": ["calm"], "emotions": ["angry", "happy"] } ],
     "quests": [],
     "conversations": [
      { "id": "main", "start": "hi", "nodes": [
        { "id": "hi", "kind": "line", "speaker": "cmdr", "portrait": "calm", "emotion": "happy", "voice": "vo-hi", "tags": ["greeting", "loud"], "autoAdvanceMs": 1500, "line": "main.hi", "next": "sub" },
        { "id": "sub", "kind": "call", "conversation": "detour", "next": "ask" },
        { "id": "ask", "kind": "choice", "pick": "player", "timeout": { "afterMs": 5000, "option": "stay" }, "options": [
           { "id": "stay", "line": "main.stay", "tags": ["quiet"], "next": "var" },
           { "id": "leave", "line": "main.leave", "next": "bye" },
           { "id": "shout", "line": "main.shout", "when": { "fact": "var.mood", "op": "gte", "value": 3 }, "unavailable": "show", "next": "bye" },
           { "id": "sneak", "line": "main.sneak", "when": { "fact": "ext.alarm", "op": "eq", "value": true }, "next": "bye" } ] },
        { "id": "var", "kind": "variant", "mode": "cycle", "options": ["va", "vb", "vc"] },
        { "id": "va", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "main.va", "next": "ask" },
        { "id": "vb", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "main.vb", "next": "ask" },
        { "id": "vc", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "main.vc", "next": "ask" },
        { "id": "bye", "kind": "end", "result": "done" } ] },
      { "id": "detour", "start": "d1", "nodes": [
        { "id": "d1", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "detour.d1", "next": "d2" },
        { "id": "d2", "kind": "effects", "effects": [ { "add": "mood", "by": 1 } ], "next": "back" },
        { "id": "back", "kind": "return", "result": "back" } ] },
      { "id": "alarm", "start": "a1", "nodes": [
        { "id": "a1", "kind": "line", "speaker": "cmdr", "portrait": "calm", "emotion": "angry", "line": "alarm.a1", "next": "aback" },
        { "id": "aback", "kind": "return", "result": "ok" } ] },
      { "id": "quit", "start": "q1", "nodes": [
        { "id": "q1", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "quit.q1", "next": "qend" },
        { "id": "qend", "kind": "end", "result": "quit" } ] }
     ]
    }
    """;

    public static DialogueSet Load(string? document = null) => DialogueLoader.Load(document ?? Document, Fixture.Catalog);

    public static string With(string old, string replacement) => Fixture.With(old, replacement, Document);

    public static IReadOnlyDictionary<string, FactValue> Facts(bool alarm = false) => new Dictionary<string, FactValue> { ["ext.alarm"] = FactValue.From(alarm) };

    /// <summary>A conversation that goes round a variant of three lines: after each line the player may ask for it again (the variant is reached once more) or stop.</summary>
    public static string VariantDocument(string mode, bool withExhausted = false)
    {
        var spent = withExhausted ? """{ "id": "spent", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "v.spent", "next": "gate" },""" : "";
        var exhausted = withExhausted ? """, "exhausted": "spent" """ : "";
        return $$"""
        {
         "format": 3, "id": "demo.variant",
         "variables": [], "externalFacts": [],
         "speakers": [ { "id": "cmdr", "portraits": ["calm"] } ], "quests": [],
         "conversations": [ { "id": "loop", "start": "v", "nodes": [
            { "id": "v", "kind": "variant", "mode": "{{mode}}", "options": ["a", "b", "c"]{{exhausted}} },
            { "id": "a", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "v.a", "next": "gate" },
            { "id": "b", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "v.b", "next": "gate" },
            { "id": "c", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "v.c", "next": "gate" },
            {{spent}}
            { "id": "gate", "kind": "choice", "pick": "player", "options": [
               { "id": "again", "line": "v.again", "next": "v" }, { "id": "stop", "line": "v.stop", "next": "fin" } ] },
            { "id": "fin", "kind": "end", "result": "done" } ] } ]
        }
        """;
    }
    /// <summary>The lines a variant conversation shows over <paramref name="rounds"/> rounds of asking again.</summary>
    public static List<string> Rounds(DialogueRuntime runtime, int rounds)
    {
        var shown = new List<string>();
        var step = runtime.Start("loop", NoFacts);
        for (var i = 0; i < rounds; i++)
        {
            shown.Add(step.Line!.Node);
            step = runtime.Next(NoFacts);
            step = runtime.Choose("again", NoFacts);
        }
        return shown;
    }
}
