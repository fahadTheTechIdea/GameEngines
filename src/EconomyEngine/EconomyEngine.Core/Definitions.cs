using ScenarioEngine.Contracts;

namespace EconomyEngine;

// The declarations of an economy document, as plain immutable data. Every named thing has an id (stable, language free) and, when a player can see it, a localisation key. Indexes are positions in the document's lists.

/// <summary>A money. The reference currency has anchor 1,000,000 ppm; the others state what one unit is worth in it at the start (<paramref name="Anchor"/>), and the live rate follows purchasing power parity at most <paramref name="MaxRateStepPpm"/> a period.</summary>
public sealed record CurrencyDef(int Index, string Id, string Key, bool Reference, long Anchor, long Money, long GrowthPpm, long MaxRateStepPpm);

/// <summary>A good. Elasticities, speeds and bounds are per mille; <paramref name="Base"/> is its price in thousandths of a minor unit of the reference currency when supply equals demand at a price level of 1.</summary>
public sealed record GoodDef(int Index, string Id, string Key, long Base, int DemandElasticity, int SupplyElasticity, int Floor, int Ceiling, int Speed, int MaxStep, int Inertia, int Decay, long Weight, IReadOnlyList<string> Tags);

/// <summary>A named band of the price ratio (price over its reference, per mille), from <paramref name="From"/> up.</summary>
public sealed record BandDef(int Index, string Id, string Key, long From);

/// <summary>A recurring date for settlements: every <paramref name="Every"/> periods from <paramref name="Offset"/>; a manual schedule fires only when asked (a calendar date, a decree).</summary>
public sealed record ScheduleDef(int Index, string Id, string Key, int Every, int Offset, bool Manual);

public enum PolicyKind { Sales, Import, Export, Subsidy }

/// <summary>A tax, tariff or subsidy. Sales, import and export accrue as a rate on value and are remitted to the treasury on <paramref name="Schedule"/>; a subsidy pays <paramref name="PerUnit"/> thousandths of a minor unit for each unit produced of a tagged good (Schedule is -1).</summary>
public sealed record PolicyDef(int Index, string Id, PolicyKind Kind, int Rate, long PerUnit, IReadOnlyList<string> Tags, int Schedule);

public enum BudgetKind { Wage, Upkeep }

/// <summary>What a treasury pays on a schedule: <paramref name="Amount"/> plus <paramref name="PerHead"/> for each of the market's staff; indexed amounts follow the price level.</summary>
public sealed record BudgetDef(int Index, string Id, BudgetKind Kind, int Schedule, long Amount, long PerHead, bool Indexed);

/// <summary>A good traded in a market: units made and used per period, the stock at the start, the periods of use it keeps in stock, and the most it can hold.</summary>
public sealed record PairDef(int Index, int Good, long Production, long Consumption, long Stock, int Cover, long Capacity);

public sealed record MarketDef(int Index, string Id, string Key, int Currency, long Treasury, long Staff, int Spread, IReadOnlyList<PairDef> Pairs, IReadOnlyList<int> Policies, IReadOnlyList<BudgetDef> Budget, IReadOnlyList<LineDef> Lines);

/// <summary>A one-way route between two markets: shipments take <paramref name="Delay"/> periods and carry at most <paramref name="Capacity"/> units a period; merchants ship when the gain is at least <paramref name="Margin"/> and move <paramref name="Share"/> of the surplus.</summary>
public sealed record RouteDef(int Index, string Id, int From, int To, int Delay, long Capacity, long CostPerWeight, int Margin, int Share);

public enum LoanKind { Annuity, Bullet }

/// <summary>A loan from the world (lender -1) or a market treasury to a market treasury, in one currency, paid on a schedule.</summary>
public sealed record LoanDef(string Id, int Lender, int Borrower, long Principal, long RatePpm, int Term, LoanKind Kind, int Schedule, int DefaultAfter);

public enum EffectKind { Supply, Demand, Price, Rate, Growth, Route }

/// <summary>One thing a shock does. Supply, Demand and Price name goods by id or tag (both empty: every good); Route names a route (-1: every route); Rate and Growth act on the currency of the target market.</summary>
public sealed record EffectDef(EffectKind Kind, int Good, string Tag, int Route, long Amount);

public sealed record ShockDef(int Index, string Id, string Key, int Duration, IReadOnlyList<EffectDef> Effects);

public enum ModifierKind { Supply, Demand }

/// <summary>A seasonal or political multiplier: while the condition over host facts holds, supply or demand of the named goods changes by <paramref name="Permille"/>.</summary>
public sealed record ModifierDef(string Id, ModifierKind Kind, int Good, string Tag, int Permille, Condition When, IReadOnlyList<string> Facts);

/// <summary>The price rules every good shares: the bounds of the response to price, the periods a stock gap is spread over, the margin that must be crossed to leave a band, the noise on demand, and how much of its output a market loses while its wages or upkeep are in arrears.</summary>
public sealed record PriceRules(int ResponseMin, int ResponseMax, int SettlePeriods, int BandMargin, int Noise, int ArrearsPenalty);

/// <summary>How merchants judge a destination: <see cref="Direct"/> by the price of the next market only; <see cref="Landed"/> by the best price they can reach from it through the routes, less everything the goods pay on the way.</summary>
public enum RoutingMode { Direct, Landed }

public sealed record LimitsDef(int MaxShipments, int MaxLoans, int MaxShocks, int History);

/// <summary>A good and a whole number of units: what a recipe consumes or makes in one run.</summary>
public record RecipeAmount(int Good, long Amount);

/// <summary>
/// How a held good wears out: <paramref name="Units"/> units of it are lost for every <paramref name="Runs"/> runs of the line that holds it ("1 hammer in 200 runs"; "3 kiln bricks in 10 runs"). Whole numbers, no
/// chance: a line keeps the runs it has done towards the next loss (saved state), so wearing in small steps loses the same units as wearing in one.
/// </summary>
public sealed record WearDef(long Units, long Runs);

/// <summary>A good a recipe needs held in stock to run (a tool, a catalyst, a kiln): never consumed by a run, but it wears out when the document gives it a <see cref="WearDef"/>.</summary>
public sealed record RecipeRequirement(int Good, long Amount, WearDef? Wear) : RecipeAmount(Good, Amount);

/// <summary>
/// A production recipe: one run consumes <see cref="Inputs"/> from a market's stock and, <see cref="Duration"/> periods later (at once when it is 0), puts <see cref="Outputs"/> into it. A <see cref="Locked"/> recipe does
/// not run until it is unlocked by a request (a discovery); the document only says that it must be, and the unlocked set is state. <see cref="Requires"/> are goods a line must hold in stock to run at all
/// (a tool, a catalyst, a kiln): they are never consumed by a run, so they are not inputs; a tool the document says wears out loses units as the line runs (see <see cref="WearDef"/>).
/// </summary>
public sealed record RecipeDef(int Index, string Id, string Key, IReadOnlyList<RecipeAmount> Inputs, IReadOnlyList<RecipeAmount> Outputs, int Duration, bool Locked, IReadOnlyList<RecipeRequirement> Requires);

/// <summary>
/// A market's production line for a recipe: it starts at most <see cref="Capacity"/> runs a period. <see cref="InputPairs"/>, <see cref="OutputPairs"/> and <see cref="RequirePairs"/> are the pairs of the market that hold
/// the recipe's goods, in the order of the recipe's inputs, outputs and requirements. A line with a <see cref="Throttle"/> runs a fraction of its capacity that follows its margin; without one it never looks at prices.
/// </summary>
public sealed record LineDef(int Index, int Market, int Recipe, long Capacity, IReadOnlyList<int> InputPairs, IReadOnlyList<int> OutputPairs, IReadOnlyList<int> RequirePairs, ThrottleDef? Throttle);

/// <summary>
/// The margin throttle of a line, in per mille of what one run earns over what it costs: at or below <paramref name="Stop"/> the line does not run, at or above <paramref name="Full"/> it runs at its capacity, and in
/// between it runs the fraction that is linear in the margin. Stop is under Full; 1,000 is the break-even point where the outputs are worth exactly what a run costs. Without <paramref name="Costs"/> a run costs
/// its inputs at market prices (the margin of pass 49); with them it also costs the line's own wage and overhead, which is the document's profit margin.
/// </summary>
public sealed record ThrottleDef(int Stop, int Full, LineCostsDef? Costs);

/// <summary>
/// What running a line once costs besides its inputs, declared by the document in thousandths of a minor unit of the market's currency (the unit of a price): <paramref name="Wage"/> for the labour of a run and
/// <paramref name="Overhead"/> for its other variable costs (upkeep of the kiln, rent by the run). Both are variable costs, counted only per run: fixed costs a line pays whether it runs or not do not belong in a
/// decision to run, and the treasury's budget lines already carry the market's. With <paramref name="Indexed"/> both follow the currency's price level like an indexed budget line; otherwise they are nominal.
/// </summary>
public sealed record LineCostsDef(long Wage, long Overhead, bool Indexed);
