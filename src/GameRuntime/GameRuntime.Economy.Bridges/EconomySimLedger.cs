using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EconomyEngine;

namespace GameRuntime;

/// <summary>
/// The SimEngine ledger as the record of what traders hold. A trade the economy made is written into the ledger as the inventory's own lifecycle flows on the trader's storage, through the one writer of the stock
/// ledger (<see cref="StockFlowEffect"/>, the director effect <c>stock.flows</c>): a buy issues the goods and consumes the cash, a sale consumes the goods and issues the cash, each recorded as the flow the
/// inventory already knows (<see cref="StockFlows.Issued"/>, <see cref="StockFlows.Consumed"/>), so the history is in the same facts (<c>ResourceFlowFact</c>, forwarded as <c>sim.event.stock-changed</c>) as every
/// other inventory movement. Nothing here writes the inventory directly and nothing here keeps a balance. Cash is the resource <c>econ.cash.&lt;currency&gt;</c> in minor units, goods are <c>econ.good.&lt;good&gt;</c>.
/// </summary>
/// <remarks>
/// The economy makes the trade first and the ledger follows a tick later, so a trader whose storage cannot take the flows (it does not hold the cash) would leave the two disagreeing. That is a fault, not
/// something to carry on past, as in the combat stock bridge: the host checks <see cref="Affordable"/> before it asks the economy for a trade. A trader that is not a holder in the ledger (a shopkeeper
/// with no storage) is counted in <see cref="Unrecorded"/> and not written. The ledger flow kinds are the inventory's; there is no flow between a trader and a market, because the market is not in the sim world.
/// </remarks>
public sealed class EconomySimLedger : IGameComponent
{
    public const string BadHolder = "ECON_SIM_HOLDER";

    /// <summary>Units of work a flow line costs against the budget of a tick.</summary>
    public const int FlowLineCost = 1;

    private readonly EconomySet _set;
    private readonly IReadOnlyDictionary<string, int> _holders;
    private readonly Func<int, string, long> _holdings;
    private readonly int _linesPerTick;
    private readonly string _economyChannel;
    private readonly List<StockLine> _pending = new();
    private readonly HashSet<string> _outstanding = new(StringComparer.Ordinal);
    private IEventPort? _events;
    private int _batch;
    private long _recorded;
    private long _unrecorded;

    /// <param name="holders">Trader id to the entity id of its storage in the sim world.</param>
    /// <param name="holdings">What a holder (entity id) holds of a resource now, as the ledger shows it; the host reads it from the sim view.</param>
    /// <param name="linesPerTick">The most flow lines sent in one tick; what does not fit waits, in order.</param>
    public EconomySimLedger(string id, EconomySet set, IReadOnlyDictionary<string, int> holders, Func<int, string, long> holdings, int linesPerTick = 40, string economyChannel = "")
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(holders);
        ArgumentNullException.ThrowIfNull(holdings);
        if (linesPerTick < 1) throw new ArgumentException("at least one line must be sent per tick", nameof(linesPerTick));
        foreach (var (trader, entity) in holders)
            if (entity <= 0) throw new EngineCommon.EngineRefusedException(BadHolder, "trader '" + trader + "' is held by entity " + entity + ", which is not a sim entity");
        Id = id;
        _set = set;
        _holders = holders;
        _holdings = holdings;
        _linesPerTick = linesPerTick;
        _economyChannel = economyChannel;
    }

    public string Id { get; }

    /// <summary>How many trades were written to the ledger.</summary>
    public long Recorded => _recorded;

    /// <summary>How many trades were by a trader that is not a holder, so not written.</summary>
    public long Unrecorded => _unrecorded;

    /// <summary>True when nothing is waiting to be sent or answered, so a checkpoint loses nothing.</summary>
    public bool Settled => _pending.Count == 0 && _outstanding.Count == 0;

    public static string GoodResource(string goodId) => "econ.good." + goodId;

    public static string CashResource(string currencyId) => "econ.cash." + currencyId;

    /// <summary>
    /// True when the trader's storage would take a trade: for a buy it holds the cash (the total, tax included), for a sale it holds the goods. A quote that the market refuses (not enough stock, no room) is not
    /// affordable either. The host asks before it requests the trade.
    /// </summary>
    public bool Affordable(EconomyRuntime economy, string trader, string market, string good, long quantity)
    {
        ArgumentNullException.ThrowIfNull(economy);
        if (!_holders.TryGetValue(trader, out var holder)) return false;
        TradeResult quote;
        try { quote = economy.Quote(market, good, quantity); }
        catch (EngineCommon.EngineRefusedException)
        {
            // outcome: a trade the market itself would refuse is not affordable; the refusal code reaches the requester when it asks for the trade
            return false;
        }
        var currency = _set.Currencies[_set.MarketById[market].Currency].Id;
        return quantity > 0 ? _holdings(holder, CashResource(currency)) >= quote.Total : _holdings(holder, GoodResource(good)) >= -quantity;
    }

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<EconomyTradeExecuted>(OnTrade);
        context.Events.Connect<CommandResolved>(OnResolved);
    }

    private static int Whole(long value, string what)
    {
        if (value is < 0 or > int.MaxValue) throw new InvalidOperationException("the ledger counts whole units up to " + int.MaxValue + "; " + what + " is " + value);
        return (int)value;
    }

    private void OnTrade(EconomyTradeExecuted trade)
    {
        if (trade.Channel != _economyChannel) return;
        if (!_holders.TryGetValue(trade.Trader, out var holder))
        {
            _unrecorded++;
            return;
        }
        var buy = trade.Quantity > 0;
        var units = Whole(Math.Abs(trade.Quantity), "a quantity");
        var money = Whole(trade.Total, "a total");
        var good = GoodResource(trade.Good);
        var cash = CashResource(trade.Currency);
        // A sale first brings the cash in, so the running balance of one request never goes short on cash a later line of the same request spends.
        if (buy)
        {
            if (money > 0) _pending.Add(new StockLine(StockFlows.Consumed, holder, cash, money));
            _pending.Add(new StockLine(StockFlows.Issued, holder, good, units));
        }
        else
        {
            if (money > 0) _pending.Add(new StockLine(StockFlows.Issued, holder, cash, money));
            _pending.Add(new StockLine(StockFlows.Consumed, holder, good, units));
        }
        _recorded++;
    }

    public void Update(in UpdateContext context)
    {
        if (_events is null) throw new InvalidOperationException("Update before Init");
        if (_pending.Count == 0) return;
        var count = Math.Min(_linesPerTick / FlowLineCost, _pending.Count);
        var sent = _pending.GetRange(0, count);
        _pending.RemoveRange(0, count);
        var requestId = "economy-ledger-" + (++_batch).ToString(CultureInfo.InvariantCulture);
        _outstanding.Add("effect:" + requestId);
        _events.Emit(new EffectRequested(requestId, "sim." + StockFlowEffect.EffectName, StockPayload.Write(sent), context.Tick));
    }

    private void OnResolved(CommandResolved resolved)
    {
        if (resolved.Code == "QUEUED" || !_outstanding.Remove(resolved.CommandId)) return;
        if (!resolved.Applied) throw new InvalidOperationException("the world refused the ledger flows " + resolved.CommandId + " (" + resolved.Code + "): the economy and the stock ledger disagree about a trader");
    }
}

/// <summary>The inventory vocabulary the ledger needs for an economy: a counted unit, a slot unit and one holder store, a resource for each good and one for the cash of each currency. Generated from the economy document, so the vocabulary can never name a good it lacks nor miss one it has.</summary>
public static class EconomySimContent
{
    /// <summary>The capacity policy of a trader's storage: large enough for any trade (the inventory refuses a total beyond 2^31 units in one place).</summary>
    public const string HolderStore = "econ.holder";

    private const long HolderSlots = 2_000_000_000;
    private const string CountUnit = "unit.count";
    private const string SlotUnit = "unit.slot";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string Document(EconomySet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        JsonObject Unit(string id) => new() { ["id"] = id, ["dimension"] = "count", ["scale"] = new JsonObject { ["numerator"] = 1, ["denominator"] = 1 }, ["discrete"] = true };
        JsonObject Resource(string id, string category) => new()
        {
            ["id"] = id, ["unit"] = CountUnit, ["category"] = category, ["phase"] = "solid", ["traits"] = new JsonArray(), ["capacity"] = new JsonObject { [SlotUnit] = 1 },
        };
        var resources = new JsonArray();
        foreach (var good in set.Goods) resources.Add(Resource(EconomySimLedger.GoodResource(good.Id), "goods"));
        foreach (var currency in set.Currencies) resources.Add(Resource(EconomySimLedger.CashResource(currency.Id), "cash"));
        var document = new JsonObject
        {
            ["units"] = new JsonArray { Unit(CountUnit), Unit(SlotUnit) },
            ["resources"] = resources,
            ["capacity_policies"] = new JsonArray { new JsonObject { ["id"] = HolderStore, ["limits"] = new JsonObject { [SlotUnit] = HolderSlots } } },
        };
        return document.ToJsonString(Indented);
    }
}
