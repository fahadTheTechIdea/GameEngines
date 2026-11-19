using ScenarioEngine.Contracts;

namespace EconomyEngine;

/// <summary>
/// A loaded economy document: every declaration plus the digest of the exact text it was read from. It is immutable and holds no price or stock; the runtime holds those. Every query is a pure function of it.
/// </summary>
public sealed class EconomySet
{
    public EconomySet(string id, string digest, ulong seed, int stepsPerDay, int periodSteps, RoutingMode routing, LimitsDef limits, PriceRules price, IReadOnlyList<CurrencyDef> currencies, IReadOnlyList<GoodDef> goods,
        IReadOnlyList<BandDef> bands, IReadOnlyList<ScheduleDef> schedules, IReadOnlyList<PolicyDef> policies, IReadOnlyList<ModifierDef> modifiers, IReadOnlyList<ShockDef> shocks,
        IReadOnlyList<MarketDef> markets, IReadOnlyList<RouteDef> routes, IReadOnlyList<LoanDef> loans, IReadOnlyDictionary<string, FactDefinition> hostFacts,
        IReadOnlyList<RecipeDef> recipes, IReadOnlyList<int> recipeOrder)
    {
        Id = id; Digest = digest; Seed = seed; StepsPerDay = stepsPerDay; PeriodSteps = periodSteps; Routing = routing; Limits = limits; Price = price; Currencies = currencies; Goods = goods; Bands = bands; Schedules = schedules;
        Policies = policies; Modifiers = modifiers; Shocks = shocks; Markets = markets; Routes = routes; Loans = loans; HostFacts = hostFacts; Recipes = recipes; RecipeOrder = recipeOrder;
        RecipeById = recipes.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var positions = new int[recipes.Count];
        for (var i = 0; i < recipeOrder.Count; i++) positions[recipeOrder[i]] = i;
        RecipePosition = positions;
        LineCount = markets.Sum(m => m.Lines.Count);
        LinesInOrder = markets.Select(m => m.Lines.OrderBy(l => positions[l.Recipe]).ToArray()).ToArray();
        var lineSlots = new int[markets.Count][];
        for (var m = 0; m < markets.Count; m++)
        {
            lineSlots[m] = Enumerable.Repeat(-1, recipes.Count).ToArray();
            for (var i = 0; i < markets[m].Lines.Count; i++) lineSlots[m][markets[m].Lines[i].Recipe] = i;
        }
        LineOf = lineSlots;
        CurrencyById = currencies.ToDictionary(c => c.Id, StringComparer.Ordinal);
        GoodById = goods.ToDictionary(g => g.Id, StringComparer.Ordinal);
        BandById = bands.ToDictionary(b => b.Id, StringComparer.Ordinal);
        ScheduleById = schedules.ToDictionary(s => s.Id, StringComparer.Ordinal);
        PolicyById = policies.ToDictionary(p => p.Id, StringComparer.Ordinal);
        ShockById = shocks.ToDictionary(s => s.Id, StringComparer.Ordinal);
        MarketById = markets.ToDictionary(m => m.Id, StringComparer.Ordinal);
        RouteById = routes.ToDictionary(r => r.Id, StringComparer.Ordinal);
        ReferenceCurrency = currencies.First(c => c.Reference).Index;
        PairCount = markets.Sum(m => m.Pairs.Count);
        var slots = new int[markets.Count][];
        for (var m = 0; m < markets.Count; m++)
        {
            slots[m] = Enumerable.Repeat(-1, goods.Count).ToArray();
            foreach (var pair in markets[m].Pairs) slots[m][pair.Good] = pair.Index;
        }
        PairOf = slots;
        var bases = new int[markets.Count];
        for (var m = 1; m < markets.Count; m++) bases[m] = bases[m - 1] + markets[m - 1].Pairs.Count;
        PairBase = bases;
        MarketRank = RanksOf(markets.Select(m => m.Id).ToList());
        GoodRank = RanksOf(goods.Select(g => g.Id).ToList());
        RouteRank = RanksOf(routes.Select(r => r.Id).ToList());
        var leaving = new List<int>[markets.Count];
        for (var m = 0; m < leaving.Length; m++) leaving[m] = new List<int>();
        foreach (var route in routes) leaving[route.From].Add(route.Index);
        RoutesFrom = leaving.Select(l => l.ToArray()).ToArray();
        RoutesByRank = routes.OrderBy(r => RouteRank[r.Index]).Select(r => r.Index).ToArray();
        Facts = EconomyFacts.Definitions(this);
    }

    public string Id { get; }

    /// <summary>The SHA-256 of the exact text of the document.</summary>
    public string Digest { get; }

    public ulong Seed { get; }
    public int StepsPerDay { get; }

    /// <summary>Steps in one period: the unit prices, shipments and settlements move in.</summary>
    public int PeriodSteps { get; }

    /// <summary>How merchants judge a route's destination (document field <c>routing.mode</c>).</summary>
    public RoutingMode Routing { get; }

    public LimitsDef Limits { get; }
    public PriceRules Price { get; }
    public IReadOnlyList<CurrencyDef> Currencies { get; }
    public IReadOnlyList<GoodDef> Goods { get; }
    public IReadOnlyList<BandDef> Bands { get; }
    public IReadOnlyList<ScheduleDef> Schedules { get; }
    public IReadOnlyList<PolicyDef> Policies { get; }
    public IReadOnlyList<ModifierDef> Modifiers { get; }
    public IReadOnlyList<ShockDef> Shocks { get; }
    public IReadOnlyList<MarketDef> Markets { get; }
    public IReadOnlyList<RouteDef> Routes { get; }
    public IReadOnlyList<LoanDef> Loans { get; }

    /// <summary>The production recipes, in document order.</summary>
    public IReadOnlyList<RecipeDef> Recipes { get; }

    /// <summary>The recipe indexes in the order production runs: a recipe after every recipe that makes one of its inputs, ties by recipe id (ordinally).</summary>
    public IReadOnlyList<int> RecipeOrder { get; }

    /// <summary>For a recipe, where it stands in <see cref="RecipeOrder"/>.</summary>
    public IReadOnlyList<int> RecipePosition { get; }

    /// <summary>How many production lines the document has.</summary>
    public int LineCount { get; }

    /// <summary>For a market, its lines in the order they run (by <see cref="RecipePosition"/>).</summary>
    public LineDef[][] LinesInOrder { get; }

    /// <summary>For a market and a recipe, the position of the market's line for it in <see cref="MarketDef.Lines"/>, or -1 when the market has none.</summary>
    public int[][] LineOf { get; }

    /// <summary>The facts the host supplies (the season, a decree), with their types.</summary>
    public IReadOnlyDictionary<string, FactDefinition> HostFacts { get; }

    /// <summary>Every fact a document condition or a dialogue may read: the engine's own and the host's.</summary>
    public IReadOnlyDictionary<string, FactDefinition> Facts { get; }

    public int ReferenceCurrency { get; }

    /// <summary>How many (market, good) pairs the document has.</summary>
    public int PairCount { get; }

    /// <summary>For a market and a good, the index of the pair in the market, or -1 when the market does not trade the good.</summary>
    public int[][] PairOf { get; }

    /// <summary>For a market, the position of its first pair among all pairs of the document (the index of a deterministic roll).</summary>
    public int[] PairBase { get; }

    /// <summary>
    /// Where each market, good and route stands when all of them are sorted by id, ordinally (0 first). Anything that must break a tie, or number graph nodes, uses these and never the position in the
    /// document, so reordering the document changes no result.
    /// </summary>
    public int[] MarketRank { get; }

    public int[] GoodRank { get; }
    public int[] RouteRank { get; }

    /// <summary>The indexes of the routes sorted by id, ordinally: the order in which routes load in a period, so two routes that leave one market share its stock the same way whatever order the document lists them in.</summary>
    public int[] RoutesByRank { get; }

    /// <summary>For a market, the indexes of the routes that leave it, in document order.</summary>
    public int[][] RoutesFrom { get; }

    private static int[] RanksOf(IReadOnlyList<string> ids)
    {
        var order = Enumerable.Range(0, ids.Count).ToArray();
        Array.Sort(order, (a, b) => string.CompareOrdinal(ids[a], ids[b]));
        var ranks = new int[ids.Count];
        for (var i = 0; i < order.Length; i++) ranks[order[i]] = i;
        return ranks;
    }

    public IReadOnlyDictionary<string, CurrencyDef> CurrencyById { get; }
    public IReadOnlyDictionary<string, GoodDef> GoodById { get; }
    public IReadOnlyDictionary<string, BandDef> BandById { get; }
    public IReadOnlyDictionary<string, ScheduleDef> ScheduleById { get; }
    public IReadOnlyDictionary<string, PolicyDef> PolicyById { get; }
    public IReadOnlyDictionary<string, ShockDef> ShockById { get; }
    public IReadOnlyDictionary<string, MarketDef> MarketById { get; }
    public IReadOnlyDictionary<string, RouteDef> RouteById { get; }
    public IReadOnlyDictionary<string, RecipeDef> RecipeById { get; }
}
