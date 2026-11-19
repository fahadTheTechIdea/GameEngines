using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>Save and open: exact round trips, a digest that moves with every kind of change, and the order of the checks: format, document, ranges, digest, then what the state says about the document.</summary>
public sealed class SaveTests
{
    [Fact]
    public void ThePlayedTownHasEveryKindOfStateTheTamperCasesNeed()
    {
        var runtime = Tamper.Played();
        Assert.NotEmpty(runtime.Shipments);
        Assert.NotEmpty(runtime.Loans);
        Assert.NotEmpty(runtime.ActiveShocks);
        Assert.Contains(runtime.Markets, m => m.Accrued.Any(a => a > 0));
        Assert.Contains(runtime.Markets, m => m.History.Any(h => h > 0));
    }

    [Fact]
    public void AnOpenedSaveWritesTheSameBytesAndContinuesTheSameWay()
    {
        var runtime = Tamper.Played();
        var bytes = EconomySave.Write(runtime);
        var opened = EconomySave.Open(Fixture.Town, bytes);
        Assert.Equal(bytes, EconomySave.Write(opened));
        Assert.Equal(runtime.StateDigest(), opened.StateDigest());
        Assert.Equal(runtime.Step, opened.Step);
        Fixture.Periods(runtime, 25, Fixture.Host("winter"));
        Fixture.Periods(opened, 25, Fixture.Host("winter"));
        Assert.Equal(runtime.StateDigest(), opened.StateDigest());
        Assert.Equal(EconomySave.Write(runtime), EconomySave.Write(opened));
    }

    [Fact]
    public void EveryKindOfChangeChangesTheDigest()
    {
        var digests = new List<string> { Tamper.Played().StateDigest() };
        void Changed(Action<EconomyRuntime> change)
        {
            var runtime = Tamper.Played();
            change(runtime);
            digests.Add(runtime.StateDigest());
        }
        Changed(r => r.Trade("riverton", "grain", 1));
        Changed(r => r.Trade("riverton", "grain", -1));
        Changed(r => r.Fund("riverton", 1));
        Changed(r => r.SetGrowth("crown", 5));
        Changed(r => r.ApplyShock("devaluation", "seaport", 1000));
        Changed(r => r.Lend("other", "riverton", "hillfort", 10, 0, 2, LoanKind.Annuity, "taxday", 2));
        Changed(r => r.Advance(r.Step + 1, Fixture.Spring));
        Changed(r => r.Advance(r.Step + 24, Fixture.Spring));
        Changed(r => r.Advance(r.Step + 24 * 5, Fixture.Host("winter")));
        Assert.Equal(digests.Count, digests.Distinct().Count());
    }

    [Fact]
    public void DifferentSeedsOfNoiseGiveDifferentStatesAndTheSameSeedTheSame()
    {
        string Noisy(int seed) => Edit.Doc(root => { root["seed"] = seed; root["price"]!["noise"] = 150; });
        EconomyRuntime Run(string text)
        {
            var runtime = EconomyRuntime.Create(EconomyLoader.Load(text));
            Fixture.Periods(runtime, 10);
            return runtime;
        }
        Assert.Equal(Run(Noisy(1)).StateDigest(), Run(Noisy(1)).StateDigest());
        Assert.NotEqual(Run(Noisy(1)).StateDigest(), Run(Noisy(2)).StateDigest());
    }

    // ---- refusals, in the order of the checks ------------------------------------------------------------------------------------------

    private static string Code(Func<EconomyRuntime> open) => Assert.Throws<EngineCommon.EngineRefusedException>(open).Code;

    [Fact]
    public void NotASaveOrAnOldOrIncompleteOneIsRefusedAsFormat()
    {
        var good = EconomySave.Write(Tamper.Played());
        Assert.Equal(EconomyCodes.SaveFormat, Code(() => EconomySave.Open(Fixture.Town, "{not json"u8.ToArray())));
        Assert.Equal(EconomyCodes.SaveFormat, Code(() => EconomySave.Open(Fixture.Town, "null"u8.ToArray())));
        Assert.Equal(EconomyCodes.SaveFormat, Code(() => EconomySave.Open(Fixture.Town, Tamper.Stale(good, i => i.Format = 0))));
        Assert.Equal(EconomyCodes.SaveFormat, Code(() => EconomySave.Open(Fixture.Town, Tamper.Stale(good, i => i.Format = 1))));
        Assert.Equal(EconomyCodes.SaveFormat, Code(() => EconomySave.Open(Fixture.Town, Tamper.Stale(good, i => i.Format = 2))));
        Assert.Equal(EconomyCodes.SaveFormat, Code(() => EconomySave.Open(Fixture.Town, Tamper.Stale(good, i => i.Format = 7))));
        Assert.Equal(EconomyCodes.SaveFormat3, Code(() => EconomySave.Open(Fixture.Town, Tamper.Stale(good, i => i.Format = 3))));
        Assert.Equal(EconomyCodes.SaveFormat4, Code(() => EconomySave.Open(Fixture.Town, Tamper.Stale(good, i => i.Format = 4))));
        Assert.Equal(EconomyCodes.SaveFormat5, Code(() => EconomySave.Open(Fixture.Town, Tamper.Stale(good, i => i.Format = 5))));
        var node = JsonNode.Parse(good)!.AsObject();
        node.Remove("Step");
        Assert.Equal(EconomyCodes.SaveFormat, Code(() => EconomySave.Open(Fixture.Town, System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()))));
        var extra = JsonNode.Parse(good)!.AsObject();
        extra["Surprise"] = 1;
        Assert.Equal(EconomyCodes.SaveFormat, Code(() => EconomySave.Open(Fixture.Town, System.Text.Encoding.UTF8.GetBytes(extra.ToJsonString()))));
        var market = JsonNode.Parse(good)!.AsObject();
        market["Markets"]![0]!.AsObject().Remove("Treasury");
        Assert.Equal(EconomyCodes.SaveFormat, Code(() => EconomySave.Open(Fixture.Town, System.Text.Encoding.UTF8.GetBytes(market.ToJsonString()))));
        Assert.Equal(EconomyCodes.SaveFormat, Code(() => EconomySave.Open(Fixture.Town, new byte[EconomySave.MaxBytes + 1])));
    }

    [Fact]
    public void ASaveOfAnotherDocumentIsRefusedBeforeItsStateIsLookedAt()
    {
        var good = EconomySave.Write(Tamper.Played());
        var other = EconomyLoader.Load(Edit.Doc(root => root["seed"] = 99));
        Assert.Equal(EconomyCodes.SaveDocument, Code(() => EconomySave.Open(other, good)));
        Assert.Equal(EconomyCodes.SaveDocument, Code(() => EconomySave.Open(Fixture.Town, Tamper.Stale(good, i => { i.Id = "someone-else"; }))));
        // Document mismatch wins over a broken state.
        Assert.Equal(EconomyCodes.SaveDocument, Code(() => EconomySave.Open(other, Tamper.Stale(good, i => i.Step = -1))));
    }

    [Fact]
    public void ARangeIsCheckedBeforeTheDigestSoAForgedValueIsRefusedAsStateEvenWithTheOldDigest()
    {
        var good = EconomySave.Write(Tamper.Played());
        Assert.Equal(EconomyCodes.SaveState, Code(() => EconomySave.Open(Fixture.Town, Tamper.Stale(good, i => i.Step = -1))));
        Assert.Equal(EconomyCodes.SaveDigest, Code(() => EconomySave.Open(Fixture.Town, Tamper.Stale(good, i => i.Step += 1))));
        Assert.Equal(EconomyCodes.SaveDigest, Code(() => EconomySave.Open(Fixture.Town, Tamper.Stale(good, i => i.Markets[0].Treasury += 1))));
        Assert.Equal(EconomyCodes.SaveDigest, Code(() => EconomySave.Open(Fixture.Town, Tamper.Stale(good, i => i.Digest = new string('0', 64)))));
    }

    private static readonly List<(string Name, Action<EconomyImage> Edit)> ForgeryCases = new()
    {
            ("negative step", i => i.Step = -1),
            ("a currency missing", i => i.Currencies.RemoveAt(1)),
            ("price level zero", i => i.Currencies[0].Level = 0),
            ("price level above the ceiling", i => i.Currencies[0].Level = EconomyFacts.MaxLevel + 1),
            ("rate zero", i => i.Currencies[1].Rate = 0),
            ("rate above the ceiling", i => i.Currencies[1].Rate = EconomyFacts.MaxRate + 1),
            ("negative money", i => i.Currencies[0].Money = -1),
            ("growth beyond 500000", i => i.Currencies[0].Growth = 500_001),
            ("world account out of range", i => i.Currencies[0].World = long.MaxValue / 2),
            ("minted out of range", i => i.Currencies[0].Minted = long.MinValue / 2),
            ("a market missing", i => i.Markets.RemoveAt(2)),
            ("negative treasury", i => i.Markets[0].Treasury = -1),
            ("treasury above the ceiling", i => i.Markets[0].Treasury = EconomyFacts.MaxMoney + 1),
            ("accruals of the wrong count", i => i.Markets[0].Accrued.Add(0)),
            ("negative accrual", i => i.Markets[0].Accrued[0] = -1),
            ("arrears of the wrong count", i => i.Markets[0].Arrears.RemoveAt(0)),
            ("negative arrears", i => i.Markets[0].Arrears[0] = -5),
            ("a pair missing", i => i.Markets[0].Pairs.RemoveAt(0)),
            ("history of the wrong size", i => i.Markets[0].History.RemoveAt(0)),
            ("history out of range", i => i.Markets[0].History[0] = -1),
            ("price zero", i => i.Markets[0].Pairs[0].Price = 0),
            ("price above the ceiling", i => i.Markets[0].Pairs[0].Price = EconomyFacts.MaxPrice + 1),
            ("stock above the warehouse", i => i.Markets[0].Pairs[0].Stock = 1_000_001),
            ("negative stock", i => i.Markets[0].Pairs[0].Stock = -1),
            ("stock one over the warehouse", i => i.Markets[0].Pairs[0].Stock = 100_001),
            ("last change beyond 1000", i => i.Markets[0].Pairs[0].LastChange = 1001),
            ("a band the document lacks", i => i.Markets[0].Pairs[0].Band = 9),
            ("negative supply", i => i.Markets[0].Pairs[0].Supply = -1),
            ("negative spot demand", i => i.Markets[0].Pairs[0].SpotDemand = -1),
            ("met more than wanted", i => { i.Markets[0].Pairs[0].Demand = 5; i.Markets[0].Pairs[0].Met = 6; }),
            ("short while meeting demand", i => { var p = i.Markets[0].Pairs[0]; p.Met = p.Demand; p.Short = true; }),
            ("not short while missing demand", i => { var p = i.Markets[0].Pairs[0]; p.Demand = p.Met + 1; p.Short = false; }),
            ("a shipment on a route the document lacks", i => i.Shipments[0].Route = 99),
            ("a shipment of a good the document lacks", i => i.Shipments[0].Good = 99),
            ("a shipment of nothing", i => i.Shipments[0].Quantity = 0),
            ("a shipment that arrives later than the delay", i => i.Shipments[0].Arrives += 40),
            ("a shipment that arrived already", i => i.Shipments[0].Arrives = 0),
            ("too many shipments", i => { while (i.Shipments.Count <= 200) i.Shipments.Add(i.Shipments[0]); }),
            ("a loan id that is not an id", i => i.Loans[0].Id = "Not An Id"),
            ("two loans with one id", i => i.Loans.Add(i.Loans[0])),
            ("a lender that is the borrower", i => i.Loans[0].Lender = i.Loans[0].Borrower),
            ("a borrower the document lacks", i => i.Loans[0].Borrower = 9),
            ("a loan between currencies", i => { i.Loans[0].Lender = 2; i.Loans[0].Borrower = 0; }),
            ("a loan on a schedule the document lacks", i => i.Loans[0].Schedule = 9),
            ("a loan of a kind the document lacks", i => i.Loans[0].Kind = 2),
            ("a loan with a status the document lacks", i => i.Loans[0].Status = 3),
            ("a loan with a rate beyond 100 percent", i => i.Loans[0].RatePpm = 1_000_001),
            ("a negative loan balance", i => i.Loans[0].Balance = -1),
            ("a loan that is active with nothing left", i => i.Loans[0].Remaining = 0),
            ("a loan in default that has not missed", i => { i.Loans[0].Status = 2; i.Loans[0].Missed = 0; }),
            ("a shock the document lacks", i => i.Shocks[0].Shock = 9),
            ("a shock on a market the document lacks", i => i.Shocks[0].Market = 9),
            ("a shock with scale zero", i => i.Shocks[0].Scale = 0),
            ("a shock that began in the future", i => i.Shocks[0].Start = 10_000),
            ("a shock that ended long ago", i => { i.Shocks[0].Shock = 2; i.Shocks[0].Start = 0; }),
            ("too many shocks", i => { while (i.Shocks.Count <= 8) i.Shocks.Add(i.Shocks[0]); }),
            ("accruals missing one", i => i.Markets[0].Accrued.RemoveAt(0)),
            ("a shipment that arrives in the period already reached", i => i.Shipments[0].Arrives = i.Step / 24),
            ("a shock that ends exactly now", i => { i.Shocks[0].Shock = 2; i.Shocks[0].Start = i.Step / 24 - 1; }),
            ("a shock that begins next period", i => i.Shocks[0].Start = i.Step / 24 + 1),
            ("money vanished", i => i.Currencies[0].World -= 1),
            ("a price just over its ceiling", i => { i.Markets[0].Pairs[0].Price = 8001; i.Markets[0].Pairs[0].Band = 3; }),
            ("money from nowhere", i => i.Currencies[0].World += 1),
            ("money in a treasury from nowhere", i => i.Markets[1].Treasury += 1),
            ("a price under its floor", i => { i.Markets[0].Pairs[3].Price = 1; }),
            ("a price over its ceiling", i => { i.Markets[0].Pairs[3].Price = EconomyFacts.MaxPrice; }),
            ("a band its price does not give", i => { var p = i.Markets[0].Pairs[3]; p.Band = (p.Band + 2) % 4; }),
    };

    internal static IEnumerable<string> ForgeryNames() => ForgeryCases.Select(c => c.Name);

    internal static Action<EconomyImage> ForgeryEdit(int index) => ForgeryCases[index].Edit;

    public static IEnumerable<object[]> Forgeries() => ForgeryCases.Select((c, index) => new object[] { index, c.Name });

    [Theory]
    [MemberData(nameof(Forgeries))]
    public void AForgedStateWithARecomputedDigestIsRefusedAsState(int index, string name)
    {
        var edit = ForgeryCases[index].Edit;
        var good = EconomySave.Write(Tamper.Played());
        EconomySave.Open(Fixture.Town, good);
        var forged = Tamper.Forge(good, edit);
        var code = Code(() => EconomySave.Open(Fixture.Town, forged));
        Assert.True(code == EconomyCodes.SaveState, name + " was refused as " + code);
    }

    [Fact]
    public void AShipmentOfAGoodOneEndOfItsRouteDoesNotTradeIsRefusedAsState()
    {
        var set = Mini.Set(root =>
        {
            root["goods"]!.AsArray().Add(Mini.Good("h"));
            Mini.TheMarket(root)["pairs"]!.AsArray().Add(Mini.Pair("h", 100, 100, 1000));
            root["markets"]!.AsArray().Add(Mini.Market("n", "c"));
            root["routes"]!.AsArray().Add(new JsonObject { ["id"] = "r", ["from"] = "m", ["to"] = "n", ["delay"] = 2, ["capacity"] = 10, ["costPerWeight"] = 0, ["margin"] = 0, ["share"] = 500 });
        });
        var good = EconomySave.Write(EconomyRuntime.Create(set));
        var forged = Tamper.Forge(good, i => i.Shipments.Add(new ImageShipment { Route = 0, Good = 1, Quantity = 5, Arrives = 1 }));
        Assert.Equal(EconomyCodes.SaveState, Code(() => EconomySave.Open(set, forged)));
        var allowed = Tamper.Forge(good, i => i.Shipments.Add(new ImageShipment { Route = 0, Good = 0, Quantity = 5, Arrives = 1 }));
        Assert.Single(EconomySave.Open(set, allowed).Shipments);
    }

    [Fact]
    public void AnImageSerialisesWithEveryFieldRequiredByName()
    {
        var node = JsonNode.Parse(EconomySave.Write(Tamper.Played()))!.AsObject();
        Assert.Equal(["Format", "Document", "Id", "Step", "Currencies", "Markets", "Shipments", "Loans", "Shocks", "Unlocked", "Batches", "Digest"], node.Select(p => p.Key).ToList());
        using var document = JsonDocument.Parse(EconomySave.Write(Tamper.Played()));
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }
}
