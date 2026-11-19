using System.Text.RegularExpressions;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>The authoring tool: what each command prints and every exit code.</summary>
public sealed class CliTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "dip-cli-" + Guid.NewGuid().ToString("N"));

    public CliTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "samples", name);

    private string Write(string name, string text)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = DipCli.Run(args, output, error);
        return (code, Unix(output.ToString()), Unix(error.ToString()));
    }

    private static string Unix(string text) => text.Replace(Environment.NewLine, "\n", StringComparison.Ordinal);

    private static readonly string Doc = Sample("isles.diplomacy.json");
    private static readonly string Scenario = Sample("isles.diplomacy.scenario.json");
    private static readonly string English = Sample("isles.diplomacy.en.json");

    // ---- check ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void CheckSaysWhatTheDocumentHolds()
    {
        var (code, output, error) = Run("check", Doc);
        Assert.Equal(0, code);
        Assert.Equal("", error);
        Assert.Matches(@"^ok isles digest [0-9A-F]{64} steps-per-day 1 realms 5 ideologies 3 traits 2 types 8 casus-belli 6 missions 4 deeds 7 agents 6 host-facts 1 text-keys 56\n", output);
        Assert.Contains("realm cinder host crown might 300000 stability 600 legitimacy 700 factions nobles:500,guilds:500", output);
        Assert.Contains("type vassalage mutual clauses vassal,tribute,defend term none or 60-3650 days cancel never", output);
        Assert.Contains("type peace mutual clauses non_aggression,tribute term 30-360 days cancel breach ends-war", output);
        Assert.Contains("casus-belli rivalry standing infamy 100 honour 50", output);
        Assert.Contains("shocks embargo,embargo-lifted,sabotage,trade-boom,trade-bust", output);
    }

    [Fact]
    public void CheckRefusesAWrongDocumentWithItsCodeAndExitOne()
    {
        var bad = Write("bad.json", Fixture.IslesText.Replace("\"format\": 1", "\"format\": 2", StringComparison.Ordinal));
        var (code, output, _) = Run("check", bad);
        Assert.Equal(1, code);
        Assert.StartsWith("refused DIP_FORMAT:", output);
    }

    [Fact]
    public void AFileThatIsNotThereIsAFileProblemAndExitTwo()
    {
        var (code, _, error) = Run("check", Path.Combine(_folder, "missing.json"));
        Assert.Equal(2, code);
        Assert.StartsWith("file problem:", error);
    }

    // ---- simulate -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void SimulatePrintsWhatHappenedAndAnEndLineWithTheDigest()
    {
        var (code, output, error) = Run("simulate", Doc, Scenario, "120");
        Assert.Equal(0, code);
        Assert.Equal("", error);
        Assert.Contains("40 war 0 ember vs dune ended score 180", output);
        Assert.Matches(@"end step 119 season summer treaties 3 wars 0 coalitions 0 proposals 0 changes [a-z_:,0-9]+ refused 0 state [0-9A-F]{64}\n$", output);
        Assert.DoesNotContain(" season ", output.Replace("end step 119 season summer", ""));
    }

    [Fact]
    public void SimulateIsTheSameEveryTimeAndSavesTheState()
    {
        var save = Path.Combine(_folder, "state.json");
        var first = Run("simulate", Doc, Scenario, "120", "--save", save);
        var second = Run("simulate", Doc, Scenario, "120");
        Assert.Equal(first.Out, second.Out);
        var set = DipLoader.Load(File.ReadAllText(Doc));
        var runtime = DipSave.Open(set, File.ReadAllBytes(save));
        Assert.Equal(119, runtime.Step);
        Assert.Contains(runtime.StateDigest(), first.Out);
    }

    [Fact]
    public void VerbosePrintsTheSeasonsAndTheChangesOfKnowledgeToo()
    {
        var quiet = Run("simulate", Doc, Scenario, "120").Out;
        var verbose = Run("simulate", Doc, Scenario, "120", "--verbose").Out;
        Assert.DoesNotContain(" season ", quiet.Replace("end step 119 season summer", ""));
        Assert.Contains("0 season - -> spring", verbose);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("x")]
    [InlineData("10000001")]
    public void StepsMustBeAWholeNumberInRange(string steps)
    {
        var (code, _, error) = Run("simulate", Doc, Scenario, steps);
        Assert.Equal(2, code);
        Assert.Contains("<steps> must be a whole number from 1 to 10000000", error);
    }

    [Theory]
    [InlineData("--bogus")]
    [InlineData("--save")]
    public void AnUnknownOrIncompleteOptionIsAUsageError(string option)
    {
        var (code, _, error) = Run("simulate", Doc, Scenario, "10", option);
        Assert.Equal(2, code);
        Assert.Contains("unknown or incomplete option", error);
    }

    [Fact]
    public void ACallThatIsRefusedIsPrintedCountedAndMakesExitOne()
    {
        var scenario = Write("refused.json", "{\"facts\":{\"game.crisis\":1},\"script\":[{\"at\":2,\"do\":\"war\",\"arg\":\"aurel:nobody:\"},{\"at\":3,\"do\":\"propose\",\"arg\":\"aurel:brass:nap:xx:0\"}]}");
        var (code, output, _) = Run("simulate", Doc, scenario, "10");
        Assert.Equal(1, code);
        Assert.Contains("2 refused war aurel:nobody:: DIP_UNKNOWN_REALM", output);
        Assert.Contains("3 refused propose aurel:brass:nap:xx:0: DIP_FORMAT", output);
        Assert.Contains("refused 2", output);
    }

    [Fact]
    public void ACallAfterTheLastStepIsReportedAndMakesExitOne()
    {
        var scenario = Write("late.json", "{\"facts\":{\"game.crisis\":1},\"script\":[{\"at\":50,\"do\":\"succession\",\"arg\":\"aurel\"}]}");
        var (code, output, _) = Run("simulate", Doc, scenario, "10");
        Assert.Equal(1, code);
        Assert.Contains("1 scripted calls are at or after step 10 and were not carried out", output);
    }

    [Theory]
    [InlineData("{\"facts\":{},\"script\":[{\"at\":0,\"do\":\"dance\"}]}", "script[0].do 'dance' must be one of")]
    [InlineData("{\"facts\":{},\"script\":[{\"at\":5,\"do\":\"succession\"},{\"at\":3,\"do\":\"succession\"}]}", "goes back from 5 to 3")]
    [InlineData("{\"facts\":{},\"script\":[{\"at\":0,\"do\":\"succession\",\"extra\":1}]}", "DIP_UNKNOWN_FIELD")]
    [InlineData("{\"facts\":{\"game.crisis\":[1]},\"script\":[]}", "scenario fact 'game.crisis'")]
    [InlineData("not json", "DIP_FORMAT")]
    [InlineData("{\"facts\":{},\"script\":[]}", "DIP_FACT_MISSING")]
    [InlineData("{\"facts\":{\"game.crisis\":99},\"script\":[]}", "DIP_FACT_INVALID")]
    public void AScenarioThatIsWrongIsAScenarioProblemAndExitTwo(string text, string expected)
    {
        var scenario = Write("wrong.json", text);
        var (code, _, error) = Run("simulate", Doc, scenario, "5");
        Assert.Equal(2, code);
        Assert.Contains("scenario problem", error);
        Assert.Contains(expected, error);
    }

    [Fact]
    public void AFactChangedByTheScriptIsUsedFromThenOn()
    {
        var scenario = Write("fact.json", "{\"facts\":{\"game.crisis\":1},\"script\":[{\"at\":2,\"do\":\"fact\",\"arg\":\"game.crisis=99\"}]}");
        var (code, _, error) = Run("simulate", Doc, scenario, "5");
        Assert.Equal(2, code);
        Assert.Contains("DIP_FACT_INVALID", error);
        var bad = Write("fact2.json", "{\"facts\":{\"game.crisis\":1},\"script\":[{\"at\":2,\"do\":\"fact\",\"arg\":\"nonsense\"}]}");
        var (code2, output2, _) = Run("simulate", Doc, bad, "5");
        Assert.Equal(1, code2);
        Assert.Contains("2 refused fact nonsense: DIP_FACT_INVALID", output2);
    }

    [Fact]
    public void EveryKindOfScriptedCallIsCarriedOut()
    {
        var script = string.Join(",", new[]
        {
            "{\"at\":0,\"do\":\"recruit\",\"arg\":\"dune:agent-dune-2:500\"}", "{\"at\":0,\"do\":\"might\",\"arg\":\"dune:260000\"}", "{\"at\":0,\"do\":\"leader\",\"arg\":\"dune:aurel:100\"}",
            "{\"at\":0,\"do\":\"deed\",\"arg\":\"aurel:dune:gift:100\"}", "{\"at\":0,\"do\":\"happen\",\"arg\":\"aurel:disaster:100\"}", "{\"at\":0,\"do\":\"grant\",\"arg\":\"aurel:ember:border:50\"}",
            "{\"at\":1,\"do\":\"war\",\"arg\":\"aurel:ember:border\"}", "{\"at\":1,\"do\":\"join\",\"arg\":\"0:dune:aurel\"}", "{\"at\":2,\"do\":\"score\",\"arg\":\"0:aurel:100:battle\"}", "{\"at\":2,\"do\":\"leave\",\"arg\":\"0:dune\"}",
            "{\"at\":3,\"do\":\"propose\",\"arg\":\"cinder:brass:trade:0:0\"}", "{\"at\":3,\"do\":\"withdraw\",\"arg\":\"0:cinder\"}", "{\"at\":4,\"do\":\"propose\",\"arg\":\"aurel:brass:embargo:30:0\"}", "{\"at\":4,\"do\":\"cancel\",\"arg\":\"0:aurel\"}",
            "{\"at\":21,\"do\":\"succession\",\"arg\":\"ember\"}",
        });
        var scenario = Write("all.json", "{\"facts\":{\"game.crisis\":1},\"script\":[" + script + "]}");
        var (code, output, _) = Run("simulate", Doc, scenario, "30");
        Assert.Contains("war 0 aurel vs ember declared cb border", output);
        Assert.Contains("war 0 aurel vs ember joined", output);
        Assert.Contains("war 0 aurel vs ember left", output);
        Assert.Contains("proposal 0 trade cinder -> brass withdrawn", output);
        Assert.Contains("treaty 0 embargo aurel / brass cancelled", output);
        Assert.Contains("politics ember", output);
        Assert.DoesNotContain(" refused ", output.Replace("refused 1", "").Replace("refused 0", ""));
        Assert.Equal(0, code);
        Assert.Contains("refused 0", output);
    }

    // ---- explain --------------------------------------------------------------------------------------------------------------------

    private string Saved()
    {
        var save = Path.Combine(_folder, "state.json");
        if (!File.Exists(save)) Run("simulate", Doc, Scenario, "120", "--save", save);
        return save;
    }

    [Fact]
    public void ExplainSaysWhyAProposalWouldBeAccepted()
    {
        var (code, output, _) = Run("explain", Doc, Saved(), "proposal", "aurel", "cinder", "alliance", "0", "0", "--fact", "game.crisis=2");
        Assert.Equal(0, code);
        Assert.StartsWith("proposal alliance from aurel to cinder, term none, amount 0 (cinder is decided by host)\n  bias: 1000 x -100 / 1000 = -100\n  regard:", output);
        Assert.Matches(@"  score -?\d+ \(accepts at 0, refuses below -200\): chance \d+ per mille", output);
    }

    [Fact]
    public void ExplainSaysWhereARegardCameFromWhatAMissionFacesHowARealmStandsAndWhyACoalitionDoesOrDoesNotStand()
    {
        Assert.Contains("regard of aurel for brass:", Run("explain", Doc, Saved(), "regard", "aurel", "brass").Out);
        Assert.Contains("mission sabotage by agent-aurel-1 against ember: succeeds", Run("explain", Doc, Saved(), "mission", "agent-aurel-1", "ember", "sabotage").Out);
        Assert.Contains("balance against ember: threat", Run("explain", Doc, Saved(), "coalition", "ember").Out);
        var realm = Run("explain", Doc, Saved(), "realm", "ember").Out;
        Assert.StartsWith("realm ember (ai): might 200000", realm);
        Assert.Contains("\n  faction zealots share 700 content", realm);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("regard", "aurel")]
    [InlineData("proposal", "aurel", "brass", "nap", "30")]
    public void ExplainWithoutWhatItNeedsIsAUsageError(params string[] rest)
    {
        var (code, _, error) = Run(["explain", Doc, Saved(), .. rest]);
        Assert.Equal(2, code);
        Assert.Contains("usage: DiplomacyEngine.Cli", error);
    }

    [Fact]
    public void ExplainChecksItsNumbersItsFactsAndItsNames()
    {
        Assert.Equal(2, Run("explain", Doc, Saved(), "proposal", "aurel", "brass", "nap", "xx", "0").Code);
        Assert.Equal(2, Run("explain", Doc, Saved(), "proposal", "aurel", "brass", "nap", "30", "yy").Code);
        Assert.Equal(2, Run("explain", Doc, Saved(), "proposal", "aurel", "brass", "nap", "30", "0", "--fact", "novalue").Code);
        Assert.Equal(2, Run("explain", Doc, Saved(), "proposal", "aurel", "brass", "nap", "30", "0", "--bogus", "x").Code);
        var (code, output, _) = Run("explain", Doc, Saved(), "regard", "aurel", "nobody");
        Assert.Equal(1, code);
        Assert.StartsWith("refused DIP_UNKNOWN_REALM:", output);
        var (code2, output2, _) = Run("explain", Doc, Saved(), "proposal", "aurel", "brass", "nap", "30", "0");
        Assert.Equal(1, code2);
        Assert.StartsWith("refused DIP_FACT_MISSING:", output2);
    }

    [Fact]
    public void ExplainOfASaveFromAnotherDocumentIsRefused()
    {
        var other = Write("other.json", Fixture.IslesText.Replace("\"seed\": 20261029", "\"seed\": 5", StringComparison.Ordinal));
        var (code, output, _) = Run("explain", other, Saved(), "realm", "ember");
        Assert.Equal(1, code);
        Assert.StartsWith("refused DIP_SAVE_DOCUMENT:", output);
    }

    // ---- coverage -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void CoverageOfACompleteTableIsExitZero()
    {
        var (code, output, _) = Run("coverage", Doc, English);
        Assert.Equal(0, code);
        Assert.Equal("en: 56/56 (100%) missing 0 unknown 0 problems 0\n", output);
    }

    [Fact]
    public void CoverageListsWhatIsMissingUnknownAndWrongAndExitsOne()
    {
        var table = Write("part.json", "{\"format\":1,\"locale\":\"fr\",\"messages\":{\"realm.aurel\":\"Aurélie {nom}\",\"realm.nowhere\":\"Nulle part\"}}");
        var (code, output, _) = Run("coverage", Doc, English, table);
        Assert.Equal(1, code);
        Assert.Contains("en: 56/56 (100%) missing 0 unknown 0 problems 0", output);
        Assert.Contains("fr: 1/56 (1%) missing 55 unknown 1 problems 1", output);
        Assert.Contains("  missing realm.brass", output);
        Assert.Contains("  unknown realm.nowhere", output);
        Assert.Contains("  problem realm.aurel: uses '{nom}', which a name does not give", output);
    }

    [Fact]
    public void CoverageOfATableOrDocumentThatIsWrongIsRefused()
    {
        var table = Write("broken.json", "{\"format\":1}");
        Assert.Equal(1, Run("coverage", Doc, table).Code);
        var doc = Write("bad.json", "{}");
        var (code, output, _) = Run("coverage", doc, English);
        Assert.Equal(1, code);
        Assert.StartsWith("refused DIP_", output);
    }

    // ---- usage ----------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData]
    [InlineData("check")]
    [InlineData("check", "a", "b")]
    [InlineData("simulate", "a", "b")]
    [InlineData("explain", "a", "b")]
    [InlineData("coverage", "a")]
    [InlineData("frobnicate", "a", "b")]
    public void AnythingElseIsAUsageErrorAndExitTwo(params string[] args)
    {
        var (code, output, error) = Run(args);
        Assert.Equal(2, code);
        Assert.Equal("", output);
        Assert.StartsWith("usage: DiplomacyEngine.Cli check <diplomacy.json>", error);
        Assert.Equal(8, Regex.Matches(error, "DiplomacyEngine.Cli").Count);
    }
}
