using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>The authoring tool: check, simulate, explain and coverage, each with every exit code.</summary>
public sealed class CliTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "world-cli-" + Guid.NewGuid().ToString("N"));

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
        var code = WorldCli.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void CheckAcceptsTheSampleAndSummarisesIt()
    {
        var (code, output, _) = Run("check", Sample("valley.world.json"));
        Assert.Equal(0, code);
        Assert.StartsWith("ok valley.world digest " + Fixture.Valley.Digest, output);
        Assert.Contains("steps-per-day 1 year 365 regions 3 links 2 zones 2 weathers 5 hazards 5 diseases 2 interventions 3 cards 8 groups 3 host-facts 1 text-keys 55", output);
        Assert.Contains("seasons spring:92,summer:92,autumn:91,winter:90 storytellers cassandra,phoebe,randy difficulties easy,normal,hard start cassandra/normal", output);
        Assert.Contains("hazard drought drought rate 1000000 ppm paced conditional rungs dry:1000000,severe:400000 reach 1", output);
        Assert.Contains("hazard quake quake rate 60 ppm rungs tremor:1000000,quake:126000,great:16000 reach 2", output);
        Assert.Contains("disease plague seir beta 300000 ppm incubation 4 infectious 8 waning 0 fatality 120 paced", output);
        Assert.Contains("card festival good global weight 50 cooldown 45 chain 0", output);
    }

    [Fact]
    public void CheckRefusesABadDocumentWithItsCodeAndExitOne()
    {
        var bad = Write("bad.json", Fixture.ValleyText.Replace("\"format\": 2", "\"format\": 3", StringComparison.Ordinal));
        var (code, output, _) = Run("check", bad);
        Assert.Equal(1, code);
        Assert.StartsWith("refused WE_FORMAT:", output);
        var old = Write("old.json", Fixture.ValleyText.Replace("\"format\": 2", "\"format\": 1", StringComparison.Ordinal));
        var (oldCode, oldOutput, _) = Run("check", old);
        Assert.Equal(1, oldCode);
        Assert.StartsWith("refused WE_FORMAT_SUPERSEDED:", oldOutput);
    }

    [Fact]
    public void SimulateLivesTheScenarioAndPrintsWhatHappened()
    {
        var save = Path.Combine(_folder, "state.json");
        var (code, output, error) = Run("simulate", Sample("valley.world.json"), Sample("valley.world.scenario.json"), "365", "--save", save);
        Assert.Equal(0, code);
        Assert.Equal("", error);
        Assert.Contains("0 season - -> spring", output);
        Assert.Contains("hazard storm in seaport started at tempest power 1000", output);          // the scripted storm of the scenario
        Assert.Contains("card festival forced stress", output);
        Assert.Contains("intervention hospital in riverton started", output);
        Assert.DoesNotContain(" weather ", output);                    // the weather is only printed with --verbose
        var last = output.TrimEnd().Split('\n').Last();
        Assert.StartsWith("end step 364 season winter ", last);
        Assert.Contains("refused 0 state ", last);
        Assert.True(File.Exists(save));
        // The same run, run again, ends in the same state; the saved world reopens to it.
        var again = Run("simulate", Sample("valley.world.json"), Sample("valley.world.scenario.json"), "365");
        Assert.Equal(last, again.Out.TrimEnd().Split('\n').Last());
        var runtime = WorldSave.Open(Fixture.Valley, File.ReadAllBytes(save));
        Assert.Contains(runtime.StateDigest(), last);
    }

    [Fact]
    public void SimulateWithVerbosePrintsTheWeatherAndNotCarryingOutACallIsExitOne()
    {
        var scenario = Write("late.json", """{ "facts": { "game.garrison": 30 }, "script": [ { "at": 5, "do": "hazard", "arg": "riverton:flood:major" }, { "at": 500, "do": "card", "arg": "festival" } ] }""");
        var (code, output, _) = Run("simulate", Sample("valley.world.json"), scenario, "40", "--verbose");
        Assert.Equal(1, code);
        Assert.Contains(" weather ", output);
        Assert.Contains("1 scripted calls are at or after step 40 and were not carried out", output);
    }

    [Fact]
    public void ARefusedScriptedCallIsPrintedAndMakesExitOne()
    {
        var scenario = Write("refused.json", """{ "facts": { "game.garrison": 30 }, "script": [ { "at": 3, "do": "intervene", "arg": "riverton:nope", "n": 1 }, { "at": 4, "do": "outbreak", "arg": "riverton:plague", "n": 0 } ] }""");
        var (code, output, _) = Run("simulate", Sample("valley.world.json"), scenario, "10");
        Assert.Equal(1, code);
        Assert.Contains("3 refused intervene riverton:nope: WE_UNKNOWN_INTERVENTION", output);
        Assert.Contains("4 refused outbreak riverton:plague: WE_COUNT", output);
        Assert.Contains("refused 2 state ", output);
    }

    [Fact]
    public void ASimulationThatLacksAHostFactIsAScenarioProblem()
    {
        var scenario = Write("nofact.json", """{ "facts": { }, "script": [ ] }""");
        var (code, _, error) = Run("simulate", Sample("valley.world.json"), scenario, "5");
        Assert.Equal(2, code);
        Assert.Contains("scenario problem WE_FACT_MISSING", error);
    }

    [Theory]
    [InlineData("""{ "facts": { }, "script": [ { "at": 5, "do": "dance" } ] }""", "WE_FORMAT")]
    [InlineData("""{ "facts": { }, "script": [ { "at": 5, "do": "card" }, { "at": 4, "do": "card" } ] }""", "WE_RANGE")]
    [InlineData("""{ "facts": { "x": [1] }, "script": [ ] }""", "WE_FORMAT")]
    [InlineData("""{ "script": [ ] }""", "WE_MISSING_FIELD")]
    public void AWrongScenarioIsRefusedByItsCodeWithExitTwo(string scenario, string code)
    {
        var (exit, _, error) = Run("simulate", Sample("valley.world.json"), Write("s.json", scenario), "5");
        Assert.Equal(2, exit);
        Assert.Contains("scenario problem " + code, error);
    }

    [Fact]
    public void ExplainReadsASavedWorldAndAnswersWithTheSameWordsAsTheEngine()
    {
        var save = Path.Combine(_folder, "state.json");
        Run("simulate", Sample("valley.world.json"), Sample("valley.world.scenario.json"), "200", "--save", save);
        var hazard = Run("explain", Sample("valley.world.json"), save, "hazard", "flood", "riverton", "--fact", "game.garrison=80");
        Assert.Equal(0, hazard.Code);
        Assert.StartsWith("hazard flood in riverton ", hazard.Out);
        var card = Run("explain", Sample("valley.world.json"), save, "card", "raiders", "--fact", "game.garrison=80");
        Assert.Equal(0, card.Code);
        Assert.Contains("when in riverton: game.garrison lt 60 [80] = false", card.Out);
        var oneRegion = Run("explain", Sample("valley.world.json"), save, "card", "raiders", "hillfort", "--fact", "game.garrison=10");
        Assert.Contains("when in hillfort: game.garrison lt 60 [10] = true", oneRegion.Out);
        Assert.DoesNotContain("when in riverton", oneRegion.Out);
        var disease = Run("explain", Sample("valley.world.json"), save, "disease", "plague", "seaport");
        Assert.StartsWith("disease plague (seir) in seaport: ", disease.Out);
        var weather = Run("explain", Sample("valley.world.json"), save, "weather", "riverton");
        Assert.Equal(0, weather.Code);
        Assert.StartsWith("weather in riverton: ", weather.Out);
        Assert.Contains(", spill 300 per mille, persistence ", weather.Out);
        Assert.Contains("linked to ", weather.Out);
        Assert.Equal(2, Run("explain", Sample("valley.world.json"), save, "weather").Code);
        var quake = Run("explain", Sample("valley.world.json"), save, "hazard", "quake", "riverton", "--fact", "game.garrison=80");
        Assert.Contains("branching ratio 350000 ppm", quake.Out);
        var director = Run("explain", Sample("valley.world.json"), save, "director");
        Assert.StartsWith("director cassandra on normal: ", director.Out);        // the switch to Phoebe is at step 210, after the 200 lived
    }

    [Fact]
    public void ExplainRefusesWhatItCannotAnswerAndAWrongSaveOrCommandLine()
    {
        var save = Path.Combine(_folder, "state.json");
        Run("simulate", Sample("valley.world.json"), Sample("valley.world.scenario.json"), "20", "--save", save);
        Assert.Equal(1, Run("explain", Sample("valley.world.json"), save, "hazard", "nope", "riverton", "--fact", "game.garrison=1").Code);
        Assert.StartsWith("refused WE_FACT_MISSING", Run("explain", Sample("valley.world.json"), save, "hazard", "flood", "riverton").Out);
        Assert.StartsWith("refused WE_SAVE_FORMAT", Run("explain", Sample("valley.world.json"), Write("bad-save.json", "{}"), "director").Out);
        var wrong = Run("explain", Sample("valley.world.json"), save, "climate", "x");
        Assert.Equal(2, wrong.Code);
        Assert.Equal(2, Run("explain", Sample("valley.world.json"), save, "hazard", "flood").Code);
        var badFact = Run("explain", Sample("valley.world.json"), save, "hazard", "flood", "riverton", "--fact", "novalue");
        Assert.Equal(2, badFact.Code);
        Assert.Contains("--fact needs id=value", badFact.Err);
    }

    [Fact]
    public void CoverageReportsEachTableAndExitsOneWhenOneIsIncomplete()
    {
        var (code, output, _) = Run("coverage", Sample("valley.world.json"), Sample("valley.world.en.json"));
        Assert.Equal(0, code);
        Assert.Contains("en: 55/55 (100%) missing 0 unknown 0 problems 0", output);
        var partial = Write("fr.json", """{ "format": 1, "locale": "fr", "messages": { "season.spring": "Printemps", "bogus": "x" } }""");
        var second = Run("coverage", Sample("valley.world.json"), Sample("valley.world.en.json"), partial);
        Assert.Equal(1, second.Code);
        Assert.Contains("fr: 1/55", second.Out);
        Assert.Contains("  missing season.summer", second.Out);
        Assert.Contains("  unknown bogus", second.Out);
        Assert.StartsWith("refused", Run("coverage", Sample("valley.world.json"), Write("broken.json", "{")).Out);
    }

    [Fact]
    public void ABadCommandLineOrAMissingFileIsExitTwoWithTheUsage()
    {
        Assert.Equal(2, Run().Code);
        Assert.Equal(2, Run("check").Code);
        Assert.Equal(2, Run("dance", "x").Code);
        var missing = Run("check", Path.Combine(_folder, "nope.json"));
        Assert.Equal(2, missing.Code);
        Assert.Contains("file problem", missing.Err);
        Assert.Contains("usage: WorldEventsEngine.Cli check", Run("check", Sample("valley.world.json"), "extra").Err);
        Assert.Equal(2, Run("simulate", Sample("valley.world.json"), Sample("valley.world.scenario.json"), "0").Code);
        Assert.Equal(2, Run("simulate", Sample("valley.world.json"), Sample("valley.world.scenario.json"), "5", "--bogus").Code);
    }
}
