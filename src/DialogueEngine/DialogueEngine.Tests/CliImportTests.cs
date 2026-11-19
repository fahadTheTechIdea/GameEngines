using DialogueEngine;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>The <c>import</c> command: it converts, writes both files, reports every feature, and its mistakes have their exit codes.</summary>
public sealed class CliImportTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "dlg-import-" + Guid.NewGuid().ToString("N"));

    public CliImportTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    private string Path_(string name) => Path.Combine(_folder, name);

    private string Write(string name, string text)
    {
        File.WriteAllText(Path_(name), text);
        return Path_(name);
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = DialogueCli.Run(args, output, error);
        return (code, output.ToString().Replace("\r\n", "\n"), error.ToString().Replace("\r\n", "\n"));
    }

    private const string Script = "title: Start\n---\nAnn: Hello.\n-> Yes\n    Ann: Good.\n-> No\n<<shake>>\nBob: Bye.\n===\n";

    [Fact]
    public void AScriptIsConvertedToFilesThatTheOtherCommandsAcceptAndEveryFeatureIsReported()
    {
        var source = Write("shop.yarn", Script);
        var (code, output, error) = Run("import", "yarn", source, "--id", "shop.demo", "--locale", "en", "--out", Path_("shop.json"), "--strings", Path_("shop.en.json"));
        Assert.Equal(0, code);
        Assert.Equal("", error);
        Assert.Contains("converted yarn:", output, StringComparison.Ordinal);
        Assert.Contains("approximated command without arguments x1:", output, StringComparison.Ordinal);
        Assert.Contains("converted options x1:", output, StringComparison.Ordinal);
        Assert.Contains("speakers the game catalog must provide: ann, bob", output, StringComparison.Ordinal);
        var check = Run("check", Path_("shop.json"));
        Assert.Equal(0, check.Code);
        Assert.StartsWith("ok shop.demo", check.Output, StringComparison.Ordinal);
        Assert.Equal(0, Run("coverage", Path_("shop.json"), Path_("shop.en.json")).Code);
    }

    [Fact]
    public void AnInkStoryIsConvertedWithAStartKnot()
    {
        var story = Write("story.json", "{\"inkVersion\":21,\"root\":[[\"^Out.\",\"\\n\",\"done\",null],\"done\",{\"cave\":[\"^In.\",\"\\n\",\"end\",null]}],\"listDefs\":{}}");
        var (code, output, _) = Run("import", "ink", story, "--id", "story.demo", "--locale", "en", "--out", Path_("s.json"), "--strings", Path_("s.en.json"), "--start", "cave");
        Assert.Equal(0, code);
        Assert.Contains("converted text x1:", output, StringComparison.Ordinal);
        Assert.Contains("speakers the game catalog must provide: narrator", output, StringComparison.Ordinal);
        Assert.Contains("In.", File.ReadAllText(Path_("s.en.json")), StringComparison.Ordinal);
        Assert.DoesNotContain("Out.", File.ReadAllText(Path_("s.en.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void TheMatrixIsPrintedForBothFormats()
    {
        var yarn = Run("import", "matrix", "yarn");
        var ink = Run("import", "matrix", "ink");
        Assert.Equal(0, yarn.Code);
        Assert.Equal(0, ink.Code);
        Assert.Equal(ImportCapabilities.Render(ImportCapabilities.YarnSpinner).Replace("\r\n", "\n"), yarn.Output);
        Assert.Contains("Refused", ink.Output, StringComparison.Ordinal);
        Assert.Contains("Dropped", yarn.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ARefusedConversionPrintsItsCodeWritesNothingAndExitsOne()
    {
        var source = Write("bad.yarn", "title: Start\n---\n<<declare $x = 1.5>>\n===\n");
        var (code, output, _) = Run("import", "yarn", source, "--id", "x", "--locale", "en", "--out", Path_("o.json"), "--strings", Path_("o.en.json"));
        Assert.Equal(1, code);
        Assert.StartsWith("refused DLG_IMPORT_UNSUPPORTED: line 3", output, StringComparison.Ordinal);
        Assert.False(File.Exists(Path_("o.json")));
        Assert.False(File.Exists(Path_("o.en.json")));
    }

    [Theory]
    [InlineData("import", "yarn", "x.yarn")]                                                                         // no options
    [InlineData("import", "yarn", "x.yarn", "--id", "a", "--locale", "en", "--out", "o")]                            // no strings
    [InlineData("import", "yarn", "x.yarn", "--id", "a", "--locale", "en", "--out", "o", "--strings", "s", "--what", "x")]
    [InlineData("import", "yarn", "x.yarn", "--id", "a", "--locale", "en", "--out", "o", "--strings")]               // a dangling option
    [InlineData("import", "toml", "x.toml", "--id", "a", "--locale", "en", "--out", "o", "--strings", "s")]
    [InlineData("import", "matrix", "toml")]
    public void WrongArgumentsToImportAreAUsageMistake(params string[] args)
    {
        var (code, _, error) = Run(args);
        Assert.Equal(2, code);
        Assert.StartsWith("usage:", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingSourceIsAFileProblem()
    {
        var (code, _, error) = Run("import", "yarn", Path_("absent.yarn"), "--id", "a", "--locale", "en", "--out", Path_("o.json"), "--strings", Path_("s.json"));
        Assert.Equal(2, code);
        Assert.StartsWith("file problem:", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AReloadMapIsCheckedAgainstTheTwoDocumentsWithoutASession()
    {
        var old = Write("old.json", Fixture.Document);
        var edited = Write("new.json", Fixture.With("\"ask\"", "\"question\""));
        var good = Write("good.json", """{ "format": 1, "nodes": { "briefing/ask": "briefing/question" } }""");
        var (code, output, _) = Run("reload-map", old, edited, good);
        Assert.Equal(0, code);
        Assert.Equal("ok map: 0 conversations, 1 nodes, 0 variables, 0 quests, 0 stages renamed\n", output);
        var (badCode, badOutput, _) = Run("reload-map", old, edited, Write("bad.json", """{ "format": 1, "nodes": { "briefing/ask": "briefing/nowhere" } }"""));
        Assert.Equal(1, badCode);
        Assert.StartsWith("refused DLG_MAP_UNKNOWN_TARGET:", badOutput, StringComparison.Ordinal);
        Assert.Equal(1, Run("reload-map", old, edited, Write("worse.json", "[]")).Code);
        Assert.Equal(2, Run("reload-map", old, edited).Code);
        Assert.Equal(2, Run("reload-map", old, edited, Path_("absent.json")).Code);
    }

    [Fact]
    public void SeveralStartsAndAllowedFunctionsAreChosenOnTheCommandLineAndTheEntriesAndFactsArePrinted()
    {
        const string script = "title: One\n---\n<<if has_key()>>\nAnn: Open.\n<<endif>>\n===\ntitle: Two\n---\nAnn: Two.\n===\n";
        var source = Write("two.yarn", script);
        var common = new[] { "--id", "two.demo", "--locale", "en", "--out", Path_("two.json"), "--strings", Path_("two.en.json"), "--function", "has_key()=has-key:bool" };
        var (code, output, error) = Run(["import", "yarn", source, .. common, "--starts", "One,Two"]);
        Assert.Equal(0, code);
        Assert.Equal("", error);
        Assert.Contains("conversations the game can start: one -> talk-one, two -> talk-two", output, StringComparison.Ordinal);
        Assert.Contains("external facts the host must supply: ext.has-key (Boolean)", output, StringComparison.Ordinal);
        Assert.Equal(0, Run("check", Path_("two.json")).Code);

        var all = Run(["import", "yarn", source, .. common, "--all-starts"]);
        Assert.Equal(0, all.Code);
        Assert.Contains("one -> talk-one, two -> talk-two", all.Output, StringComparison.Ordinal);

        var single = Run(["import", "yarn", source, .. common]);
        Assert.Contains("conversations the game can start: one -> talk", single.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void AFunctionThatIsNotAllowedOrAMalformedListEntryIsRefusedWithTheFunctionCode()
    {
        var source = Write("fn.yarn", "title: Start\n---\n<<if has_key()>>\nAnn: Open.\n<<endif>>\n===\n");
        var common = new[] { "--id", "x", "--locale", "en", "--out", Path_("o.json"), "--strings", Path_("o.en.json") };
        var notAllowed = Run(["import", "yarn", source, .. common]);
        Assert.Equal(1, notAllowed.Code);
        Assert.StartsWith("refused DLG_IMPORT_FUNCTION: ", notAllowed.Output, StringComparison.Ordinal);
        var malformed = Run(["import", "yarn", source, .. common, "--function", "has_key()=has-key:float"]);
        Assert.Equal(1, malformed.Code);
        Assert.StartsWith("refused DLG_IMPORT_FUNCTION: ", malformed.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(Path_("o.json")));
    }
}
