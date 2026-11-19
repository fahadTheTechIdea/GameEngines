using System.Text.Json;
using System.Text.Json.Nodes;

namespace DialogueEngine.Tests;

/// <summary>
/// A seeded generator of whole dialogue documents: a main conversation made by <see cref="GraphGenerator"/> with subflows called from it (and returning to it), variants of every mode, two quests with
/// stage rules, branches on external facts, content warnings, and the conversations that meet while one is open (one that queues, one that preempts). Every addition goes on an existing edge or is
/// a forward-only graph of its own, so a document is valid by construction: the loader must accept it. The random numbers are test data only.
/// </summary>
public sealed class RichGraph
{
    private readonly int _seed;
    private readonly Random _random;
    private int _extra;

    public RichGraph(int seed)
    {
        _seed = seed;
        _random = new Random(seed * 31 + 7);
    }

    public string Text { get; private set; } = "";

    public JsonObject Document { get; private set; } = null!;

    public static readonly string[] Starters = ["talk", "bark", "bark2", "alarm"];
    public static readonly string[] Quests = ["q1", "q2"];
    public static readonly string[] Interrupters = ["sub1", "alarm", "bark", "bark2"];

    private string Fresh(string stem) => stem + (_extra++).ToString("000");

    /// <summary>A queueing conversation whose first node reads a host fact, so starting it can be refused for want of that fact after it was taken from the queue.</summary>
    private JsonObject Gated(string id, int priority)
    {
        var nodes = new JsonArray(
            new JsonObject { ["id"] = "g0", ["kind"] = "branch", ["cases"] = new JsonArray(new JsonObject { ["when"] = new JsonObject { ["fact"] = "ext.level", ["op"] = "gte", ["value"] = 2 }, ["next"] = "g1" }), ["otherwise"] = "g2" },
            Line("g1", Fresh("sk"), "g2"),
            Line("g2", Fresh("sk"), "g3", ["cw.b"]),
            End("g3"));
        return new JsonObject { ["id"] = id, ["start"] = "g0", ["priority"] = priority, ["onBusy"] = "queue", ["nodes"] = nodes };
    }

    private static JsonObject Line(string id, string key, string next, string[]? tags = null, string? alternative = null)
    {
        var node = new JsonObject { ["id"] = id, ["kind"] = "line", ["speaker"] = "cmdr", ["portrait"] = "calm", ["line"] = key, ["next"] = next };
        if (tags is not null) node["tags"] = new JsonArray(tags.Select(t => (JsonNode)t).ToArray());
        if (alternative is not null) node["alternative"] = alternative;
        return node;
    }

    private static JsonObject End(string id, string kind = "end") => kind == "end" ? new JsonObject { ["id"] = id, ["kind"] = "end", ["result"] = "done" } : new JsonObject { ["id"] = id, ["kind"] = "return", ["result"] = "back" };

    /// <summary>A small forward-only graph: every edge goes to a later node, so it cannot loop. It ends with an end or a return node.</summary>
    private JsonObject Sub(string id, string? calls, string endKind, bool skipLines, int priority = 0, string? policy = null)
    {
        var size = _random.Next(4, 8);
        var nodes = new List<JsonObject>();
        string Id(int i) => "n" + i;
        for (var i = 0; i < size - 1; i++)
        {
            var kind = i == 0 ? "line" : new[] { "line", "line", "choice", "branch", "variant", "effects", "call" }[_random.Next(7)];
            var first = Id(i + 1);   // the first edge of every node leads to the next node, so all are reached and none can loop
            var later = Id(_random.Next(i + 1, size));
            switch (kind)
            {
                case "line":
                    nodes.Add(Line(Id(i), Fresh("sk"), first, skipLines && _random.Next(3) == 0 ? ["cw.b"] : null));
                    break;
                case "choice":
                    nodes.Add(new JsonObject
                    {
                        ["id"] = Id(i), ["kind"] = "choice", ["pick"] = "player",
                        ["options"] = new JsonArray(
                            new JsonObject { ["id"] = "a", ["line"] = Fresh("sk"), ["next"] = first },
                            new JsonObject { ["id"] = "b", ["line"] = Fresh("sk"), ["next"] = later, ["when"] = new JsonObject { ["fact"] = "ext.flag", ["op"] = "eq", ["value"] = true }, ["unavailable"] = "show" }),
                    });
                    break;
                case "branch":
                    nodes.Add(new JsonObject
                    {
                        ["id"] = Id(i), ["kind"] = "branch",
                        ["cases"] = new JsonArray(new JsonObject { ["when"] = new JsonObject { ["fact"] = "ext.level", ["op"] = "gte", ["value"] = _random.Next(0, 6) }, ["next"] = first }),
                        ["otherwise"] = later,
                    });
                    break;
                case "variant":
                {
                    var mode = new[] { "sequence", "cycle", "once", "shuffle" }[_random.Next(4)];
                    var node = new JsonObject { ["id"] = Id(i), ["kind"] = "variant", ["mode"] = mode, ["options"] = new JsonArray(first, later) };
                    if (mode == "once") node["exhausted"] = later;
                    nodes.Add(node);
                    break;
                }
                case "effects":
                    nodes.Add(new JsonObject { ["id"] = Id(i), ["kind"] = "effects", ["effects"] = new JsonArray(new JsonObject { ["signal"] = "tell", ["args"] = new JsonArray(new JsonObject { ["var"] = "gold" }, new JsonObject { ["int"] = i }) }, new JsonObject { ["add"] = "gold", ["by"] = 1 }), ["next"] = first });
                    break;
                default:
                    nodes.Add(calls is null
                        ? new JsonObject { ["id"] = Id(i), ["kind"] = "effects", ["effects"] = new JsonArray(new JsonObject { ["add"] = "gold", ["by"] = 2 }), ["next"] = first }
                        : new JsonObject { ["id"] = Id(i), ["kind"] = "call", ["conversation"] = calls, ["next"] = first });
                    break;
            }
        }
        nodes.Add(End(Id(size - 1), endKind));
        var conversation = new JsonObject { ["id"] = id, ["start"] = Id(0), ["nodes"] = new JsonArray(nodes.Select(n => (JsonNode)n).ToArray()) };
        if (priority > 0) conversation["priority"] = priority;
        if (policy is not null) conversation["onBusy"] = policy;
        return conversation;
    }

    public RichGraph Build(int size)
    {
        var generator = new GraphGenerator(_seed).Build(size);
        Document = generator.Document;
        var main = generator.Conversation;
        main["priority"] = 1;
        var nodes = generator.Nodes;

        // Subflows: sub2 is a leaf, sub1 may call sub2. Both come back with a return, one of them sometimes ends the whole conversation instead.
        var conversations = (JsonArray)Document["conversations"]!;
        conversations.Add(Sub("sub2", null, _random.Next(4) == 0 ? "end" : "return", skipLines: true));
        conversations.Add(Sub("sub1", "sub2", _random.Next(4) == 0 ? "end" : "return", skipLines: true));
        conversations.Add(Sub("bark", null, "end", skipLines: false, priority: 2, policy: "queue"));
        conversations.Add(Gated("bark2", 3));
        var alarm = Sub("alarm", "sub2", "end", skipLines: false, priority: 5, policy: "preempt");
        if (_random.Next(3) == 0) alarm["interruptible"] = false;
        conversations.Add(alarm);

        // Decorations on edges of the main graph: a call, a variant, a quest effect or a branch on external facts goes in front of the node the edge led to.
        var edges = generator.Edges.ToList();
        foreach (var (source, holder, field) in edges)
        {
            if (_random.Next(4) != 0) continue;
            var target = holder[field]!.GetValue<string>();
            var id = Fresh("x");
            JsonObject added;
            switch (_random.Next(5))
            {
                case 0:
                    added = new JsonObject { ["id"] = id, ["kind"] = "call", ["conversation"] = _random.Next(2) == 0 ? "sub1" : "sub2", ["next"] = target };
                    break;
                case 1:
                {
                    var mode = new[] { "sequence", "cycle", "once", "shuffle" }[_random.Next(4)];
                    var forward = Enumerable.Range(source + 1, size - source - 1).ToList();
                    string Pick() => forward.Count == 0 ? target : GraphGenerator.Id(forward[_random.Next(forward.Count)]);
                    added = new JsonObject { ["id"] = id, ["kind"] = "variant", ["mode"] = mode, ["options"] = new JsonArray(target, Pick(), Pick()) };
                    if (mode == "once") added["exhausted"] = Pick();
                    break;
                }
                case 2:
                {
                    JsonNode effect = _random.Next(5) switch
                    {
                        0 => new JsonObject { ["startQuest"] = "q1" },
                        1 => new JsonObject { ["startQuest"] = "q2" },
                        2 => new JsonObject { ["setStage"] = "q1", ["stage"] = "s2" },
                        3 => new JsonObject { ["setStage"] = "q2", ["stage"] = "b" },
                        _ => new JsonObject { ["finishQuest"] = "q1", ["status"] = "abandoned" },
                    };
                    added = new JsonObject { ["id"] = id, ["kind"] = "effects", ["effects"] = new JsonArray(effect, new JsonObject { ["add"] = "gold", ["by"] = 1 }), ["next"] = target };
                    break;
                }
                case 3:
                {
                    var forward = Enumerable.Range(source + 1, size - source - 1).ToList();
                    var other = forward.Count == 0 ? target : GraphGenerator.Id(forward[_random.Next(forward.Count)]);
                    added = new JsonObject
                    {
                        ["id"] = id, ["kind"] = "branch",
                        ["cases"] = new JsonArray(
                            new JsonObject { ["when"] = new JsonObject { ["fact"] = "ext.flag", ["op"] = "eq", ["value"] = true }, ["next"] = other },
                            new JsonObject { ["when"] = new JsonObject { ["fact"] = "quest.q1.status", ["op"] = "eq", ["value"] = "active" }, ["next"] = target }),
                        ["otherwise"] = target,
                    };
                    break;
                }
                default:
                    added = new JsonObject { ["id"] = id, ["kind"] = "effects", ["effects"] = new JsonArray(new JsonObject { ["signal"] = "note", ["args"] = new JsonArray(new JsonObject { ["text"] = "a;b=c%" }, new JsonObject { ["var"] = "trust" }) }), ["next"] = target };
                    break;
            }
            holder[field] = id;
            nodes.Add(added);
        }

        // Content warnings: some main lines can be replaced, some answers hidden.
        foreach (var node in nodes.OfType<JsonObject>())
        {
            var kind = node["kind"]!.GetValue<string>();
            if (kind == "line" && _random.Next(4) == 0)
            {
                node["tags"] = new JsonArray("cw.a");
                node["alternative"] = node["line"]!.GetValue<string>() + ".safe";
            }
            else if (kind == "choice" && node["options"] is JsonArray options && options.Count > 1 && _random.Next(3) == 0 && options[0] is JsonObject first)
            {
                first["tags"] = new JsonArray("cw.b");
            }
        }

        Document["variables"] = new JsonArray(
            new JsonObject { ["id"] = "trust", ["type"] = "int", ["initial"] = 2, ["min"] = 0, ["max"] = 5 },
            new JsonObject { ["id"] = "briefed", ["type"] = "bool", ["initial"] = false },
            new JsonObject { ["id"] = "gold", ["type"] = "int", ["initial"] = 0, ["min"] = 0, ["max"] = 99 });
        Document["externalFacts"] = new JsonArray(
            new JsonObject { ["id"] = "flag", ["type"] = "bool" },
            new JsonObject { ["id"] = "level", ["type"] = "int", ["min"] = 0, ["max"] = 5 },
            new JsonObject { ["id"] = "cw-a", ["type"] = "bool" },
            new JsonObject { ["id"] = "cw-b", ["type"] = "bool" });
        Document["contentWarnings"] = new JsonArray(
            new JsonObject { ["tag"] = "cw.a", ["fact"] = "cw-a", ["action"] = "replace" },
            new JsonObject { ["tag"] = "cw.b", ["fact"] = "cw-b", ["action"] = "skip" });
        Document["quests"] = new JsonArray(
            new JsonObject
            {
                ["id"] = "q1", ["title"] = "quest.q1.title", ["start"] = "s1",
                ["stages"] = new JsonArray(
                    new JsonObject { ["id"] = "s1", ["journal"] = "quest.q1.s1", ["advance"] = new JsonArray(new JsonObject { ["when"] = new JsonObject { ["fact"] = "var.trust", ["op"] = "gte", ["value"] = 4 }, ["to"] = "s2" }) },
                    new JsonObject { ["id"] = "s2", ["journal"] = "quest.q1.s2", ["advance"] = new JsonArray(new JsonObject { ["when"] = new JsonObject { ["fact"] = "ext.level", ["op"] = "gte", ["value"] = 3 }, ["to"] = "s3" }) },
                    new JsonObject { ["id"] = "s3", ["journal"] = "quest.q1.s3", ["advance"] = new JsonArray(), ["ends"] = "completed" }),
            },
            new JsonObject
            {
                ["id"] = "q2", ["title"] = "quest.q2.title", ["start"] = "a",
                ["stages"] = new JsonArray(
                    new JsonObject { ["id"] = "a", ["journal"] = "quest.q2.a", ["advance"] = new JsonArray(new JsonObject { ["when"] = new JsonObject { ["fact"] = "ext.flag", ["op"] = "eq", ["value"] = true }, ["to"] = "b" }) },
                    new JsonObject { ["id"] = "b", ["journal"] = "quest.q2.b", ["advance"] = new JsonArray(), ["ends"] = "failed" }),
            });
        if (_random.Next(5) != 0) Document["historyLimit"] = _random.Next(0, 13);
        Text = Document.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return this;
    }
}
