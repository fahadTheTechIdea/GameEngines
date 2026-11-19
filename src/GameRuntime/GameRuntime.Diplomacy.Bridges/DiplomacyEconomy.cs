using DiplomacyEngine;
using EconomyEngine;
using EngineCommon;

namespace GameRuntime;

/// <summary>
/// The diplomacy's economic consequences, through the economy's own request paths. When a treaty or a mission asks for a shock (an embargo, a trade boom, a sabotage), this bridge asks the economy for it as an
/// <see cref="EconomyShockRequested"/> for the realm's market; the economy applies it in its own Update, for its own duration, and refuses it itself if it cannot. When a tribute falls due, this bridge takes the payment from
/// the payer's treasury (a negative <see cref="EconomyFundRequested"/>) and, once that is applied, gives it to the payee's (a positive one), and only then tells the diplomacy it was paid; a refusal (the payer cannot pay) tells
/// the diplomacy it was not, and a credit that is refused gives the money back. The economy never learns a diplomacy exists; the diplomacy never reads a price or a treasury. <see cref="Check"/> verifies, when the game is
/// assembled, that every shock the diplomacy document can ask for is one the economy document has, and every realm's market is one the economy has. A realm with no market has no economy to shock or pay from: its
/// shocks are not asked for, and its payments are left for the host to settle (the diplomacy counts one not settled in time as missed).
/// </summary>
public sealed class DiplomacyEconomy : IGameComponent
{
    public const string Mismatch = "DIP_ECONOMY_MISMATCH";

    private enum Stage { Debit, Credit, Refund }

    private readonly Dictionary<string, (long Treaty, Stage Stage, string Payer, string Payee, long Amount)> _open = new(StringComparer.Ordinal);
    private readonly string _diplomacyChannel;
    private readonly string _economyChannel;
    private IEventPort? _events;
    private long _requested;
    private long _skipped;
    private long _paid;
    private long _failed;

    public DiplomacyEconomy(string id, DipSet diplomacy, EconomySet economy, string diplomacyChannel = "", string economyChannel = "")
    {
        ArgumentNullException.ThrowIfNull(diplomacy);
        ArgumentNullException.ThrowIfNull(economy);
        Check(diplomacy, economy);
        Id = id;
        _diplomacyChannel = diplomacyChannel;
        _economyChannel = economyChannel;
    }

    public string Id { get; }

    /// <summary>How many shocks and fund movements have been asked of the economy.</summary>
    public long Requested => _requested;

    /// <summary>How many effects were not asked of the economy because the realm has no market.</summary>
    public long Skipped => _skipped;

    /// <summary>How many payments were moved in full and reported paid.</summary>
    public long Paid => _paid;

    /// <summary>How many payments the economy refused and were reported not paid.</summary>
    public long Failed => _failed;

    public static void Check(DipSet diplomacy, EconomySet economy)
    {
        foreach (var shock in diplomacy.Shocks)
            if (!economy.ShockById.ContainsKey(shock)) throw new EngineRefusedException(Mismatch, "the diplomacy document asks for the shock '" + shock + "', which the economy document does not have");
        foreach (var realm in diplomacy.Realms.Where(r => r.Market.Length > 0))
            if (!economy.MarketById.ContainsKey(realm.Market)) throw new EngineRefusedException(Mismatch, "realm '" + realm.Id + "' names the market '" + realm.Market + "', which the economy document does not have");
    }

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<DiplomacyEffectIssued>(OnEffect);
        context.Events.Connect<EconomyApplied>(OnApplied);
        context.Events.Connect<EconomyRefused>(OnRefused);
    }

    private string NextId(string what, long treaty) => Id + ":" + what + ":" + treaty + ":" + _requested;

    private void OnEffect(DiplomacyEffectIssued e)
    {
        if (e.Channel != _diplomacyChannel) return;
        if (e.Kind == EnumNames.Name(DiplomacyEngine.EffectKind.Shock))
        {
            if (e.Market.Length == 0) { _skipped++; return; }
            _events!.Emit(new EconomyShockRequested(Id + ":" + e.Step + ":" + _requested, e.Name, e.Market, (int)e.Scale, _economyChannel));
            _requested++;
            return;
        }
        if (e.Kind != EnumNames.Name(DiplomacyEngine.EffectKind.Payment)) return;
        if (e.Market.Length == 0 || e.OtherMarket.Length == 0) { _skipped++; return; }
        var debit = NextId("debit", e.Treaty);
        _open[debit] = (e.Treaty, Stage.Debit, e.Market, e.OtherMarket, e.Scale);
        _events!.Emit(new EconomyFundRequested(debit, e.Market, -e.Scale, _economyChannel));
        _requested++;
    }

    private void OnApplied(EconomyApplied e)
    {
        if (e.Channel != _economyChannel || !_open.Remove(e.RequestId, out var step)) return;
        switch (step.Stage)
        {
            case Stage.Debit:
            {
                var credit = NextId("credit", step.Treaty);
                _open[credit] = (step.Treaty, Stage.Credit, step.Payer, step.Payee, step.Amount);
                _events!.Emit(new EconomyFundRequested(credit, step.Payee, step.Amount, _economyChannel));
                _requested++;
                return;
            }
            case Stage.Credit:
                _paid++;
                Tell(step.Treaty, true);
                return;
            case Stage.Refund:
                return;
        }
    }

    private void OnRefused(EconomyRefused e)
    {
        if (e.Channel != _economyChannel || !_open.Remove(e.RequestId, out var step)) return;
        switch (step.Stage)
        {
            case Stage.Debit:
                _failed++;
                Tell(step.Treaty, false);
                return;
            case Stage.Credit:
            {
                // The payee's treasury could not take it: the payer's money goes back, and the payment was not made.
                var refund = NextId("refund", step.Treaty);
                _open[refund] = (step.Treaty, Stage.Refund, step.Payer, step.Payee, step.Amount);
                _events!.Emit(new EconomyFundRequested(refund, step.Payer, step.Amount, _economyChannel));
                _requested++;
                _failed++;
                Tell(step.Treaty, false);
                return;
            }
            case Stage.Refund:
                return;
        }
    }

    private void Tell(long treaty, bool paid) =>
        _events!.Emit(new DiplomacyRequested(Id + ":" + (paid ? "paid" : "unpaid") + ":" + treaty + ":" + _requested++, DiplomacyRequestNames.NameOf(DiplomacyRequestKind.Fulfil), "", "", "", treaty, paid ? 1 : 0, _diplomacyChannel));

    public void Update(in UpdateContext context)
    {
        // Nothing to do on a tick of its own: it acts on the diplomacy's effect events and the economy's answers.
    }
}
