using Xunit;

namespace EconomyEngine.Tests;

/// <summary>The authoring tool: every command, and every mistake has its exit code (0 done, 1 refused or incomplete, 2 usage, scenario or file problem).</summary>
public sealed class CliTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("economy-cli-");

    public void Dispose() => _dir.Delete(true);

    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "samples", name);

    private string Write(string name, string text)
    {
        var path = Path.Combine(_dir.FullName, name);
        File.WriteAllText(path, text);
        return path;
    }

    private static (int Exit, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = EconomyCli.Run(args, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public void CheckAcceptsTheSampleAndSummarisesIt()
    {
        var (exit, output, error) = Run("check", Sample("town.economy.json"));
        Assert.Equal(0, exit);
        Assert.Equal("", error);
        Assert.StartsWith("ok valley-economy digest " + Fixture.Town.Digest, output, StringComparison.Ordinal);
        Assert.Contains("currencies 2 goods 10 markets 3 pairs 22 routes 6", output, StringComparison.Ordinal);
        Assert.Contains("routing landed", output.Split('\n').Select(l => l.TrimEnd('\r')));
        Assert.Contains("market seaport currency florin pairs 6 policies 4 budget 2", output, StringComparison.Ordinal);
        Assert.Contains("route sea-river seaport -> riverton delay 3 capacity 1000", output, StringComparison.Ordinal);
        Assert.Contains("currency florin anchor 500000 growth 0 max-rate-step 20000", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckAndExplainShowTheRequirementsAndTheThrottleOfTheWorkshop()
    {
        var (exit, output, error) = Run("check", Sample("workshop.economy.json"));
        Assert.Equal((0, ""), (exit, error));
        Assert.Contains("recipe forge-steel: 2 ore + 1 fuel + 1 labour -> 1 steel in 1 periods, requires in stock 1 hammer", output, StringComparison.Ordinal);
        Assert.Contains("line forge forge-steel capacity 60 throttle stop 900 full 1200", output, StringComparison.Ordinal);
        Assert.Contains("line forge make-nails capacity 30\n", output.Replace("\r", ""), StringComparison.Ordinal);                   // a line without a throttle says nothing of one
        var scenario = Write("quiet.scenario.json", "{ \"facts\": {}, \"script\": [] }");
        var state = Path.Combine(_dir.FullName, "state.json");
        Assert.Equal(0, Run("simulate", Sample("workshop.economy.json"), scenario, "3", "--save", state).Exit);
        var (explainExit, explained, _) = Run("explain", Sample("workshop.economy.json"), state, "production", "forge", "forge-steel");
        Assert.Equal(0, explainExit);
        Assert.Contains("requires hammer: 1 (needs 1 free at this line's turn, never used up: stock 1, held by earlier lines this period 0; held)", explained, StringComparison.Ordinal);
        Assert.Contains("margin forge-steel:", explained, StringComparison.Ordinal);
        Assert.Contains("yield steel:", explained, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckSaysWhichRuleRefusedADocument()
    {
        var path = Write("bad.json", Edit.Doc(root => root["format"] = 1));
        var (exit, output, _) = Run("check", path);
        Assert.Equal(1, exit);
        Assert.StartsWith("refused ECON_FORMAT:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ACommandLineThatMakesNoSenseOrAFileThatIsMissingExitsWithTwo()
    {
        Assert.Equal(2, Run().Exit);
        Assert.Equal(2, Run("check").Exit);
        Assert.Equal(2, Run("dance", "a").Exit);
        Assert.Equal(2, Run("check", Path.Combine(_dir.FullName, "missing.json")).Exit);
        Assert.Equal(2, Run("check", Sample("town.economy.json"), "extra").Exit);
        Assert.StartsWith("usage:", Run("dance", "a").Err, StringComparison.Ordinal);
        Assert.Contains("file problem", Run("check", Path.Combine(_dir.FullName, "missing.json")).Err, StringComparison.Ordinal);
    }

    [Fact]
    public void SimulateLivesAScenarioPrintsWhatHappenedAndTheDigestAndSavesTheState()
    {
        var save = Path.Combine(_dir.FullName, "state.json");
        var (exit, output, error) = Run("simulate", Sample("town.economy.json"), Sample("town.scenario.json"), "90", "--save", save);
        Assert.Equal(0, exit);
        Assert.Equal("", error);
        Assert.Contains("period 10: trade riverton grain 400: unit ", output, StringComparison.Ordinal);
        Assert.Contains("period 20: shock drought on riverton", output, StringComparison.Ordinal);
        Assert.Contains("period 45: shock devaluation on seaport", output, StringComparison.Ordinal);
        Assert.Contains("period 60: settle decree", output, StringComparison.Ordinal);
        Assert.Contains("state at period 90, step 2160", output, StringComparison.Ordinal);
        Assert.Contains("loan levy-loan active balance", output, StringComparison.Ordinal);
        var digest = output.Split('\n').Last(l => l.StartsWith("digest ", StringComparison.Ordinal)).Trim()[7..];
        var opened = EconomySave.Open(Fixture.Town, File.ReadAllBytes(save));
        Assert.Equal(digest, opened.StateDigest());
        Assert.Equal(90, opened.Period);
        var again = Run("simulate", Sample("town.economy.json"), Sample("town.scenario.json"), "90");
        Assert.Equal(output, again.Out);
    }

    [Fact]
    public void SimulateVerboseListsTheChangesAndARefusedCallIsExitOne()
    {
        var scenario = Write("scenario.json", """
            { "facts": { "cal.season": "autumn", "story.plague": 0 },
              "script": [ { "period": 1, "do": "trade", "market": "riverton", "good": "grain", "quantity": 99999999 },
                          { "period": 2, "do": "fund", "market": "hillfort", "delta": 500 },
                          { "period": 3, "do": "growth", "currency": "florin", "ppm": 20000 } ] }
            """);
        var (exit, output, _) = Run("simulate", Sample("town.economy.json"), scenario, "40", "--verbose");
        Assert.Equal(1, exit);
        Assert.Contains("trade riverton refused ECON_STOCK", output, StringComparison.Ordinal);
        Assert.Contains("fund hillfort 500", output, StringComparison.Ordinal);
        Assert.Contains("growth florin 20000", output, StringComparison.Ordinal);
        Assert.Contains("florin rate", output, StringComparison.Ordinal);
        Assert.Contains(" left by ", output, StringComparison.Ordinal);
        Assert.Contains(" arrived by ", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ \"script\": [ { \"period\": 1, \"do\": \"dance\" } ] }", "unknown 'do'")]
    [InlineData("{ \"script\": [ { \"do\": \"settle\", \"schedule\": \"decree\" } ] }", "whole number 'period'")]
    [InlineData("{ \"script\": [ { \"period\": 1, \"do\": \"trade\", \"market\": \"riverton\", \"good\": \"grain\" } ] }", "whole number 'quantity'")]
    [InlineData("{ \"script\": [ { \"period\": 1, \"do\": \"settle\" } ] }", "text 'schedule'")]
    [InlineData("{ \"facts\": { \"x\": [1] } }", "fact 'x'")]
    [InlineData("[]", "a scenario is an object")]
    [InlineData("{ nope", "not JSON")]
    public void AScenarioThatDoesNotReadExitsWithTwoAndSaysWhere(string text, string expected)
    {
        var (exit, _, error) = Run("simulate", Sample("town.economy.json"), Write("s.json", text), "5");
        Assert.Equal(2, exit);
        Assert.Contains("scenario problem", error, StringComparison.Ordinal);
        Assert.Contains(expected, error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("many")]
    [InlineData("100001")]
    public void ANumberOfPeriodsOutsideOneToAHundredThousandExitsWithTwo(string periods) => Assert.Equal(2, Run("simulate", Sample("town.economy.json"), Sample("town.scenario.json"), periods).Exit);

    [Fact]
    public void SimulateWithAnUnknownOptionOrAnEmptyScenarioFileIsHandled()
    {
        Assert.Equal(2, Run("simulate", Sample("town.economy.json"), Sample("town.scenario.json"), "5", "--loud").Exit);
        // The sample's modifiers read the season: a scenario that gives no facts is refused by the engine, whole.
        var missing = Run("simulate", Sample("town.economy.json"), Write("empty.json", "{}"), "3");
        Assert.Equal(1, missing.Exit);
        Assert.StartsWith("refused ECON_FACT_MISSING", missing.Out, StringComparison.Ordinal);
        var (exit, output, _) = Run("simulate", Write("mini.json", Mini.Text()), Write("empty2.json", "{}"), "3");
        Assert.Equal(0, exit);
        Assert.Contains("state at period 3", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SimulateRefusesADocumentThatDoesNotLoad()
    {
        var path = Write("bad.json", Edit.Doc(root => root.Remove("markets")));
        var (exit, output, _) = Run("simulate", path, Sample("town.scenario.json"), "5");
        Assert.Equal(1, exit);
        Assert.StartsWith("refused ECON_MISSING_FIELD", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplainSaysWhyAPriceALoanAndACurrencyAreWhatTheyAre()
    {
        var save = Path.Combine(_dir.FullName, "state.json");
        Assert.Equal(0, Run("simulate", Sample("town.economy.json"), Sample("town.scenario.json"), "40", "--save", save).Exit);
        var price = Run("explain", Sample("town.economy.json"), save, "price", "riverton", "grain");
        Assert.Equal(0, price.Exit);
        Assert.StartsWith("price of grain in riverton = ", price.Out, StringComparison.Ordinal);
        foreach (var term in new[] { "reference grain", "bounds grain", "supply grain", "demand grain", "stock grain", "imbalance grain", "next-change grain" }) Assert.Contains(term, price.Out, StringComparison.Ordinal);
        var loan = Run("explain", Sample("town.economy.json"), save, "loan", "levy-loan");
        Assert.Equal(0, loan.Exit);
        Assert.Contains("balance levy-loan", loan.Out, StringComparison.Ordinal);
        Assert.Contains("interest levy-loan", loan.Out, StringComparison.Ordinal);
        var currency = Run("explain", Sample("town.economy.json"), save, "currency", "florin");
        Assert.Equal(0, currency.Exit);
        Assert.Contains("parity florin", currency.Out, StringComparison.Ordinal);
        Assert.Contains("floating", currency.Out, StringComparison.Ordinal);
        Assert.Contains("reference", Run("explain", Sample("town.economy.json"), save, "currency", "crown").Out, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplainSaysHowARoutesCapacityIsSharedAndWhatAMarketCanReach()
    {
        var save = Path.Combine(_dir.FullName, "state.json");
        Assert.Equal(0, Run("simulate", Sample("town.economy.json"), Sample("town.scenario.json"), "40", "--save", save).Exit);
        var route = Run("explain", Sample("town.economy.json"), save, "route", "river-hill");
        Assert.Equal(0, route.Exit);
        Assert.StartsWith("route river-hill (riverton -> hillfort) = ", route.Out, StringComparison.Ordinal);
        Assert.Contains("landed routing, capacity 960", route.Out, StringComparison.Ordinal);
        foreach (var good in new[] { "grain", "cloth", "iron", "ale" }) Assert.Contains(" " + good + ": ", route.Out, StringComparison.Ordinal);
        var reach = Run("explain", Sample("town.economy.json"), save, "reach", "riverton", "grain");
        Assert.Equal(0, reach.Exit);
        Assert.StartsWith("best value of grain from riverton = ", reach.Out, StringComparison.Ordinal);
        Assert.Contains("here grain", reach.Out, StringComparison.Ordinal);
        Assert.Contains("sells-at grain", reach.Out, StringComparison.Ordinal);
        Assert.StartsWith("refused ECON_UNKNOWN_ROUTE", Run("explain", Sample("town.economy.json"), save, "route", "no-such").Out, StringComparison.Ordinal);
        Assert.Equal(2, Run("explain", Sample("town.economy.json"), save, "route").Exit);
        Assert.Equal(2, Run("explain", Sample("town.economy.json"), save, "reach", "riverton").Exit);
    }

    [Fact]
    public void ExplainSaysWhyAProductionLineRanAsManyTimesAsItDidAndCheckListsTheChain()
    {
        var save = Path.Combine(_dir.FullName, "state.json");
        Assert.Equal(0, Run("simulate", Sample("town.economy.json"), Sample("town.scenario.json"), "45", "--save", save).Exit);
        var smelt = Run("explain", Sample("town.economy.json"), save, "production", "hillfort", "smelt-steel");
        Assert.Equal(0, smelt.Exit);
        Assert.StartsWith("production of smelt-steel in hillfort = ", smelt.Out, StringComparison.Ordinal);
        Assert.Contains("short of ore", smelt.Out, StringComparison.Ordinal);
        Assert.Contains("input ore:", smelt.Out, StringComparison.Ordinal);
        Assert.Contains("room steel:", smelt.Out, StringComparison.Ordinal);
        Assert.StartsWith("refused ECON_NO_LINE", Run("explain", Sample("town.economy.json"), save, "production", "seaport", "mill").Out, StringComparison.Ordinal);
        Assert.StartsWith("refused ECON_UNKNOWN_RECIPE", Run("explain", Sample("town.economy.json"), save, "production", "hillfort", "magic").Out, StringComparison.Ordinal);
        Assert.Equal(2, Run("explain", Sample("town.economy.json"), save, "production", "hillfort").Exit);
        var check = Run("check", Sample("town.economy.json")).Out;
        Assert.Contains("recipes 5 lines 5 locked 1", check, StringComparison.Ordinal);
        Assert.Contains("recipe mill: 3 grain -> 2 flour in 1 periods, requires in stock 1 tools (1 lost in 400 runs)", check, StringComparison.Ordinal);       // a millstone wears out
        Assert.Contains("recipe alloy-steel: 3 coal + 1 ore -> 2 steel in 1 periods (locked until discovered)", check, StringComparison.Ordinal);
        Assert.Contains("line hillfort smelt-steel capacity 40 throttle stop 900 full 1200 costs wage 1200 overhead 500 indexed", check, StringComparison.Ordinal);
        Assert.True(check.IndexOf("recipe mill:", StringComparison.Ordinal) < check.IndexOf("recipe bakery:", StringComparison.Ordinal), "the chain is listed in the order it runs");
    }

    [Fact]
    public void ExplainRefusesWhatItCannotExplainAndAStateOfAnotherDocument()
    {
        var save = Path.Combine(_dir.FullName, "state.json");
        Run("simulate", Sample("town.economy.json"), Sample("town.scenario.json"), "5", "--save", save);
        Assert.Equal(1, Run("explain", Sample("town.economy.json"), save, "price", "riverton", "gold").Exit);
        Assert.StartsWith("refused ECON_UNKNOWN_GOOD", Run("explain", Sample("town.economy.json"), save, "price", "riverton", "gold").Out, StringComparison.Ordinal);
        Assert.StartsWith("refused ECON_UNKNOWN_LOAN", Run("explain", Sample("town.economy.json"), save, "loan", "no-such").Out, StringComparison.Ordinal);
        var other = Write("other.json", Edit.Doc(root => root["seed"] = 5));
        Assert.StartsWith("refused ECON_SAVE_DOCUMENT", Run("explain", other, save, "currency", "crown").Out, StringComparison.Ordinal);
        Assert.Equal(2, Run("explain", Sample("town.economy.json"), save, "price", "riverton").Exit);
        Assert.Equal(2, Run("explain", Sample("town.economy.json"), save, "mood", "x").Exit);
        Assert.Equal(2, Run("explain", Sample("town.economy.json"), Path.Combine(_dir.FullName, "missing.json"), "currency", "crown").Exit);
    }

    [Fact]
    public void CoverageReportsEachTableAndExitsOneUnlessEveryTableIsComplete()
    {
        var full = Run("coverage", Sample("town.economy.json"), Sample("town.en.json"));
        Assert.Equal(0, full.Exit);
        Assert.Contains("en: 31/31 (100%) missing 0 unknown 0 problems 0", full.Out, StringComparison.Ordinal);
        var partial = Write("fr.json", "{ \"format\": 1, \"locale\": \"fr\", \"messages\": { \"econ.good.grain\": \"blé\", \"econ.stray\": \"x\", \"econ.good.cloth\": \"drap {n}\" } }");
        var both = Run("coverage", Sample("town.economy.json"), Sample("town.en.json"), partial);
        Assert.Equal(1, both.Exit);
        Assert.Contains("fr: 2/31 (6%) missing 29 unknown 1 problems 1", both.Out, StringComparison.Ordinal);
        Assert.Contains("  missing econ.cur.crown", both.Out, StringComparison.Ordinal);
        Assert.Contains("  unknown econ.stray", both.Out, StringComparison.Ordinal);
        Assert.Contains("  problem econ.good.cloth:", both.Out, StringComparison.Ordinal);
        var broken = Run("coverage", Sample("town.economy.json"), Write("broken.json", "{ \"format\": 2 }"));
        Assert.Equal(1, broken.Exit);
        Assert.StartsWith("refused DLG_LOCALE_FORMAT", broken.Out, StringComparison.Ordinal);
        Assert.StartsWith("refused ECON_FORMAT", Run("coverage", Write("bad.json", Edit.Doc(root => root["format"] = 7)), Sample("town.en.json")).Out, StringComparison.Ordinal);
    }
}
