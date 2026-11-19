using System.Text.Json;
using System.Text.Json.Nodes;

namespace DialogueEngine.Tests;

/// <summary>
/// A seeded generator of conversation graphs. Each document it builds is valid by construction: every node is reached from the start through a tree edge, and every other edge is either
/// forward (to a later node) or leaves a node that waits for the player, so no cycle can run through nodes that never wait. A defect can then be put into one document and the loader must
/// name exactly that defect. The random numbers are test data only; the engine under test rolls nothing here.
/// </summary>
public sealed class GraphGenerator
{
    private readonly Random _random;
    private readonly List<JsonObject> _nodes = new();
    private readonly List<List<(JsonObject Holder, string Field)>> _slots = new();
    private readonly List<bool> _waits = new();
    private int _keys;

    public GraphGenerator(int seed) => _random = new Random(seed);

    public JsonObject Document { get; private set; } = null!;

    /// <summary>Every place a node id is written as a target, with the node it leaves: for the tests that break one reference.</summary>
    public List<(int Source, JsonObject Holder, string Field)> Edges { get; } = new();

    public JsonObject Conversation => (JsonObject)((JsonArray)Document["conversations"]!)[0]!;

    public JsonArray Nodes => (JsonArray)Conversation["nodes"]!;

    public int Size => _nodes.Count;

    public static string Id(int index) => "n" + index.ToString("000");

    public int Next(int below) => _random.Next(below);

    private string NextKey() => "k" + (_keys++).ToString("000");

    private JsonObject Guard()
    {
        var comparison = _random.Next(3) switch { 0 => "gte", 1 => "lte", _ => "eq" };
        return new JsonObject { ["fact"] = "var.trust", ["op"] = comparison, ["value"] = _random.Next(0, 6) };
    }

    private JsonArray Effects()
    {
        JsonNode effect = _random.Next(3) switch
        {
            0 => new JsonObject { ["add"] = "trust", ["by"] = 1 },
            1 => new JsonObject { ["set"] = "briefed", ["value"] = true },
            _ => new JsonObject { ["signal"] = "sig-" + _random.Next(4) },
        };
        return new JsonArray(effect);
    }

    private JsonObject NewNode(string kind, int index)
    {
        var node = new JsonObject { ["id"] = Id(index), ["kind"] = kind };
        var slots = new List<(JsonObject, string)>();
        var waits = false;
        switch (kind)
        {
            case "line":
                node["speaker"] = "cmdr";
                node["portrait"] = _random.Next(2) == 0 ? "calm" : "grim";
                node["line"] = NextKey();
                node["next"] = "";
                slots.Add((node, "next"));
                waits = true;
                break;
            case "effects":
                node["effects"] = Effects();
                node["next"] = "";
                slots.Add((node, "next"));
                break;
            case "choice":
            {
                var seeded = _random.Next(4) == 0;
                node["pick"] = seeded ? "seeded" : "player";
                var options = new JsonArray();
                var count = _random.Next(1, 4);
                for (var o = 0; o < count; o++)
                {
                    var option = new JsonObject { ["id"] = "o" + o, ["line"] = NextKey() };
                    if (seeded) option["weight"] = _random.Next(1, 9);
                    else if (_random.Next(3) == 0) { option["when"] = Guard(); option["unavailable"] = _random.Next(2) == 0 ? "show" : "hide"; }
                    if (_random.Next(3) == 0) option["effects"] = Effects();
                    option["next"] = "";
                    slots.Add((option, "next"));
                    options.Add(option);
                }
                node["options"] = options;
                if (seeded || _random.Next(3) == 0) { node["fallback"] = ""; slots.Add((node, "fallback")); }
                waits = !seeded;
                break;
            }
            case "branch":
            {
                var cases = new JsonArray();
                var count = _random.Next(1, 3);
                for (var c = 0; c < count; c++)
                {
                    var one = new JsonObject { ["when"] = Guard(), ["next"] = "" };
                    slots.Add((one, "next"));
                    cases.Add(one);
                }
                node["cases"] = cases;
                node["otherwise"] = "";
                slots.Add((node, "otherwise"));
                break;
            }
            case "random":
            {
                var outcomes = new JsonArray();
                var count = _random.Next(1, 4);
                for (var o = 0; o < count; o++)
                {
                    var one = new JsonObject { ["weight"] = _random.Next(1, 9), ["next"] = "" };
                    slots.Add((one, "next"));
                    outcomes.Add(one);
                }
                node["outcomes"] = outcomes;
                break;
            }
            case "end":
                node["result"] = "done";
                break;
        }
        _slots.Add(slots);
        _waits.Add(waits);
        return node;
    }

    /// <summary>A document with <paramref name="size"/> nodes (at least 2) in one conversation, valid by construction.</summary>
    public GraphGenerator Build(int size)
    {
        var kinds = new[] { "line", "line", "choice", "branch", "random", "effects" };
        _nodes.Clear(); _slots.Clear(); _waits.Clear(); Edges.Clear();
        _keys = 0;
        for (var index = 0; index < size - 1; index++) _nodes.Add(NewNode(index == 0 ? "line" : kinds[_random.Next(kinds.Length)], index));
        _nodes.Add(NewNode("end", size - 1));
        // Tree edges: every node but the first hangs on a free edge of an earlier node, so all are reached.
        var filled = new HashSet<(JsonObject, string)>();
        for (var k = 1; k < size; k++)
        {
            var parents = Enumerable.Range(0, k).Where(j => _slots[j].Any(s => !filled.Contains(s))).ToList();
            var parent = parents[_random.Next(parents.Count)];
            var slot = _slots[parent].First(s => !filled.Contains(s));
            slot.Item1[slot.Item2] = Id(k);
            filled.Add(slot);
        }
        // Every other edge goes forward to a later node, or, from a node that waits for the player, anywhere.
        for (var j = 0; j < size; j++)
            foreach (var slot in _slots[j])
            {
                if (!filled.Contains(slot))
                {
                    var backward = _waits[j] && _random.Next(2) == 0;
                    slot.Item1[slot.Item2] = Id(backward ? _random.Next(0, size) : _random.Next(j + 1, size));
                }
                Edges.Add((j, slot.Item1, slot.Item2));
            }
        var array = new JsonArray();
        foreach (var node in _nodes) array.Add(node);
        Document = new JsonObject
        {
            ["format"] = 3,
            ["id"] = "gen.doc",
            ["variables"] = new JsonArray(
                new JsonObject { ["id"] = "trust", ["type"] = "int", ["initial"] = 2, ["min"] = 0, ["max"] = 5 },
                new JsonObject { ["id"] = "briefed", ["type"] = "bool", ["initial"] = false }),
            ["externalFacts"] = new JsonArray(),
            ["speakers"] = new JsonArray(new JsonObject { ["id"] = "cmdr", ["portraits"] = new JsonArray("calm", "grim") }),
            ["quests"] = new JsonArray(),
            ["conversations"] = new JsonArray(new JsonObject { ["id"] = "talk", ["start"] = Id(0), ["nodes"] = array }),
        };
        return this;
    }

    public string Text() => Document.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
}
