using Xunit;

namespace EconomyEngine.Tests;

/// <summary>A trader buying from or selling to a market now: the quote, the spread, the tax, the stock and the refusals.</summary>
public sealed class TradeTests
{
    [Fact]
    public void ABuyPaysThePricePlusTheSpreadAndTheSalesTaxOnTopAndRemovesTheStock()
    {
        var runtime = Fixture.Runtime();
        var pair = runtime.Markets[0].Pairs[0];
        var price = pair.Price;
        var stock = pair.Stock;
        var accrued = runtime.Markets[0].Accrued[0];
        var result = runtime.Trade("riverton", "grain", 100);
        var unit = EconomyMath.Nearest(price, 1050, 1000);       // riverton's spread is 50 per mille
        var value = EconomyMath.Nearest(100, unit, 1000);
        var tax = EconomyMath.Nearest(value, 100, 1000);          // sales tax 100 per mille
        Assert.Equal(new TradeResult("riverton", "grain", 100, unit, value, tax, value + tax, result.Changes), result);
        Assert.Equal(stock - 100, pair.Stock);
        Assert.Equal(100, pair.SpotDemand);
        Assert.Equal(accrued + tax, runtime.Markets[0].Accrued[0]);
        Assert.Equal(price, pair.Price);
    }

    [Fact]
    public void ASaleReceivesThePriceLessTheSpreadPaysNoTaxAndAddsToTheStock()
    {
        var runtime = Fixture.Runtime();
        var pair = runtime.Markets[0].Pairs[0];
        var price = pair.Price;
        var stock = pair.Stock;
        var accrued = runtime.Markets[0].Accrued.ToArray();
        var result = runtime.Trade("riverton", "grain", -40);
        var unit = EconomyMath.Nearest(price, 950, 1000);
        Assert.Equal(unit, result.UnitPrice);
        Assert.Equal(EconomyMath.Nearest(40, unit, 1000), result.Value);
        Assert.Equal(0, result.Tax);
        Assert.Equal(result.Value, result.Total);
        Assert.Equal(-40, result.Quantity);
        Assert.Equal(stock + 40, pair.Stock);
        Assert.Equal(40, pair.SpotSupply);
        Assert.Equal(accrued, runtime.Markets[0].Accrued);
    }

    [Fact]
    public void AQuoteIsTheTradeWithoutMakingIt()
    {
        var runtime = Fixture.Runtime();
        var digest = runtime.StateDigest();
        var quote = runtime.Quote("seaport", "cloth", 30);
        Assert.Equal(digest, runtime.StateDigest());
        var made = runtime.Trade("seaport", "cloth", 30);
        Assert.Equal(quote with { Changes = made.Changes }, made);
        var sale = Fixture.Runtime();
        var saleQuote = sale.Quote("hillfort", "iron", -75);
        var sold = sale.Trade("hillfort", "iron", -75);
        Assert.Equal(saleQuote with { Changes = sold.Changes }, sold);
    }

    [Fact]
    public void ATradeTheMarketCannotMakeIsRefusedWholeAndChangesNothing()
    {
        var runtime = Fixture.Runtime();
        var digest = runtime.StateDigest();
        Assert.Equal(EconomyCodes.Stock, Fixture.Refusal(() => runtime.Trade("riverton", "grain", 1801)));
        Assert.Equal(EconomyCodes.Capacity, Fixture.Refusal(() => runtime.Trade("riverton", "grain", -(100_000 - 1800 + 1))));
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => runtime.Trade("riverton", "grain", 0)));
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => runtime.Trade("riverton", "grain", EconomyReader.MaxQuantity + 1)));
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => runtime.Trade("riverton", "grain", -EconomyReader.MaxQuantity - 1)));
        Assert.Equal(EconomyCodes.UnknownGood, Fixture.Refusal(() => runtime.Quote("riverton", "gold", 1)));
        Assert.Equal(digest, runtime.StateDigest());
        runtime.Trade("riverton", "grain", 1800);
        Assert.Equal(0, runtime.Markets[0].Pairs[0].Stock);
        runtime.Trade("riverton", "grain", -(100_000));
        Assert.Equal(100_000, runtime.Markets[0].Pairs[0].Stock);
    }

    [Fact]
    public void BuyingPushesThePriceUpAndSellingPushesItDownFromAnEquilibrium()
    {
        EconomyRuntime Balanced() => Mini.Runtime(root => { var p = Mini.ThePair(root); p["production"] = 100; p["consumption"] = 100; p["stock"] = 200; p["cover"] = 2; });
        var bought = Balanced();
        var sold = Balanced();
        var control = Balanced();
        bought.Trade("m", "g", 100);
        sold.Trade("m", "g", -100);
        for (var period = 1; period <= 3; period++)
        {
            bought.Advance(period, Fixture.NoFacts); sold.Advance(period, Fixture.NoFacts); control.Advance(period, Fixture.NoFacts);
        }
        Assert.Equal(10_000, control.Markets[0].Pairs[0].Price);
        Assert.True(bought.Markets[0].Pairs[0].Price > 10_000, "bought " + bought.Markets[0].Pairs[0].Price);
        Assert.True(sold.Markets[0].Pairs[0].Price < 10_000, "sold " + sold.Markets[0].Pairs[0].Price);
    }

    [Fact]
    public void TheTaxOnABuyFollowsEveryMatchingSalesPolicy()
    {
        var runtime = EconomyRuntime.Create(EconomyLoader.Load(Edit.Doc(root =>
        {
            root["policies"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject { ["id"] = "levy", ["kind"] = "sales", ["rate"] = 50, ["perUnit"] = 0, ["tags"] = new System.Text.Json.Nodes.JsonArray("food"), ["schedule"] = "taxday" });
            Edit.Market(root, "riverton")["policies"]!.AsArray().Add("levy");
        })));
        var result = runtime.Trade("riverton", "grain", 100);
        var tax = EconomyMath.Nearest(result.Value, 100, 1000) + EconomyMath.Nearest(result.Value, 50, 1000);
        Assert.Equal(tax, result.Tax);
        Assert.Equal(EconomyMath.Nearest(result.Value, 100, 1000), runtime.Markets[0].Accrued[0]);
        Assert.Equal(EconomyMath.Nearest(result.Value, 50, 1000), runtime.Markets[0].Accrued[3]);
        var cloth = runtime.Trade("riverton", "cloth", 10);
        Assert.Equal(EconomyMath.Nearest(cloth.Value, 100, 1000), cloth.Tax);      // cloth is not food: only the general tax
    }

    [Fact]
    public void ThePriceOfATradeIsInTheMarketsOwnCurrencyAtItsOwnSpread()
    {
        var runtime = Fixture.Runtime();
        var sea = runtime.Trade("seaport", "cloth", 10);
        var river = runtime.Trade("riverton", "cloth", 10);
        // Cloth is 12,000 milli of the crown; the florin is worth half a crown, so 24,000 milli of florins in Seaport (spread 80) and 12,000 in Riverton (spread 50).
        Assert.Equal(25_920, sea.UnitPrice);
        Assert.Equal(12_600, river.UnitPrice);
    }
}
