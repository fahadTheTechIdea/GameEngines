using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>Invariants after every call of long random sequences of every kind of call, and that the same sequence gives the same state.</summary>
public sealed class BoundsTests
{
    private static readonly string[] Seasons = ["spring", "summer", "autumn", "winter"];

    private static IReadOnlyDictionary<string, FactValue> Facts(System.Random random) => Fixture.Host(Seasons[random.Next(4)], random.Next(4));

    /// <summary>One random call; the refusals that are the right answer to a random call are counted, and any other failure is a test failure.</summary>
    private static bool Act(EconomyRuntime runtime, System.Random random, ref int loan)
    {
        var set = runtime.Set;
        var market = set.Markets[random.Next(set.Markets.Count)].Id;
        var good = set.Goods[random.Next(set.Goods.Count)].Id;
        try
        {
            switch (random.Next(10))
            {
                case 0:
                case 1:
                case 2:
                    runtime.Advance(runtime.Step + random.Next(1, 120), Facts(random));
                    break;
                case 3: runtime.Trade(market, good, random.Next(-600, 600)); break;
                case 4: runtime.Fund(market, random.Next(-30_000, 30_000)); break;
                case 5: runtime.SetGrowth(set.Currencies[random.Next(set.Currencies.Count)].Id, random.Next(-20_000, 40_000)); break;
                case 6: runtime.ApplyShock(set.Shocks[random.Next(set.Shocks.Count)].Id, random.Next(3) == 0 ? "" : market, random.Next(1, 3000)); break;
                case 7: runtime.Lend("loan" + loan++, random.Next(2) == 0 ? "world" : set.Markets[random.Next(set.Markets.Count)].Id, market, random.Next(1, 20_000), random.Next(0, 30_000), random.Next(1, 12), random.Next(2) == 0 ? LoanKind.Annuity : LoanKind.Bullet, "taxday", random.Next(1, 4)); break;
                case 8: runtime.Settle("decree"); break;
                case 9: runtime.Advance(runtime.Step + random.Next(0, 24), Facts(random)); break;
            }
            return true;
        }
        catch (EngineRefusedException)
        {
            return false;
        }
    }

    internal static void Invariants(EconomyRuntime runtime)
    {
        var set = runtime.Set;
        Assert.All(runtime.Conservation(), c => Assert.Equal(0, c));
        Assert.Equal(runtime.Step / set.PeriodSteps, runtime.Period);
        foreach (var market in set.Markets)
        {
            var state = runtime.Markets[market.Index];
            Assert.True(state.Treasury >= 0, "treasury of " + market.Id);
            Assert.All(state.Accrued, a => Assert.True(a >= 0));
            Assert.All(state.Arrears, a => Assert.True(a >= 0));
            foreach (var pair in market.Pairs)
            {
                var p = state.Pairs[pair.Index];
                var good = set.Goods[pair.Good];
                var reference = runtime.ReferencePrice(market.Currency, good);
                var low = Math.Max(1, EconomyMath.Trunc(reference, good.Floor, 1000));
                var high = Math.Max(low, Math.Min(EconomyFacts.MaxPrice, EconomyMath.Trunc(reference, good.Ceiling, 1000)));
                Assert.InRange(p.Price, low, high);
                Assert.InRange(p.Stock, 0, pair.Capacity);
                Assert.InRange(p.LastChange, -good.MaxStep, good.MaxStep);
                Assert.True(p.Met <= p.Demand);
                Assert.Equal(p.Met < p.Demand, p.Short);
                Assert.Equal(p.Band, runtime.BandOf(EconomyMath.Trunc(p.Price, 1000, reference), p.Band));
            }
        }
        Assert.True(runtime.Shipments.Count <= set.Limits.MaxShipments);
        Assert.True(runtime.Loans.Count <= set.Limits.MaxLoans);
        Assert.True(runtime.ActiveShocks.Count <= set.Limits.MaxShocks);
        foreach (var s in runtime.Shipments)
        {
            Assert.True(s.Quantity > 0);
            Assert.InRange(s.Arrives, runtime.Period + 1, runtime.Period + set.Routes[s.Route].Delay);
            Assert.True(set.PairOf[set.Routes[s.Route].From][s.Good] >= 0 && set.PairOf[set.Routes[s.Route].To][s.Good] >= 0);
        }
        foreach (var currency in set.Currencies)
        {
            var c = runtime.Currencies[currency.Index];
            Assert.InRange(c.Level, 1, EconomyFacts.MaxLevel);
            Assert.InRange(c.Rate, 1, EconomyFacts.MaxRate);
            Assert.True(c.Money >= 0);
        }
        foreach (var loan in runtime.Loans)
        {
            Assert.True(loan.Balance >= 0);
            Assert.True(loan.Missed <= loan.DefaultAfter);
            if (loan.Status == LoanStatus.Active) Assert.True(loan.Remaining >= 1);
        }
    }

    [Fact]
    public void ThreeHundredRandomSequencesOfEightyCallsKeepEveryInvariantAndSurviveASaveAndOpenEveryTwentyCalls()
    {
        var refused = 0;
        var made = 0;
        for (var seed = 0; seed < 300; seed++)
        {
            var random = new System.Random(seed);
            var runtime = Fixture.Runtime();
            var loan = 0;
            for (var call = 1; call <= 80; call++)
            {
                if (Act(runtime, random, ref loan)) made++; else refused++;
                Invariants(runtime);
                if (call % 20 != 0) continue;
                var opened = EconomySave.Open(Fixture.Town, EconomySave.Write(runtime));
                Assert.Equal(runtime.StateDigest(), opened.StateDigest());
            }
        }
        Assert.True(refused > 300, "random calls are refused often enough to test refusals: " + refused);
        Assert.True(made > 6000, "and carried out often enough to test the state: " + made);
    }

    [Fact]
    public void TheSameSequenceGivesTheSameStateAndAnotherSequenceAnother()
    {
        string Run(int seed)
        {
            var random = new System.Random(seed);
            var runtime = Fixture.Runtime();
            var loan = 0;
            for (var call = 0; call < 60; call++) Act(runtime, random, ref loan);
            return runtime.StateDigest();
        }
        Assert.Equal(Run(5), Run(5));
        Assert.NotEqual(Run(5), Run(6));
    }

    [Fact]
    public void AnEconomyOpenedFromASaveAtAnyPointEndsWhereTheOneThatNeverStoppedEnds()
    {
        var random = new System.Random(99);
        var runtime = Fixture.Runtime();
        var loan = 0;
        for (var call = 0; call < 25; call++) Act(runtime, random, ref loan);
        var copy = EconomySave.Open(Fixture.Town, EconomySave.Write(runtime));
        var left = new System.Random(1234);
        var right = new System.Random(1234);
        var loanLeft = 100;
        var loanRight = 100;
        for (var call = 0; call < 60; call++)
        {
            Assert.Equal(Act(runtime, left, ref loanLeft), Act(copy, right, ref loanRight));
            Assert.Equal(runtime.StateDigest(), copy.StateDigest());
        }
    }
}
