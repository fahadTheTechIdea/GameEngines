using AIEngine;
using EconomyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>Which market an agent trades in: the agent id (an AI agent) and the market id.</summary>
public readonly record struct EconomyPerceiver(string Agent, string Market);

/// <summary>Goods told about in one percept kind: at most <see cref="EconomyAiBridge.GoodsPerKind"/>, because each takes three attributes and an AI percept kind holds at most 16.</summary>
public sealed record EconomyGoodsPercept(string Kind, IReadOnlyList<string> Goods);

/// <summary>Recipes whose production margin is told in one percept kind: at most <see cref="EconomyAiBridge.RecipesPerKind"/>, one attribute each.</summary>
public sealed record EconomyMarginPercept(string Kind, IReadOnlyList<string> Recipes);

/// <summary>
/// Tells AI agents what the market they trade in looks like, as percepts they perceive: knowledge, not truth. A merchant agent does not read the economy; it knows the price of grain only when something tells it,
/// and what it knows is the percept it was last told (with the tick it was told at, so the AI document can let it go stale). A goods percept carries, for each good, the price over its reference
/// (<c>ratio_&lt;good&gt;</c>, per mille: what an agent buys, sells or hauls by), whether the market is short of it (<c>short_&lt;good&gt;</c>) and the best it could fetch from here over its own price
/// (<c>reach_&lt;good&gt;</c>, per mille: 1,000 when nothing beats selling here; with landed routing it counts every market the routes reach and everything the goods pay on the way, not only the next market). A margin
/// percept carries, for each recipe, what a run of the market's line for it earns (<c>margin_&lt;recipe&gt;</c>, per mille: the outputs' worth over the inputs' cost at today's prices and the shocks that are on, 1,000 at break-even;
/// 0 when the market has no line for the recipe, as a good the market does not trade reads 0). Which agents are told is the host's decision (<c>perceivers</c>); the bridge posts a percept to an agent when what it
/// would perceive changed since it was last told, and once at the start. An agent that is not named is never told and acts on whatever it knew before.
/// </summary>
/// <remarks>
/// An AI percept kind holds at most 16 attributes, so an economy of many goods or recipes is told in several kinds, each declared by the AI document: <see cref="PlanGoods"/> and <see cref="PlanMargins"/> split the whole
/// document into kinds (every good and every recipe told, in document order, nothing cut), and the host may instead name its own groups. The AI document must declare each kind with exactly the attributes of its
/// group: <c>ratio_&lt;good&gt;</c> and <c>reach_&lt;good&gt;</c> (int covering 0 to 1,000,000) and <c>short_&lt;good&gt;</c> (bool) for a goods kind, <c>margin_&lt;recipe&gt;</c> (int covering 0 to 1,000,000) for a margin kind. The
/// constructor checks that against both documents and refuses otherwise. A good or a recipe in two groups, a group over the limit and two groups with one kind are refused. Add the bridge to the session after the
/// economy component and before the AI component. Margins are the prices' margin with the shocks that are on; the host-fact modifiers are not applied, because the bridge is not given the facts.
/// </remarks>
public sealed partial class EconomyAiBridge : IGameComponent
{
    public const string BadKind = "ECONAI_PERCEPT_KIND";

    /// <summary>The most goods one percept kind tells about: three attributes each, 16 attributes a kind.</summary>
    public const int GoodsPerKind = 5;

    /// <summary>The most recipes one percept kind tells about: one attribute each, 16 attributes a kind.</summary>
    public const int RecipesPerKind = 16;

    private readonly EconomyComponent _economy;
    private readonly string _aiComponent;
    private readonly string _perceptId;
    private readonly Func<IReadOnlyList<EconomyPerceiver>> _perceivers;
    private readonly IReadOnlyList<(string Kind, IReadOnlyList<GoodDef> Goods)> _goodGroups;
    private readonly IReadOnlyList<(string Kind, IReadOnlyList<RecipeDef> Recipes)> _marginGroups;
    private readonly Dictionary<string, string> _told = new(StringComparer.Ordinal);
    private IEventPort? _events;
    private long _posted;

    /// <param name="aiComponent">The id of the AI component whose agents are told.</param>
    /// <param name="aiSet">The AI document, to check every percept kind against.</param>
    /// <param name="perceptId">The id the percepts have in the agent's knowledge.</param>
    /// <param name="perceivers">The agents that can perceive their market now, in the order they are told.</param>
    /// <param name="goods">The goods percept kinds, each with the ids of the goods it tells about; null or empty tells about no goods. <see cref="PlanGoods"/> makes the groups for the whole document.</param>
    /// <param name="margins">The margin percept kinds, each with the ids of the recipes it tells about; null or empty tells about no margins. <see cref="PlanMargins"/> makes the groups for the whole document.</param>
    public EconomyAiBridge(string id, EconomyComponent economy, string aiComponent, AiSet aiSet, string perceptId, Func<IReadOnlyList<EconomyPerceiver>> perceivers,
        IReadOnlyList<EconomyGoodsPercept>? goods, IReadOnlyList<EconomyMarginPercept>? margins = null)
    {
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(aiSet);
        ArgumentNullException.ThrowIfNull(perceivers);
        Id = id;
        _economy = economy;
        _aiComponent = aiComponent;
        _perceptId = perceptId;
        _perceivers = perceivers;
        var set = economy.Runtime.Set;
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        _goodGroups = ResolveGoods(set, goods ?? [], kinds);
        _marginGroups = ResolveMargins(set, margins ?? [], kinds);
        foreach (var (kind, tracked) in _goodGroups) CheckGoods(aiSet, tracked, kind);
        foreach (var (kind, tracked) in _marginGroups) CheckMargins(aiSet, tracked, kind);
    }

    /// <summary>The single-kind form: one goods percept kind telling about <paramref name="goods"/> (null: every good, which fits only an economy of at most <see cref="GoodsPerKind"/> goods).</summary>
    public EconomyAiBridge(string id, EconomyComponent economy, string aiComponent, AiSet aiSet, string kind, string perceptId, Func<IReadOnlyList<EconomyPerceiver>> perceivers, IReadOnlyList<string>? goods = null)
        : this(id, economy, aiComponent, aiSet, perceptId, perceivers, [new EconomyGoodsPercept(kind, goods ?? EveryGood(economy))])
    {
    }

    private static IReadOnlyList<string> EveryGood(EconomyComponent economy)
    {
        ArgumentNullException.ThrowIfNull(economy);
        return economy.Runtime.Set.Goods.Select(g => g.Id).ToList();
    }

    public string Id { get; }

    /// <summary>How many percepts have been posted.</summary>
    public long Posted => _posted;

    /// <summary>Tells the perceivers again at the next Update, because who perceives has changed or the AI component was reset.</summary>
    public void Notify() => _told.Clear();

    public void Init(in ComponentContext context) => _events = context.Events;

    public void Update(in UpdateContext context)
    {
        if (_events is null) throw new InvalidOperationException("Update before Init");
        var events = _events;
        var runtime = _economy.Runtime;
        var set = runtime.Set;
        foreach (var perceiver in _perceivers())
        {
            if (!set.MarketById.TryGetValue(perceiver.Market, out var market)) continue;   // not a market (any more): there is nothing for it to perceive
            foreach (var (kind, goods) in _goodGroups) Tell(events, context.Tick, perceiver, kind, GoodsAttributes(runtime, market.Index, goods));
            foreach (var (kind, recipes) in _marginGroups) Tell(events, context.Tick, perceiver, kind, MarginAttributes(runtime, market.Index, recipes));
        }
    }

    private void Tell(IEventPort events, long tick, EconomyPerceiver perceiver, string kind, string attrs)
    {
        var key = perceiver.Agent + "|" + kind;
        if (_told.TryGetValue(key, out var last) && last == attrs) return;
        _told[key] = attrs;
        events.Emit(new AiPerceptPosted(_aiComponent, Id + ":" + tick + ":" + perceiver.Agent + ":" + kind, perceiver.Agent, kind, _perceptId, "", attrs));
        _posted++;
    }
}
