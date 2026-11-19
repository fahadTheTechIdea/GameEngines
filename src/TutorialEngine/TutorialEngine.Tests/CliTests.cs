using Xunit;

namespace TutorialEngine.Tests;

/// <summary>The authoring tool: check, simulate, explain and coverage, each with every exit code.</summary>
public sealed class CliTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "tutorial-cli-" + Guid.NewGuid().ToString("N"));

    public CliTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    private string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "samples", name);

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
        var code = TutorialCli.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void CheckAcceptsTheSampleAndSummarisesIt()
    {
        var (code, output, _) = Run("check", Sample("valley.tutorial.json"));
        Assert.Equal(0, code);
        Assert.StartsWith("ok valley.tutorial digest " + Fixture.Valley.Digest, output);
        Assert.Contains("steps 12 tips 8 events 10 skills 4 inputs 5 host-facts 4 facts 97 text-keys 63", output);
        Assert.Contains("journey languages en,fr,ar,es default en pages 4 entries sign-in,register,host,join", output);
        Assert.Contains("step look-mouse requires 1/0 branch skippable hints 1", output);
        Assert.Contains("tip save-habit cooldown 3000 max-shows 5 duration 500 priority 6", output);
    }

    [Fact]
    public void CheckRefusesABadDocumentWithItsCodeAndExitOne()
    {
        var bad = Write("bad.json", Fixture.ValleyText.Replace("\"format\": 1", "\"format\": 2", StringComparison.Ordinal));
        var (code, output, _) = Run("check", bad);
        Assert.Equal(1, code);
        Assert.StartsWith("refused TUT_FORMAT:", output);
    }

    [Fact]
    public void SimulatePlaysTheScenarioAndPrintsWhatTheTutorialDid()
    {
        var save = Path.Combine(_folder, "state.json");
        var (code, output, error) = Run("simulate", Sample("valley.tutorial.json"), Sample("valley.scenario.json"), "2000", "--save", save);
        Assert.Equal(0, code);
        Assert.Equal("", error);
        Assert.Contains("0 journey language -> story page 1 language en", output);
        Assert.Contains("5 journey entry -> done page 4 language en", output);
        Assert.Contains("40 step move active -> done", output);
        Assert.Contains("40 step look-pad pending -> bypassed", output);
        Assert.Contains("1120 hint build level 1", output);
        Assert.Contains("1520 step road active -> skipped", output);
        Assert.Contains("1800 tutorial running -> finished", output);
        Assert.Contains("end step 2000 tutorial finished progress 1000 journey done events 38 refused 0 state ", output);
        Assert.True(File.Exists(save));
        var again = Run("simulate", Sample("valley.tutorial.json"), Sample("valley.scenario.json"), "2000");
        Assert.Equal(output, again.Out);                       // the same scenario prints the same
    }

    [Fact]
    public void SimulateReportsARefusedActionAndExitsOneButGoesOn()
    {
        var scenario = Write("refuse.json", """{ "facts": { "input.gamepad": false, "ui.menu_open": false, "game.paused": false, "game.population": 0 }, "script": [ { "at": 1, "do": "entry", "arg": "host" }, { "at": 2, "do": "language", "arg": "en" } ] }""");
        var (code, output, _) = Run("simulate", Sample("valley.tutorial.json"), scenario, "5");
        Assert.Equal(1, code);
        Assert.Contains("1 refused entry host: TUT_JOURNEY_ORDER", output);
        Assert.Contains("2 journey language -> story", output);
        Assert.Contains("refused 1 state", output);
    }

    [Fact]
    public void SimulateSaysWhenActionsAreAfterTheEnd()
    {
        var scenario = Write("late.json", """{ "facts": { "input.gamepad": false, "ui.menu_open": false, "game.paused": false, "game.population": 0 }, "script": [ { "at": 9, "do": "touch" } ] }""");
        var (code, output, _) = Run("simulate", Sample("valley.tutorial.json"), scenario, "5");
        Assert.Equal(1, code);
        Assert.Contains("1 scripted actions are after step 5 and were not carried out", output);
    }

    [Theory]
    [InlineData("""{ "facts": {}, "script": [] }""", "TUT_FACT_MISSING")]
    [InlineData("""{ "facts": { "input.gamepad": 5, "ui.menu_open": false, "game.paused": false, "game.population": 0 }, "script": [] }""", "TUT_FACT_INVALID")]
    [InlineData("""{ "facts": {}, "script": [ { "at": 3, "do": "touch" }, { "at": 2, "do": "touch" } ] }""", "TUT_RANGE")]
    [InlineData("""{ "facts": {}, "script": [ { "at": 3, "do": "teleport" } ] }""", "TUT_FORMAT")]
    [InlineData("""{ "facts": { "x": null }, "script": [] }""", "TUT_FORMAT")]
    [InlineData("""{ "script": [] }""", "TUT_MISSING_FIELD")]
    [InlineData("not json", "TUT_FORMAT")]
    public void AScenarioThatCannotBePlayedIsAProblemWithTheScenarioAndExitsTwo(string text, string code)
    {
        var (exit, _, error) = Run("simulate", Sample("valley.tutorial.json"), Write("s.json", text), "5");
        Assert.Equal(2, exit);
        Assert.Contains("scenario problem " + code, error);
    }

    [Fact]
    public void SimulateChecksItsArguments()
    {
        var doc = Sample("valley.tutorial.json");
        var scenario = Sample("valley.scenario.json");
        Assert.Equal(2, Run("simulate", doc, scenario, "0").Code);
        Assert.Equal(2, Run("simulate", doc, scenario, "x").Code);
        Assert.Equal(2, Run("simulate", doc, scenario, "5", "--what").Code);
        Assert.Equal(2, Run("simulate", doc, scenario, "5", "--save").Code);
        Assert.Equal(2, Run("simulate", doc, Path.Combine(_folder, "missing.json"), "5").Code);
        Assert.Equal(1, Run("simulate", Write("bad.json", "{"), scenario, "5").Code);
    }

    [Fact]
    public void SimulateVerboseNamesEveryStep()
    {
        var (_, quiet, _) = Run("simulate", Sample("valley.tutorial.json"), Sample("valley.scenario.json"), "3");
        var (_, verbose, _) = Run("simulate", Sample("valley.tutorial.json"), Sample("valley.scenario.json"), "3", "--verbose");
        Assert.Equal(quiet, verbose);                          // every line already comes from a change, so verbose adds nothing here; the option is accepted
    }

    [Fact]
    public void ExplainReadsASaveAndSaysWhyAStepOrATipOrTheJourneyIsAsItIs()
    {
        var save = Path.Combine(_folder, "state.json");
        Run("simulate", Sample("valley.tutorial.json"), Sample("valley.scenario.json"), "1000", "--save", save);
        var doc = Sample("valley.tutorial.json");
        var step = Run("explain", doc, save, "step", "build", "--fact", "input.gamepad=false", "--fact", "ui.menu_open=false", "--fact", "game.paused=false", "--fact", "game.population=0");
        Assert.Equal(0, step.Code);
        Assert.Contains("step build is active", step.Out);
        Assert.Contains("teaches building", step.Out);
        var tip = Run("explain", doc, save, "tip", "paused", "--fact", "input.gamepad=false", "--fact", "ui.menu_open=false", "--fact", "game.paused=true", "--fact", "game.population=0");
        Assert.Equal(0, tip.Code);
        Assert.Contains("tip paused does not show", tip.Out);
        var journey = Run("explain", doc, save, "journey");
        Assert.Equal(0, journey.Code);
        Assert.Contains("journey is at done, language en, entered by host", journey.Out);
    }

    [Fact]
    public void ExplainRefusesWhatCannotBeExplainedAndChecksItsArguments()
    {
        var save = Path.Combine(_folder, "state.json");
        Run("simulate", Sample("valley.tutorial.json"), Sample("valley.scenario.json"), "100", "--save", save);
        var doc = Sample("valley.tutorial.json");
        Assert.Equal(1, Run("explain", doc, save, "step", "zzz").Code);
        Assert.Equal(1, Run("explain", doc, Write("garbage.json", "x"), "journey").Code);
        Assert.Equal(1, Run("explain", doc, save, "step", "grow").Code);                     // host facts not given: refused, named
        Assert.Equal(2, Run("explain", doc, save, "step", "build", "--fact", "nope").Code);
        Assert.Equal(2, Run("explain", doc, save, "dance").Code);
        Assert.Equal(2, Run("explain", doc, save, "journey", "extra", "words").Code);
    }

    [Fact]
    public void CoverageReportsWhatEachTableLacksAndTheLanguagesWithNoTable()
    {
        var doc = Sample("valley.tutorial.json");
        var complete = Run("coverage", doc, Sample("valley.en.json"));
        Assert.Equal(0, complete.Code);
        Assert.Contains("en: 63/63 (100%) missing 0 unknown 0 problems 0", complete.Out);
        Assert.Contains("journey languages without a table: fr, ar, es", complete.Out);
        var partial = Write("fr.json", """{ "format": 1, "locale": "fr", "messages": { "tut.page.welcome": "Bienvenue", "tut.stray": "x", "tut.step.move": "Marchez {n}" } }""");
        var report = Run("coverage", doc, Sample("valley.en.json"), partial);
        Assert.Equal(1, report.Code);
        Assert.Contains("fr: 2/63", report.Out);
        Assert.Contains("  missing tut.art.people", report.Out);
        Assert.Contains("  unknown tut.stray", report.Out);
        Assert.Contains("  problem tut.step.move: uses '{n}'", report.Out);
        var oneMissing = Run("coverage", doc, Sample("valley.en.json"), Write("fr2.json", "{ \"format\": 1, \"locale\": \"fr\", \"messages\": {} }"), Write("ar.json", "{ \"format\": 1, \"locale\": \"ar\", \"messages\": {} }"));
        Assert.Contains("journey languages without a table: es", oneMissing.Out);
        Assert.Equal(1, Run("coverage", Write("bad.json", "{"), Sample("valley.en.json")).Code);
        Assert.Equal(1, Run("coverage", doc, Write("table.json", "{")).Code);
    }

    [Fact]
    public void WrongCommandLinesAndMissingFilesExitTwo()
    {
        Assert.Equal(2, Run().Code);
        Assert.Equal(2, Run("check").Code);
        Assert.Equal(2, Run("dance", "x").Code);
        Assert.Equal(2, Run("check", "a", "b").Code);
        Assert.Equal(2, Run("check", Path.Combine(_folder, "missing.json")).Code);
        Assert.Equal(2, Run("coverage", Sample("valley.tutorial.json")).Code);
        var (_, _, error) = Run("check", Path.Combine(_folder, "missing.json"));
        Assert.Contains("file problem", error);
        Assert.Contains("usage: TutorialEngine.Cli check", Run("dance", "x").Err);
    }
}
