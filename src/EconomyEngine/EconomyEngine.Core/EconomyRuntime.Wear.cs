namespace EconomyEngine;

public sealed partial class EconomyRuntime
{
    // Tool wear: a required good that the document says wears out loses `units` units for every `runs` runs of the line that holds it. All whole numbers, no chance. A line keeps the runs it has done towards
    // the next loss (LineState.Wear, saved), so the same runs lose the same units whether they came in one period or in many. The stock is the market's own, so the market can buy a worn tool again by the
    // ordinary demand for it: the loss counts as demand for the good in the period's price, as an input's use does, and a pair's production or trade refills it.

    /// <summary>
    /// How many runs a line can start with a tool before it wears below the amount the recipe requires. A run starts only if the tool is whole at its start, so the line may run until the loss that takes the stock
    /// under the requirement has happened: the stock above the requirement survives <c>spare / units</c> losses, the next one ends the tool, and a loss falls due every wear-runs runs, the first after the runs
    /// still owed of the last. Always at least 1 when the stock holds the requirement.
    /// </summary>
    internal static long RunsBeforeWornOut(RecipeRequirement need, long stock, long progress)
    {
        var wear = need.Wear!;
        var survivable = (stock - need.Amount) / wear.Units;
        return (long)Int128.Min(long.MaxValue, ((Int128)survivable + 1) * wear.Runs - progress);
    }

    /// <summary>The units a tool loses over <paramref name="runs"/> more runs when the line has done <paramref name="progress"/> runs towards its next loss (progress is under the wear's runs).</summary>
    internal static long Lost(WearDef wear, long progress, long runs) => (progress + runs) / wear.Runs * wear.Units;

    /// <summary>The runs done towards the next loss after <paramref name="runs"/> more.</summary>
    internal static long Progress(WearDef wear, long progress, long runs) => (progress + runs) % wear.Runs;

    /// <summary>
    /// A line that starts <paramref name="runs"/> runs holds each required good for the rest of the period and wears it, in the working copy a plan consumes. The stock loses the wear of the runs (never below nothing).
    /// The loss falls first on the units the line itself holds: it keeps holding <c>amount - lost</c> units (nothing once its own tool is worn out), so a tool another line holds is not taken for this line's wear and a
    /// worn-out tool cannot be counted again by a later line.
    /// </summary>
    private void HoldAndWearPlanned(int market, LineDef line, RecipeDef recipe, long runs, long[] stock)
    {
        var state = Markets[market].Lines[Set.LineOf[market][line.Recipe]];
        for (var r = 0; r < recipe.Requires.Count; r++)
        {
            var pair = line.RequirePairs[r];
            var lost = recipe.Requires[r].Wear is { } wear ? Lost(wear, state.Wear[r], runs) : 0;
            stock[pair] = Math.Max(0, stock[pair] - lost);
            _workReserved[pair] += Math.Max(0, recipe.Requires[r].Amount - lost);
        }
    }

    /// <summary>Wears the tools of a line that started <paramref name="runs"/> runs for real: the stock falls (never below nothing), the loss counts as demand for the good, and the runs done are kept.</summary>
    private void WearStarted(LineDef line, RecipeDef recipe, long runs, LineState lineState, MarketState state)
    {
        for (var r = 0; r < recipe.Requires.Count; r++)
        {
            if (recipe.Requires[r].Wear is not { } wear) continue;
            var pair = state.Pairs[line.RequirePairs[r]];
            var lost = Math.Min(pair.Stock, Lost(wear, lineState.Wear[r], runs));
            pair.Stock -= lost;
            pair.SpotDemand = AddSpot(pair.SpotDemand, lost);
            lineState.Wear[r] = Progress(wear, lineState.Wear[r], runs);
        }
    }
}
