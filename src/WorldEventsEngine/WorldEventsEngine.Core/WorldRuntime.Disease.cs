namespace WorldEventsEngine;

public sealed partial class WorldRuntime
{
    /// <summary>What the active interventions do to one disease in one region this step, as per mille (and, for vaccination, the share of the susceptible vaccinated).</summary>
    private readonly record struct Response(long Beta, long Flow, long Vaccinate, long Treat, long Fatality);

    private Response[]? _responses;
    private IReadOnlyList<IReadOnlyList<InterventionDef>>? _byDisease;

    private IReadOnlyList<InterventionDef> InterventionsOf(int disease) =>
        (_byDisease ??= Set.Diseases.Select(d => (IReadOnlyList<InterventionDef>)Set.Interventions.Where(i => i.Disease == d.Index).ToList()).ToList())[disease];

    /// <summary>
    /// The diseases of every region for one step: a compartment model in whole people. Susceptible people become infected at the rate beta times the infectious share of the region and of the regions linked to it (the
    /// force of infection), the exposed become infectious after the incubation period (SEIR) or at once (SIR), the infectious recover or die at the rate of one over the infectious period, and, if immunity wanes, the
    /// recovered become susceptible again (Kermack and McKendrick 1927; the exposed compartment is the usual SEIR extension). A step is one Euler step: every flow is taken from the compartments as they were at the
    /// start of the step, and every flow is a whole number, its fraction rounded up with exactly that probability (<see cref="WorldMath.StochasticDivide"/>), so the expected flow is the model's and the people add up.
    /// </summary>
    private void LiveDiseases(long step, int season, WorldChanges changes)
    {
        var diseases = Set.Diseases.Count;
        if (diseases == 0) return;
        var regions = Set.Regions.Count;
        _responses ??= new Response[diseases * regions];
        for (var d = 0; d < diseases; d++)
            for (var r = 0; r < regions; r++)
            {
                _snapshot[d][r] = Regions[r].Infectious[d];
                _responses[d * regions + r] = Respond(r, d, step, changes);
            }
        for (var r = 0; r < regions; r++)
            for (var d = 0; d < diseases; d++) LiveDisease(r, d, step, season, changes);
    }

    /// <summary>Starts or stops the automatic interventions of a disease in a region by how many are ill, and says what all the active ones do together.</summary>
    private Response Respond(int r, int d, long step, WorldChanges changes)
    {
        var state = Regions[r];
        var living = state.Living(d);
        var prevalence = living == 0 ? 0 : (state.Exposed[d] + state.Infectious[d]) * WorldMath.Permille / living;
        long beta = WorldMath.Permille, flow = WorldMath.Permille, treat = WorldMath.Permille, fatality = WorldMath.Permille, vaccinate = 0;
        foreach (var intervention in InterventionsOf(d))
        {
            var flags = state.Interventions[intervention.Index];
            if (intervention.AutoAt > 0)
            {
                var automatic = (flags & 2) != 0;
                if (!automatic && prevalence >= intervention.AutoAt)
                {
                    flags |= 2;
                    changes.Add(ChangeKind.Intervention, step, r, intervention.Index, 1, 1);
                }
                else if (automatic && prevalence < intervention.AutoAt / 2)
                {
                    flags = (byte)(flags & ~2);
                    changes.Add(ChangeKind.Intervention, step, r, intervention.Index, 0, 1);
                }
                state.Interventions[intervention.Index] = flags;
            }
            if (flags == 0) continue;
            beta = beta * intervention.Beta / WorldMath.Permille;
            flow = flow * intervention.Flow / WorldMath.Permille;
            treat = treat * intervention.Treat / WorldMath.Permille;
            fatality = fatality * intervention.Fatality / WorldMath.Permille;
            vaccinate = Math.Min(WorldMath.Permille, vaccinate + intervention.Vaccinate);
        }
        return new Response(beta, flow, vaccinate, treat, fatality);
    }

    private void LiveDisease(int r, int d, long step, int season, WorldChanges changes)
    {
        var state = Regions[r];
        var def = Set.Diseases[d];
        var regions = Set.Regions.Count;
        long s = state.Susceptible[d], e = state.Exposed[d], i = state.Infectious[d], rec = state.Recovered[d];
        var living = s + e + i + rec;
        if (living == 0) return;
        var stream = _diseaseStreams[r];
        var at = step * DiseaseSlots * 32 + d * DiseaseSlots;
        var response = _responses![d * regions + r];
        var difficulty = Set.Difficulties[_difficulty];

        long infections = 0;
        if (s > 0 && def.BetaPpm > 0)
        {
            // The infectious people that reach this region, in millionths of a person: its own, and each linked region's share, which both regions' travel restrictions cut.
            Int128 reaching = (Int128)i * WorldMath.Ppm;
            for (var edge = Set.Graph.FirstEdge(r); edge < Set.Graph.EndEdge(r); edge++)
            {
                var other = Set.Graph.Target(edge);
                var flowPpm = Set.Graph.Weight(edge);
                var there = _snapshot[d][other];
                if (there == 0 || flowPpm == 0) continue;
                var cut = response.Flow * _responses[d * regions + other].Flow / WorldMath.Permille;
                reaching += (Int128)there * flowPpm * cut / WorldMath.Permille;
            }
            if (reaching > 0)
            {
                var beta = (long)def.BetaPpm * def.Seasonal[season] / WorldMath.Permille * response.Beta / WorldMath.Permille * difficulty.Disease / WorldMath.Permille;
                infections = Math.Min(s, WorldMath.StochasticDivide((Int128)s * reaching * beta, (Int128)living * WorldMath.Ppm * WorldMath.Ppm, Set.Seed, stream, at));
            }
        }
        var becomeInfectious = def.Model == DiseaseModel.Seir && e > 0 ? Math.Min(e, WorldMath.StochasticDivide(e, def.Incubation, Set.Seed, stream, at + 1)) : 0;
        var leave = i > 0 ? Math.Min(i, WorldMath.StochasticDivide((Int128)i * response.Treat, (Int128)def.Infectious * WorldMath.Permille, Set.Seed, stream, at + 2)) : 0;
        var die = leave > 0 && def.Fatality > 0 ? Math.Min(leave, WorldMath.StochasticDivide((Int128)leave * def.Fatality * response.Fatality, (Int128)WorldMath.Permille * WorldMath.Permille, Set.Seed, stream, at + 3)) : 0;
        var wane = def.Waning > 0 && rec > 0 ? Math.Min(rec, WorldMath.StochasticDivide(rec, def.Waning, Set.Seed, stream, at + 4)) : 0;
        var vaccinated = response.Vaccinate > 0 && s > infections ? Math.Min(s - infections, WorldMath.StochasticDivide((Int128)s * response.Vaccinate, WorldMath.Permille, Set.Seed, stream, at + 5)) : 0;

        s = s - infections - vaccinated + wane;
        if (def.Model == DiseaseModel.Seir)
        {
            e = e + infections - becomeInfectious;
            i = i + becomeInfectious - leave;
        }
        else i = i + infections - leave;
        rec = rec + (leave - die) + vaccinated - wane;
        state.Dead[d] += die;

        if (def.SeedPpm > 0 && s > 0 && Roll(stream, at + 6, WorldMath.Ppm) < def.SeedPpm && (def.When is null || Holds(def.When, r)))
        {
            if (def.Paced && _phase != DirectorPhase.Build) _suppressed++;
            else
            {
                var cases = Math.Min(def.SeedCases, s);
                s -= cases;
                if (def.Model == DiseaseModel.Seir) e += cases;
                else i += cases;
            }
        }
        state.Susceptible[d] = s;
        state.Exposed[d] = e;
        state.Infectious[d] = i;
        state.Recovered[d] = rec;
        state.Peak[d] = Math.Max(state.Peak[d], i);
        TrackOutbreak(r, d, step, changes);
    }

    /// <summary>
    /// None, introduced (any case), outbreak (the share of the region exposed or infectious reached the document's threshold) and back: leaving the outbreak needs the share to fall to half the threshold, so a number
    /// hovering at the line does not announce itself over and over. An outbreak adds the disease's stress and asks its effects of the other layers once, when it begins.
    /// </summary>
    private void TrackOutbreak(int r, int d, long step, WorldChanges changes)
    {
        var state = Regions[r];
        var def = Set.Diseases[d];
        var infected = state.Exposed[d] + state.Infectious[d];
        var living = state.Living(d);
        var prevalence = living == 0 ? 0 : infected * WorldMath.Permille / living;
        var before = state.Outbreak[d];
        var now = before;
        if (infected == 0) now = OutbreakState.None;
        else if (before == OutbreakState.None) now = OutbreakState.Introduced;
        else if (before == OutbreakState.Introduced && prevalence >= def.OutbreakAt) now = OutbreakState.Outbreak;
        else if (before == OutbreakState.Outbreak && prevalence < def.OutbreakAt / 2) now = OutbreakState.Introduced;
        if (now == before) return;
        state.Outbreak[d] = now;
        OutbreakChange change;
        if (now == OutbreakState.None) change = OutbreakChange.Ended;
        else if (now == OutbreakState.Outbreak) change = OutbreakChange.Outbreak;
        else if (before == OutbreakState.Outbreak) change = OutbreakChange.Contained;
        else change = OutbreakChange.Introduced;
        changes.Add(ChangeKind.Outbreak, step, r, d, sub2: (int)change, amount: infected, amount2: prevalence);
        if (change != OutbreakChange.Outbreak) return;
        AddStress(def.Stress, true);
        IssueEffects(def.OnOutbreak, r, "disease:" + def.Id, WorldMath.Permille, -1, step, changes);
    }

    /// <summary>Moves up to <paramref name="cases"/> susceptible people of a region into the disease (exposed for SEIR, infectious for SIR). Returns how many moved.</summary>
    private long Introduce(int region, int disease, long cases)
    {
        var state = Regions[region];
        var moved = Math.Min(cases, state.Susceptible[disease]);
        if (moved <= 0) return 0;
        Touch();
        state.Susceptible[disease] -= moved;
        if (Set.Diseases[disease].Model == DiseaseModel.Seir) state.Exposed[disease] += moved;
        else state.Infectious[disease] += moved;
        // The peak is the most infectious people the region ever had at once; people put straight into the infectious state count, or a save taken before the next disease step would be refused (its peak below those infectious).
        state.Peak[disease] = Math.Max(state.Peak[disease], state.Infectious[disease]);
        return moved;
    }
}
