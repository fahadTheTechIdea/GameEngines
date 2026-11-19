using System.Globalization;
using System.Numerics;
using EngineCommon;
using EngineCommon.Balance;
using ScenarioEngine.Contracts;
using WorldEventsEngine;
using Call = WorldCli.Call;

/// <summary>
/// The conditional estimate (<see cref="ConditionalEstimator"/>) of "within the horizon a natural onset of <c>hazard</c> is drawn at the rung or worse" (the event of <see cref="RareHazardModel"/>), from the ladder's tails. A run lives the
/// scenario with the engine's own watcher (<see cref="IHazardWatcher"/>) on the world: at every trial of the target hazard the engine says the chance it is about to use (<c>WorldRuntime.OnsetChance</c> of the state of that moment,
/// the excitation of earlier onsets included, or 0 when the hazard's condition, an exclusion or the director's pacing would veto the onset), and the run adds that chance times the tail of the target rung (the chance that the severity drawn
/// at an onset reaches it). The run stops after the first onset the engine itself drew at the rung or worse, the trial of which counts too. Nothing here knows how a chance is made, so no modifier can drift from the engine; the hazards that
/// <see cref="PredrawHazardModel"/> refuses (a condition, an exclusion, pacing) are handled because the engine reports 0 for a vetoed trial. The event is an onset the world drew itself, as in <see cref="RareHazardModel"/> except that an
/// onset a card effect starts is not one (it is not a trial). The horizon is the scenario's: a trial in its last step counts, and what an onset does after the horizon does not matter, because the rung is drawn at the onset.
/// </summary>
internal sealed class ConditionalHazardModel : IConditionalModel
{
    private readonly string _document;
    private readonly IReadOnlyDictionary<string, FactValue> _startFacts;
    private readonly List<Call> _script;
    private readonly long _steps;
    private readonly int _hazard;
    private readonly int _rung;
    private readonly long _tailPpm;

    private ConditionalHazardModel(string document, IReadOnlyDictionary<string, FactValue> startFacts, List<Call> script, long steps, int hazard, int rung, long tailPpm)
    {
        _document = document;
        _startFacts = startFacts;
        _script = script;
        _steps = steps;
        _hazard = hazard;
        _rung = rung;
        _tailPpm = tailPpm;
    }

    /// <summary>What a run sees of the target hazard's trials: the sum of their chances, and whether the onset that reached the rung has happened.</summary>
    private sealed class Watch(int hazard, int rung) : IHazardWatcher
    {
        public BigInteger Chances;
        public bool Informative;
        public bool Reached;

        public void Trial(long step, int trialHazard, int region, long chancePpm)
        {
            if (trialHazard != hazard || Reached) return;
            Chances += chancePpm;
            if (chancePpm > 0) Informative = true;
        }

        public void Started(long step, int startedHazard, int region, int startedRung)
        {
            if (startedHazard == hazard && startedRung >= rung) Reached = true;
        }
    }

    /// <summary>Reads the target <c>hazard:rung</c> against the document (as every rare-event model does, <see cref="BalanceCodes.RareTarget"/> when it is not there).</summary>
    public static ConditionalHazardModel Create(WorldSet set, string document, IReadOnlyDictionary<string, FactValue> startFacts, List<Call> script, long steps, string target)
    {
        var (hazard, rung) = RareTarget.Read(set, target);
        return new ConditionalHazardModel(document, startFacts, script, steps, hazard.Index, rung.Index, rung.TailPpm);
    }

    public ConditionalOutcome Run(long seed)
    {
        var runtime = WorldRuntime.Create(WorldLoader.Load(SeedDocument.WithSeed(_document, seed)));
        var watch = new Watch(_hazard, _rung);
        runtime.Watch(watch);
        long work = 0;
        WorldCli.Live(runtime, new Dictionary<string, FactValue>(_startFacts, StringComparer.Ordinal), _script, _steps, (_, _, _) => { }, (_, _, _) => work++, 0, 0, () => watch.Reached);
        return new ConditionalOutcome(watch.Chances * _tailPpm, watch.Informative, work);
    }

    /// <summary>The heading text of a conditional report for this model.</summary>
    public static string Heading(WorldSet set, string target, long steps, long rootSeed) => RareHazardModel.Heading(set, target, steps, rootSeed) + " conditional";
}
