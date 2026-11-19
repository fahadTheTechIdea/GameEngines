using System.Text.Json.Nodes;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>Taxes remitted on their dates, wages and upkeep paid or owed, loans serviced, and the one law of money: it moves only between treasuries and the world account.</summary>
public sealed class SettleTests
{
    private static JsonObject Policy(string id, string kind, int rate, long perUnit, string schedule, params string[] tags) => new()
    {
        ["id"] = id, ["kind"] = kind, ["rate"] = rate, ["perUnit"] = perUnit, ["tags"] = new JsonArray(tags.Select(t => (JsonNode)t).ToArray()), ["schedule"] = schedule,
    };

    private static JsonObject Line(string id, string kind, string schedule, long amount, long perHead, bool indexed) => new()
    {
        ["id"] = id, ["kind"] = kind, ["schedule"] = schedule, ["amount"] = amount, ["perHead"] = perHead, ["indexed"] = indexed,
    };

    private static JsonObject LoanDoc(string id, string lender, string borrower, long principal, long rate, int term, string kind, string schedule = "pay", int defaultAfter = 3) => new()
    {
        ["id"] = id, ["lender"] = lender, ["borrower"] = borrower, ["principal"] = principal, ["ratePpm"] = rate, ["term"] = term, ["kind"] = kind, ["schedule"] = schedule, ["defaultAfter"] = defaultAfter,
    };

    private static void Add(JsonObject root, string array, JsonObject item) => root[array]!.AsArray().Add(item);

    private static void Link(JsonObject market, string array, JsonNode item) => market[array]!.AsArray().Add(item);

    // ---- taxes -------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void SalesTaxAccruesOnTheValueSoldAndIsRemittedOnItsScheduleToTheTreasury()
    {
        var runtime = Mini.Runtime(root =>
        {
            Add(root, "policies", Policy("tax", "sales", 100, 0, "pay"));
            Link(Mini.TheMarket(root), "policies", "tax");
            Mini.TheGood(root)["speed"] = 0;
        });
        var treasury = runtime.Markets[0].Treasury;
        long accrued = 0;
        for (var period = 1; period <= 2; period++)
        {
            runtime.Advance(period, Fixture.NoFacts);
            var pair = runtime.Markets[0].Pairs[0];
            // price 10,000 milli never moves (speed 0); consumers pay 10 percent more, so ratio 1100 and demand responds 1000 - 1000 * 100 / 1000 = 900 per mille of 100.
            Assert.Equal(90, pair.Demand);
            accrued += EconomyMath.Nearest(EconomyMath.Nearest(90, 10_000, 1000), 100, 1000);
            Assert.Equal(accrued, runtime.Markets[0].Accrued[0]);
            Assert.Equal(treasury, runtime.Markets[0].Treasury);
        }
        var changes = runtime.Advance(3, Fixture.NoFacts);
        accrued += 90;
        Assert.Equal(0, runtime.Markets[0].Accrued[0]);
        Assert.Equal(treasury + accrued, runtime.Markets[0].Treasury);
        Assert.Equal(new Settled(0, SettleKind.Tax, "tax", accrued, accrued), Assert.Single(changes.Settlements));
        Assert.Equal(-accrued, runtime.Currencies[0].World);
        Assert.All(runtime.Conservation(), c => Assert.Equal(0, c));
    }

    [Fact]
    public void ATaxOnlyCoversTheGoodsItsTagsName()
    {
        var runtime = Mini.Runtime(root =>
        {
            Mini.TheGood(root)["tags"] = new JsonArray("food");
            Add(root, "goods", Mini.Good("h"));
            Add(root, "policies", Policy("tax", "sales", 100, 0, "pay", "luxury"));
            var market = Mini.TheMarket(root);
            Link(market, "policies", "tax");
            Link(market, "pairs", Mini.Pair("h", 100, 100, 100));
            Edit.Obj(root, "goods", 1)["tags"] = new JsonArray("luxury");
        });
        runtime.Advance(1, Fixture.NoFacts);
        Assert.Equal(100, runtime.Markets[0].Pairs[0].Demand);      // food: untaxed
        Assert.True(runtime.Markets[0].Pairs[1].Demand < 100);       // luxury: taxed
        Assert.True(runtime.Markets[0].Accrued[0] > 0);
    }

    [Fact]
    public void ASubsidyLiftsSupplyAndIsPaidFromTheTreasuryEveryPeriod()
    {
        var plain = Mini.Runtime(root => { Mini.ThePair(root)["production"] = 100; });
        var subsidised = Mini.Runtime(root =>
        {
            Add(root, "policies", Policy("aid", "subsidy", 0, 2000, ""));
            Link(Mini.TheMarket(root), "policies", "aid");
            Mini.ThePair(root)["production"] = 100;
        });
        var treasury = subsidised.Markets[0].Treasury;
        plain.Advance(1, Fixture.NoFacts);
        var changes = subsidised.Advance(1, Fixture.NoFacts);
        // producer price 10,000 + 2,000: ratio 1200, supply response 1000 + 1000 * 200 / 1000 = 1200 per mille.
        Assert.Equal(120, subsidised.Markets[0].Pairs[0].Supply);
        Assert.Equal(100, plain.Markets[0].Pairs[0].Supply);
        var paid = EconomyMath.Nearest(120, 2000, 1000);
        Assert.Equal(new Settled(0, SettleKind.Subsidy, "aid", paid, paid), Assert.Single(changes.Settlements));
        Assert.Equal(treasury - paid, subsidised.Markets[0].Treasury);
        Assert.Equal(paid, subsidised.Currencies[0].World);
        Assert.All(subsidised.Conservation(), c => Assert.Equal(0, c));
    }

    [Fact]
    public void ASubsidyTheTreasuryCannotCoverIsPaidInProportionAndNeverBeyondTheTreasury()
    {
        var runtime = Mini.Runtime(root =>
        {
            Add(root, "policies", Policy("aid", "subsidy", 0, 2000, ""));
            var market = Mini.TheMarket(root);
            Link(market, "policies", "aid");
            market["treasury"] = 50;
            Mini.ThePair(root)["production"] = 100;
        });
        var changes = runtime.Advance(1, Fixture.NoFacts);
        // Wanted: 100 * 2000 / 1000 = 200; the treasury holds 50, so a quarter of the unit amount is offered (ratio 250 per mille) and the payment stops at what is there.
        var settled = Assert.Single(changes.Settlements);
        Assert.InRange(settled.Paid, 1, 50);
        Assert.True(settled.Paid <= settled.Due);
        Assert.Equal(50 - settled.Paid, runtime.Markets[0].Treasury);
        Assert.True(runtime.Markets[0].Treasury >= 0);
        runtime.Advance(2, Fixture.NoFacts);
        Assert.True(runtime.Markets[0].Treasury >= 0);
    }

    // ---- wages and upkeep ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void WagesAreTheAmountPlusTheAmountPerHeadAndArePaidToTheWorld()
    {
        var runtime = Mini.Runtime(root =>
        {
            var market = Mini.TheMarket(root);
            market["staff"] = 10;
            Link(market, "budget", Line("wages", "wage", "pay", 200, 100, false));
        });
        var changes = Fixture_Advance(runtime, 3);
        Assert.Equal(new Settled(0, SettleKind.Wage, "wages", 1200, 1200), Assert.Single(changes.Settlements));
        Assert.Equal(10_000 - 1200, runtime.Markets[0].Treasury);
        Assert.Equal(1200, runtime.Currencies[0].World);
        Assert.False(runtime.InArrears(0));
    }

    private static EconomyChanges Fixture_Advance(EconomyRuntime runtime, int periods) => Fixture.Periods(runtime, periods, Fixture.NoFacts);

    [Fact]
    public void IndexedPayFollowsThePriceLevel()
    {
        var runtime = Mini.Runtime(root =>
        {
            var market = Mini.TheMarket(root);
            market["staff"] = 10;
            Link(market, "budget", Line("wages", "wage", "pay", 0, 100, true));
            root["currencies"]!.AsArray()[0]!["growthPpm"] = 100_000;
        });
        Fixture_Advance(runtime, 2);
        var changes = Fixture_Advance(runtime, 1);
        var level = runtime.Currencies[0].Level;
        Assert.Equal(EconomyMath.Nearest(1000, level, 1_000_000), Assert.Single(changes.Settlements, s => s.Kind == SettleKind.Wage).Due);
        Assert.True(level > 1_000_000);
    }

    [Fact]
    public void WagesTheTreasuryCannotPayStayOwedAreFirstInLineNextTimeAndCutOutputWhileOwed()
    {
        var runtime = Mini.Runtime(root =>
        {
            var market = Mini.TheMarket(root);
            market["staff"] = 10;
            market["treasury"] = 500;
            Link(market, "budget", Line("wages", "wage", "pay", 0, 100, false));
            Mini.TheGood(root)["speed"] = 0;
            Mini.ThePair(root)["production"] = 1000;
            Mini.ThePair(root)["consumption"] = 0;
            Mini.ThePair(root)["capacity"] = 1_000_000_000;
        });
        var changes = Fixture_Advance(runtime, 3);
        var settled = Assert.Single(changes.Settlements);
        Assert.Equal(new Settled(0, SettleKind.Wage, "wages", 500, 1000), settled);
        Assert.Equal(500, runtime.Markets[0].Arrears[0]);
        Assert.Equal(0, runtime.Markets[0].Treasury);
        Assert.True(runtime.InArrears(0));
        // While wages are owed the market loses arrearsPenalty (300 per mille) of its output.
        runtime.Advance(4, Fixture.NoFacts);
        Assert.Equal(700, runtime.Markets[0].Pairs[0].Supply);
        // A grant lets it pay what is owed and the new wages at the next pay day.
        runtime.Fund("m", 5000);
        Fixture_Advance(runtime, 2);
        Assert.Equal(0, runtime.Markets[0].Arrears[0]);
        Assert.False(runtime.InArrears(0));
        Assert.Equal(5000 - 1500, runtime.Markets[0].Treasury);
        Assert.All(runtime.Conservation(), c => Assert.Equal(0, c));
    }

    [Fact]
    public void AnUpkeepLineIsReportedAsUpkeep()
    {
        var runtime = Mini.Runtime(root => Link(Mini.TheMarket(root), "budget", Line("roads", "upkeep", "pay", 300, 0, false)));
        var changes = Fixture_Advance(runtime, 3);
        Assert.Equal(new Settled(0, SettleKind.Upkeep, "roads", 300, 300), Assert.Single(changes.Settlements));
    }

    // ---- loans -------------------------------------------------------------------------------------------------------------------------

    private static EconomyRuntime WithLoan(string kind, long principal, long rate, int term, string lender = "world", int defaultAfter = 3, long borrowerTreasury = 100_000)
    {
        return Mini.Runtime(root =>
        {
            var market = Mini.TheMarket(root);
            market["treasury"] = borrowerTreasury;
            Add(root, "markets", Mini.Market("l", "c"));
            Edit.ById(root, "markets", "l")["treasury"] = 500_000;
            Add(root, "loans", LoanDoc("loan", lender, "m", principal, rate, term, kind, "pay", defaultAfter));
        });
    }

    [Fact]
    public void AnAnnuityLoanIsServicedWithInterestOnTheBalanceAndEndsAtZero()
    {
        var runtime = WithLoan("annuity", 10_000, 10_000, 4);
        var payment = EconomyMath.Annuity(10_000, 10_000, 4);
        var balance = 10_000L;
        var treasury = runtime.Markets[0].Treasury;
        for (var n = 1; n <= 4; n++)
        {
            var changes = Fixture_Advance(runtime, 3);
            var interest = EconomyMath.Nearest(balance, 10_000, 1_000_000);
            var principal = n == 4 ? balance : payment - interest;
            var settled = Assert.Single(changes.Settlements, s => s.Kind == SettleKind.Loan);
            Assert.Equal(new Settled(0, SettleKind.Loan, "loan", interest + principal, interest + principal), settled);
            balance -= principal;
            treasury -= interest + principal;
            Assert.Equal(balance, runtime.Loans[0].Balance);
            Assert.Equal(treasury, runtime.Markets[0].Treasury);
            Assert.Equal(4 - n, runtime.Loans[0].Remaining);
        }
        Assert.Equal(LoanStatus.Closed, runtime.Loans[0].Status);
        Assert.Equal(0, runtime.Loans[0].Balance);
        var more = Fixture_Advance(runtime, 6);
        Assert.DoesNotContain(more.Settlements, s => s.Kind == SettleKind.Loan);
        Assert.All(runtime.Conservation(), c => Assert.Equal(0, c));
    }

    [Fact]
    public void ABulletLoanPaysInterestOnlyUntilTheLastPeriod()
    {
        var runtime = WithLoan("bullet", 10_000, 20_000, 3);
        var due = new List<long>();
        for (var n = 1; n <= 3; n++) due.Add(Assert.Single(Fixture_Advance(runtime, 3).Settlements, s => s.Kind == SettleKind.Loan).Due);
        Assert.Equal([200L, 200L, 10_200L], due);
        Assert.Equal(LoanStatus.Closed, runtime.Loans[0].Status);
    }

    [Fact]
    public void AMissedPaymentAddsTheInterestToTheBalanceAndTooManyInARowIsADefault()
    {
        var runtime = WithLoan("annuity", 10_000, 10_000, 6, defaultAfter: 2, borrowerTreasury: 0);
        var first = Fixture_Advance(runtime, 3);
        Assert.Equal(new Settled(0, SettleKind.Loan, "loan", 0, EconomyMath.Nearest(10_000, 10_000, 1_000_000) + EconomyMath.Annuity(10_000, 10_000, 6) - 100), Assert.Single(first.Settlements, s => s.Kind == SettleKind.Loan));
        Assert.Equal(new LoanChanged("loan", LoanOutcome.Missed, 10_100), Assert.Single(first.Loans));
        Assert.Equal(10_100, runtime.Loans[0].Balance);
        Assert.Equal(1, runtime.Loans[0].Missed);
        Assert.Equal(6, runtime.Loans[0].Remaining);
        var second = Fixture_Advance(runtime, 3);
        Assert.Contains(new LoanChanged("loan", LoanOutcome.Defaulted, runtime.Loans[0].Balance), second.Loans);
        Assert.Equal(LoanStatus.Defaulted, runtime.Loans[0].Status);
        var balance = runtime.Loans[0].Balance;
        var third = Fixture_Advance(runtime, 6);
        Assert.Empty(third.Loans);
        Assert.Equal(balance, runtime.Loans[0].Balance);
    }

    [Fact]
    public void APaymentMadeAfterAMissClearsTheCountOfMisses()
    {
        var runtime = WithLoan("annuity", 10_000, 10_000, 6, defaultAfter: 3, borrowerTreasury: 0);
        Fixture_Advance(runtime, 3);
        Assert.Equal(1, runtime.Loans[0].Missed);
        runtime.Fund("m", 50_000);
        Fixture_Advance(runtime, 3);
        Assert.Equal(0, runtime.Loans[0].Missed);
        Assert.Equal(5, runtime.Loans[0].Remaining);
        Assert.Equal(LoanStatus.Active, runtime.Loans[0].Status);
    }

    [Fact]
    public void ALoanFromAMarketMovesMoneyBetweenTheTwoTreasuriesAndLeavesTheWorldAlone()
    {
        var runtime = WithLoan("annuity", 10_000, 10_000, 4, lender: "l");
        var lender = runtime.Markets[1].Treasury;
        var borrower = runtime.Markets[0].Treasury;
        var changes = Fixture_Advance(runtime, 3);
        var paid = Assert.Single(changes.Settlements, s => s.Kind == SettleKind.Loan).Paid;
        Assert.Equal(lender + paid, runtime.Markets[1].Treasury);
        Assert.Equal(borrower - paid, runtime.Markets[0].Treasury);
        Assert.Equal(0, runtime.Currencies[0].World);
    }

    [Fact]
    public void LendingMovesThePrincipalAndOpensALoanThatIsServicedLikeADocumentOne()
    {
        var runtime = Mini.Runtime(root => Add(root, "markets", Mini.Market("l", "c")));
        runtime.Lend("new", "world", "m", 6000, 0, 3, LoanKind.Bullet, "pay", 2);
        Assert.Equal(16_000, runtime.Markets[0].Treasury);
        Assert.Equal(-6000, runtime.Currencies[0].World);
        Assert.Equal(6000, runtime.Loans[0].Balance);
        runtime.Lend("from-market", "l", "m", 4000, 0, 2, LoanKind.Annuity, "pay", 2);
        Assert.Equal(6000, runtime.Markets[1].Treasury);
        Assert.Equal(20_000, runtime.Markets[0].Treasury);
        Assert.Equal(2000, runtime.Loans[1].Payment);
        Assert.All(runtime.Conservation(), c => Assert.Equal(0, c));
        var changes = Fixture_Advance(runtime, 3);
        Assert.Equal(2, changes.Settlements.Count(s => s.Kind == SettleKind.Loan));
    }

    [Fact]
    public void ALoanTheLenderCannotFundOrThatBreaksAnyRuleIsRefusedAndChangesNothing()
    {
        var runtime = Mini.Runtime(root => { Add(root, "markets", Mini.Market("l", "c")); root["limits"]!["maxLoans"] = 1; });
        var digest = runtime.StateDigest();
        Assert.Equal(EconomyCodes.Funds, Fixture.Refusal(() => runtime.Lend("a", "l", "m", 10_001, 0, 3, LoanKind.Bullet, "pay", 2)));
        Assert.Equal(EconomyCodes.Loan, Fixture.Refusal(() => runtime.Lend("a", "m", "m", 100, 0, 3, LoanKind.Bullet, "pay", 2)));
        Assert.Equal(EconomyCodes.UnknownMarket, Fixture.Refusal(() => runtime.Lend("a", "nowhere", "m", 100, 0, 3, LoanKind.Bullet, "pay", 2)));
        Assert.Equal(EconomyCodes.UnknownSchedule, Fixture.Refusal(() => runtime.Lend("a", "world", "m", 100, 0, 3, LoanKind.Bullet, "never", 2)));
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => runtime.Lend("a", "world", "m", 0, 0, 3, LoanKind.Bullet, "pay", 2)));
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => runtime.Lend("a", "world", "m", 100, 0, 1201, LoanKind.Bullet, "pay", 2)));
        Assert.Equal(digest, runtime.StateDigest());
        runtime.Lend("a", "world", "m", 100, 0, 3, LoanKind.Bullet, "pay", 2);
        Assert.Equal(EconomyCodes.DuplicateLoan, Fixture.Refusal(() => runtime.Lend("a", "world", "m", 100, 0, 3, LoanKind.Bullet, "pay", 2)));
        Assert.Equal(EconomyCodes.Capacity, Fixture.Refusal(() => runtime.Lend("b", "world", "m", 100, 0, 3, LoanKind.Bullet, "pay", 2)));
    }

    // ---- manual schedules, grants ---------------------------------------------------------------------------------------------------

    [Fact]
    public void AManualScheduleIsSettledByHandAndAScheduleThatRunsByItselfIsRefused()
    {
        var runtime = Mini.Runtime(root =>
        {
            var market = Mini.TheMarket(root);
            market["staff"] = 4;
            Link(market, "budget", Line("hand-pay", "wage", "hand", 0, 100, false));
            Link(market, "budget", Line("auto-pay", "wage", "pay", 0, 100, false));
        });
        Fixture_Advance(runtime, 2);
        Assert.Equal(10_000, runtime.Markets[0].Treasury);     // neither is due: the manual one is not due by itself, the other is on period 3
        var changes = runtime.Settle("hand");
        Assert.Equal(new Settled(0, SettleKind.Wage, "hand-pay", 400, 400), Assert.Single(changes.Settlements));
        Assert.Equal(9600, runtime.Markets[0].Treasury);
        Assert.Equal(EconomyCodes.Schedule, Fixture.Refusal(() => runtime.Settle("pay")));
        Assert.Equal(EconomyCodes.UnknownSchedule, Fixture.Refusal(() => runtime.Settle("nope")));
        var later = Fixture_Advance(runtime, 5);
        Assert.DoesNotContain(later.Settlements, s => s.Id == "hand-pay");
    }

    [Fact]
    public void AGrantMovesMoneyBetweenTheWorldAndATreasuryAndNeverBelowZero()
    {
        var runtime = Mini.Runtime();
        runtime.Fund("m", 2500);
        Assert.Equal(12_500, runtime.Markets[0].Treasury);
        Assert.Equal(-2500, runtime.Currencies[0].World);
        runtime.Fund("m", -12_500);
        Assert.Equal(0, runtime.Markets[0].Treasury);
        Assert.Equal(10_000, runtime.Currencies[0].World);
        Assert.Equal(EconomyCodes.Funds, Fixture.Refusal(() => runtime.Fund("m", -1)));
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => runtime.Fund("m", 0)));
        runtime.Fund("m", 1);
        Assert.Equal(EconomyCodes.Funds, Fixture.Refusal(() => runtime.Fund("m", EconomyFacts.MaxMoney)));
        Assert.All(runtime.Conservation(), c => Assert.Equal(0, c));
    }
}
