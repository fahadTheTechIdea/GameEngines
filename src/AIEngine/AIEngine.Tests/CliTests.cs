using Xunit;

namespace AIEngine.Tests;

/// <summary>The AI authoring tool: a document is accepted with its figures or refused with its code, a scenario is explained, and every mistake of the command line has its exit code.</summary>
public sealed class CliTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ai-cli-" + Guid.NewGuid().ToString("N"));

    public CliTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    private string File(string name, string text)
    {
        var path = Path.Combine(_folder, name);
        System.IO.File.WriteAllText(path, text);
        return path;
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = AiCli.Run(args, output, error);
        return (code, output.ToString().Replace("\r\n", "\n"), error.ToString().Replace("\r\n", "\n"));
    }

    private const string Scenario = """
        { "behavior": "commander", "tick": 15, "keys": { "ready": true, "distance": 250 }, "percepts": [
          { "kind": "unit", "id": "a1", "owner": "", "attrs": { "hostile": false, "standing": true, "health": 100 } },
          { "kind": "unit", "id": "b1", "owner": "", "attrs": { "hostile": true, "standing": true, "health": 90 } } ] }
        """;

    [Fact]
    public void ACheckOfAGoodDocumentSaysSoWithTheCostOfEachBehaviour()
    {
        var (code, output, error) = Run("check", File("good.json", Fixture.Document));
        Assert.Equal(0, code);
        Assert.Equal("", error);
        var lines = output.TrimEnd('\n').Split('\n');
        Assert.StartsWith("ok demo.ai digest " + Fixture.Load().Digest + " keys 11 percept-kinds 2 intents 3 behaviours 3", lines[0]);
        Assert.Equal("behaviour commander every 1 nodes 15 memory-slots 0 max-cost 348", lines[1]);
        Assert.Equal("behaviour scout every 1 nodes 10 memory-slots 4 max-cost 9", lines[2]);
        Assert.Equal("behaviour worker every 10 nodes 3 memory-slots 1 max-cost 6", lines[3]);
        Assert.Equal("budget a step needs at least 348", lines[4]);
    }

    [Theory]
    [InlineData("\"capacity\": 16, \"memory\": 50", "\"capacity\": 0, \"memory\": 50", AiCodes.Range)]
    [InlineData("\"ownerCapacity\": 4, ", "", AiCodes.MissingField)]
    [InlineData("\"format\": 1", "\"format\": 2", AiCodes.Format)]
    [InlineData("\"kind\": \"unit\", \"capacity\"", "\"kind\": \"unit\", \"extra\": 1, \"capacity\"", AiCodes.UnknownField)]
    public void ACheckOfABadDocumentPrintsTheCodeAndWhereAndExitsOne(string old, string replacement, string code)
    {
        var (exit, output, error) = Run("check", File("bad.json", Fixture.With(old, replacement)));
        Assert.Equal(1, exit);
        Assert.Equal("", error);
        Assert.StartsWith("refused " + code + ": ", output);
        Assert.Single(output.TrimEnd('\n').Split('\n'));
    }

    [Fact]
    public void ATextThatIsNotJsonIsRefusedAsAFormatMistake()
    {
        var (exit, output, _) = Run("check", File("junk.json", "{ nope"));
        Assert.Equal(1, exit);
        Assert.StartsWith("refused " + AiCodes.Format + ": ", output);
    }

    [Fact]
    public void ExplainRunsOneThinkAndPrintsTheTraceAndTheIntentsItRaised()
    {
        var (code, output, error) = Run("explain", File("doc.json", Fixture.Document), File("scenario.json", Scenario));
        Assert.Equal(0, code);
        Assert.Equal("", error);
        Assert.Equal(
            "agent subject behaviour commander tick 15 spent 21 steps 15\n"
            + "main [sequence] success\n"
            + "  set-phase [let] success - phase = 0\n"
            + "  arm [succeed] success\n"
            + "    arm-seq [sequence] success\n"
            + "      on-beat [condition] success - holds\n"
            + "      mark-due [let] success - due = true\n"
            + "  is-ready [condition] success - holds\n"
            + "  is-due [condition] success - holds\n"
            + "  clear-due [let] success - due = false\n"
            + "  count-foes [count] success - foes = 1\n"
            + "  has-foes [condition] success - holds\n"
            + "  each-own [forEach] success - 1 found, 1 run\n"
            + "    shoot [sequence] success\n"
            + "      pick-foe [select] success - target = 'b1' of 1 found\n"
            + "      fire-at [act] success - raises fire(attacker='a1', defender='b1', metres=250)\n"
            + "intent fire(attacker='a1', defender='b1', metres=250) from fire-at\n", output);
    }

    [Fact]
    public void ExplainSaysWhenNothingWasRaisedAndWhyTheTreeStopped()
    {
        var calm = Scenario.Replace("\"ready\": true", "\"ready\": false", StringComparison.Ordinal);
        var (code, output, _) = Run("explain", File("doc.json", Fixture.Document), File("calm.json", calm));
        Assert.Equal(0, code);
        Assert.Contains("is-ready [condition] failure - does not hold", output);
        Assert.EndsWith("no intent raised\n", output);
    }

    [Theory]
    [InlineData("\"ready\": true", "\"nobody\": true", AiCodes.Reference)]                                   // not a key of the document
    [InlineData("\"ready\": true", "\"ready\": 1", AiCodes.ValueType)]                                       // not a boolean
    [InlineData("\"kind\": \"unit\", \"id\": \"b1\"", "\"kind\": \"ghost\", \"id\": \"b1\"", AiCodes.Reference)]   // not a percept kind
    [InlineData("\"health\": 90", "\"wealth\": 90", AiCodes.Reference)]                                       // not an attribute of the kind
    [InlineData("\"health\": 90", "\"health\": \"90\"", AiCodes.ValueType)]
    [InlineData("\"tick\": 15", "\"tick\": -1", AiCodes.Range)]
    [InlineData("\"behavior\": \"commander\"", "\"behavior\": \"nobody\"", AiCodes.UnknownBehavior)]
    [InlineData("\"tick\": 15,", "", AiCodes.MissingField)]
    [InlineData("\"tick\": 15,", "\"tick\": 15, \"more\": 1,", AiCodes.UnknownField)]
    [InlineData("\"health\": 100", "\"health\": 5000", AiCodes.Percept)]                                       // outside its range
    public void AScenarioTheDocumentCannotHoldIsRefusedWithItsCode(string old, string replacement, string code)
    {
        Assert.Contains(old, Scenario, StringComparison.Ordinal);
        var (exit, output, error) = Run("explain", File("doc.json", Fixture.Document), File("scenario.json", Scenario.Replace(old, replacement, StringComparison.Ordinal)));
        Assert.Equal(1, exit);
        Assert.Equal("", error);
        Assert.StartsWith("refused " + code + ": ", output);
    }

    [Fact]
    public void ABadDocumentIsRefusedByExplainBeforeAnyScenarioIsRead()
    {
        var (exit, output, _) = Run("explain", File("bad.json", Fixture.With("\"format\": 1", "\"format\": 2")), File("scenario.json", Scenario));
        Assert.Equal(1, exit);
        Assert.StartsWith("refused " + AiCodes.Format, output);
    }

    [Theory]
    [InlineData]
    [InlineData("check")]
    [InlineData("explain", "only-one")]
    [InlineData("check", "a", "b")]
    [InlineData("explain", "a", "b", "c")]
    [InlineData("build", "a")]
    public void WrongArgumentsPrintTheUsageAndExitTwo(params string[] args)
    {
        var (code, output, error) = Run(args);
        Assert.Equal(2, code);
        Assert.Equal("", output);
        Assert.StartsWith("usage: AIEngine.Cli check", error);
    }

    [Fact]
    public void AFileThatCannotBeReadIsAFileProblemWithExitTwo()
    {
        var missing = Path.Combine(_folder, "missing.json");
        var (code, output, error) = Run("check", missing);
        Assert.Equal(2, code);
        Assert.Equal("", output);
        Assert.StartsWith("file problem: ", error);
        var (second, _, secondError) = Run("explain", File("doc.json", Fixture.Document), missing);
        Assert.Equal(2, second);
        Assert.StartsWith("file problem: ", secondError);
    }

    [Theory]
    [InlineData("GameRuntime", "GameRuntime.Field.Headless", "field-ai.json")]
    [InlineData("GameRuntime", "GameRuntime.Combat.Headless", "skirmish-bots.json")]
    [InlineData("GameRuntime", "GameRuntime.Headless", "tycoon-bots.json")]
    public void TheDocumentsTheGamesShipAreAcceptedByTheTool(string area, string project, string file)
    {
        var folder = AppContext.BaseDirectory;
        while (folder is not null && !System.IO.File.Exists(Path.Combine(folder, "GameFrameWork.slnx"))) folder = Path.GetDirectoryName(folder);
        Assert.NotNull(folder);
        var (code, output, error) = Run("check", Path.Combine(folder!, "src", area, project, file));
        Assert.Equal(0, code);
        Assert.Equal("", error);
        Assert.StartsWith("ok ", output);
    }
}
