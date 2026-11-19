using System.Text.Json;
using ScenarioEngine.Contracts;
using Xunit;

namespace AIEngine.Tests;

/// <summary>
/// The exported document must run the tree exactly. An independent reference evaluator (reading the interchange with System.Text.Json alone) and the AI engine running the exported
/// document are asked the same question at every boundary of the tree, on a dense stepped grid and at 10,000 seeded points; they must give the same leaf, action and weight, and the
/// engine's trace must name the same source nodes.
/// </summary>
public sealed class TreeExactnessTests
{
    /// <summary>The exported document in a running engine: one agent, its keys told, one think a point.</summary>
    private sealed class Engine
    {
        private readonly AiRuntime _runtime;
        private readonly string _behavior;
        private readonly long _every;
        private long _tick;
        private readonly long _budget;
        public Dictionary<string, string> Source { get; }
        public HashSet<string> Pruned { get; }

        public Engine(string tree)
        {
            var result = TreeImport.Run(tree, "exact.ai");
            Assert.NotNull(result.Set);
            _runtime = new AiRuntime(result.Set!, 1);
            _behavior = result.Set!.Behaviors.Keys.Single();
            _every = result.Set.Behaviors[_behavior].Every;
            _budget = result.Set.MaxCost;
            _runtime.Add("a", _behavior, 0);
            _tick = _runtime.Due("a");
            _runtime.Trace("a", true);
            using var map = JsonDocument.Parse(result.TraceMap!);
            Source = map.RootElement.GetProperty("nodes").EnumerateArray().ToDictionary(n => n.GetProperty("node").GetString()!, n => n.GetProperty("source").GetString()!);
            Pruned = map.RootElement.GetProperty("pruned").EnumerateArray().Select(p => p.GetProperty("source").GetString()!).ToHashSet();
        }

        public Intent Think(IReadOnlyDictionary<string, long> values)
        {
            foreach (var (key, value) in values) _runtime.SetKey("a", key, FactValue.From(value));
            var report = _runtime.Step(_tick, _budget);
            _tick += _every;
            return Assert.Single(report.Intents);
        }

        /// <summary>The source nodes of the tests the last think evaluated, in order.</summary>
        public IReadOnlyList<string> TestedSources() =>
            _runtime.Explain("a")!.Steps.Where(s => s.Kind == "condition").Select(s => Source[s.Node]).ToList();
    }

    private static void AgreeAt(TreeFixture.Reference reference, Engine engine, IReadOnlyDictionary<string, long> point, bool checkTrace)
    {
        var expected = reference.Evaluate(point);
        var intent = engine.Think(point);
        var where = string.Join(", ", point.Select(p => p.Key + "=" + p.Value));
        Assert.True(expected.Action == intent.Name, "action differs at " + where + ": reference " + expected.Action + ", engine " + intent.Name);
        Assert.True(expected.Weight == intent.Arg("weight").Integer, "weight differs at " + where);
        Assert.True(expected.Leaf == intent.Node, "leaf differs at " + where + ": reference " + expected.Leaf + ", engine " + intent.Node);
        if (!checkTrace) return;
        var live = expected.Path.Where(s => !engine.Pruned.Contains(s)).ToList();
        Assert.True(live.SequenceEqual(engine.TestedSources()), "the trace names " + string.Join(">", engine.TestedSources()) + " but the tree passes " + string.Join(">", live) + " at " + where);
    }

    private static int Compare(string tree, int boundaryCap, int randomPoints, ulong seed)
    {
        var reference = new TreeFixture.Reference(tree);
        var engine = new Engine(tree);
        var compared = 0;
        foreach (var point in TreeFixture.Boundary(reference, boundaryCap))
        {
            AgreeAt(reference, engine, point, checkTrace: true);
            compared++;
        }
        var random = new TreeFixture.Seeded(seed);
        var features = reference.Features();
        for (var i = 0; i < randomPoints; i++)
        {
            AgreeAt(reference, engine, features.ToDictionary(f => f.Feature, f => random.Between(f.Min, f.Max)), checkTrace: i % 10 == 0);
            compared++;
        }
        return compared;
    }

    [Fact]
    public void TheGuardSampleAgreesWithTheReferenceOnADenseGridAndOnTenThousandSeededPoints()
    {
        var tree = TreeFixture.Sample("guard.tree.json");
        var reference = new TreeFixture.Reference(tree);
        var engine = new Engine(tree);
        var compared = 0;
        // Dense: every fifth threat and health, every 25th distance, plus the two ends of each (about 9,000 points).
        foreach (var threat in Enumerable.Range(0, 21).Select(i => i * 5L))
            foreach (var health in Enumerable.Range(0, 21).Select(i => i * 5L))
                foreach (var distance in Enumerable.Range(0, 21).Select(i => i * 25L))
                {
                    AgreeAt(reference, engine, new Dictionary<string, long> { ["threat"] = threat, ["health"] = health, ["distance"] = distance }, checkTrace: false);
                    compared++;
                }
        // Every boundary of every test, with the trace checked.
        foreach (var point in TreeFixture.Boundary(reference, 100_000))
        {
            AgreeAt(reference, engine, point, checkTrace: true);
            compared++;
        }
        var random = new TreeFixture.Seeded(2026);
        for (var i = 0; i < 10_000; i++)
        {
            AgreeAt(reference, engine, new Dictionary<string, long> { ["threat"] = random.Between(0, 100), ["health"] = random.Between(0, 100), ["distance"] = random.Between(0, 500) }, checkTrace: i % 20 == 0);
            compared++;
        }
        Assert.True(compared > 19_000);
    }

    [Fact]
    public void SixtyRandomTreesWithEveryComparisonAndThresholdsOutsideTheRangeAgreeWithTheReference()
    {
        var compared = 0;
        var reduced = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var tree = TreeFixture.Random(seed);
            compared += Compare(tree, 4_000, 500, seed);
            if (TreeImport.Run(tree, "r.ai").Report.Notes.Any(n => n.Code == TreeCodes.BranchUnreachable)) reduced++;
        }
        Assert.True(compared > 60 * 500);
        // The generator really does produce tests the ranges decide, so the importer's reduction is exercised, not only the plain path.
        Assert.InRange(reduced, 20, 60);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void ABalancedTreeAsDeepAsTheEngineAllowsRunsExactly(int depth)
    {
        var tree = TreeFixture.Balanced(depth);
        Assert.Equal(1 << depth, Compare(tree, 100, 0, 1));
        Assert.Equal(4 * (1 << depth) - 3, TreeImport.Run(tree, "b.ai").Nodes);
    }

    [Fact]
    public void TheEnginesOwnExplanationNamesTheSourceLeafAndTheTestsOnTheWay()
    {
        var result = TreeImport.Run(TreeFixture.Basic, "basic.ai");
        var runtime = new AiRuntime(result.Set!, 1);
        runtime.Add("a", "scout", 0);
        runtime.SetKey("a", "hp", FactValue.From(80));
        runtime.SetKey("a", "dist", FactValue.From(40));
        runtime.Trace("a", true);
        var report = runtime.Step(0, result.Set!.MaxCost);
        var text = runtime.Explain("a")!.ToText();
        Assert.Equal("atk", Assert.Single(report.Intents).Node);
        Assert.Contains("a.test [condition] failure - does not hold", text);
        Assert.Contains("b.test [condition] success - holds", text);
        Assert.Contains("atk [act] success - raises attack(weight=70)", text);
    }
}
