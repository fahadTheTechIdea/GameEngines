using System.Globalization;
using EconomyEngine;
using EngineCommon;
using EngineCommon.Balance;

public static partial class EconomyCli
{
    private const long MaxSweepPeriods = 100_000;

    /// <summary>
    /// <c>simulate &lt;economy.json&gt; &lt;scenario.json&gt; [&lt;periods&gt;] --seeds N [--from-seed S] [--days D] [--metrics a,b] [--rows]</c>: the scenario lived once for each seed, headless, and the spread of the named integer metrics reported by the shared
    /// <see cref="SeedSweep"/>. <c>--days</c> must cover whole periods.
    /// </summary>
    private static int SimulateSeeds(string document, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        SweepOptions options;
        try { options = SweepOptions.Parse(args.Skip(3).ToList()); }
        catch (EngineRefusedException refusal) { return Wrong(error, "refused " + refusal.Code + ": " + refusal.Message); }
        EconomySet set;
        try { set = EconomyLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        long periods;
        if (options.Horizon is not null) periods = options.Horizon.Value;
        else
        {
            var steps = checked(options.Days!.Value * set.StepsPerDay);
            if (steps % set.PeriodSteps != 0) return Wrong(error, "refused " + BalanceCodes.DaysRange + ": " + options.Days + " days is " + steps + " steps, which is not a whole number of periods of " + set.PeriodSteps + " steps");
            periods = steps / set.PeriodSteps;
        }
        if (periods < 1 || periods > MaxSweepPeriods) return Wrong(error, "refused " + BalanceCodes.OptionValue + ": the horizon is 1 to " + MaxSweepPeriods.ToString(CultureInfo.InvariantCulture) + " periods, not " + periods.ToString(CultureInfo.InvariantCulture));
        Scenario scenario;
        try { scenario = ReadScenario(File.ReadAllText(args[2])); }
        catch (ScenarioProblem problem) { return Wrong(error, "scenario problem: " + problem.Message); }
        catch (System.Text.Json.JsonException problem) { return Wrong(error, "scenario problem: not JSON: " + problem.Message); }
        catch (IOException exception) { return Wrong(error, "file problem: " + exception.Message); }
        try
        {
            var result = SeedSweep.Run(options, seed => LiveOneSeed(EconomyLoader.Load(SeedDocument.WithSeed(document, seed)), scenario, periods));
            output.Write(SeedSweep.Render($"balance economy {set.Id} digest {set.Digest} seeds {options.Seeds} from {options.FromSeed} horizon {periods} periods", options, result));
            return 0;
        }
        catch (EngineRefusedException refusal) when (refusal.Code.StartsWith("BAL_", StringComparison.Ordinal)) { return Wrong(error, "refused " + refusal.Code + ": " + refusal.Message); }
        catch (EngineRefusedException refusal) { return Wrong(error, "scenario problem " + refusal.Code + ": " + refusal.Message); }
    }

    /// <summary>One independent headless run of the scenario on a loaded document, reduced to named integer metrics. A price ratio is per mille of the reference price; the periods counts are of periods ended in that state.</summary>
    private static SortedDictionary<string, long> LiveOneSeed(EconomySet set, Scenario scenario, long periods)
    {
        var runtime = EconomyRuntime.Create(set);
        var metrics = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var market in set.Markets)
        {
            metrics["arrears.periods." + market.Id] = 0;
            foreach (var pair in market.Pairs)
            {
                var at = market.Id + "." + set.Goods[pair.Good].Id;
                metrics["ratio.min." + at] = long.MaxValue;
                metrics["ratio.max." + at] = long.MinValue;
                metrics["short.periods." + at] = 0;
                foreach (var band in set.Bands) metrics["band." + band.Id + ".periods." + at] = 0;
            }
        }
        foreach (var name in new[] { "arrivals", "bands", "blocks", "launches", "loans", "rates", "settlements", "shocks", "shortages" }) metrics["changes." + name] = 0;
        var refused = Live(runtime, scenario, periods, (_, _) => { }, (_, _) => { },
            (_, changes) =>
            {
                metrics["changes.arrivals"] += changes.Arrivals.Count;
                metrics["changes.bands"] += changes.Bands.Count;
                metrics["changes.blocks"] += changes.Blocks.Count;
                metrics["changes.launches"] += changes.Launches.Count;
                metrics["changes.loans"] += changes.Loans.Count;
                metrics["changes.rates"] += changes.Rates.Count;
                metrics["changes.settlements"] += changes.Settlements.Count;
                metrics["changes.shocks"] += changes.Shocks.Count;
                metrics["changes.shortages"] += changes.Shortages.Count;
                foreach (var market in set.Markets)
                {
                    if (runtime.InArrears(market.Index)) metrics["arrears.periods." + market.Id]++;
                    foreach (var pair in market.Pairs)
                    {
                        var at = market.Id + "." + set.Goods[pair.Good].Id;
                        var ratio = runtime.Ratio(market.Index, pair.Index);
                        var state = runtime.Markets[market.Index].Pairs[pair.Index];
                        metrics["ratio.min." + at] = Math.Min(metrics["ratio.min." + at], ratio);
                        metrics["ratio.max." + at] = Math.Max(metrics["ratio.max." + at], ratio);
                        if (state.Short) metrics["short.periods." + at]++;
                        metrics["band." + set.Bands[state.Band].Id + ".periods." + at]++;
                    }
                }
            });
        metrics["scenario.refused"] = refused;
        foreach (var market in set.Markets)
        {
            metrics["treasury.end." + market.Id] = runtime.Markets[market.Index].Treasury;
            foreach (var pair in market.Pairs) metrics["ratio.end." + market.Id + "." + set.Goods[pair.Good].Id] = runtime.Ratio(market.Index, pair.Index);
        }
        foreach (var currency in set.Currencies)
        {
            metrics["money.end." + currency.Id] = runtime.Currencies[currency.Index].Money;
            metrics["rate.end." + currency.Id] = runtime.Currencies[currency.Index].Rate;
        }
        return metrics;
    }
}
