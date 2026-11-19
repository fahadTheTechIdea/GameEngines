using DialogueEngine;
using ScenarioEngine.Contracts;

namespace GameRuntime.Tutorial.Headless;

/// <summary>What the demo's game keeps that the tutorial reads as host facts: the input device, whether a menu is open, whether the game is paused and how many people live in the valley.</summary>
public sealed class ValleyGame
{
    public bool Gamepad { get; set; }
    public bool MenuOpen { get; set; }
    public bool Paused { get; set; }
    public long Population { get; set; }

    public IReadOnlyDictionary<string, FactValue> Facts() => new Dictionary<string, FactValue>(StringComparer.Ordinal)
    {
        ["input.gamepad"] = FactValue.From(Gamepad), ["ui.menu_open"] = FactValue.From(MenuOpen), ["game.paused"] = FactValue.From(Paused), ["game.population"] = FactValue.From(Population),
    };
}

/// <summary>The one speaker of the demo's conversation.</summary>
internal sealed class Mayor : ISpeakerCatalog
{
    public bool HasSpeaker(string id) => id == "mayor";
}

/// <summary>The conversation the mayor speaks when the "talk" step begins. It greets differently once the player is more than half way through the tutorial, reading the tutorial's progress as an external fact.</summary>
internal static class MayorTalk
{
    public const string Document = """
    { "format": 3, "id": "valley.mayor", "variables": [],
      "externalFacts": [ { "id": "tutorial.progress", "type": "int", "min": 0, "max": 1000 } ],
      "speakers": [ { "id": "mayor", "portraits": [ "calm" ] } ],
      "quests": [],
      "conversations": [ { "id": "mayor-intro", "start": "check", "nodes": [
        { "id": "check", "kind": "branch", "cases": [ { "when": { "fact": "ext.tutorial.progress", "op": "gte", "value": 500 }, "next": "halfway" } ], "otherwise": "hello" },
        { "id": "hello", "kind": "line", "speaker": "mayor", "portrait": "calm", "line": "mayor.hello", "next": "needs" },
        { "id": "halfway", "kind": "line", "speaker": "mayor", "portrait": "calm", "line": "mayor.halfway", "next": "needs" },
        { "id": "needs", "kind": "line", "speaker": "mayor", "portrait": "calm", "line": "mayor.needs", "next": "bye" },
        { "id": "bye", "kind": "end", "result": "done" } ] } ] }
    """;

    public const string Messages = """
    { "format": 1, "locale": "en", "messages": {
      "mayor.hello": "Welcome, neighbour. The valley has been waiting for you.",
      "mayor.halfway": "You are already well on your way. The valley is lucky to have you.",
      "mayor.needs": "We need more hands, more roofs and a market that works. Start where you like." } }
    """;
}
