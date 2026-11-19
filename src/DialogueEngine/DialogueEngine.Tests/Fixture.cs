using Xunit;
using DialogueEngine;
using ScenarioEngine.Contracts;

namespace DialogueEngine.Tests;

internal sealed class Speakers(params string[] ids) : ISpeakerCatalog
{
    public bool HasSpeaker(string id) => ids.Contains(id);
}

/// <summary>One small document that uses every kind of node, effect and quest rule, and helpers to change one thing in it.</summary>
internal static class Fixture
{
    public static readonly ISpeakerCatalog Catalog = new Speakers("cmdr", "scout");

    public const string Document = """
    {
     "format": 3, "id": "demo.briefing",
     "variables": [
      { "id": "trust", "type": "int", "initial": 2, "min": 0, "max": 5 },
      { "id": "briefed", "type": "bool", "initial": false },
      { "id": "stance", "type": "string", "initial": "neutral", "allowed": ["neutral", "bold"] }
     ],
     "externalFacts": [ { "id": "hold-status", "type": "string" }, { "id": "squads", "type": "int", "min": 0, "max": 1000 } ],
     "speakers": [ { "id": "cmdr", "portraits": ["calm", "grim"] }, { "id": "scout", "portraits": ["calm"] } ],
     "quests": [ { "id": "hold", "title": "quest.hold.title", "start": "order", "stages": [
        { "id": "order", "journal": "quest.hold.order", "advance": [ { "when": { "fact": "ext.hold-status", "op": "eq", "value": "completed" }, "to": "done" } ] },
        { "id": "done", "journal": "quest.hold.done", "advance": [], "ends": "completed" } ] } ],
     "conversations": [ { "id": "briefing", "start": "hello", "nodes": [
       { "id": "hello", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "brief.hello", "args": { "count": "ext.squads" }, "next": "ask" },
       { "id": "ask", "kind": "choice", "pick": "player", "options": [
          { "id": "accept", "line": "brief.accept", "effects": [ { "startQuest": "hold" }, { "add": "trust", "by": 1 }, { "signal": "order-accepted" } ], "next": "rand" },
          { "id": "refuse", "line": "brief.refuse", "effects": [ { "set": "stance", "value": "bold" } ], "next": "bye" },
          { "id": "secret", "line": "brief.secret", "when": { "fact": "var.trust", "op": "gte", "value": 4 }, "unavailable": "show", "next": "bye" } ] },
       { "id": "rand", "kind": "random", "outcomes": [ { "weight": 1, "next": "lucky" }, { "weight": 1, "next": "plain" } ] },
       { "id": "lucky", "kind": "line", "speaker": "scout", "portrait": "calm", "line": "brief.lucky", "next": "fin" },
       { "id": "plain", "kind": "line", "speaker": "cmdr", "portrait": "grim", "line": "brief.plain", "next": "fin" },
       { "id": "fin", "kind": "effects", "effects": [ { "set": "briefed", "value": true } ], "next": "check" },
       { "id": "check", "kind": "branch", "cases": [ { "when": { "fact": "var.briefed", "op": "eq", "value": true }, "next": "bye" } ], "otherwise": "bye" },
       { "id": "bye", "kind": "end", "result": "done" } ] } ]
    }
    """;

    public static DialogueSet Load(string? document = null) => DialogueLoader.Load(document ?? Document, Catalog);

    /// <summary>The fixture with one piece of text replaced; the replacement must happen, so a test cannot pass by changing nothing.</summary>
    public static string With(string old, string replacement, string? document = null)
    {
        var text = document ?? Document;
        if (!text.Contains(old, StringComparison.Ordinal)) throw new InvalidOperationException("the fixture does not contain: " + old);
        return text.Replace(old, replacement, StringComparison.Ordinal);
    }

    public static IReadOnlyDictionary<string, FactValue> Facts(long squads = 7, string hold = "active") => new Dictionary<string, FactValue>
    {
        ["ext.squads"] = FactValue.From(squads),
        ["ext.hold-status"] = FactValue.From(hold),
    };

    public static string Refusal(Action action) => Assert.Throws<EngineCommon.EngineRefusedException>(action).Code;

    /// <summary>Plays a whole conversation, taking the named answer at the choice, and writes down everything the host would see.</summary>
    public static List<string> Play(DialogueRuntime runtime, string answer = "accept", IReadOnlyDictionary<string, FactValue>? facts = null)
    {
        facts ??= Facts();
        var seen = new List<string>();
        var step = runtime.Start("briefing", facts);
        for (var guard = 0; guard < 50; guard++)
        {
            foreach (var e in step.Events) seen.Add("event " + e);
            switch (step.Kind)
            {
                case StepKind.Line:
                    seen.Add("line " + step.Line!.Node + " " + step.Line.Speaker + "/" + step.Line.Portrait + " " + step.Line.Key + " " + string.Join(",", step.Line.Args.Select(a => a.Name + "=" + a.Value.Integer)));
                    step = runtime.Next(facts);
                    break;
                case StepKind.Choices:
                    seen.Add("choices " + string.Join(",", step.Choices!.Options.Select(o => o.Id + (o.Enabled ? "" : "!"))));
                    step = runtime.Choose(answer, facts);
                    break;
                case StepKind.Ended:
                    seen.Add("ended " + step.Result);
                    return seen;
            }
        }
        throw new InvalidOperationException("the conversation did not end");
    }
}