using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>The facts the engine offers (for dialogue conditions and AI sensors) and the words of a document (localisation keys, coverage, fallback).</summary>
public sealed class FactsNamesTests
{
    [Fact]
    public void TheDocumentDeclaresEveryFactItOffersWithATypeAndARange()
    {
        var facts = Fixture.Town.Facts;
        Assert.Equal(4 + 5 * 10 + 2 * 2 + 2, facts.Count);
        Assert.Equal(new FactDefinition(FactKind.Integer, 0, EconomyFacts.FactCeiling), facts["market.treasury"]);
        Assert.Equal(new FactDefinition(FactKind.String), facts["market.currency"]);
        Assert.Equal(new FactDefinition(FactKind.Boolean), facts["market.arrears"]);
        Assert.Equal(new FactDefinition(FactKind.Integer, 0, EconomyFacts.FactCeiling), facts["market.price.grain"]);
        Assert.Equal(new FactDefinition(FactKind.Integer, 0, EconomyFacts.MaxRatio), facts["market.ratio.cloth"]);
        Assert.Equal(new FactDefinition(FactKind.Integer, 0, EconomyFacts.FactCeiling), facts["market.stock.iron"]);
        Assert.Equal(new FactDefinition(FactKind.String), facts["market.band.ale"]);
        Assert.Equal(new FactDefinition(FactKind.Boolean), facts["market.short.ale"]);
        Assert.Equal(new FactDefinition(FactKind.Integer, 0, EconomyFacts.FactCeiling), facts["currency.florin.rate"]);
        Assert.Equal(new FactDefinition(FactKind.Integer, 0, EconomyFacts.FactCeiling), facts["currency.crown.level"]);
        Assert.Equal(new FactDefinition(FactKind.String), facts["cal.season"]);
        Assert.Equal(new FactDefinition(FactKind.Integer, 0, 3), facts["story.plague"]);
    }

    [Fact]
    public void ReadGivesExactlyTheEnginesOwnFactsWithValuesOfTheirDeclaredKindAndRange()
    {
        var runtime = Tamper.Played();
        foreach (var market in runtime.Set.Markets)
        {
            var facts = EconomyFacts.Read(runtime, market.Id);
            var own = runtime.Set.Facts.Keys.Where(EconomyFacts.IsReserved).ToHashSet();
            Assert.Equal(own, facts.Keys.ToHashSet());
            foreach (var (id, value) in facts)
            {
                var declared = runtime.Set.Facts[id];
                Assert.Equal(declared.Kind, value.Kind);
                if (declared.Kind == FactKind.Integer) Assert.InRange(value.Integer, declared.Minimum!.Value, declared.Maximum!.Value);
            }
        }
    }

    [Fact]
    public void TheFactsAreTheStateOfTheMarket()
    {
        var runtime = Tamper.Played();
        var facts = EconomyFacts.Read(runtime, "seaport");
        var state = runtime.Markets[2];
        Assert.Equal(state.Treasury, facts["market.treasury"].Integer);
        Assert.Equal(40, facts["market.staff"].Integer);
        Assert.Equal("florin", facts["market.currency"].Text);
        Assert.Equal(runtime.InArrears(2), facts["market.arrears"].Boolean);
        Assert.Equal(state.Pairs[1].Price, facts["market.price.cloth"].Integer);
        Assert.Equal(runtime.Ratio(2, 1), facts["market.ratio.cloth"].Integer);
        Assert.Equal(state.Pairs[1].Stock, facts["market.stock.cloth"].Integer);
        Assert.Equal(runtime.Set.Bands[state.Pairs[1].Band].Id, facts["market.band.cloth"].Text);
        Assert.Equal(state.Pairs[1].Short, facts["market.short.cloth"].Boolean);
        Assert.Equal(runtime.Currencies[1].Rate, facts["currency.florin.rate"].Integer);
        Assert.Equal(runtime.Currencies[0].Level, facts["currency.crown.level"].Integer);
        var prefixed = EconomyFacts.Read(runtime, "seaport", "ext.");
        Assert.Equal(facts.Count, prefixed.Count);
        Assert.Equal(facts["market.price.grain"], prefixed["ext.market.price.grain"]);
    }

    [Fact]
    public void AGoodTheMarketDoesNotTradeReadsAsNothing()
    {
        var runtime = EconomyRuntime.Create(EconomyLoader.Load(Edit.Doc(root => { var pairs = Edit.Market(root, "hillfort")["pairs"]!.AsArray(); pairs.Remove(pairs.Last(p => p!["good"]!.GetValue<string>() == "ale")); })));
        var facts = EconomyFacts.Read(runtime, "hillfort");
        Assert.Equal(0, facts["market.price.ale"].Integer);
        Assert.Equal(0, facts["market.ratio.ale"].Integer);
        Assert.Equal(0, facts["market.stock.ale"].Integer);
        Assert.Equal("", facts["market.band.ale"].Text);
        Assert.False(facts["market.short.ale"].Boolean);
        Assert.NotEqual(0, facts["market.price.grain"].Integer);
        Assert.Equal(EconomyCodes.UnknownMarket, Fixture.Refusal(() => EconomyFacts.Read(runtime, "nowhere")));
    }

    [Fact]
    public void WholeNumbersAboveTheFactCeilingReadAsTheCeilingAnd()
    {
        var runtime = Fixture.Runtime();
        runtime.Fund("riverton", 5_000_000_000);
        runtime.Markets[0].Pairs[0].Stock = 99_999;
        var facts = EconomyFacts.Read(runtime, "riverton");
        Assert.Equal(EconomyFacts.FactCeiling, facts["market.treasury"].Integer);
        Assert.Equal(99_999, facts["market.stock.grain"].Integer);
        runtime.Markets[0].Pairs[0].Price = 5_000_000_000_000;
        facts = EconomyFacts.Read(runtime, "riverton");
        Assert.Equal(EconomyFacts.FactCeiling, facts["market.price.grain"].Integer);
        Assert.Equal(1_000_000_000, EconomyFacts.FactCeiling);
        Assert.All(runtime.Set.Facts.Values.Where(f => f.Kind == FactKind.Integer), f => Assert.True(f.Maximum <= EconomyFacts.FactCeiling));
    }

    [Theory]
    [InlineData("market.price.grain", true)]
    [InlineData("market.x", true)]
    [InlineData("currency.crown.rate", true)]
    [InlineData("cal.season", false)]
    [InlineData("marketing", false)]
    [InlineData("currencies.x", false)]
    [InlineData("story.market.x", false)]
    public void OnlyTheTwoPrefixesAreTheEnginesOwn(string id, bool reserved) => Assert.Equal(reserved, EconomyFacts.IsReserved(id));

    // ---- words -------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ADocumentUsesThirtyOneKeysEachOnceInOrdinalOrder()
    {
        var keys = EconomyNames.RequiredKeys(Fixture.Town);
        Assert.Equal(31, keys.Count);
        Assert.Equal(keys.OrderBy(k => k, StringComparer.Ordinal).ToList(), keys);
        Assert.Equal(keys.Distinct().Count(), keys.Count);
        Assert.Contains("econ.market.riverton", keys);
        Assert.Contains("econ.shock.press", keys);
    }

    [Fact]
    public void TheEnglishTableCoversTheDocumentAndNamesRenderThroughTheSharedChain()
    {
        var table = LocaleTable.Load(Fixture.Text("town.en.json"));
        var report = EconomyNames.Coverage(Fixture.Town, table);
        Assert.True(report.IsComplete);
        Assert.Equal(100, report.Percent);
        var chain = LocaleChain.Create("en", [table], "en");
        Assert.Equal("crown", EconomyNames.Currency(Fixture.Town, chain, 0));
        Assert.Equal("florin", EconomyNames.Currency(Fixture.Town, chain, 1));
        Assert.Equal("grain", EconomyNames.Good(Fixture.Town, chain, 0));
        Assert.Equal("dear", EconomyNames.Band(Fixture.Town, chain, 2));
        Assert.Equal("tax day", EconomyNames.Schedule(Fixture.Town, chain, 1));
        Assert.Equal("drought", EconomyNames.Shock(Fixture.Town, chain, 0));
        Assert.Equal("Seaport", EconomyNames.Market(Fixture.Town, chain, 2));
    }

    [Fact]
    public void CoverageSaysWhatIsMissingWhatNoDocumentUsesAndWhatUsesAPlaceholderTheKeyDoesNotGive()
    {
        var table = LocaleTable.Load("{ \"format\": 1, \"locale\": \"en\", \"messages\": { \"econ.good.grain\": \"grain {who}\", \"econ.not.used\": \"x\" } }");
        var report = EconomyNames.Coverage(Fixture.Town, table);
        Assert.False(report.IsComplete);
        Assert.Equal(30, report.Missing.Count);
        Assert.DoesNotContain("econ.good.grain", report.Missing);
        Assert.Equal(["econ.not.used"], report.Unknown);
        Assert.Equal("econ.good.grain", Assert.Single(report.Problems).Key);
        Assert.Equal(1, report.Translated);
    }

    [Fact]
    public void ATableInAnotherLanguageFallsBackKeyByKeyAndTheFallbackIsRemembered()
    {
        var english = LocaleTable.Load(Fixture.Text("town.en.json"));
        var partial = LocaleTable.Load("{ \"format\": 1, \"locale\": \"fr\", \"messages\": { \"econ.good.grain\": \"blé\" } }");
        var chain = LocaleChain.Create("fr", [english, partial], "en");
        Assert.Equal("blé", EconomyNames.Good(Fixture.Town, chain, 0));
        Assert.Equal("cloth", EconomyNames.Good(Fixture.Town, chain, 1));
        Assert.Equal(1, chain.Fallbacks["fr:econ.good.cloth"]);
        var onlyFrench = LocaleChain.Create("fr", [partial], "fr");
        Assert.Equal(LocaleCodes.MissingKey, Fixture.Refusal(() => EconomyNames.Good(Fixture.Town, onlyFrench, 1)));
    }
}
