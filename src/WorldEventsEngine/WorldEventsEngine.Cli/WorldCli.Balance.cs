using System.Globalization;
using EngineCommon;
using EngineCommon.Balance;
using ScenarioEngine.Contracts;
using WorldEventsEngine;

public static partial class WorldCli
{
    private const long MaxSweepSteps = 10_000_000;

    /// <summary>Adds the per-onset magnitudes (<see cref="OnsetTracker"/>) to the sweep: lists of values beside the metrics, and the rows of them with <c>--rows</c>.</summary>
    private const string OnsetsFlag = "--onsets";

    /// <summary>
    /// <c>simulate &lt;world.json&gt; &lt;scenario.json&gt; [&lt;steps&gt;] --seeds N [--from-seed S] [--days D] [--metrics a,b] [--rows]</c>: the same scenario lived once for each seed, headless, and the spread of the named integer metrics reported by the shared
    /// <see cref="SeedSweep"/>. The world is the same document with only its seed replaced, so a sweep that starts at the document's own seed has that seed's run as its first row.
    /// </summary>
    private static int SimulateSeeds(string document, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        SweepOptions options;
        try { options = SweepOptions.Parse(args.Skip(3).ToList(), [OnsetsFlag]); }
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
            var onsets = options.Has(OnsetsFlag);
            var result = SeedSweep.RunWithEvents(options, seed => LiveOneSeed(SeedDocument.WithSeed(document, seed), startFacts, script, steps, onsets));
            output.Write(SeedSweep.Render($"balance world-events {set.Id} digest {set.Digest} seeds {options.Seeds} from {options.FromSeed} horizon {steps} steps{(onsets ? " onsets" : "")}", options, result));
            return 0;
        }
        catch (EngineRefusedException refusal) when (refusal.Code.StartsWith("BAL_", StringComparison.Ordinal)) { return Wrong(error, "refused " + refusal.Code + ": " + refusal.Message); }
        catch (EngineRefusedException refusal) { return Wrong(error, "scenario problem " + refusal.Code + ": " + refusal.Message); }
    }

    /// <summary>One independent headless run of the scenario on a document with its seed already set, reduced to named integer metrics (and, with <paramref name="onsets"/>, the magnitude of each onset).</summary>
    private static SeedObservation LiveOneSeed(string seededDocument, Dictionary<string, FactValue> startFacts, List<Call> script, long steps, bool onsets)
    {
        var set = WorldLoader.Load(seededDocument);
        var runtime = WorldRuntime.Create(set);
        var facts = new Dictionary<string, FactValue>(startFacts, StringComparer.Ordinal);
        var tracker = onsets ? new OnsetTracker(runtime) : null;
        var metrics = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var kind in Enum.GetValues<ChangeKind>()) metrics["changes." + Words.Name(kind)] = 0;
        foreach (var hazard in set.Hazards)
        {
            metrics["hazard." + hazard.Id + ".started"] = 0;
            foreach (var rung in hazard.Ladder) metrics["hazard." + hazard.Id + ".rung." + rung.Id] = 0;
        }
        foreach (var disease in set.Diseases)
        {
            metrics["disease." + disease.Id + ".outbreaks"] = 0;
            metrics["disease." + disease.Id + ".infected-max"] = 0;
        }
        metrics["hazards.concurrent-max"] = 0;
        metrics["stress.max"] = 0;
        var (refused, _) = Live(runtime, facts, script, steps, (_, _, _) => { },
            (step, changes, fromStep) =>
            {
                tracker?.Observe(step, changes, fromStep);
                foreach (var change in changes)
                {
                    metrics["changes." + Words.Name(change.Kind)]++;
                    if (change.Kind == ChangeKind.Hazard && change.Sub2 == (int)HazardChange.Started)
                    {
                        var hazard = set.Hazards[change.Index];
                        metrics["hazard." + hazard.Id + ".started"]++;
                        metrics["hazard." + hazard.Id + ".rung." + hazard.Ladder[change.Sub].Id]++;
                    }
                    else if (change.Kind == ChangeKind.Outbreak && change.Sub2 == (int)OutbreakChange.Outbreak)
                    {
                        metrics["disease." + set.Diseases[change.Index].Id + ".outbreaks"]++;
                    }
                }
                metrics["hazards.concurrent-max"] = Math.Max(metrics["hazards.concurrent-max"], runtime.ActiveHazards.Count);
                metrics["stress.max"] = Math.Max(metrics["stress.max"], runtime.Stress);
                foreach (var disease in set.Diseases)
                {
                    long infected = 0;
                    foreach (var region in runtime.Regions) infected += region.Exposed[disease.Index] + region.Infectious[disease.Index];
                    var name = "disease." + disease.Id + ".infected-max";
                    metrics[name] = Math.Max(metrics[name], infected);
                }
            });
        metrics["stress.end"] = runtime.Stress;
        metrics["director.played"] = runtime.Played;
        metrics["director.held-back"] = runtime.Suppressed;
        metrics["director.dropped"] = runtime.Dropped;
        metrics["scenario.refused"] = refused;
        foreach (var disease in set.Diseases)
        {
            metrics["disease." + disease.Id + ".dead"] = runtime.Regions.Sum(r => r.Dead[disease.Index]);
            metrics["disease." + disease.Id + ".peak-region"] = runtime.Regions.Max(r => r.Peak[disease.Index]);
        }
        return tracker is null ? new SeedObservation(metrics, new Dictionary<string, IReadOnlyList<long>>()) : tracker.Finish(metrics);
    }
}
