using System.Globalization;
using EconomyEngine;
using EngineCommon;

namespace GameRuntime;

public sealed partial class EconomyComponent
{
    private static readonly IReadOnlyDictionary<string, LoanKind> LoanKinds = new Dictionary<string, LoanKind>(StringComparer.Ordinal)
    {
        ["annuity"] = LoanKind.Annuity, ["bullet"] = LoanKind.Bullet,
    };

    private void Answer(IEventPort events, long tick, in EconomyPending request)
    {
        try
        {
            switch (request.Kind)
            {
                case EconomyRequestKind.Trade: Traded(events, tick, request, Runtime.Trade(request.A, request.B, request.N), true); break;
                case EconomyRequestKind.Shock: Applied(events, tick, request, Runtime.ApplyShock(request.A, request.B, ToInt(request.N))); break;
                case EconomyRequestKind.Settle: Applied(events, tick, request, Runtime.Settle(request.A)); break;
                case EconomyRequestKind.Growth: Runtime.SetGrowth(request.A, request.N); Applied(events, tick, request, new EconomyChanges()); break;
                case EconomyRequestKind.Fund: Runtime.Fund(request.A, request.N); Applied(events, tick, request, new EconomyChanges()); break;
                case EconomyRequestKind.Lend: Lend(events, tick, request); break;
                case EconomyRequestKind.Quote: Traded(events, tick, request, Runtime.Quote(request.A, request.B, request.N), false); break;
                case EconomyRequestKind.Price: PriceAnswer(events, tick, request); break;
                case EconomyRequestKind.Unlock: Applied(events, tick, request, Runtime.UnlockRecipe(request.A)); break;
            }
        }
        catch (EngineRefusedException refused)
        {
            // A refusal is a normal answer to a bad request: it is published with its code and the economy has already been left as it was.
            events.Emit(new EconomyRefused(Id, Channel, request.RequestId, tick, refused.Code));
        }
        _answered++;
    }

    private static int ToInt(long value)
    {
        if (value is < int.MinValue or > int.MaxValue) throw new EngineRefusedException(EconomyCodes.Range, "a number is " + value + ", beyond what this request can carry");
        return (int)value;
    }

    private void Applied(IEventPort events, long tick, in EconomyPending request, EconomyChanges changes)
    {
        events.Emit(new EconomyApplied(Id, Channel, request.RequestId, tick));
        Raise(events, tick, changes);
    }

    private void Lend(IEventPort events, long tick, in EconomyPending request)
    {
        if (!LoanKinds.TryGetValue(request.E, out var kind)) throw new EngineRefusedException(EconomyCodes.Range, "a loan kind is annuity or bullet, not '" + request.E + "'");
        Runtime.Lend(request.A, request.B, request.C, request.N, request.M, ToInt(request.P), kind, request.D, ToInt(request.Q));
        Applied(events, tick, request, new EconomyChanges());
    }

    private void Traded(IEventPort events, long tick, in EconomyPending request, TradeResult result, bool made)
    {
        var currency = Runtime.Set.Currencies[Runtime.Set.MarketById[result.Market].Currency].Id;
        if (made) events.Emit(new EconomyTradeExecuted(Id, Channel, request.RequestId, tick, request.C, result.Market, result.Good, currency, result.Quantity, result.UnitPrice, result.Value, result.Tax, result.Total));
        else events.Emit(new EconomyQuoted(Id, Channel, request.RequestId, tick, request.C, result.Market, result.Good, currency, result.Quantity, result.UnitPrice, result.Value, result.Tax, result.Total));
    }

    private void PriceAnswer(IEventPort events, long tick, in EconomyPending request)
    {
        var explanation = EconomyExplain.Price(Runtime, request.A, request.B);
        var terms = string.Join(';', explanation.Terms.Select(t => t.Kind + "|" + t.Id + "|" + t.Value.ToString(CultureInfo.InvariantCulture)));
        events.Emit(new EconomyPriceAnswered(Id, Channel, request.RequestId, tick, request.A, request.B, explanation.Value, explanation.State, terms));
    }
}
