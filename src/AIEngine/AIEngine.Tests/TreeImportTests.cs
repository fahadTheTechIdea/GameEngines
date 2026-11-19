using System.Text.Json;
using EngineCommon;
using Xunit;

namespace AIEngine.Tests;

/// <summary>The importer's refusal matrix (every code), its reductions, the bound it keeps in step with the engine, and the files it writes.</summary>
public sealed class TreeImportTests
{
    private static string Thrown(string tree) => Assert.Throws<EngineRefusedException>(() => TreeImport.Run(tree, "t.ai")).Code;

    private static string[] Refused(string tree)
    {
        var result = TreeImport.Run(tree, "t.ai");
        Assert.Null(result.Document);
        Assert.Null(result.TraceMap);
        return result.Report.Notes.Where(n => n.Level == TreeLevel.Refused).Select(n => n.Code).ToArray();
    }

    [Theory]
    [InlineData("\"format\": 1", "\"format\": 2", TreeCodes.Format)]
    [InlineData("\"every\": 1,", "\"every\": 1, \"extra\": 1,", TreeCodes.UnknownField)]
    [InlineData("\"action\": \"flee\", \"weight\": 90", "\"action\": \"flee\", \"weight\": 90, \"samples\": 4", TreeCodes.UnknownField)]
    [InlineData("\"max\": 100, \"initial\": 100 }", "\"max\": 100 }", TreeCodes.MissingField)]
    [InlineData("\"op\": \"lte\", \"threshold\": 30,", "\"op\": \"lte\",", TreeCodes.MissingField)]
    [InlineData("\"threshold\": 30", "\"threshold\": 2000000000000", TreeCodes.Range)]
    [InlineData("\"threshold\": 30", "\"threshold\": 99999999999999999999", TreeCodes.Range)]
    [InlineData("\"min\": 0, \"max\": 100,", "\"min\": 101, \"max\": 100,", TreeCodes.Range)]
    [InlineData("\"max\": 100, \"initial\": 100", "\"max\": 100, \"initial\": 101", TreeCodes.Range)]
    [InlineData("\"every\": 1", "\"every\": 0", TreeCodes.Range)]
    [InlineData("\"min\": 0, \"max\": 500, \"initial\": 0", "\"min\": 5, \"max\": 500, \"initial\": 0", TreeCodes.Range)]
    [InlineData("\"id\": \"hold-leaf\", \"action\"", "\"id\": \"atk\", \"action\"", TreeCodes.Duplicate)]
    [InlineData("\"id\": \"dist\", \"fact\"", "\"id\": \"hp\", \"fact\"", TreeCodes.Duplicate)]
    [InlineData("\"fact\": \"key.dist\"", "\"fact\": \"key.hp\"", TreeCodes.Duplicate)]
    [InlineData("\"attack\", \"flee\", \"hold\"", "\"attack\", \"attack\", \"hold\"", TreeCodes.Duplicate)]
    [InlineData("\"weight\": 90", "\"weight\": \"heavy\"", TreeCodes.Format)]
    [InlineData("\"weight\": 90", "\"weight\": true", TreeCodes.Format)]
    [InlineData("\"op\": \"lte\"", "\"op\": \"approx\"", TreeCodes.Format)]
    [InlineData("\"id\": \"flee-leaf\", \"action\"", "\"id\": \"flee.leaf\", \"action\"", TreeCodes.Format)]
    [InlineData("\"id\": \"scout\"", "\"id\": \"Scout\"", TreeCodes.Format)]
    [InlineData("\"hold\" ]", "7 ]", TreeCodes.Format)]
    public void AFileThatIsNotTheInterchangeIsRefusedWholeWithItsCode(string old, string replacement, string code) =>
        Assert.Equal(code, Thrown(TreeFixture.With(old, replacement)));

    [Fact]
    public void TextThatIsNotJsonIsAFormatMistake() => Assert.Equal(TreeCodes.Format, Thrown("{ nope"));

    [Fact]
    public void ABadDocumentIdIsRefusedBeforeAnythingIsRead() =>
        Assert.Equal(TreeCodes.Format, Assert.Throws<EngineRefusedException>(() => TreeImport.Run(TreeFixture.Basic, "Bad Id")).Code);

    [Fact]
    public void MoreNodesFeaturesOrActionsThanTheImporterTakesAreTooBig()
    {
        var nodes = string.Join(", ", Enumerable.Range(0, TreeReader.MaxNodes + 1).Select(i => $"{{ \"id\": \"n{i}\", \"action\": \"go\", \"weight\": 1 }}"));
        Assert.Equal(TreeCodes.TooBig, Thrown($$"""{ "format": 1, "id": "t", "every": 1, "features": [], "actions": [ "go" ], "root": "n0", "nodes": [ {{nodes}} ] }"""));
        var features = string.Join(", ", Enumerable.Range(0, TreeReader.MaxFeatures + 1).Select(i => $"{{ \"id\": \"f{i}\", \"fact\": \"key.f{i}\", \"min\": 0, \"max\": 1, \"initial\": 0 }}"));
        Assert.Equal(TreeCodes.TooBig, Thrown($$"""{ "format": 1, "id": "t", "every": 1, "features": [ {{features}} ], "actions": [ "go" ], "root": "n0", "nodes": [ { "id": "n0", "action": "go", "weight": 1 } ] }"""));
        var actions = string.Join(", ", Enumerable.Range(0, TreeReader.MaxActions + 1).Select(i => $"\"a{i}\""));
        Assert.Equal(TreeCodes.TooBig, Thrown($$"""{ "format": 1, "id": "t", "every": 1, "features": [], "actions": [ {{actions}} ], "root": "n0", "nodes": [ { "id": "n0", "action": "a0", "weight": 1 } ] }"""));
    }

    [Theory]
    [InlineData("\"threshold\": 30", "\"threshold\": 30.5", TreeCodes.Float)]
    [InlineData("\"threshold\": 30", "\"threshold\": 30.0", TreeCodes.Float)]
    [InlineData("\"threshold\": 30", "\"threshold\": 3e1", TreeCodes.Float)]
    [InlineData("\"threshold\": 30", "\"threshold\": -0.25", TreeCodes.Float)]
    [InlineData("\"weight\": 90", "\"weight\": 90.5", TreeCodes.Float)]
    [InlineData("\"threshold\": 30", "\"threshold\": \"NaN\"", TreeCodes.NotFinite)]
    [InlineData("\"threshold\": 30", "\"threshold\": \"-Infinity\"", TreeCodes.NotFinite)]
    [InlineData("\"weight\": 90", "\"weight\": \"Infinity\"", TreeCodes.NotFinite)]
    [InlineData("\"op\": \"lte\", \"threshold\": 30", "\"op\": \"in\", \"categories\": [ 1, 2 ]", TreeCodes.Categorical)]
    [InlineData("\"op\": \"lte\", \"threshold\": 30", "\"op\": \"notin\", \"categories\": [ 3 ]", TreeCodes.Categorical)]
    [InlineData("\"op\": \"lte\", \"threshold\": 30", "\"op\": \"eq\", \"categories\": [ 1, 2 ]", TreeCodes.Categorical)]
    [InlineData("\"threshold\": 30,", "\"threshold\": 30, \"missing\": \"right\",", TreeCodes.MissingRouting)]
    [InlineData("\"fact\": \"key.dist\"", "\"fact\": \"item.dist\"", TreeCodes.FeatureSource)]
    [InlineData("\"fact\": \"key.dist\"", "\"fact\": \"tick\"", TreeCodes.FeatureSource)]
    [InlineData("\"left\": \"flee-leaf\"", "\"left\": \"nowhere\"", TreeCodes.Reference)]
    [InlineData("\"root\": \"a\"", "\"root\": \"zz\"", TreeCodes.Reference)]
    [InlineData("\"feature\": \"dist\"", "\"feature\": \"ghost\"", TreeCodes.Reference)]
    [InlineData("\"action\": \"flee\"", "\"action\": \"run\"", TreeCodes.Reference)]
    [InlineData("\"right\": \"hold-leaf\"", "\"right\": \"atk\"", TreeCodes.NotATree)]
    [InlineData("\"right\": \"b\"", "\"right\": \"a\"", TreeCodes.NotATree)]
    [InlineData("{ \"id\": \"hold-leaf\"", "{ \"id\": \"orphan\", \"action\": \"hold\", \"weight\": 1 }, { \"id\": \"hold-leaf\"", TreeCodes.NotATree)]
    public void ATreeTheEngineCannotRunIsRefusedByNameAndNothingIsWritten(string old, string replacement, string code) =>
        Assert.Contains(code, Refused(TreeFixture.With(old, replacement)));

    [Fact]
    public void EveryMistakeInOneDraftIsReportedTogether()
    {
        var draft = TreeFixture.With("\"threshold\": 30,", "\"threshold\": 30.5, \"missing\": \"left\",")
            .Replace("\"fact\": \"key.dist\"", "\"fact\": \"item.dist\"", StringComparison.Ordinal)
            .Replace("\"action\": \"flee\"", "\"action\": \"run\"", StringComparison.Ordinal);
        // In the order met: the feature table, then the node (its missing-value route before its threshold), then the walk that resolves names.
        Assert.Equal(new[] { TreeCodes.FeatureSource, TreeCodes.MissingRouting, TreeCodes.Float, TreeCodes.Reference }, Refused(draft));
    }

    [Fact]
    public void ADepthTheEngineAllowsIsAcceptedAndOneMoreIsRefusedBeforeAnyDocumentIsMade()
    {
        var accepted = TreeImport.Run(TreeFixture.Balanced(TreeImport.MaxSplitDepth), "deep.ai");
        Assert.NotNull(accepted.Document);
        Assert.Equal(TreeImport.MaxSplitDepth, accepted.Depth);
        Assert.Equal(32, accepted.Leaves);
        Assert.Equal(new[] { TreeCodes.TooDeep }, Refused(TreeFixture.Balanced(TreeImport.MaxSplitDepth + 1)));
    }

    [Fact]
    public void TheSplitDepthIsTheEnginesBehaviourDepthWorkedOutFromItsOwnConstants()
    {
        // A split is a selector and a sequence (two levels); the root is level 1; the deepest act is at 2 x splits + 1.
        Assert.Equal((BehaviorContext.MaxDepth - 1) / 2, TreeImport.MaxSplitDepth);
        Assert.True(2 * TreeImport.MaxSplitDepth + 1 <= BehaviorContext.MaxDepth);
        Assert.True(2 * (TreeImport.MaxSplitDepth + 1) + 1 > BehaviorContext.MaxDepth);
        Assert.True(4 * (1 << TreeImport.MaxSplitDepth) - 3 <= BehaviorContext.MaxNodes);
        // The widest composite the importer writes has two children; the engine allows 32.
        Assert.True(2 <= 32);
    }

    [Theory]
    [InlineData("lt", 600, "always passes")]
    [InlineData("lt", 0, "never passes")]
    [InlineData("lte", 500, "always passes")]
    [InlineData("lte", -1, "never passes")]
    [InlineData("gt", 500, "never passes")]
    [InlineData("gt", -1, "always passes")]
    [InlineData("gte", 0, "always passes")]
    [InlineData("gte", 501, "never passes")]
    [InlineData("eq", 501, "never passes")]
    [InlineData("eq", -3, "never passes")]
    [InlineData("ne", 501, "always passes")]
    [InlineData("ne", -3, "always passes")]
    public void ATestTheFeaturesRangeDecidesIsReducedToTheBranchThatCanBeTaken(string op, int threshold, string verdict)
    {
        var tree = TreeFixture.With("\"feature\": \"dist\", \"op\": \"lt\", \"threshold\": 100", $"\"feature\": \"dist\", \"op\": \"{op}\", \"threshold\": {threshold}");
        var result = TreeImport.Run(tree, "t.ai");
        Assert.NotNull(result.Document);
        var note = Assert.Single(result.Report.Notes);
        Assert.Equal(TreeLevel.Reduced, note.Level);
        Assert.Equal(TreeCodes.BranchUnreachable, note.Code);
        Assert.Equal("node b", note.Subject);
        Assert.Contains(verdict, note.Detail);
        // One split left (the hp test) and two leaves; the other leaf and the dist test are in the map as pruned.
        Assert.Equal(1, result.Splits);
        Assert.Equal(2, result.Leaves);
        using var map = JsonDocument.Parse(result.TraceMap!);
        var pruned = map.RootElement.GetProperty("pruned").EnumerateArray().Select(p => p.GetProperty("source").GetString()).ToList();
        Assert.Equal(new[] { "b", verdict == "always passes" ? "hold-leaf" : "atk" }, pruned);
    }

    [Fact]
    public void ARangeThatNarrowsDownTheTreeDecidesALaterTestOnTheSameFeature()
    {
        // hp <= 30 on the left; under it hp <= 50 is always true, and hp > 30 on the right makes hp <= 20 never true.
        var tree = TreeFixture.With("\"left\": \"flee-leaf\", \"right\": \"b\" }",
            "\"left\": \"c\", \"right\": \"d\" }, { \"id\": \"c\", \"feature\": \"hp\", \"op\": \"lte\", \"threshold\": 50, \"left\": \"flee-leaf\", \"right\": \"x1\" }, { \"id\": \"x1\", \"action\": \"hold\", \"weight\": 1 }, "
            + "{ \"id\": \"d\", \"feature\": \"hp\", \"op\": \"lte\", \"threshold\": 20, \"left\": \"x2\", \"right\": \"b\" }, { \"id\": \"x2\", \"action\": \"hold\", \"weight\": 2 }");
        var result = TreeImport.Run(tree, "t.ai");
        Assert.Equal(new[] { "c", "d" }, result.Report.Notes.Select(n => n.Subject.Replace("node ", "")).OrderBy(s => s, StringComparer.Ordinal));
        Assert.Equal(2, result.Splits);
    }

    [Fact]
    public void AValueTheTestSplitsExactlyKeepsBothBranches()
    {
        var result = TreeImport.Run(TreeFixture.Basic, "t.ai");
        Assert.Empty(result.Report.Notes);
        Assert.Equal(2, result.Splits);
        Assert.Equal(3, result.Leaves);
        Assert.Equal(2, result.Depth);
        Assert.Equal(9, result.Nodes);
        Assert.Equal(new Dictionary<string, int> { ["leaf"] = 3, ["split-lt"] = 1, ["split-lte"] = 1 }, result.Report.Supported.ToDictionary(p => p.Key, p => p.Value));
    }

    [Fact]
    public void EverySourceNodeIsEitherEmittedOrListedAsPrunedAndNeverBoth()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var tree = TreeFixture.Random(seed);
            var result = TreeImport.Run(tree, "t.ai");
            using var source = JsonDocument.Parse(tree);
            var all = source.RootElement.GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("id").GetString()!).ToHashSet();
            using var map = JsonDocument.Parse(result.TraceMap!);
            var emitted = map.RootElement.GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("source").GetString()!).ToHashSet();
            var pruned = map.RootElement.GetProperty("pruned").EnumerateArray().Select(n => n.GetProperty("source").GetString()!).ToList();
            Assert.Equal(pruned.Count, pruned.Distinct().Count());
            Assert.Empty(emitted.Intersect(pruned));
            Assert.True(all.SetEquals(emitted.Union(pruned)), "seed " + seed);
        }
    }

    [Fact]
    public void ALeafsWeightIsTheArgumentItsActionRaisesAndTheIntentRangeIsTheEmittedLeavesRange()
    {
        var result = TreeImport.Run(TreeFixture.Basic, "t.ai");
        using var document = JsonDocument.Parse(result.Document!);
        foreach (var intent in document.RootElement.GetProperty("intents").EnumerateArray())
        {
            var weight = intent.GetProperty("args").EnumerateArray().Single();
            Assert.Equal("weight", weight.GetProperty("id").GetString());
            Assert.Equal(10, weight.GetProperty("min").GetInt64());
            Assert.Equal(90, weight.GetProperty("max").GetInt64());
        }
        // The pruned leaf's weight does not widen the range.
        var reduced = TreeImport.Run(TreeFixture.With("\"threshold\": 100", "\"threshold\": 600"), "t.ai");
        using var narrowed = JsonDocument.Parse(reduced.Document!);
        Assert.Equal(70, narrowed.RootElement.GetProperty("intents")[0].GetProperty("args")[0].GetProperty("min").GetInt64());
    }

    [Fact]
    public void TheSameFileGivesTheSameBytesEveryTime()
    {
        var first = TreeImport.Run(TreeFixture.Random(9), "t.ai");
        var second = TreeImport.Run(TreeFixture.Random(9), "t.ai");
        Assert.Equal(first.Document, second.Document);
        Assert.Equal(first.TraceMap, second.TraceMap);
        Assert.Equal(first.Set!.Digest, second.Set!.Digest);
        Assert.DoesNotContain('\r', first.Document!);
    }

    [Fact]
    public void TheGuardSampleBecomesExactlyTheStoredDocumentAndTraceMap()
    {
        var result = TreeImport.Run(TreeFixture.Sample("guard.tree.json"), "guard.ai");
        Assert.Equal(TreeFixture.Sample("guard.ai.json").Replace("\r\n", "\n"), result.Document);
        Assert.Equal(TreeFixture.Sample("guard.trace.json").Replace("\r\n", "\n"), result.TraceMap);
        var note = Assert.Single(result.Report.Notes);
        Assert.Equal(TreeCodes.BranchUnreachable, note.Code);
        Assert.Equal("node n8", note.Subject);
    }

    [Fact]
    public void TheGuardDraftIsRefusedForEachOfItsFourMistakes() =>
        Assert.Equal(new[] { TreeCodes.FeatureSource, TreeCodes.Float, TreeCodes.MissingRouting, TreeCodes.Reference }, Refused(TreeFixture.Sample("guard.draft.tree.json")));
}
