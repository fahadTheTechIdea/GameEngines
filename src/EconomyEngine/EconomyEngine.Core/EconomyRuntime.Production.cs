using EngineCommon;

namespace EconomyEngine;

/// <summary>What one production line would do (or did) in one period: how many runs it wanted, how many it can start, and what held it to that.</summary>
/// <param name="Line">The line's position in <see cref="MarketDef.Lines"/>.</param>
/// <param name="Wanted">The runs the line's capacity allows this period (after any arrears).</param>
/// <param name="Runs">The runs it starts.</param>
/// <param name="Limit">What held it to <paramref name="Runs"/>: <see cref="LineLimit.Capacity"/> when it ran at full capacity.</param>
/// <param name="Good">The input that ran short, the output with no room or the required tool that is missing or about to wear out; -1 for capacity, the margin or a locked recipe.</param>
public readonly record struct LinePlan(int Line, long Wanted, long Runs, LineLimit Limit, int Good);

public sealed partial class EconomyRuntime
{
    private readonly List<Batch> _batches = new();
    private bool[] _unlocked = [];
    private long[] _workStock = [];
    private long[] _workIncoming = [];
    private long[] _workReserved = [];
    private LinePlan[] _plans = [];

    /// <summary>The production runs in progress, oldest first.</summary>
    public IReadOnlyList<Batch> Batches => _batches;

    /// <summary>True when the recipe may run: it was never locked, or it has been unlocked.</summary>
    public bool IsUnlocked(int recipe) => _unlocked[recipe];

    /// <summary>
    /// The lines of a market in the order they run, with the plan each would have if the next period closed now. Nothing changes. The host-fact modifiers apply only when <paramref name="hostFacts"/> is given
    /// (the runtime does not keep facts); the shocks that are on always apply.
    /// </summary>
    public IReadOnlyList<LinePlan> PlanLines(int market, IReadOnlyDictionary<string, ScenarioEngine.Contracts.FactValue>? hostFacts = null)
    {
        var lines = Set.LinesInOrder[market];
        var plans = new LinePlan[lines.Length];
        if (lines.Length == 0) return plans;
        var modifiers = ModifiersForPlan(hostFacts);
        var (stock, incoming) = StartPlan(market);
        for (var i = 0; i < lines.Length; i++) plans[i] = PlanLine(market, lines[i], stock, incoming, Period + 1, modifiers);
        return plans;
    }

    /// <summary>The modifiers a plan outside a period uses: the document's for the given facts, or none (every multiplier 1,000) when no facts are given.</summary>
    internal Modifiers ModifiersForPlan(IReadOnlyDictionary<string, ScenarioEngine.Contracts.FactValue>? hostFacts) => hostFacts is null ? new Modifiers(Set.Goods.Count) : ModifiersFor(hostFacts);

    /// <summary>
    /// The stock of each pair of a market, and the units its batches on their way will bring, as a working copy a plan consumes and fills as it goes. A line sees what the lines before it in the order left,
    /// exactly as the period does; the copy keeps the plan from touching the state.
    /// </summary>
    internal (long[] Stock, long[] Incoming) StartPlan(int market)
    {
        var pairs = Markets[market].Pairs;
        if (_workStock.Length < pairs.Length) { _workStock = new long[pairs.Length]; _workIncoming = new long[pairs.Length]; _workReserved = new long[pairs.Length]; }
        for (var i = 0; i < pairs.Length; i++) { _workStock[i] = pairs[i].Stock; _workIncoming[i] = pairs[i].Incoming; _workReserved[i] = 0; }
        return (_workStock, _workIncoming);
    }

    /// <summary>The units of each pair the lines planned so far this period hold as tools or catalysts (see <see cref="Free"/>); reset by <see cref="StartPlan"/>.</summary>
    internal long[] Reserved => _workReserved;

    /// <summary>
    /// What a line planned now may use of a pair: the working stock less what earlier lines of this period hold of it as a required good, never below nothing. A tool wears out of the stock but the holding stays, so a
    /// tool worn below an earlier line's holding leaves nothing for a later one.
    /// </summary>
    internal long Free(long[] stock, int pair) => Math.Max(0, stock[pair] - _workReserved[pair]);

    /// <summary>
    /// One line's turn. A locked recipe does not run, and neither does one whose required goods (tools, catalysts: held, never used up) are not all free (in stock and not already held by a line earlier in the order this period, see <see cref="Free"/>; <see cref="LineLimit.Held"/>, naming the first missing one).
    /// A line that starts runs holds the amount it requires of each such good for the rest of the period, so two lines needing one hammer cannot both count it; a line that starts none holds nothing.
    /// Otherwise the runs are the line's capacity, scaled down by the arrears penalty while the market owes wages and by the line's margin throttle if it has one (<see cref="LineLimit.Margin"/> while the throttle
    /// holds it under its capacity), cut to what a worn tool can still do (<see cref="LineLimit.Worn"/>, see <see cref="RunsBeforeWornOut"/>), to what the inputs allow (the stock divided by what a run takes, for each input) and to what the warehouse has room for (the free space of each output, less what batches on
    /// their way will bring, divided by what a run makes); the tool, input or output that allows the fewest names the limit, the capacity or margin before a tool before an input before an output on a tie. The
    /// inputs are taken from <paramref name="stock"/>, the wear of the tools out of it, and the outputs
    /// (after the supply modifiers and shocks of <paramref name="period"/>, see <see cref="Yield"/>) added to it at once when the recipe takes no time, or to <paramref name="incoming"/> when they come later.
    /// </summary>
    internal LinePlan PlanLine(int market, LineDef line, long[] stock, long[] incoming, long period, Modifiers modifiers, long[]? priceLevels = null)
    {
        var recipe = Set.Recipes[line.Recipe];
        var index = Set.LineOf[market][line.Recipe];
        if (!_unlocked[line.Recipe]) return new LinePlan(index, 0, 0, LineLimit.Locked, -1);
        var requires = recipe.Requires;
        for (var r = 0; r < requires.Count; r++)
            if (Free(stock, line.RequirePairs[r]) < requires[r].Amount) return new LinePlan(index, 0, 0, LineLimit.Held, requires[r].Good);
        var penalty = InArrears(market) ? Set.Price.ArrearsPenalty : 0;
        var wanted = Scale(line.Capacity, EconomyMath.Permille - penalty);
        var limit = LineLimit.Capacity;
        if (line.Throttle is not null)
        {
            var fraction = MarginOf(market, line, period, modifiers, priceLevels).Fraction;
            if (fraction < EconomyMath.Permille) { wanted = Scale(wanted, fraction); limit = LineLimit.Margin; }
        }
        var runs = wanted;
        var good = -1;
        for (var r = 0; r < requires.Count; r++)
        {
            if (requires[r].Wear is null) continue;
            var durable = RunsBeforeWornOut(requires[r], Free(stock, line.RequirePairs[r]), Markets[market].Lines[index].Wear[r]);
            if (durable >= runs) continue;
            runs = durable; limit = LineLimit.Worn; good = requires[r].Good;
        }
        var inputs = recipe.Inputs;
        for (var i = 0; i < inputs.Count; i++)
        {
            var fit = Free(stock, line.InputPairs[i]) / inputs[i].Amount;
            if (fit >= runs) continue;
            runs = fit; limit = LineLimit.Input; good = inputs[i].Good;
        }
        var outputs = recipe.Outputs;
        var pairs = Set.Markets[market].Pairs;
        for (var o = 0; o < outputs.Count; o++)
        {
            var pair = line.OutputPairs[o];
            var room = Math.Max(0, pairs[pair].Capacity - stock[pair] - incoming[pair]);
            var fit = room / outputs[o].Amount;
            if (fit >= runs) continue;
            runs = fit; limit = LineLimit.Room; good = outputs[o].Good;
        }
        if (runs > 0)
        {
            for (var i = 0; i < inputs.Count; i++) stock[line.InputPairs[i]] -= runs * inputs[i].Amount;
            HoldAndWearPlanned(market, line, recipe, runs, stock);
            for (var o = 0; o < outputs.Count; o++)
            {
                var pair = line.OutputPairs[o];
                if (recipe.Duration == 0) stock[pair] = Math.Min(pairs[pair].Capacity, stock[pair] + Yield(market, Set.Goods[outputs[o].Good], runs * outputs[o].Amount, period, modifiers));
                else incoming[pair] += runs * outputs[o].Amount;
            }
        }
        return new LinePlan(index, wanted, runs, limit, good);
    }

    private static long AddSpot(long spot, long units) => Math.Min(EconomyFacts.MaxStock, spot + units);

    /// <summary>
    /// Production batches whose time has come put their outputs into the stock of their market, after the supply modifiers and shocks of this period (what does not fit the warehouse is lost, as for a shipment), and count as supply in this period's price. Runs before
    /// the lines start new ones, so a recipe later in the order can use what an earlier one finished.
    /// </summary>
    private void Deliver(long period, Modifiers modifiers)
    {
        if (_batches.Count == 0) return;
        var kept = 0;
        for (var i = 0; i < _batches.Count; i++)
        {
            var batch = _batches[i];
            if (batch.Completes > period)
            {
                _batches[kept++] = batch;
                continue;
            }
            var recipe = Set.Recipes[batch.Recipe];
            var market = Set.Markets[batch.Market];
            var line = market.Lines[Set.LineOf[batch.Market][batch.Recipe]];
            for (var o = 0; o < recipe.Outputs.Count; o++)
            {
                var units = batch.Runs * recipe.Outputs[o].Amount;
                var pair = Markets[batch.Market].Pairs[line.OutputPairs[o]];
                var before = pair.Stock;
                pair.Stock = Math.Min(market.Pairs[line.OutputPairs[o]].Capacity, pair.Stock + Yield(batch.Market, Set.Goods[recipe.Outputs[o].Good], units, period, modifiers));
                pair.Incoming -= units;
                pair.SpotSupply = AddSpot(pair.SpotSupply, pair.Stock - before);
            }
        }
        _batches.RemoveRange(kept, _batches.Count - kept);
    }

    /// <summary>
    /// Starts the runs of every market's lines for the period. Production never crosses markets, so it goes market by market, and each market's lines run in the order of their recipes (a recipe after those that
    /// make its inputs). The units a run consumes count as demand and the units it makes as supply in the period's price, so the scarcity and the glut of a chain move the prices of its goods.
    /// </summary>
    private void Produce(long period, Modifiers modifiers, long[] priceLevels, EconomyChanges changes)
    {
        for (var m = 0; m < Set.Markets.Count; m++)
        {
            var lines = Set.LinesInOrder[m];
            if (lines.Length == 0) continue;
            if (_plans.Length < lines.Length) _plans = new LinePlan[lines.Length];
            var (stock, incoming) = StartPlan(m);
            for (var i = 0; i < lines.Length; i++) _plans[i] = PlanLine(m, lines[i], stock, incoming, period, modifiers, priceLevels);
            var state = Markets[m];
            for (var i = 0; i < lines.Length; i++) Start(m, lines[i], _plans[i], state, period, modifiers, changes);
        }
    }

    private void Start(int market, LineDef line, LinePlan plan, MarketState state, long period, Modifiers modifiers, EconomyChanges changes)
    {
        var recipe = Set.Recipes[line.Recipe];
        var lineState = state.Lines[plan.Line];
        if (plan.Limit != lineState.Limit || plan.Good != lineState.LimitGood) changes.Limits.Add(new ProductionLimitChanged(market, line.Recipe, lineState.Limit, plan.Limit, plan.Good));
        lineState.Limit = plan.Limit;
        lineState.LimitGood = plan.Good;
        lineState.LastRuns = plan.Runs;
        if (plan.Runs == 0) return;
        for (var i = 0; i < recipe.Inputs.Count; i++)
        {
            var pair = state.Pairs[line.InputPairs[i]];
            var units = plan.Runs * recipe.Inputs[i].Amount;
            pair.Stock -= units;
            pair.SpotDemand = AddSpot(pair.SpotDemand, units);
        }
        WearStarted(line, recipe, plan.Runs, lineState, state);
        if (recipe.Duration == 0)
        {
            for (var o = 0; o < recipe.Outputs.Count; o++)
            {
                var pair = state.Pairs[line.OutputPairs[o]];
                var units = Yield(market, Set.Goods[recipe.Outputs[o].Good], plan.Runs * recipe.Outputs[o].Amount, period, modifiers);
                var before = pair.Stock;
                pair.Stock = Math.Min(Set.Markets[market].Pairs[line.OutputPairs[o]].Capacity, before + units);
                pair.SpotSupply = AddSpot(pair.SpotSupply, pair.Stock - before);
            }
            return;
        }
        for (var o = 0; o < recipe.Outputs.Count; o++) state.Pairs[line.OutputPairs[o]].Incoming += plan.Runs * recipe.Outputs[o].Amount;
        _batches.Add(new Batch(market, line.Recipe, plan.Runs, period + recipe.Duration));
    }

    /// <summary>
    /// Unlocks a recipe a document declared locked (a discovery made elsewhere): its lines may run from the next period. A recipe the document never locked is refused (<see cref="EconomyCodes.RecipeOpen"/>), so a
    /// bridge cannot present an open recipe as a discovery; one already unlocked is left as it is, so hearing of a discovery twice does no harm.
    /// </summary>
    public EconomyChanges UnlockRecipe(string recipeId)
    {
        if (!Set.RecipeById.TryGetValue(CheckId(recipeId, "recipe"), out var recipe)) throw new EngineRefusedException(EconomyCodes.UnknownRecipe, "there is no recipe '" + recipeId + "'");
        if (!recipe.Locked) throw new EngineRefusedException(EconomyCodes.RecipeOpen, "recipe '" + recipeId + "' is not locked: the document lets it run from the start");
        var changes = new EconomyChanges();
        if (_unlocked[recipe.Index]) return changes;
        _unlocked[recipe.Index] = true;
        changes.Unlocks.Add(new RecipeUnlocked(recipe.Index));
        return changes;
    }
}
