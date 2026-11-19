using EngineCommon;

namespace EconomyEngine;

/// <summary>
/// What a production line earns by running, at the prices of its market. <paramref name="InputCost"/> is what one run's inputs cost to buy at today's prices, <paramref name="Wage"/> and <paramref name="Overhead"/>
/// the line's own cost of a run as the document declares it (0 for a line whose throttle has no costs, and already indexed to the price level when the document says so), and <paramref name="OutputValue"/> what its
/// outputs are worth at today's prices after the supply modifiers and shocks that act on them (thousandths of a minor unit). <paramref name="Ratio"/> is the value over the whole cost (inputs, wage and overhead)
/// in per mille (1,000 is break-even), at most <see cref="EconomyFacts.MaxRatio"/>; <paramref name="Fraction"/> is the share of its capacity the line's throttle lets it run, per mille (1,000 for a line without a throttle).
/// </summary>
public readonly record struct LineMargin(long InputCost, long OutputValue, long Ratio, long Fraction, long Wage, long Overhead);

public sealed partial class EconomyRuntime
{
    /// <summary>
    /// The supply multiplier (per mille) that acts on a good a recipe makes in <paramref name="market"/> during <paramref name="period"/>: the host-fact modifiers and the active shocks, exactly the two factors
    /// that act on a pair's own production in <c>UpdateMarket</c> (the price response and the arrears cut have their own counterparts for a line: the throttle and the arrears cut on its runs).
    /// </summary>
    internal (long Modifier, long Shock) SupplyFactors(int market, GoodDef good, long period, Modifiers modifiers) =>
        (modifiers.Supply[good.Index], Shocked(market, good, EffectKind.Supply, period));

    /// <summary>Units of an output that <paramref name="units"/> made by runs actually join the stock: the same two factors in the same order as a pair's own supply (modifiers, then shocks), each truncated.</summary>
    internal long Yield(int market, GoodDef good, long units, long period, Modifiers modifiers)
    {
        var (modifier, shock) = SupplyFactors(market, good, period, modifiers);
        return Scale(Scale(units, modifier), shock);
    }

    /// <summary>
    /// The margin of a line now. Both sides are valued at the market's own prices (the line sells and buys where it stands, so no currency is involved); the outputs are valued after supply modifiers and shocks, so a
    /// shock that cuts what the line makes cuts its margin and the throttle follows. The cost of a run is its inputs at market prices plus, when the throttle declares <see cref="LineCostsDef"/>, the wage and the
    /// overhead of a run (the document's profit margin). Sales tax is not a term: buyers pay it on top of the price and it reaches the producer through the demand response, so the price already carries it; a subsidy
    /// pays a pair's own production, not what a recipe makes; tariffs are costs of shipping. The throttle fraction is 0 at or under its stop, 1,000 at or over its full, and linear between.
    /// <paramref name="priceLevels"/> are the price levels the market's prices are expressed in (inside a period, before the levels move on); null means the present levels.
    /// </summary>
    internal LineMargin MarginOf(int market, LineDef line, long period, Modifiers modifiers, long[]? priceLevels = null)
    {
        var recipe = Set.Recipes[line.Recipe];
        var state = Markets[market];
        Int128 cost = 0;
        for (var i = 0; i < recipe.Inputs.Count; i++) cost += (Int128)recipe.Inputs[i].Amount * state.Pairs[line.InputPairs[i]].Price;
        Int128 value = 0;
        for (var o = 0; o < recipe.Outputs.Count; o++)
        {
            var good = Set.Goods[recipe.Outputs[o].Good];
            var (modifier, shock) = SupplyFactors(market, good, period, modifiers);
            value += (Int128)recipe.Outputs[o].Amount * state.Pairs[line.OutputPairs[o]].Price * modifier / EconomyMath.Permille * shock / EconomyMath.Permille;
        }
        var inputCost = (long)Int128.Min(long.MaxValue, cost);
        var (wage, overhead) = CostsOf(market, line, priceLevels);
        cost += wage;
        cost += overhead;
        var ratio = (long)Int128.Min(EconomyFacts.MaxRatio, value * EconomyMath.Permille / cost);   // a recipe has an input and a price is at least 1, so the cost is positive
        var outputValue = (long)Int128.Min(long.MaxValue, value);
        return new LineMargin(inputCost, outputValue, ratio, FractionOf(line.Throttle, ratio), wage, overhead);
    }

    /// <summary>The wage and the overhead of one run of a line, as its throttle declares them (none without costs), scaled to the price level the prices are in when the document says they are indexed.</summary>
    private (long Wage, long Overhead) CostsOf(int market, LineDef line, long[]? priceLevels)
    {
        if (line.Throttle?.Costs is not { } costs) return (0, 0);
        if (!costs.Indexed) return (costs.Wage, costs.Overhead);
        var currency = Set.Markets[market].Currency;
        var level = priceLevels is null ? Currencies[currency].Level : priceLevels[currency];
        return (Indexed(costs.Wage, level), Indexed(costs.Overhead, level));
    }

    /// <summary>An amount at a price level (parts per million of the start), rounded to the nearest unit; formed in 128 bits and capped well inside a long, because a level can be ten million times the start.</summary>
    private static long Indexed(long amount, long level) => (long)Int128.Min(long.MaxValue / 4, ((Int128)amount * level + EconomyMath.Ppm / 2) / EconomyMath.Ppm);

    private static long FractionOf(ThrottleDef? throttle, long ratio)
    {
        if (throttle is null || ratio >= throttle.Full) return EconomyMath.Permille;
        if (ratio <= throttle.Stop) return 0;
        return (ratio - throttle.Stop) * EconomyMath.Permille / (throttle.Full - throttle.Stop);
    }

    /// <summary>
    /// The margin of a market's line for a recipe as it stands, with the supply shocks that are on and (when <paramref name="hostFacts"/> is given) the host-fact modifiers; without facts the modifiers are not applied,
    /// and the answer says what a merchant sees in the prices, not what the next period will use. Refused when the market has no line for the recipe.
    /// </summary>
    public LineMargin MarginOfLine(int market, int recipe, IReadOnlyDictionary<string, ScenarioEngine.Contracts.FactValue>? hostFacts = null)
    {
        var slot = Set.LineOf[market][recipe];
        if (slot < 0) throw new EngineRefusedException(EconomyCodes.NoLine, "market '" + Set.Markets[market].Id + "' has no production line for '" + Set.Recipes[recipe].Id + "'");
        return MarginOf(market, Set.Markets[market].Lines[slot], Period + 1, hostFacts is null ? new Modifiers(Set.Goods.Count) : ModifiersFor(hostFacts));
    }
}
