using EngineCommon.Balance;
using WorldEventsEngine;

/// <summary>
/// Follows every natural hazard onset and every outbreak of one headless run and reports a magnitude for each when it ends, as whole numbers, so a sweep can hand the offline tail tool a sample of onsets and not only of seeds.
/// Per seed, beside the lists: <c>onset.hazard.&lt;id&gt;.natural</c> natural onsets, <c>.drawn-at-least.&lt;rung&gt;</c> how many of them were drawn at that rung or worse (its <c>positive</c> column in the report is the number of seeds that saw at least
/// one, the plain Monte Carlo count that the rare-event mode estimates). Per hazard onset (a natural one, see <see cref="OriginRule"/>): <c>hazard.&lt;id&gt;.rung</c> the highest rung it reached (its index), <c>.steps</c> how many steps it was going and <c>.size</c> the sum over those steps of the engine's own
/// <see cref="WorldRuntime.Intensity"/> (per mille), the area under the onset-peak-recovery curve. Per outbreak, from the first case in a region to the step no one is exposed or infectious any more (scripted seedings count: the
/// scenario's seeding is how an outbreak begins in these worlds): <c>outbreak.&lt;id&gt;.dead</c> the deaths of that disease in the region meanwhile, <c>.peak</c> the most people exposed or infectious at once, <c>.steps</c> how long it went
/// and <c>.outbroke</c> 1 when the share reached the document's outbreak line and 0 when it stayed a few cases. An onset or outbreak still going at the horizon is cut off there: it reports the magnitude it had reached (a lower
/// bound of its final one) and its <c>.open</c> value is 1 (0 when it ended), and it is counted in <c>.open-at-end</c>; leaving the cut-off ones out would drop exactly the largest events (a flu that fills the valley is still
/// going a year on), so they are in, marked, and a horizon long enough to end most of them is the author's choice. Everything is read from the changes the engine returned and its public state; nothing is added to the engine.
/// </summary>
internal sealed class OnsetTracker
{
    private sealed class Onset { public int Rung; public long Start; public long Size; public long Steps; }

    private sealed class Outbreak { public long Start; public long DeadAtStart; public long Peak; public bool Outbroke; }

    private readonly WorldRuntime _runtime;
    private readonly WorldSet _set;
    private readonly Dictionary<(int Hazard, int Region), Onset> _onsets = [];
    private readonly Dictionary<(int Disease, int Region), Outbreak> _outbreaks = [];
    private readonly SortedDictionary<string, List<long>> _events = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, long> _counts = new(StringComparer.Ordinal);

    public OnsetTracker(WorldRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
        _set = runtime.Set;
        OriginRule.Require(_set);
        foreach (var hazard in _set.Hazards)
        {
            _counts["onset.hazard." + hazard.Id + ".natural"] = 0;
            _counts["onset.hazard." + hazard.Id + ".open-at-end"] = 0;
            foreach (var rung in hazard.Ladder) _counts["onset.hazard." + hazard.Id + ".drawn-at-least." + rung.Id] = 0;
            foreach (var kind in new[] { "rung", "size", "steps", "open" }) _events["hazard." + hazard.Id + "." + kind] = [];
        }
        foreach (var disease in _set.Diseases)
        {
            _counts["onset.outbreak." + disease.Id + ".started"] = 0;
            _counts["onset.outbreak." + disease.Id + ".open-at-end"] = 0;
            foreach (var kind in new[] { "dead", "peak", "steps", "outbroke", "open" }) _events["outbreak." + disease.Id + "." + kind] = [];
        }
    }

    /// <summary>Takes in one lived step: its changes (with how many came from scripted calls) and the state after it.</summary>
    public void Observe(long step, IReadOnlyList<WorldChange> changes, int fromStep)
    {
        for (var i = 0; i < changes.Count; i++)
        {
            var change = changes[i];
            if (change.Kind == ChangeKind.Hazard) ObserveHazard(step, change, OriginRule.IsNaturalOnset(change, i, fromStep));
            else if (change.Kind == ChangeKind.Outbreak) ObserveOutbreak(step, change);
        }
        foreach (var instance in _runtime.ActiveHazards)
            if (_onsets.TryGetValue((instance.Hazard, instance.Region), out var onset))
            {
                onset.Size += _runtime.Intensity(instance);
                onset.Steps++;
            }
        foreach (var ((disease, region), outbreak) in _outbreaks)
        {
            var state = _runtime.Regions[region];
            outbreak.Peak = Math.Max(outbreak.Peak, state.Exposed[disease] + state.Infectious[disease]);
        }
    }

    private void ObserveHazard(long step, WorldChange change, bool natural)
    {
        var key = (change.Index, change.Region);
        var id = _set.Hazards[change.Index].Id;
        switch ((HazardChange)change.Sub2)
        {
            case HazardChange.Started:
                if (!natural) return;
                _onsets[key] = new Onset { Rung = change.Sub, Start = step };
                _counts["onset.hazard." + id + ".natural"]++;
                for (var rung = 0; rung <= change.Sub; rung++) _counts["onset.hazard." + id + ".drawn-at-least." + _set.Hazards[change.Index].Ladder[rung].Id]++;
                return;
            case HazardChange.Escalated:
                if (_onsets.TryGetValue(key, out var escalated)) escalated.Rung = Math.Max(escalated.Rung, change.Sub);
                return;
            case HazardChange.Ended:
                if (!_onsets.Remove(key, out var ended)) return;
                Report(id, ended, 0);
                return;
            case HazardChange.Receding:
                return;
        }
    }

    private void ObserveOutbreak(long step, WorldChange change)
    {
        var key = (change.Index, change.Region);
        var id = _set.Diseases[change.Index].Id;
        switch ((OutbreakChange)change.Sub2)
        {
            case OutbreakChange.Introduced:
                if (_outbreaks.ContainsKey(key)) return;
                _outbreaks[key] = new Outbreak { Start = step, DeadAtStart = _runtime.Regions[change.Region].Dead[change.Index], Peak = change.Amount };
                _counts["onset.outbreak." + id + ".started"]++;
                return;
            case OutbreakChange.Outbreak:
                if (_outbreaks.TryGetValue(key, out var outbroke)) outbroke.Outbroke = true;
                return;
            case OutbreakChange.Ended:
                if (!_outbreaks.Remove(key, out var ended)) return;
                Report(id, change.Region, change.Index, ended, step, 0);
                return;
            case OutbreakChange.Contained:
                return;
        }
    }

    private void Report(string id, Onset onset, long open)
    {
        _events["hazard." + id + ".rung"].Add(onset.Rung);
        _events["hazard." + id + ".steps"].Add(onset.Steps);
        _events["hazard." + id + ".size"].Add(onset.Size);
        _events["hazard." + id + ".open"].Add(open);
    }

    private void Report(string id, int region, int disease, Outbreak outbreak, long step, long open)
    {
        _events["outbreak." + id + ".dead"].Add(_runtime.Regions[region].Dead[disease] - outbreak.DeadAtStart);
        _events["outbreak." + id + ".peak"].Add(outbreak.Peak);
        _events["outbreak." + id + ".steps"].Add(step - outbreak.Start);
        _events["outbreak." + id + ".outbroke"].Add(outbreak.Outbroke ? 1 : 0);
        _events["outbreak." + id + ".open"].Add(open);
    }

    /// <summary>What the run came to: the per-onset values and, as metrics, how many onsets there were and how many were still going at the horizon.</summary>
    public SeedObservation Finish(IDictionary<string, long> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        foreach (var ((hazard, _), onset) in _onsets)
        {
            var id = _set.Hazards[hazard].Id;
            _counts["onset.hazard." + id + ".open-at-end"]++;
            Report(id, onset, 1);
        }
        foreach (var ((disease, region), outbreak) in _outbreaks)
        {
            var id = _set.Diseases[disease].Id;
            _counts["onset.outbreak." + id + ".open-at-end"]++;
            Report(id, region, disease, outbreak, _runtime.Step, 1);
        }
        foreach (var (name, count) in _counts) metrics[name] = count;
        return new SeedObservation(new SortedDictionary<string, long>(metrics, StringComparer.Ordinal), _events.ToDictionary(e => e.Key, e => (IReadOnlyList<long>)e.Value, StringComparer.Ordinal));
    }
}
