using System.Globalization;
using EngineCommon;
using EngineCommon.Balance;
using ScenarioEngine.Contracts;
using WorldEventsEngine;
using Call = WorldCli.Call;

/// <summary>
/// The rare event "within the horizon, a natural onset of <c>hazard</c> reaches at least rung <c>rung</c>" as the levels of a fixed-effort splitting run (<see cref="FixedEffortSplitting"/>). The score of a run is the highest rung
/// plus one of any natural onset of the hazard so far (<see cref="OriginRule"/>), and level k is "score at least k", so a ladder of rungs 0 to R gives R + 1 nested levels: first any onset, then a bad one, up to the rung asked for.
/// A stage runs the scenario step by step (the same loop as every other command) and stops at the first step that ends with the score at the level; the checkpoint is the world at that moment. A later stage branches the checkpoint's
/// world under a derived seed (<see cref="WorldRuntime.Branch"/>: the same state, fresh luck from there on), carries on the scenario from the next scripted call, and stops at its own level. A checkpoint that already stands above the
/// next level (an onset can jump straight past it) reaches it without a run. Scripted calls and card draws cannot raise the score: only the world's own onsets are drawn from the ladder.
/// </summary>
internal sealed class RareHazardModel : ISplittingModel<WorldCheckpoint>
{
    private readonly string _document;
    private readonly IReadOnlyDictionary<string, FactValue> _startFacts;
    private readonly List<Call> _script;
    private readonly long _steps;
    private readonly int _hazard;

    private RareHazardModel(string document, IReadOnlyDictionary<string, FactValue> startFacts, List<Call> script, long steps, int hazard, int stages)
    {
        _document = document;
        _startFacts = startFacts;
        _script = script;
        _steps = steps;
        _hazard = hazard;
        Stages = stages;
    }

    public int Stages { get; }

    /// <summary>Reads the target <c>hazard:rung</c> against the document's hazards and rungs (<see cref="BalanceCodes.RareTarget"/> when either is not there, naming what is) and refuses a hazard whose natural onsets cannot be told from spread ones.</summary>
    public static RareHazardModel Create(WorldSet set, string document, IReadOnlyDictionary<string, FactValue> startFacts, List<Call> script, long steps, string target)
    {
        OriginRule.Require(set);
        var (hazard, rung) = RareTarget.Read(set, target);
        return new RareHazardModel(document, startFacts, script, steps, hazard.Index, rung.Index + 1);
    }

    public StageOutcome<WorldCheckpoint> First(long seed)
    {
        var runtime = WorldRuntime.Create(Seeded(seed));
        return Go(runtime, new Dictionary<string, FactValue>(_startFacts, StringComparer.Ordinal), 0, 0, 0, 1);
    }

    public StageOutcome<WorldCheckpoint> Next(WorldCheckpoint checkpoint, int stage, long seed)
    {
        if (checkpoint.Score >= stage) return new StageOutcome<WorldCheckpoint>(checkpoint);
        var runtime = checkpoint.Runtime.Branch(Seeded(seed));
        return Go(runtime, new Dictionary<string, FactValue>(checkpoint.Facts, StringComparer.Ordinal), runtime.Step + 1, checkpoint.Next, checkpoint.Score, stage);
    }

    private WorldSet Seeded(long seed) => WorldLoader.Load(SeedDocument.WithSeed(_document, seed));

    private StageOutcome<WorldCheckpoint> Go(WorldRuntime runtime, Dictionary<string, FactValue> facts, long from, int next, int startScore, int stage)
    {
        var score = startScore;
        var (_, nextCall) = WorldCli.Live(runtime, facts, _script, _steps, (_, _, _) => { },
            (_, changes, fromStep) =>
            {
                for (var i = 0; i < changes.Count; i++)
                    if (OriginRule.IsNaturalOnset(changes[i], i, fromStep) && changes[i].Index == _hazard) score = Math.Max(score, changes[i].Sub + 1);
            }, from, next, () => score >= stage);
        return score >= stage ? new StageOutcome<WorldCheckpoint>(new WorldCheckpoint(runtime, facts, nextCall, score)) : new StageOutcome<WorldCheckpoint>(null);
    }

    /// <summary>The heading text of a rare-event report for this model.</summary>
    public static string Heading(WorldSet set, string target, long steps, long rootSeed) =>
        $"rare world-events {set.Id} digest {set.Digest} target {target} horizon {steps.ToString(CultureInfo.InvariantCulture)} steps root-seed {rootSeed.ToString(CultureInfo.InvariantCulture)}";
}
