namespace GameRuntime;

/// <summary>What a request asks for. One list, so a component that serves requests and the registry that refuses the unserved ones cannot disagree about which requests exist.</summary>
internal enum EconomyRequestKind { Trade, Shock, Settle, Growth, Fund, Lend, Quote, Price, Unlock }

/// <summary>
/// One queued request. <see cref="A"/> to <see cref="E"/> are the texts the kind takes, <see cref="N"/> to <see cref="Q"/> its numbers; the rest are empty. Trade: market, good, trader, quantity in N. Shock: shock,
/// market, scale in N. Settle: schedule. Growth: currency, ppm in N. Fund: market, delta in N. Lend: loan, lender, borrower, schedule, kind; principal in N, rate in M, term in P, default limit in Q.
/// Quote: market, good, quantity in N. Price: market, good. Unlock: recipe.
/// </summary>
internal readonly record struct EconomyPending(EconomyRequestKind Kind, string RequestId, string A = "", string B = "", string C = "", string D = "", string E = "", long N = 0, long M = 0, long P = 0, long Q = 0);

/// <summary>The one place every economy request event is connected: the handler gets the request channel and the request as an <see cref="EconomyPending"/>.</summary>
internal static class EconomyRequestPort
{
    public static void Connect(IEventPort events, Action<string, EconomyPending> handler)
    {
        events.Connect<EconomyTradeRequested>(r => handler(r.Channel, new EconomyPending(EconomyRequestKind.Trade, r.RequestId, r.Market, r.Good, r.Trader, N: r.Quantity)));
        events.Connect<EconomyShockRequested>(r => handler(r.Channel, new EconomyPending(EconomyRequestKind.Shock, r.RequestId, r.Shock, r.Market, N: r.ScalePermille)));
        events.Connect<EconomySettleRequested>(r => handler(r.Channel, new EconomyPending(EconomyRequestKind.Settle, r.RequestId, r.Schedule)));
        events.Connect<EconomyGrowthRequested>(r => handler(r.Channel, new EconomyPending(EconomyRequestKind.Growth, r.RequestId, r.Currency, N: r.GrowthPpm)));
        events.Connect<EconomyFundRequested>(r => handler(r.Channel, new EconomyPending(EconomyRequestKind.Fund, r.RequestId, r.Market, N: r.Delta)));
        events.Connect<EconomyLendRequested>(r => handler(r.Channel, new EconomyPending(EconomyRequestKind.Lend, r.RequestId, r.Loan, r.Lender, r.Borrower, r.Schedule, r.Kind, r.Principal, r.RatePpm, r.Term, r.DefaultAfter)));
        events.Connect<EconomyQuoteRequested>(r => handler(r.Channel, new EconomyPending(EconomyRequestKind.Quote, r.RequestId, r.Market, r.Good, N: r.Quantity)));
        events.Connect<EconomyPriceRequested>(r => handler(r.Channel, new EconomyPending(EconomyRequestKind.Price, r.RequestId, r.Market, r.Good)));
        events.Connect<EconomyRecipeUnlockRequested>(r => handler(r.Channel, new EconomyPending(EconomyRequestKind.Unlock, r.RequestId, r.Recipe)));
    }
}
