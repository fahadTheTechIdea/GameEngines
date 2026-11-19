using GameContent.Catalog;
using Xunit;
using static GameContent.Tests.PackBuilder;

namespace GameContent.Tests;

/// <summary>The balance warnings, the price property, the cross-pack duplicate rules and the pack statistics.</summary>
public sealed class EconomyTests
{
    private static string Priced(string id, double price, string cls = "raw", string origin = "natural", string unit = "kg") =>
        Res(id, cls: cls, origin: origin, unit: unit, properties: "\"base-price\": " + price.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static void Warned(string code, CatalogReport report)
    {
        Assert.True(report.Has(code), "expected " + code + " but got:\n" + report);
        Assert.True(report.IsValid, "a warning must not refuse the pack:\n" + report);
    }

    [Fact]
    public void ThePriceMustBeAPositiveNumber()
    {
        Assert.True(Check(Res("t.a", properties: "\"base-price\": 0")).Has(CatalogCodes.BadPrice));
        Assert.True(Check(Res("t.a", properties: "\"base-price\": \"cheap\"")).Has(CatalogCodes.BadPrice));
        Assert.True(Check(Res("t.a", properties: "\"base-price\": -3")).Has(CatalogCodes.BadPrice));
        Assert.True(Check(Priced("t.a", 2.5)).IsValid);
        Assert.Equal(2.5m, CatalogPrices.Of(Load(Single(Priced("t.a", 2.5))).Required().Resources.Single()));
        Assert.Null(CatalogPrices.Of(Load(Single(Res("t.a"))).Required().Resources.Single()));
    }

    [Fact]
    public void ALoopThatReturnsMoreValueThanItTakesIsWarnedEvenWhenQuantitiesBreakEven()
    {
        // Going round: 10 a become 5 b and 5 c, and 5 b become 10 a again. The counts of a and b break even, but each lap keeps 5 c.
        var resources = Priced("t.a", 1) + "," + Priced("t.b", 2, origin: "crafted") + "," + Priced("t.c", 2, origin: "crafted");
        var split = Rec("t.split", kind: "transform", inputs: Line("t.a", "10"), outputs: Line("t.b", "5") + "," + Line("t.c", "5"));
        var report = Check(resources, split + "," + Rec("t.rebuild", kind: "trade", inputs: Line("t.b", "5"), outputs: Line("t.a", "10")));
        Warned(CatalogCodes.ValueLoop, report);
        Assert.False(report.Has(CatalogCodes.FreeCycle));
        // If the second leg gives back less than it was worth, the loop loses and nothing is said.
        Assert.False(Check(resources, split + "," + Rec("t.rebuild", kind: "trade", inputs: Line("t.b", "5"), outputs: Line("t.a", "4"))).Has(CatalogCodes.ValueLoop));
    }

    [Fact]
    public void AQuantityLoopIsStillAnErrorAndAnEvenTradeLoopIsFine()
    {
        var resources = Priced("t.a", 1) + "," + Priced("t.b", 1);
        var even = Rec("t.a-b", kind: "trade", inputs: Line("t.a", "10"), outputs: Line("t.b", "10")) + "," + Rec("t.b-a", kind: "trade", inputs: Line("t.b", "10"), outputs: Line("t.a", "10"));
        Assert.Empty(Check(resources, even).Items);
        var gain = Rec("t.a-b", kind: "trade", inputs: Line("t.a", "10"), outputs: Line("t.b", "12")) + "," + Rec("t.b-a", kind: "trade", inputs: Line("t.b", "10"), outputs: Line("t.a", "12"));
        Assert.True(Check(resources, gain).Has(CatalogCodes.FreeCycle));
    }

    [Fact]
    public void ATradeThatMakesOrLosesALotOfValueAndACraftFromScrapAreOutliers()
    {
        var resources = Priced("t.a", 1) + "," + Priced("t.b", 1, origin: "crafted");
        Warned(CatalogCodes.ValueOutlier, Check(resources, Rec("t.rich", kind: "trade", inputs: Line("t.a", "10"), outputs: Line("t.b", "20"))));
        Warned(CatalogCodes.ValueOutlier, Check(resources, Rec("t.robbed", kind: "trade", inputs: Line("t.a", "10"), outputs: Line("t.b", "1"))));
        Assert.False(Check(resources, Rec("t.fair", kind: "trade", inputs: Line("t.a", "10"), outputs: Line("t.b", "9"))).Has(CatalogCodes.ValueOutlier));
        Warned(CatalogCodes.ValueOutlier, Check(resources, Rec("t.gold", kind: "craft", inputs: Line("t.a", "1"), outputs: Line("t.b", "100"))));
        Assert.False(Check(resources, Rec("t.craft", kind: "craft", inputs: Line("t.a", "1"), outputs: Line("t.b", "3"))).Has(CatalogCodes.ValueOutlier));
        // Unpriced goods are simply outside the rule.
        Assert.False(Check(AB, Rec("t.gold", kind: "craft", inputs: Line("t.a", "1"), outputs: Line("t.b", "100"))).Has(CatalogCodes.ValueOutlier));
    }

    [Fact]
    public void ADurationAThousandTimesTheMedianOfItsKindIsAnOutlier()
    {
        var resources = Res("t.a") + "," + Res("t.b", origin: "crafted") + "," + string.Join(",", Enumerable.Range(1, 5).Select(i => Res("t.m" + i, origin: "crafted")));
        string Craft(string id, string output, string unit, int value) => Rec(id, inputs: Line("t.a", "1"), outputs: Line(output, "1"), duration: "{ \"value\": " + value + ", \"unit\": \"" + unit + "\" }");
        var typical = string.Join(",", Enumerable.Range(1, 5).Select(i => Craft("t.make-" + i, "t.m" + i, "s", 30)));
        Warned(CatalogCodes.DurationOutlier, Check(resources, typical + "," + Craft("t.slow", "t.b", "d", 30)));
        Assert.False(Check(resources, typical + "," + Craft("t.slowish", "t.b", "h", 1)).Has(CatalogCodes.DurationOutlier));
        // Too few recipes of a kind to judge.
        Assert.False(Check(resources, Craft("t.one", "t.b", "d", 30)).Has(CatalogCodes.DurationOutlier));
    }

    [Fact]
    public void AnEnergyUseAThousandTimesTheMedianIsAnOutlier()
    {
        var resources = Res("t.power", cls: "energy", unit: "kW.h") + "," + Res("t.a") + "," + string.Join(",", Enumerable.Range(1, 6).Select(i => Res("t.m" + i, origin: "crafted")));
        string Burn(int i, string amount) => Rec("t.burn-" + i, inputs: Line("t.a", "1") + "," + Line("t.power", amount), outputs: Line("t.m" + i, "1"));
        var even = string.Join(",", Enumerable.Range(1, 5).Select(i => Burn(i, "2")));
        Assert.False(Check(resources, even).Has(CatalogCodes.EnergyOutlier));
        Warned(CatalogCodes.EnergyOutlier, Check(resources, even + "," + Burn(6, "5000")));
    }

    [Fact]
    public void ACraftedResourceWithOneMakerAndManyUsersIsAChokepoint()
    {
        var resources = Res("t.a") + "," + Res("t.key", origin: "crafted", cls: "intermediate") + "," + string.Join(",", Enumerable.Range(1, 9).Select(i => Res("t.m" + i, origin: "crafted")));
        var make = Rec("t.make-key", inputs: Line("t.a", "1"), outputs: Line("t.key", "1"));
        string Use(int i) => Rec("t.use-" + i, inputs: Line("t.key", "1"), outputs: Line("t.m" + i, "1"));
        var users = string.Join(",", Enumerable.Range(1, 9).Select(Use));
        Warned(CatalogCodes.Chokepoint, Check(resources, make + "," + users));
        // A second maker removes it.
        Assert.False(Check(resources, make + "," + Rec("t.make-key-2", inputs: Line("t.a", "3"), outputs: Line("t.key", "1")) + "," + users).Has(CatalogCodes.Chokepoint));
        // Few users is not a chokepoint.
        Assert.False(Check(resources, make + "," + string.Join(",", Enumerable.Range(1, 3).Select(Use))).Has(CatalogCodes.Chokepoint));
    }

    [Fact]
    public void WarningsAreReportedBesideErrorsAndCountedBySeverity()
    {
        var resources = Priced("t.a", 1) + "," + Priced("t.b", 1, origin: "crafted");
        var report = Check(resources, Rec("t.rich", kind: "trade", inputs: Line("t.a", "10"), outputs: Line("t.b", "20")));
        Assert.Single(report.Warnings);
        Assert.Empty(report.Errors);
        Assert.Equal(CatalogSeverity.Warning, report.Items.Single().Severity);
        Assert.StartsWith("warning ", report.ToString());
    }

    // ---- across a library ------------------------------------------------------------------------------------------------

    private static CatalogSet Pack(string id, string resources) =>
        Load(new MemoryCatalogSource(new Dictionary<string, string> { [id + "/pack.json"] = Manifest(id), [id + "/doc.json"] = Document(id, resources) }), id).Required();

    [Fact]
    public void TheSameNameOrSlugInTwoPacksIsADuplicate()
    {
        var named = Pack("one", Res("one.bread").Replace("\"name\": \"one.bread\"", "\"name\": \"Bread\""));
        var other = Pack("two", Res("two.loaf").Replace("\"name\": \"two.loaf\"", "\"name\": \"  bread \""));
        Assert.True(CatalogLibrary.CheckDuplicates([named, other]).Has(CatalogCodes.DuplicateName));
        var slugA = Pack("one", Res("one.flour"));
        var slugB = Pack("two", Res("two.flour"));
        Assert.True(CatalogLibrary.CheckDuplicates([slugA, slugB]).Has(CatalogCodes.DuplicateSlug));
        Assert.True(CatalogLibrary.CheckDuplicates([slugA, Pack("two", Res("two.grain"))]).IsValid);
        // The same pack seen twice (it is a dependency of two others) is not a duplicate of itself.
        Assert.True(CatalogLibrary.CheckDuplicates([slugA, slugA]).IsValid);
    }

    // ---- the shipped packs ------------------------------------------------------------------------------------------------

    [Fact]
    public void TheShippedLibraryHasNoWarningsNoDuplicatesAndPricesItsEconomy()
    {
        var sets = Directory.GetDirectories(ShippedPackTests.PacksRoot).Select(d => CatalogLoader.Load(new DirectoryCatalogSource(ShippedPackTests.PacksRoot), Path.GetFileName(d)!)).ToList();
        foreach (var result in sets) Assert.True(result.Report.Items.Count == 0, result.Set?.Root.Id + ":\n" + result.Report);
        var library = CatalogLibrary.CheckDuplicates(sets.Select(r => r.Required()));
        Assert.True(library.Items.Count == 0, library.ToString());
        var core = sets.Single(r => r.Set!.Root.Id == "core").Required();
        Assert.Equal(1m, CatalogPrices.Of(core.Resources.Single(r => r.Id == "core.coin")));
        Assert.True(core.Resources.Count(r => CatalogPrices.Of(r) is not null) >= 30);
    }

    [Fact]
    public void StatsDescribeTheOwnContentOfAPack()
    {
        var result = CatalogLoader.Load(new DirectoryCatalogSource(ShippedPackTests.PacksRoot), "trading");
        var stats = CatalogStats.Of(result.Required(), result.Report);
        Assert.Equal("trading", stats.Pack);
        Assert.Equal(result.Required().Root.Resources.Count, stats.Resources);
        Assert.Equal(stats.Natural + stats.Crafted, stats.Resources);
        Assert.InRange(stats.Tiers, 2, 12);
        Assert.True(stats.PricedResources >= 13);
        Assert.Contains("pack trading:", stats.Format());
        Assert.Empty(stats.WarningsByCode);
    }
}
