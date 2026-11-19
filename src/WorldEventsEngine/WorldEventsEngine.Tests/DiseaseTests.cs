using System.Text.Json.Nodes;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>Diseases: whole people that add up, the compartment model against an independent floating-point integration of the same equations, the classical results of the theory, and the responses.</summary>
public sealed class DiseaseTests
{
    /// <summary>One region of a million people, no links, a disease of the given kind and numbers.</summary>
    private static WorldRuntime Isolated(string model, int beta, int incubation, int infectious, int waning, int fatality, Action<JsonObject>? more = null, long population = 1_000_000)
    {
        return Fixture.MiniWorld(root =>
        {
            var regions = Tiny.Array(root, "regions");
            regions.RemoveAt(1);
            Tiny.Region(root, "a")["population"] = population;
            Tiny.Array(root, "links").Clear();
            root["start"]!["storyteller"] = "calm";             // no cards: nothing but the disease moves the stress
            Tiny.Array(root, "interventions")[2]!["autoAt"] = 0;      // and no response of its own: the equations are tested bare
            var disease = Tiny.Disease(root);
            disease["model"] = model;
            disease["betaPpm"] = beta;
            disease["incubation"] = incubation;
            disease["infectious"] = infectious;
            disease["waning"] = waning;
            disease["fatality"] = fatality;
            more?.Invoke(root);
        });
    }

    private sealed record Oracle(double S, double E, double I, double R, double Dead);

    /// <summary>The same equations integrated in floating point (Euler, one step at a time): the expected flows the integer model draws from.</summary>
    private static List<Oracle> Integrate(string model, double beta, double incubation, double infectious, double waning, double fatality, double s, double e, double i, double r, int steps, double treat = 1, double betaFactor = 1)
    {
        var series = new List<Oracle> { new(s, e, i, r, 0) };
        double dead = 0;
        for (var step = 0; step < steps; step++)
        {
            var n = s + e + i + r;
            var infections = Math.Min(s, s * i * beta * betaFactor / n);
            var toInfectious = model == "seir" ? e / incubation : 0;
            var leave = i * treat / infectious;
            var die = leave * fatality;
            var wane = waning > 0 ? r / waning : 0;
            s = s - infections + wane;
            if (model == "seir") { e = e + infections - toInfectious; i = i + toInfectious - leave; }
            else i = i + infections - leave;
            r = r + leave - die - wane;
            dead += die;
            series.Add(new Oracle(s, e, i, r, dead));
        }
        return series;
    }

    private static void Seed(WorldRuntime runtime, long cases) => runtime.SeedOutbreak("a", "d", cases);

    [Fact]
    public void TheSeirModelFollowsTheIntegratedEquationsToWithinRounding()
    {
        var runtime = Isolated("seir", 400_000, 2, 4, 0, 100);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        var s0 = runtime.Regions[0].Susceptible[0];
        Seed(runtime, 2000);
        var oracle = Integrate("seir", 0.4, 2, 4, 0, 0.1, s0 - 2000, 2000, 0, 0, 100);
        var checkpoints = new[] { 10, 20, 30, 40, 60, 80, 100 };
        for (var step = 1; step <= 100; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            if (!checkpoints.Contains(step)) continue;
            var o = oracle[step];
            var state = runtime.Regions[0];
            Assert.True(Math.Abs(state.Infectious[0] - o.I) <= 0.02 * o.I + 200, $"step {step}: infectious {state.Infectious[0]} against {o.I:F0}");
            Assert.True(Math.Abs(state.Susceptible[0] - o.S) <= 0.01 * 1_000_000, $"step {step}: susceptible {state.Susceptible[0]} against {o.S:F0}");
            Assert.True(Math.Abs(state.Dead[0] - o.Dead) <= 0.03 * o.Dead + 200, $"step {step}: dead {state.Dead[0]} against {o.Dead:F0}");
        }
    }

    [Fact]
    public void TheSirModelFollowsTheIntegratedEquationsToWithinRounding()
    {
        var runtime = Isolated("sir", 500_000, 0, 4, 0, 0);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        Seed(runtime, 5000);
        var oracle = Integrate("sir", 0.5, 0, 4, 0, 0, 995_000, 0, 5000, 0, 80);
        for (var step = 1; step <= 80; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            if (step % 10 != 0) continue;
            var state = runtime.Regions[0];
            Assert.True(Math.Abs(state.Infectious[0] - oracle[step].I) <= 0.02 * oracle[step].I + 200, $"step {step}: infectious {state.Infectious[0]} against {oracle[step].I:F0}");
            Assert.True(Math.Abs(state.Recovered[0] - oracle[step].R) <= 0.01 * 1_000_000, $"step {step}: recovered {state.Recovered[0]} against {oracle[step].R:F0}");
        }
    }

    [Fact]
    public void TheFinalSizeOfAnEpidemicWithTwiceTheThresholdIsTheClassicalSeventyEightPercent()
    {
        // Kermack and McKendrick: with R0 = 2 and no one immune, the share ever infected solves z = 1 - exp(-R0 z), z = 0.797.
        var runtime = Isolated("sir", 500_000, 0, 4, 0, 0);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        Seed(runtime, 100);
        Fixture.Live(runtime, 400);
        var state = runtime.Regions[0];
        Assert.Equal(0, state.Infectious[0]);
        var share = (double)state.Recovered[0] / 1_000_000;
        Assert.InRange(share, 0.77, 0.83);
    }

    [Fact]
    public void BelowTheThresholdAnOutbreakDiesOutAndEndsWithFewInfected()
    {
        var runtime = Isolated("sir", 150_000, 0, 4, 0, 0);       // R0 = 0.6
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        Seed(runtime, 1000);
        var changes = Fixture.Live(runtime, 150).Of(ChangeKind.Outbreak);
        Assert.Equal(0, runtime.Regions[0].Infectious[0]);
        Assert.Equal((int)OutbreakChange.Ended, changes.Last().Sub2);
        Assert.DoesNotContain(changes, c => c.Sub2 == (int)OutbreakChange.Outbreak);        // never reached the 5% line
        Assert.InRange(runtime.Regions[0].Recovered[0], 1500, 4000);                      // about 1000 / (1 - 0.6) ever infected
    }

    [Fact]
    public void WaningImmunityKeepsADiseaseAlive()
    {
        var runtime = Isolated("sir", 500_000, 0, 4, 20, 0);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        Seed(runtime, 5000);
        var oracle = Integrate("sir", 0.5, 0, 4, 20, 0, 995_000, 0, 5000, 0, 600);
        Fixture.Live(runtime, 600);
        var state = runtime.Regions[0];
        Assert.True(state.Infectious[0] > 0);
        Assert.InRange((double)state.Infectious[0], oracle[600].I * 0.8, oracle[600].I * 1.2);            // the endemic level of the equations
    }

    [Fact]
    public void TheFatalityIsTheShareOfThoseWhoLeaveTheInfectiousWhoDie()
    {
        var runtime = Isolated("sir", 500_000, 0, 4, 0, 100);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        Seed(runtime, 20_000);
        Fixture.Live(runtime, 300);
        var state = runtime.Regions[0];
        Assert.Equal(0, state.Infectious[0]);
        var share = (double)state.Dead[0] / (state.Dead[0] + state.Recovered[0] - 0);
        Assert.InRange(share, 0.095, 0.105);
    }

    [Fact]
    public void PeopleAreNeverCreatedOrLostWhateverTheSeed()
    {
        for (var seed = 1; seed <= 12; seed++)
        {
            var runtime = Fixture.MiniWorld(root =>
            {
                root["seed"] = seed;
                Tiny.Disease(root)["seedPpm"] = 50_000;
                Tiny.Disease(root)["seedCases"] = 3;
                Tiny.Disease(root)["waning"] = 25;
                Tiny.Array(root, "interventions")[2]!["autoAt"] = 30;
            });
            runtime.Intervene("b", "vacc", true);
            for (var step = 0; step < 150; step++)
            {
                runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
                foreach (var region in runtime.Set.Regions)
                {
                    var state = runtime.Regions[region.Index];
                    Assert.Equal(region.Population, state.Susceptible[0] + state.Exposed[0] + state.Infectious[0] + state.Recovered[0] + state.Dead[0]);
                    Assert.True(state.Susceptible[0] >= 0 && state.Exposed[0] >= 0 && state.Infectious[0] >= 0 && state.Recovered[0] >= 0 && state.Dead[0] >= 0);
                }
            }
        }
    }

    [Fact]
    public void AnImmuneShareStartsRecoveredAndTheRestSusceptible()
    {
        var runtime = Fixture.MiniWorld();
        Assert.Equal(1000, runtime.Regions[1].Recovered[0]);
        Assert.Equal(4000, runtime.Regions[1].Susceptible[0]);
        Assert.Equal(0, runtime.Regions[0].Recovered[0]);
        Assert.Equal(10_000, runtime.Regions[0].Susceptible[0]);
    }

    [Fact]
    public void AQuarantineCutsTheTransmissionAndSoTheSizeOfTheEpidemic()
    {
        long Size(bool quarantine)
        {
            var runtime = Isolated("sir", 500_000, 0, 4, 0, 0);
            runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
            if (quarantine) runtime.Intervene("a", "quar", true);         // beta times 0.3: R0 = 0.6
            Seed(runtime, 2000);
            Fixture.Live(runtime, 300);
            return runtime.Regions[0].Recovered[0];
        }
        Assert.True(Size(false) > 700_000);
        Assert.True(Size(true) < 20_000);
    }

    [Fact]
    public void VaccinationMovesTheSusceptibleToTheImmuneAtItsRate()
    {
        var runtime = Isolated("sir", 0, 0, 4, 0, 0);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.Intervene("a", "vacc", true);
        Fixture.Live(runtime, 5);
        var expected = 1_000_000 * Math.Pow(0.9, 5);
        Assert.InRange((double)runtime.Regions[0].Susceptible[0], expected * 0.995, expected * 1.005);
        Assert.Equal(1_000_000, runtime.Regions[0].Susceptible[0] + runtime.Regions[0].Recovered[0]);
    }

    [Fact]
    public void ARegionReachedOnlyThroughALinkIsInfectedThroughItAndNotWithoutIt()
    {
        long InfectedByStepThree(int flow, bool quarantineSource = false)
        {
            var runtime = Fixture.MiniWorld(root =>
            {
                Tiny.Array(root, "links")[0]!["flowPpm"] = flow;
                Tiny.Disease(root)["betaPpm"] = 500_000;
                Tiny.Array(root, "interventions")[2]!["autoAt"] = 0;
                Tiny.Region(root, "b")["immune"] = new JsonArray();
                root["start"]!["storyteller"] = "calm";
            });
            runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
            if (quarantineSource) runtime.Intervene("a", "quar", true);
            Seed(runtime, 100);
            Fixture.Live(runtime, 3);
            return runtime.Regions[1].Exposed[0] + runtime.Regions[1].Infectious[0];
        }
        Assert.Equal(0, InfectedByStepThree(0));                       // nothing crosses a link of no flow
        var open = InfectedByStepThree(300_000);
        var closed = InfectedByStepThree(300_000, true);
        Assert.True(open > 8, "open " + open);                          // a busy link carries the disease over at once
        Assert.True(closed * 3 < open, "open " + open + ", closed " + closed);          // the source's quarantine also cuts the mixing along its links
    }

    [Fact]
    public void AnAutomaticInterventionStartsAtItsLineAndLetsGoAtHalfOfItWithoutFlapping()
    {
        var runtime = Fixture.MiniWorld(root => { Tiny.Array(root, "regions").RemoveAt(1); Tiny.Array(root, "links").Clear(); Tiny.Region(root, "a")["population"] = 100_000; Tiny.Disease(root)["betaPpm"] = 500_000; });
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        Seed(runtime, 500);
        var changes = Fixture.Live(runtime, 300).Of(ChangeKind.Intervention);
        Assert.NotEmpty(changes);
        Assert.All(changes, c => Assert.Equal(1, c.Sub2));                                    // all by itself
        Assert.Equal(1, changes[0].Sub);                                                      // starts first
        for (var i = 1; i < changes.Count; i++) Assert.NotEqual(changes[i].Sub, changes[i - 1].Sub);          // starts and stops alternate
        Assert.True(changes.Count <= 6);
    }

    [Fact]
    public void AnOutbreakIsIntroducedThenDeclaredThenContainedAndEndedAndItsEffectsComeOnce()
    {
        var runtime = Isolated("sir", 500_000, 0, 4, 0, 0);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        var stressBefore = runtime.Stress;
        runtime.SeedOutbreak("a", "d", 100);
        var changes = Fixture.Live(runtime, 300);
        var states = changes.Of(ChangeKind.Outbreak).Select(c => (OutbreakChange)c.Sub2).ToList();
        Assert.Equal([OutbreakChange.Outbreak, OutbreakChange.Contained, OutbreakChange.Ended], states.SkipWhile(s => s == OutbreakChange.Introduced).ToList());
        Assert.Single(changes, c => c.Kind == ChangeKind.Effect && c.Name == "sig.outbreak");
        Assert.True(stressBefore == 0);
    }

    [Fact]
    public void AnOutbreakAddsTheDiseasesStressOnceItIsDeclared()
    {
        var runtime = Isolated("sir", 500_000, 0, 4, 0, 0, root => Tiny.Teller(root, "calm")["decay"] = 0);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.SeedOutbreak("a", "d", 100);
        Assert.Equal(0, runtime.Stress);
        long at = -1;
        for (var step = 1; step < 100 && at < 0; step++)
        {
            var changes = runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            if (changes.Items.Any(c => c.Kind == ChangeKind.Outbreak && c.Sub2 == (int)OutbreakChange.Outbreak)) at = step;
        }
        Assert.True(at > 0);
        Assert.Equal(200, runtime.Stress);
    }

    [Fact]
    public void SeedingAddsCasesAtTheRateWhereTheConditionHoldsAndNowhereElse()
    {
        var always = Fixture.MiniWorld(root => { Tiny.Disease(root)["seedPpm"] = 1_000_000; Tiny.Disease(root)["seedCases"] = 3; });
        always.Advance(0, always.CycleSeasonAt, Fixture.Host());
        Assert.Equal(3, always.Regions[0].Exposed[0]);
        var gated = Fixture.MiniWorld(root => { Tiny.Disease(root)["seedPpm"] = 1_000_000; Tiny.Disease(root)["seedCases"] = 3; Tiny.Disease(root)["when"] = Tiny.Cond("ext.alert", "gt", 5); });
        gated.Advance(0, gated.CycleSeasonAt, Fixture.Host(0));
        Assert.Equal(0, gated.Regions[0].Exposed[0]);
        gated.Advance(1, gated.CycleSeasonAt, Fixture.Host(9));
        Assert.Equal(3, gated.Regions[0].Exposed[0] + gated.Regions[0].Infectious[0]);
    }

    [Fact]
    public void ASeedingThatIsPacedWaitsForTheDirectorToBuild()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            Tiny.Disease(root)["betaPpm"] = 0;                        // only seeding moves the susceptible
            Tiny.Disease(root)["seedPpm"] = 1_000_000;
            Tiny.Disease(root)["seedCases"] = 1;
            Tiny.Disease(root)["paced"] = true;
            root["start"]!["storyteller"] = "calm";
            Tiny.Teller(root, "calm")["peakAt"] = 500;                  // the calm teller peaks too, and draws no cards
            Tiny.Teller(root, "calm")["decay"] = 100;
            Tiny.Teller(root, "calm")["peakHold"] = 2;
            Tiny.Teller(root, "calm")["relaxBelow"] = 200;
            Tiny.Teller(root, "calm")["restMin"] = 3;
        });
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.PlayCard("c-bad", "a");
        runtime.PlayCard("c-bad", "a");
        var held = 0;
        var seeded = 0;
        for (var step = 1; step < 40; step++)
        {
            var before = runtime.Regions[0].Susceptible[0];
            var phase = runtime.Phase;
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            var taken = before - runtime.Regions[0].Susceptible[0];
            if (phase == DirectorPhase.Build) { Assert.Equal(1, taken); seeded++; }
            else { Assert.Equal(0, taken); held++; }
        }
        Assert.True(held > 3);
        Assert.True(seeded > 3);
        Assert.True(runtime.Suppressed >= held);
    }

    [Fact]
    public void ADiseasesBurdenMovesAModifierInProportionToHowManyAreIll()
    {
        var runtime = Isolated("sir", 0, 0, 4, 0, 0);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        Assert.Equal(1000, runtime.Modifier(0, 1));
        runtime.SeedOutbreak("a", "d", 50_000);                         // 50 per mille: half of the 100 per mille at which the burden is full
        Assert.Equal(850, runtime.Modifier(0, 1));
        runtime.SeedOutbreak("a", "d", 200_000);                        // far beyond the line: the burden is full and no more
        Assert.Equal(700, runtime.Modifier(0, 1));
    }

    [Fact]
    public void TheRegionsPopulationFactFallsByTheDead()
    {
        var runtime = Isolated("sir", 500_000, 0, 4, 0, 100, null, 100_000);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        Seed(runtime, 5000);
        Fixture.Live(runtime, 200);
        var dead = runtime.Regions[0].Dead[0];
        Assert.True(dead > 0);
        Assert.Equal(100_000 - dead, runtime.Facts("", "a")["world.a.population"].Integer);
    }

    [Fact]
    public void StochasticRoundingIsUnbiasedAndRepeatable()
    {
        long sum = 0;
        for (var index = 0L; index < 20_000; index++) sum += WorldMath.StochasticDivide(3, 10, 11, "stream", index);
        Assert.InRange((double)sum / 20_000, 0.28, 0.32);
        for (var index = 0L; index < 200; index++) Assert.Equal(WorldMath.StochasticDivide(7, 3, 5, "s", index), WorldMath.StochasticDivide(7, 3, 5, "s", index));
        Assert.All(Enumerable.Range(0, 200), i => Assert.InRange(WorldMath.StochasticDivide(7, 3, 5, "s", i), 2, 3));
        Assert.Equal(4, WorldMath.StochasticDivide(12, 3, 1, "x", 0));              // exact quotients are never rounded
        Assert.Equal(0, WorldMath.StochasticDivide(0, 3, 1, "x", 0));
        Assert.Equal(0, WorldMath.StochasticDivide(5, 0, 1, "x", 0));
    }

    [Fact]
    public void ASeasonalMultiplierScalesTheTransmission()
    {
        long Size(int multiplier)
        {
            var runtime = Isolated("sir", 150_000, 0, 4, 0, 0, root => Tiny.Disease(root)["seasonal"] = new JsonArray(multiplier, multiplier));
            runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
            Seed(runtime, 1000);
            Fixture.Live(runtime, 300);
            return runtime.Regions[0].Recovered[0];
        }
        Assert.True(Size(1000) < 5000);              // R0 0.6: dies out
        Assert.True(Size(4000) > 500_000);           // R0 2.4: sweeps
    }

    [Fact]
    public void ATreatmentThatShortensTheIllnessLowersTheEquationsPeak()
    {
        var runtime = Isolated("sir", 500_000, 0, 4, 0, 0, root => Tiny.Array(root, "interventions")[0]!["treat"] = 2000);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.Intervene("a", "quar", true);
        Seed(runtime, 5000);
        var oracle = Integrate("sir", 0.5, 0, 4, 0, 0, 995_000, 0, 5000, 0, 60, treat: 2, betaFactor: 0.3);
        for (var step = 1; step <= 60; step++) runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
        Assert.True(Math.Abs(runtime.Regions[0].Infectious[0] - oracle[60].I) <= 0.05 * oracle[60].I + 100);
    }
}
