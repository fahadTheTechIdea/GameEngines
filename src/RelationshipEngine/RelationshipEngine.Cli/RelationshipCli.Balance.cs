using System.Globalization;
using EngineCommon;
using EngineCommon.Balance;
using RelationshipEngine;

public static partial class RelationshipCli
{
    private const long MaxSweepDays = 3650;

    /// <summary>
    /// <c>simulate &lt;relationship.json&gt; &lt;scenario.json&gt; [&lt;days&gt;] --seeds N [--from-seed S] [--days D] [--metrics a,b] [--rows]</c>: the scenario lived once for each seed, headless, and the spread of the named integer metrics reported by the shared
    /// <see cref="SeedSweep"/>. The seed decides the accept noise, the gossip chances and the balance pushes.
    /// </summary>
    private static int SimulateSeeds(string document, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        SweepOptions options;
        try { options = SweepOptions.Parse(args.Skip(3).ToList()); }
        catch (EngineRefusedException refusal) { return Wrong(error, "refused " + refusal.Code + ": " + refusal.Message); }
        RelationshipSet set;
        try { set = RelationshipLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        var days = options.Horizon ?? options.Days!.Value;
        if (days < 1 || days > MaxSweepDays) return Wrong(error, "refused " + BalanceCodes.OptionValue + ": the horizon is 1 to " + MaxSweepDays.ToString(CultureInfo.InvariantCulture) + " days, not " + days.ToString(CultureInfo.InvariantCulture));
        Scenario scenario;
        try { scenario = ReadScenario(File.ReadAllText(args[2]), set.StepsPerDay); }
        catch (ScenarioProblem problem) { return Wrong(error, "scenario problem: " + problem.Message); }
        catch (System.Text.Json.JsonException problem) { return Wrong(error, "scenario problem: not JSON: " + problem.Message); }
        catch (IOException exception) { return Wrong(error, "file problem: " + exception.Message); }
        try
        {
            var result = SeedSweep.Run(options, seed => LiveOneSeed(RelationshipLoader.Load(SeedDocument.WithSeed(document, seed)), scenario, days));
            output.Write(SeedSweep.Render($"balance relationship {set.Id} digest {set.Digest} seeds {options.Seeds} from {options.FromSeed} horizon {days} days", options, result));
            return 0;
        }
        catch (EngineRefusedException refusal) when (refusal.Code.StartsWith("BAL_", StringComparison.Ordinal)) { return Wrong(error, "refused " + refusal.Code + ": " + refusal.Message); }
        catch (EngineRefusedException refusal) { return Wrong(error, "scenario problem " + refusal.Code + ": " + refusal.Message); }
    }

    /// <summary>One independent headless run of the scenario on a loaded document, reduced to named integer metrics.</summary>
    private static SortedDictionary<string, long> LiveOneSeed(RelationshipSet set, Scenario scenario, long days)
    {
        var runtime = RelationshipRuntime.Create(set);
        foreach (var actor in scenario.Actors) runtime.AddActor(actor.Id, actor.Traits, actor.Tastes);
        var metrics = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var actor in scenario.Actors)
        {
            metrics["critical.steps." + actor.Id] = 0;
            metrics["mood.min." + actor.Id] = long.MaxValue;
        }
        foreach (var name in new[] { "balance", "bonds", "communities", "evictions", "memories", "moodlets", "moods", "needs", "standing", "states" }) metrics["changes." + name] = 0;
        metrics["interactions.accepted"] = 0;
        metrics["interactions.declined"] = 0;
        var refused = Live(runtime, scenario, days * set.StepsPerDay,
            (_, result) => metrics[result.Accepted ? "interactions.accepted" : "interactions.declined"]++,
            (_, _) => { },
            (_, changes) =>
            {
                metrics["changes.balance"] += changes.Balance.Count;
                metrics["changes.bonds"] += changes.Bonds.Count;
                metrics["changes.communities"] += changes.Communities.Count;
                metrics["changes.evictions"] += changes.Evictions.Count;
                metrics["changes.memories"] += changes.Memories.Count;
                metrics["changes.moodlets"] += changes.Moodlets.Count;
                metrics["changes.moods"] += changes.Moods.Count;
                metrics["changes.needs"] += changes.Needs.Count;
                metrics["changes.standing"] += changes.Standing.Count;
                metrics["changes.states"] += changes.States.Count;
                foreach (var actor in scenario.Actors)
                {
                    var state = runtime.Actor(actor.Id);
                    metrics["mood.min." + actor.Id] = Math.Min(metrics["mood.min." + actor.Id], runtime.MoodValue(state));
                    if (state.Critical.Any(c => c)) metrics["critical.steps." + actor.Id]++;
                }
            });
        metrics["scenario.refused"] = refused;
        metrics["rolls"] = runtime.Rolls;
        metrics["edges.total"] = runtime.EdgeCount;
        var standing = scenario.Actors.Select(a => runtime.Actor(a.Id).Standing).ToList();
        metrics["standing.spread"] = standing.Count == 0 ? 0 : standing.Max() - standing.Min();
        metrics["communities.end"] = scenario.Actors.Select(a => runtime.Actor(a.Id).Community).Distinct(StringComparer.Ordinal).Count();
        foreach (var actor in scenario.Actors)
        {
            var state = runtime.Actor(actor.Id);
            metrics["mood.end." + actor.Id] = runtime.MoodValue(state);
            metrics["standing.end." + actor.Id] = state.Standing;
            metrics["edges.end." + actor.Id] = state.Edges.Count;
        }
        return metrics;
    }
}
