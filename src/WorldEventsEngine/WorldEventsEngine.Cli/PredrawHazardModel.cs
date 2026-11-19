using System.Globalization;
using EngineCommon;
using EngineCommon.Balance;
using ScenarioEngine.Contracts;
using WorldEventsEngine;
using Call = WorldCli.Call;

/// <summary>
/// The rare event of <see cref="RareHazardModel"/> ("within the horizon a natural onset of the hazard is drawn at the rung or worse") as the levels of a fixed-effort splitting run whose checkpoints lie just BEFORE the severity of an onset is
/// drawn, so the rung can be drawn again from them. Level 1 is "a natural onset happens": a stage-1 run goes until the first step with a natural onset of the hazard, and its checkpoint is the world as it stood before that step (found by
/// living the same seed again up to the step before). A checkpoint at level L stands for an onset whose trial passed and whose severity is known to be among the worst <c>Ladder[L - 1].TailPpm</c> parts per million. Stage L + 1 branches the
/// checkpoint under a derived seed with <see cref="WorldRuntime.BranchForcingOnset"/> (the trial passes, the severity is drawn again below that bound by the engine's own code, the hazard's other regions skip the step), lives the step and
/// carries on to the horizon, and stops at the first natural onset of the rung L or worse: at once when the redrawn onset is one (the checkpoint is then the same state, one level higher), otherwise at a later onset, whose own pre-draw state is found by
/// living that branch again up to the step before. The estimate is the usual product of stage fractions.
/// <para>
/// What it is and is not. The restarts do not wait for the world to reach a level and then continue: the level of an onset is a one-draw fact, so the restart redraws it. The stage fractions after the first are therefore about the ladder's
/// own ratios (T_L / T_(L-1)) plus the chance of a later onset, and they cost one step when the redraw already succeeds. Two things are approximations and are stated, not hidden: the hazard's trials in the other regions of the forced step are skipped
/// (the unforced engine would take them, so an onset in two regions in the same step is counted once; it is rare and measured in the document), and escalation of a lasting onset is not a level (as in <see cref="RareHazardModel"/>). A hazard
/// whose onset can be vetoed after its trial passes (a condition, an exclusion, the director's pacing) is refused with <see cref="BalanceCodes.PredrawHazard"/>: a restart could not reproduce the veto. Nothing is caught here; an onset the
/// restart cannot reproduce stops the run with <see cref="BalanceCodes.PredrawLost"/>.
/// </para>
/// </summary>
internal sealed class PredrawHazardModel : ISplittingModel<PredrawCheckpoint>
{
    private readonly string _document;
    private readonly IReadOnlyDictionary<string, FactValue> _startFacts;
    private readonly List<Call> _script;
    private readonly long _steps;
    private readonly HazardDef _hazard;

    /// <summary>What a run watched for while it lived: the first natural onset of the target hazard that reached the rung asked for, and how many steps it lived.</summary>
    private sealed class Watch
    {
        public long Step = -1;
        public int Region = -1;
        public long Work;
    }

    private PredrawHazardModel(string document, IReadOnlyDictionary<string, FactValue> startFacts, List<Call> script, long steps, HazardDef hazard, int stages)
    {
        _document = document;
        _startFacts = startFacts;
        _script = script;
        _steps = steps;
        _hazard = hazard;
        Stages = stages;
    }

    public int Stages { get; }

    /// <summary>Reads the target against the document (as <see cref="RareHazardModel.Create"/> does) and refuses a hazard whose natural onsets cannot be told from spread ones, or whose onset a condition can veto after its trial passes.</summary>
    public static PredrawHazardModel Create(WorldSet set, string document, IReadOnlyDictionary<string, FactValue> startFacts, List<Call> script, long steps, string target)
    {
        OriginRule.Require(set);
        var (hazard, rung) = RareTarget.Read(set, target);
        if (hazard.When is not null || hazard.Excludes.Count > 0 || hazard.Paced)
            throw new EngineRefusedException(BalanceCodes.PredrawHazard, "hazard '" + hazard.Id + "' can have an onset vetoed after its trial passes (a condition, an exclusion or the director's pacing), which a restart from before the draw could not reproduce");
        return new PredrawHazardModel(document, startFacts, script, steps, hazard, rung.Index + 1);
    }

    public StageOutcome<PredrawCheckpoint> First(long seed)
    {
        var watch = new Watch();
        var runtime = WorldRuntime.Create(Seeded(seed));
        var facts = Facts(_startFacts);
        WorldCli.Live(runtime, facts, _script, _steps, (_, _, _) => { }, (step, changes, fromStep) =>
        {
            watch.Work++;
            if (watch.Step >= 0) return;
            for (var i = 0; i < changes.Count; i++)
                if (IsTarget(changes[i], i, fromStep))
                {
                    watch.Step = step;
                    watch.Region = changes[i].Region;
                    return;
                }
        }, 0, 0, () => watch.Step >= 0);
        if (watch.Step < 0) return new StageOutcome<PredrawCheckpoint>(null, watch.Work);
        var before = WorldRuntime.Create(Seeded(seed));
        var beforeFacts = Facts(_startFacts);
        var (_, next) = WorldCli.Live(before, beforeFacts, _script, watch.Step, (_, _, _) => { }, (_, _, _) => { });
        return new StageOutcome<PredrawCheckpoint>(new PredrawCheckpoint(before, beforeFacts, next, watch.Step, watch.Region), watch.Work + watch.Step);
    }

    public StageOutcome<PredrawCheckpoint> Next(PredrawCheckpoint checkpoint, int stage, long seed)
    {
        var level = stage - 1;
        var forced = new ForcedOnset(_hazard.Index, checkpoint.Region, checkpoint.Step, _hazard.Ladder[level - 1].TailPpm);
        var reseeded = Seeded(seed);
        var watch = new Watch();
        var runtime = checkpoint.Before.BranchForcingOnset(reseeded, forced);
        WorldCli.Live(runtime, Facts(checkpoint.Facts), _script, _steps, (_, _, _) => { }, (step, changes, fromStep) =>
        {
            watch.Work++;
            var forcedOnsetSeen = false;
            for (var i = 0; i < changes.Count; i++)
            {
                if (!IsTarget(changes[i], i, fromStep)) continue;
                if (step == checkpoint.Step) forcedOnsetSeen = true;
                if (watch.Step < 0 && changes[i].Sub >= level)
                {
                    watch.Step = step;
                    watch.Region = changes[i].Region;
                }
            }
            if (step == checkpoint.Step && !forcedOnsetSeen)
                throw new EngineRefusedException(BalanceCodes.PredrawLost, "the onset of '" + _hazard.Id + "' forced in step " + step.ToString(CultureInfo.InvariantCulture) + " did not start, so the restart does not reproduce the checkpoint");
        }, checkpoint.Step, checkpoint.Next, () => watch.Step >= 0);
        if (watch.Step < 0) return new StageOutcome<PredrawCheckpoint>(null, watch.Work);
        if (watch.Step == checkpoint.Step) return new StageOutcome<PredrawCheckpoint>(checkpoint, watch.Work);
        // A later onset reached the rung: the checkpoint is the state just before its draw, found by living the same branch (the same seed) up to the step before.
        var before = checkpoint.Before.BranchForcingOnset(reseeded, forced);
        var beforeFacts = Facts(checkpoint.Facts);
        var (_, next) = WorldCli.Live(before, beforeFacts, _script, watch.Step, (_, _, _) => { }, (_, _, _) => { }, checkpoint.Step, checkpoint.Next);
        return new StageOutcome<PredrawCheckpoint>(new PredrawCheckpoint(before, beforeFacts, next, watch.Step, watch.Region), watch.Work + (watch.Step - checkpoint.Step));
    }

    private bool IsTarget(WorldChange change, int index, int fromStep) => OriginRule.IsNaturalOnset(change, index, fromStep) && change.Index == _hazard.Index;

    private static Dictionary<string, FactValue> Facts(IReadOnlyDictionary<string, FactValue> from) => new(from, StringComparer.Ordinal);

    private WorldSet Seeded(long seed) => WorldLoader.Load(SeedDocument.WithSeed(_document, seed));

    /// <summary>The heading text of a rare-event report for this model.</summary>
    public static string Heading(WorldSet set, string target, long steps, long rootSeed) => RareHazardModel.Heading(set, target, steps, rootSeed) + " restart pre-draw";
}
