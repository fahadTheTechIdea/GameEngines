using Xunit;

namespace AIEngine.Tests;

/// <summary>The <c>import-tree</c> command: what it prints, what it writes and does not write, and each exit code.</summary>
public sealed class TreeCliTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ai-tree-" + Guid.NewGuid().ToString("N"));

    public TreeCliTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    private string Path_(string name) => Path.Combine(_folder, name);

    private string Tree(string text)
    {
        File.WriteAllText(Path_("tree.json"), text);
        return Path_("tree.json");
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = AiCli.Run(args, output, error);
        return (code, output.ToString().Replace("\r\n", "\n"), error.ToString().Replace("\r\n", "\n"));
    }

    [Fact]
    public void ATreeThatIsCarriedOverWritesTheDocumentAndTheMapAndTheDocumentPassesCheck()
    {
        var (code, output, error) = Run("import-tree", Tree(TreeFixture.Sample("guard.tree.json")), "--id", "guard.ai", "--out", Path_("out.json"), "--trace", Path_("map.json"));
        Assert.Equal(0, code);
        Assert.Equal("", error);
        var lines = output.TrimEnd('\n').Split('\n');
        Assert.StartsWith("ok guard.ai digest ", lines[0]);
        Assert.EndsWith(" splits 4 leaves 5 depth 3 nodes 17 budget a step needs at least 17", lines[0]);
        Assert.StartsWith("reduced TREE_BRANCH_UNREACHABLE: node n8: ", lines[1]);
        Assert.Equal(new[] { "supported leaf 5", "supported split-lte 4" }, lines.Skip(2));
        Assert.Equal(TreeFixture.Sample("guard.ai.json").Replace("\r\n", "\n"), File.ReadAllText(Path_("out.json")));
        Assert.Equal(TreeFixture.Sample("guard.trace.json").Replace("\r\n", "\n"), File.ReadAllText(Path_("map.json")));
        var (checkCode, checkOutput, _) = Run("check", Path_("out.json"));
        Assert.Equal(0, checkCode);
        Assert.StartsWith("ok guard.ai digest " + lines[0].Split(' ')[3] + " keys 3 percept-kinds 0 intents 4 behaviours 1", checkOutput);
    }

    [Fact]
    public void WithoutTraceNoMapIsWritten()
    {
        Assert.Equal(0, Run("import-tree", Tree(TreeFixture.Basic), "--out", Path_("out.json"), "--id", "basic.ai").Code);
        Assert.True(File.Exists(Path_("out.json")));
        Assert.Equal(new[] { "out.json", "tree.json" }, Directory.GetFiles(_folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void ADraftWithMistakesPrintsEveryRefusalWritesNothingAndExitsOne()
    {
        var (code, output, error) = Run("import-tree", Tree(TreeFixture.Sample("guard.draft.tree.json")), "--id", "guard.ai", "--out", Path_("out.json"), "--trace", Path_("map.json"));
        Assert.Equal(1, code);
        Assert.Equal("", error);
        var lines = output.TrimEnd('\n').Split('\n');
        Assert.Equal(new[] { "TREE_FEATURE_SOURCE", "TREE_FLOAT", "TREE_MISSING_ROUTING", "TREE_REFERENCE" }, lines.Take(4).Select(l => l.Split(':')[0].Replace("refused ", "")));
        Assert.Equal("not written: 4 refused, 0 reduced", lines[4]);
        Assert.False(File.Exists(Path_("out.json")));
        Assert.False(File.Exists(Path_("map.json")));
    }

    [Fact]
    public void AFileThatIsNotTheInterchangeIsOneRefusalLineAndExitsOne()
    {
        var (code, output, _) = Run("import-tree", Tree(TreeFixture.With("\"format\": 1", "\"format\": 2")), "--id", "x", "--out", Path_("out.json"));
        Assert.Equal(1, code);
        Assert.StartsWith("refused TREE_FORMAT: ", output);
        Assert.Single(output.TrimEnd('\n').Split('\n'));
        Assert.False(File.Exists(Path_("out.json")));
    }

    [Fact]
    public void ABadDocumentIdIsRefusedWithItsCode()
    {
        var (code, output, _) = Run("import-tree", Tree(TreeFixture.Basic), "--id", "Not An Id", "--out", Path_("out.json"));
        Assert.Equal(1, code);
        Assert.StartsWith("refused TREE_FORMAT: ", output);
    }

    [Fact]
    public void ATooDeepTreePrintsTheDepthItHasAndExitsOne()
    {
        var (code, output, _) = Run("import-tree", Tree(TreeFixture.Balanced(6)), "--id", "deep", "--out", Path_("out.json"));
        Assert.Equal(1, code);
        Assert.Contains("refused TREE_TOO_DEEP: tree balanced: ", output);
        Assert.Contains("has 6 splits", output);
    }

    [Theory]
    [InlineData]
    [InlineData("import-tree")]
    [InlineData("import-tree", "tree.json")]
    [InlineData("import-tree", "tree.json", "--id", "x")]
    [InlineData("import-tree", "tree.json", "--out", "o.json")]
    [InlineData("import-tree", "tree.json", "--id", "x", "--out")]
    [InlineData("import-tree", "tree.json", "--id", "x", "--id", "y", "--out", "o.json")]
    [InlineData("import-tree", "tree.json", "--id", "x", "--out", "o.json", "--trace", "a.json", "--trace", "b.json")]
    [InlineData("import-tree", "tree.json", "--id", "x", "--out", "o.json", "--verbose", "1")]
    [InlineData("import-tree", "tree.json", "--id", "x", "--out", "o.json", "stray")]
    [InlineData("import-tree", "tree.json", "--name", "x", "--out", "o.json")]
    public void WrongArgumentsPrintTheUsageAndExitTwo(params string[] args)
    {
        var (code, output, error) = Run(args);
        Assert.Equal(2, code);
        Assert.Equal("", output);
        Assert.Contains("AIEngine.Cli import-tree <tree.json> --id <document-id> --out <ai-document.json> [--trace <map.json>]", error);
    }

    [Fact]
    public void AMissingTreeFileIsAFileProblemAndExitsTwo()
    {
        var (code, output, error) = Run("import-tree", Path_("absent.json"), "--id", "x", "--out", Path_("out.json"));
        Assert.Equal(2, code);
        Assert.Equal("", output);
        Assert.StartsWith("file problem: ", error);
    }
}
