using System.Globalization;
using EngineCommon;
using EngineCommon.Balance;
using ScenarioEngine.Contracts;
using WorldEventsEngine;

public static partial class WorldCli
{
    /// <summary>
    /// <c>simulate &lt;world.json&gt; &lt;scenario.json&gt; [&lt;steps&gt;] --rare &lt;hazard&gt;:&lt;rung&gt; [--predraw | --conditional] [--effort N | --efforts a,b,c | --efforts auto [--pilot-effort E] [--pilot-replications P]] [--replications R] [--from-seed S] [--days D] [--rows]</c>: the chance that, within the horizon, a natural onset of the hazard
    /// reaches the rung, by fixed-effort multilevel splitting (<see cref="FixedEffortSplitting"/>) over the scenario, for the rungs a plain seed sweep never reaches. The number printed is in parts per billion with the uncertainty of
    /// <see cref="SplitSummary"/>; <c>--from-seed</c> is the root seed all the derived seeds come from.
    /// </summary>
    private static int SimulateRare(string document, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        RareOptions options;
        try { options = RareOptions.Parse(args.Skip(3).ToList()); }
        catch (EngineRefusedException refusal) { return Wrong(error, "refused " + refusal.Code + ": " + refusal.Message); }
        WorldSet set;
        try { set = WorldLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        var steps = options.Horizon ?? checked(options.Days!.Value * set.StepsPerDay);
        if (steps < 1 || steps > MaxSweepSteps) return Wrong(error, "refused " + BalanceCodes.OptionValue + ": the horizon is 1 to " + MaxSweepSteps.ToString(CultureInfo.InvariantCulture) + " steps, not " + steps.ToString(CultureInfo.InvariantCulture));
        Dictionary<string, FactValue> startFacts;
        List<Call> script;
        try { (startFacts, script) = ReadScenario(File.ReadAllText(args[2])); }
        catch (EngineRefusedException refusal) { return Wrong(error, "scenario problem " + refusal.Code + ": " + refusal.Message); }
        catch (IOException exception) { return Wrong(error, "file problem: " + exception.Message); }
        try
        {
            if (options.Conditional)
            {
                var conditional = ConditionalHazardModel.Create(set, document, startFacts, script, steps, options.Target);
                var estimate = ConditionalEstimator.Run(conditional, options.FromSeed, options.Effort, options.Replications);
                output.Write(ConditionalEstimator.Render(ConditionalHazardModel.Heading(set, options.Target, steps, options.FromSeed), options.Target, estimate, options.Rows));
                return 0;
            }
            if (options.Predraw)
            {
                var predraw = PredrawHazardModel.Create(set, document, startFacts, script, steps, options.Target);
                var predrawn = options.Auto ? FixedEffortSplitting.RunAuto(predraw, options.FromSeed, options.Effort, options.Replications, options.PilotEffort, options.PilotReplications) : options.Efforts is null ? FixedEffortSplitting.Run(predraw, options.FromSeed, options.Effort, options.Replications) : FixedEffortSplitting.Run(predraw, options.FromSeed, options.Efforts, options.Replications);
                output.Write(FixedEffortSplitting.Render(PredrawHazardModel.Heading(set, options.Target, steps, options.FromSeed), options.Target, predrawn, options.Rows));
                return 0;
            }
            var model = RareHazardModel.Create(set, document, startFacts, script, steps, options.Target);
            var report = options.Auto ? FixedEffortSplitting.RunAuto(model, options.FromSeed, options.Effort, options.Replications, options.PilotEffort, options.PilotReplications) : options.Efforts is null ? FixedEffortSplitting.Run(model, options.FromSeed, options.Effort, options.Replications) : FixedEffortSplitting.Run(model, options.FromSeed, options.Efforts, options.Replications);
            output.Write(FixedEffortSplitting.Render(RareHazardModel.Heading(set, options.Target, steps, options.FromSeed), options.Target, report, options.Rows));
            return 0;
        }
        catch (EngineRefusedException refusal) when (refusal.Code.StartsWith("BAL_", StringComparison.Ordinal)) { return Wrong(error, "refused " + refusal.Code + ": " + refusal.Message); }
        catch (EngineRefusedException refusal) { return Wrong(error, "scenario problem " + refusal.Code + ": " + refusal.Message); }
    }
}
