using System.Text;
using System.Text.Json;
using Xunit;

namespace AIEngine.Tests;

/// <summary>Small trees in the interchange to change one thing in, a builder of balanced and chained trees, and a seeded generator of random trees.</summary>
internal static class TreeFixture
{
    public const string Basic = """
      { "format": 1, "id": "scout", "every": 1,
        "features": [
          { "id": "hp", "fact": "key.hp", "min": 0, "max": 100, "initial": 100 },
          { "id": "dist", "fact": "key.dist", "min": 0, "max": 500, "initial": 0 } ],
        "actions": [ "attack", "flee", "hold" ],
        "root": "a",
        "nodes": [
          { "id": "a", "feature": "hp", "op": "lte", "threshold": 30, "left": "flee-leaf", "right": "b" },
          { "id": "flee-leaf", "action": "flee", "weight": 90 },
          { "id": "b", "feature": "dist", "op": "lt", "threshold": 100, "left": "atk", "right": "hold-leaf" },
          { "id": "atk", "action": "attack", "weight": 70 },
          { "id": "hold-leaf", "action": "hold", "weight": 10 } ] }
      """;

    public static string With(string old, string replacement)
    {
        Assert.Contains(old, Basic);
        return Basic.Replace(old, replacement, StringComparison.Ordinal);
    }

    public static string Sample(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", name));

    /// <summary>A tree over one feature <c>x</c> (0 to 2^depth - 1) that halves the range at every split: 2^depth leaves, no split decided by a range, the deepest tree a given depth can be.</summary>
    public static string Balanced(int depth)
    {
        var top = (1 << depth) - 1;
        var nodes = new List<string>();
        var counter = 0;
        string Build(int low, int high)
        {
            var id = "n" + counter++;
            if (low == high)
            {
                nodes.Add($"{{ \"id\": \"{id}\", \"action\": \"go\", \"weight\": {low} }}");
                return id;
            }
            var mid = (low + high) / 2;
            var slot = nodes.Count;
            nodes.Add("");
            var left = Build(low, mid);
            var right = Build(mid + 1, high);
            nodes[slot] = $"{{ \"id\": \"{id}\", \"feature\": \"x\", \"op\": \"lte\", \"threshold\": {mid}, \"left\": \"{left}\", \"right\": \"{right}\" }}";
            return id;
        }
        Build(0, top);
        return $$"""{ "format": 1, "id": "balanced", "every": 1, "features": [ { "id": "x", "fact": "key.x", "min": 0, "max": {{top}}, "initial": 0 } ], "actions": [ "go" ], "root": "n0", "nodes": [ {{string.Join(", ", nodes)}} ] }""";
    }

    /// <summary>What the independent reference makes of a tree: the leaf a point reaches, its action and weight, and the splits it passed through on the way.</summary>
    public sealed record Outcome(string Leaf, string Action, long Weight, IReadOnlyList<string> Path);

    /// <summary>
    /// The reference evaluator. It reads the interchange with System.Text.Json alone (nothing of the importer) and walks it the way a decision tree is defined: at a split, go left when
    /// "feature op threshold" holds, otherwise right, until a leaf.
    /// </summary>
    public sealed class Reference
    {
        private readonly Dictionary<string, JsonElement> _nodes = new();
        private readonly string _root;
        private readonly JsonDocument _document;

        public Reference(string tree)
        {
            _document = JsonDocument.Parse(tree);
            _root = _document.RootElement.GetProperty("root").GetString()!;
            foreach (var node in _document.RootElement.GetProperty("nodes").EnumerateArray()) _nodes[node.GetProperty("id").GetString()!] = node;
        }

        public Outcome Evaluate(IReadOnlyDictionary<string, long> values)
        {
            var path = new List<string>();
            var id = _root;
            while (true)
            {
                var node = _nodes[id];
                if (node.TryGetProperty("action", out var action)) return new Outcome(id, action.GetString()!, node.GetProperty("weight").GetInt64(), path);
                path.Add(id);
                var value = values[node.GetProperty("feature").GetString()!];
                var threshold = node.GetProperty("threshold").GetInt64();
                var holds = node.GetProperty("op").GetString() switch
                {
                    "lt" => value < threshold,
                    "lte" => value <= threshold,
                    "gt" => value > threshold,
                    "gte" => value >= threshold,
                    "eq" => value == threshold,
                    "ne" => value != threshold,
                    var other => throw new InvalidOperationException("the fixture wrote an op the reference does not know: " + other),
                };
                id = node.GetProperty(holds ? "left" : "right").GetString()!;
            }
        }

        /// <summary>Every split's threshold per feature, with the feature ranges, for building a grid of values that hits both sides of every test.</summary>
        public IReadOnlyList<(string Feature, long Min, long Max, long[] Thresholds)> Features()
        {
            var thresholds = new Dictionary<string, List<long>>();
            foreach (var node in _nodes.Values)
                if (node.TryGetProperty("feature", out var feature))
                {
                    var name = feature.GetString()!;
                    if (!thresholds.TryGetValue(name, out var list)) thresholds[name] = list = new List<long>();
                    list.Add(node.GetProperty("threshold").GetInt64());
                }
            return _document.RootElement.GetProperty("features").EnumerateArray()
                .Select(f => (f.GetProperty("id").GetString()!, f.GetProperty("min").GetInt64(), f.GetProperty("max").GetInt64(),
                    thresholds.GetValueOrDefault(f.GetProperty("id").GetString()!)?.ToArray() ?? Array.Empty<long>())).ToList();
        }
    }

    /// <summary>A small deterministic generator (xorshift64*), so a seed gives the same trees and points on every machine and runtime.</summary>
    public sealed class Seeded(ulong seed)
    {
        private ulong _state = seed * 0x9E3779B97F4A7C15UL + 0x1234567UL;

        public ulong Next()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 0x2545F4914F6CDD1DUL;
        }

        public long Between(long low, long high) => low + (long)(Next() % (ulong)(high - low + 1));
    }

    private static readonly string[] Ops = ["lt", "lte", "gt", "gte", "eq", "ne"];
    private static readonly string[] Actions = ["alpha", "beta", "gamma"];

    /// <summary>
    /// A random tree of up to five splits deep over two or three features. Thresholds are drawn from a little outside each feature's range so some tests are decided by the range (and the importer
    /// must leave them out) and the rest cut it anywhere, with every comparison.
    /// </summary>
    public static string Random(ulong seed)
    {
        var random = new Seeded(seed);
        var features = new List<(string Id, long Min, long Max)>();
        var count = (int)random.Between(2, 3);
        for (var i = 0; i < count; i++)
        {
            var min = random.Between(-30, 5);
            features.Add(("f" + i, min, min + random.Between(0, 40)));
        }
        var nodes = new List<string>();
        var next = 0;
        string Build(int depth)
        {
            var id = "n" + next++;
            if (depth == 0 || (depth < 5 && random.Between(0, 4) == 0))
            {
                nodes.Add($"{{ \"id\": \"{id}\", \"action\": \"{Actions[random.Between(0, Actions.Length - 1)]}\", \"weight\": {random.Between(-50, 50)} }}");
                return id;
            }
            var slot = nodes.Count;
            nodes.Add("");
            var feature = features[(int)random.Between(0, features.Count - 1)];
            var threshold = random.Between(feature.Min - 3, feature.Max + 3);
            var op = Ops[random.Between(0, Ops.Length - 1)];
            var left = Build(depth - 1);
            var right = Build(depth - 1);
            nodes[slot] = $"{{ \"id\": \"{id}\", \"feature\": \"{feature.Id}\", \"op\": \"{op}\", \"threshold\": {threshold}, \"left\": \"{left}\", \"right\": \"{right}\" }}";
            return id;
        }
        Build(5);
        var table = string.Join(", ", features.Select(f => $"{{ \"id\": \"{f.Id}\", \"fact\": \"key.{f.Id}\", \"min\": {f.Min}, \"max\": {f.Max}, \"initial\": {f.Min} }}"));
        return $$"""{ "format": 1, "id": "random", "every": 1, "features": [ {{table}} ], "actions": [ "alpha", "beta", "gamma" ], "root": "n0", "nodes": [ {{string.Join(", ", nodes)}} ] }""";
    }

    /// <summary>The values to try: each feature's two ends and, for every threshold, the value below it, on it and above it (clipped to the range), then every combination up to <paramref name="cap"/>.</summary>
    public static IEnumerable<Dictionary<string, long>> Boundary(Reference reference, int cap)
    {
        var axes = reference.Features().Select(f =>
        {
            var values = new SortedSet<long> { f.Min, f.Max };
            foreach (var t in f.Thresholds)
                foreach (var v in new[] { t - 1, t, t + 1 })
                    if (v >= f.Min && v <= f.Max) values.Add(v);
            return (f.Feature, Values: values.ToArray());
        }).ToList();
        var total = axes.Aggregate(1L, (n, a) => n * a.Values.Length);
        var chooser = new Seeded(7);
        var count = Math.Min(total, cap);
        for (var n = 0L; n < count; n++)
        {
            // Every combination when they fit under the cap; otherwise a seeded draw of that many combinations.
            var index = total <= cap ? n : chooser.Between(0, total - 1);
            var point = new Dictionary<string, long>();
            var rest = index;
            foreach (var axis in axes)
            {
                point[axis.Feature] = axis.Values[(int)(rest % axis.Values.Length)];
                rest /= axis.Values.Length;
            }
            yield return point;
        }
    }
}
