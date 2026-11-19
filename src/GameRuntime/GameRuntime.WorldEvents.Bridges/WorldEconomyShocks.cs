using EconomyEngine;
using EngineCommon;
using WorldEventsEngine;

namespace GameRuntime;

/// <summary>
/// The world's economic consequences, through the economy's own shock path. When the world issues a shock (a drought's onset, a blockade card, a plague outbreak), this bridge asks the economy for it as an
/// <see cref="EconomyShockRequested"/> for the region's market (every market when the effect has no region), with the world's scale; the economy applies it in its own Update, for its own duration, and refuses it itself
/// if it cannot. The economy never learns a world exists; the world never reads a price. <see cref="Check"/> verifies, when the game is assembled, that every shock the world document can ask for is one the economy
/// document has, and every region's market is one the economy has.
/// </summary>
public sealed class WorldEconomyShocks : IGameComponent
{
    public const string Mismatch = "WE_ECONOMY_MISMATCH";

    private readonly string _worldChannel;
    private readonly string _economyChannel;
    private IEventPort? _events;
    private long _requested;

    public WorldEconomyShocks(string id, WorldSet world, EconomySet economy, string worldChannel = "", string economyChannel = "")
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(economy);
        Check(world, economy);
        Id = id;
        _worldChannel = worldChannel;
        _economyChannel = economyChannel;
    }

    public string Id { get; }

    /// <summary>How many shocks have been asked of the economy.</summary>
    public long Requested => _requested;

    public static void Check(WorldSet world, EconomySet economy)
    {
        foreach (var shock in world.Shocks)
            if (!economy.ShockById.ContainsKey(shock)) throw new EngineRefusedException(Mismatch, "the world document asks for the shock '" + shock + "', which the economy document does not have");
        foreach (var region in world.Regions.Where(r => r.Market.Length > 0))
            if (!economy.MarketById.ContainsKey(region.Market)) throw new EngineRefusedException(Mismatch, "region '" + region.Id + "' names the market '" + region.Market + "', which the economy document does not have");
    }

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<WorldEffectIssued>(OnEffect);
    }

    private void OnEffect(WorldEffectIssued e)
    {
        if (e.Channel != _worldChannel || e.Kind != Words.Name(WorldEventsEngine.EffectKind.Shock)) return;
        _events!.Emit(new EconomyShockRequested(Id + ":" + e.Step + ":" + _requested, e.Name, e.Market, (int)e.Scale, _economyChannel));
        _requested++;
    }

    public void Update(in UpdateContext context)
    {
        // Nothing to do on a tick of its own: it acts on the world's effect events.
    }
}
