using System.Text.Json.Nodes;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>
/// D2, the property test: documents generated from seeds. A valid one must load and play without any failure the engine does not name; the same one with exactly one defect put in
/// must be refused with the code of that defect; and a document damaged at random in its text must load or be refused with an engine code, never fail any other way.
/// </summary>
public sealed class GeneratedGraphTests
{
    private const int Seeds = 250;

    private static GraphGenerator Make(int seed) => new GraphGenerator(seed).Build(2 + new Random(seed * 7919).Next(0, 39));

    private static string Code(string text) => Fixture.Refusal(() => DialogueLoader.Load(text, Fixture.Catalog));

    public static IEnumerable<object[]> SeedList() => Enumerable.Range(1, Seeds).Select(s => new object[] { s });

    private static IEnumerable<JsonObject> AllObjects(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            yield return obj;
            foreach (var property in obj.ToList()) if (property.Value is not null) foreach (var inner in AllObjects(property.Value)) yield return inner;
        }
        else if (node is JsonArray array)
            foreach (var item in array.ToList()) if (item is not null) foreach (var inner in AllObjects(item)) yield return inner;
    }

    // ---- valid documents ----------------------------------------------------------------------------------------------------------------

    private static List<string> Play(DialogueSet set, int seed)
    {
        var random = new Random(seed);
        var runtime = new DialogueRuntime(set, (ulong)seed);
        var facts = new Dictionary<string, FactValue>();
        var seen = new List<string>();
        try
        {
            var step = runtime.Start("talk", facts);
            for (var guard = 0; guard < 300; guard++)
            {
                foreach (var e in step.Events) seen.Add("event " + e);
                switch (step.Kind)
                {
                    case StepKind.Line: seen.Add("line " + step.Line!.Node); step = runtime.Next(facts); break;
                    case StepKind.Choices:
                        var enabled = step.Choices!.Options.Where(o => o.Enabled).ToList();
                        seen.Add("choices " + string.Join(",", step.Choices.Options.Select(o => o.Id + (o.Enabled ? "" : "!"))));
                        step = runtime.Choose(enabled[random.Next(enabled.Count)].Id, facts);
                        break;
                    case StepKind.Ended: seen.Add("end " + step.Result); return seen;
                }
            }
            seen.Add("cut");
        }
        catch (EngineRefusedException refusal) when (refusal.Code == DialogueCodes.NoOption)
        {
            // A choice whose answers are all conditional can have none available and no fallback; the engine says so, and that is a defined outcome.
            seen.Add("refused " + refusal.Code);
        }
        return seen;
    }

    [Theory]
    [MemberData(nameof(SeedList))]
    public void AGeneratedValidDocumentLoadsAndPlaysTheSameEveryTimeWithNoUnnamedFailure(int seed)
    {
        var text = Make(seed).Text();
        var set = DialogueLoader.Load(text, Fixture.Catalog);
        Assert.Equal(Make(seed).Text(), text);                                  // the generator is deterministic
        var first = Play(set, seed);
        Assert.Equal(first, Play(DialogueLoader.Load(text, Fixture.Catalog), seed));
        Assert.NotEmpty(first);
    }

    [Fact]
    public void TheGeneratedDocumentsAreVariedEnoughToMeanSomething()
    {
        var kinds = new HashSet<string>();
        var sizes = new HashSet<int>();
        var ended = 0;
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var generator = Make(seed);
            sizes.Add(generator.Size);
            foreach (var node in generator.Nodes) kinds.Add((string)node!["kind"]!);
            if (Play(DialogueLoader.Load(generator.Text(), Fixture.Catalog), seed).Any(l => l.StartsWith("end ", StringComparison.Ordinal))) ended++;
        }
        Assert.Equal(["branch", "choice", "effects", "end", "line", "random"], kinds.Order().ToArray());
        Assert.True(sizes.Count > 25, "sizes " + sizes.Count);
        Assert.InRange(ended, Seeds / 3, Seeds);                                 // many conversations reach their end, and some loop until cut
    }

    // ---- one defect each ----------------------------------------------------------------------------------------------------------------

    private static bool Dangling(GraphGenerator g) { var (_, holder, field) = g.Edges[g.Next(g.Edges.Count)]; holder[field] = "ghost"; return true; }

    private static bool BadStart(GraphGenerator g) { g.Conversation["start"] = "ghost"; return true; }

    private static bool DuplicateNode(GraphGenerator g) { g.Nodes.Add(g.Nodes[g.Next(g.Nodes.Count)]!.DeepClone()); return true; }

    private static bool Unreachable(GraphGenerator g) { g.Nodes.Add(new JsonObject { ["id"] = "zz-lost", ["kind"] = "end", ["result"] = "done" }); return true; }

    private static bool SilentLoop(GraphGenerator g)
    {
        // A branch that leads back to itself, put in front of the old start: everything stays reachable, but this node never waits.
        g.Nodes.Add(new JsonObject
        {
            ["id"] = "zz-spin", ["kind"] = "branch",
            ["cases"] = new JsonArray(new JsonObject { ["when"] = new JsonObject { ["fact"] = "var.trust", ["op"] = "gte", ["value"] = 0 }, ["next"] = "zz-spin" }),
            ["otherwise"] = GraphGenerator.Id(0),
        });
        g.Conversation["start"] = "zz-spin";
        return true;
    }

    private static bool BadCondition(GraphGenerator g)
    {
        var guards = AllObjects(g.Document).Where(o => o.ContainsKey("fact")).ToList();
        if (guards.Count == 0) return false;
        guards[g.Next(guards.Count)]["fact"] = "var.ghost";
        return true;
    }

    private static bool DuplicateAnswer(GraphGenerator g)
    {
        var choices = g.Nodes.Cast<JsonObject>().Where(n => (string)n["kind"]! == "choice").ToList();
        if (choices.Count == 0) return false;
        var options = (JsonArray)choices[g.Next(choices.Count)]["options"]!;
        options.Add(options[0]!.DeepClone());
        return true;
    }

    private static bool UnknownSpeaker(GraphGenerator g) { ((JsonObject)g.Nodes[0]!)["speaker"] = "nobody"; return true; }

    private static bool UnknownPortrait(GraphGenerator g) { ((JsonObject)g.Nodes[0]!)["portrait"] = "angry"; return true; }

    private static bool UnknownField(GraphGenerator g) { ((JsonObject)g.Nodes[g.Next(g.Nodes.Count)]!)["bogus"] = 1; return true; }

    private static bool MissingField(GraphGenerator g) => ((JsonObject)g.Nodes[0]!).Remove("next");

    private static bool BadId(GraphGenerator g) { ((JsonObject)g.Nodes[0]!)["id"] = "Bad Id"; return true; }

    private static bool BadKind(GraphGenerator g) { ((JsonObject)g.Nodes[0]!)["kind"] = "teleport"; return true; }

    private static bool WeightZero(GraphGenerator g)
    {
        var randoms = AllObjects(g.Document).Where(o => o.ContainsKey("weight")).ToList();
        if (randoms.Count == 0) return false;
        randoms[g.Next(randoms.Count)]["weight"] = 0;
        return true;
    }

    public static IEnumerable<object[]> Defects() => new (string Name, Func<GraphGenerator, bool> Put, string Code)[]
    {
        ("dangling next", Dangling, DialogueCodes.Reference),
        ("start is not a node", BadStart, DialogueCodes.Reference),
        ("duplicate node id", DuplicateNode, DialogueCodes.Duplicate),
        ("unreachable node", Unreachable, DialogueCodes.Unreachable),
        ("loop that never waits", SilentLoop, DialogueCodes.Loop),
        ("condition on an unknown fact", BadCondition, DialogueCodes.Reference),
        ("two answers with one id", DuplicateAnswer, DialogueCodes.Duplicate),
        ("unknown speaker", UnknownSpeaker, DialogueCodes.UnknownSpeaker),
        ("unknown portrait", UnknownPortrait, DialogueCodes.UnknownPortrait),
        ("unknown field", UnknownField, DialogueCodes.UnknownField),
        ("missing field", MissingField, DialogueCodes.MissingField),
        ("bad id", BadId, DialogueCodes.Format),
        ("unknown node kind", BadKind, DialogueCodes.Format),
        ("weight out of range", WeightZero, DialogueCodes.Range),
    }.Select(d => new object[] { d.Name, d.Put, d.Code });

    [Theory]
    [MemberData(nameof(Defects))]
    public void OneDefectInAGeneratedDocumentIsRefusedWithThatDefectsCode(string name, Func<GraphGenerator, bool> put, string code)
    {
        var applied = 0;
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var generator = Make(seed);
            if (!put(generator)) continue;           // this document has nowhere to put that defect (no choice, no weight)
            applied++;
            var actual = Code(generator.Text());
            Assert.True(code == actual, $"seed {seed}, defect '{name}': expected {code}, got {actual}");
        }
        Assert.True(applied >= Seeds / 3, $"defect '{name}' was put in only {applied} of {Seeds} documents");
    }

    // ---- damaged text -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ADocumentDamagedAtRandomIsLoadedOrRefusedWithAnEngineCodeNeverAnythingElse()
    {
        var loaded = 0;
        var refused = new Dictionary<string, int>();
        for (var seed = 1; seed <= Seeds * 2; seed++)
        {
            var random = new Random(seed);
            var text = Make(1 + seed % Seeds).Text();
            for (var edit = 0; edit < 1 + random.Next(3); edit++)
            {
                if (text.Length < 4) break;
                var at = random.Next(text.Length - 2);
                text = random.Next(5) switch
                {
                    0 => text.Remove(at, 1 + random.Next(Math.Min(8, text.Length - at))),
                    1 => text.Insert(at, "\"{}[],:0123456789abcxyz"[random.Next(23)].ToString()),
                    2 => text[..at],
                    3 => text.Insert(at, text.Substring(at, Math.Min(12, text.Length - at))),
                    _ => text.Remove(at, 1).Insert(at, "}]\",0n"[random.Next(6)].ToString()),
                };
            }
            try { DialogueLoader.Load(text, Fixture.Catalog); loaded++; }
            catch (EngineRefusedException refusal) { refused[refusal.Code] = refused.GetValueOrDefault(refusal.Code) + 1; }
        }
        Assert.True(refused.Values.Sum() > 100, "refused " + refused.Values.Sum());
        Assert.True(refused.Count >= 4, "codes seen: " + string.Join(", ", refused.Keys));
        Assert.Equal(Seeds * 2, loaded + refused.Values.Sum());
    }
}
