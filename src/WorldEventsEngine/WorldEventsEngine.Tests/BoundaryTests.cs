using System.Text.Json.Nodes;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>
/// Written from the survivors of the first round of mutation checks (docs/WORLD_EVENTS_ENGINE.md section 10): each test sits on the edge of one comparison, one swapped field or one unscaled number that the
/// first tests walked past.
/// </summary>
public sealed class BoundaryTests
{
    private static void OneRegion(JsonObject root)
    {
        Tiny.Array(root, "regions").RemoveAt(1);
        Tiny.Array(root, "links").Clear();
        root["start"]!["storyteller"] = "calm";
        Tiny.Array(root, "interventions")[2]!["autoAt"] = 0;
    }

    // ---- arithmetic and climate ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheSaturationOfAFactIsTheShareModelsLimitAndNotMore()
    {
        Assert.Equal(WorldFacts.Saturate, WorldMath.Saturated(WorldFacts.Saturate + 5));
        Assert.Equal(700_000_000, WorldMath.Saturated(700_000_000));
        Assert.Equal(0, WorldMath.Saturated(-3));
    }

    [Fact]
    public void TheBellRunsFromMinusToPlusTheSameAndIsCutFromFourSixteenBitParts()
    {
        Assert.Equal(-1998, WorldMath.Bell(0));
        Assert.Equal(1998, WorldMath.Bell(ulong.MaxValue));
        Assert.Equal(-1998 + 999, WorldMath.Bell(0xFFFF));                    // only the lowest part full: 65,535 of 65,536 of a thousand is 999
        Assert.Equal(-1998 + 999, WorldMath.Bell(0xFFFFUL << 48));            // and the highest part counts the same
        Assert.Equal(0, WorldMath.Noise(1234, 0));
        Assert.Equal(1998 * 577 / 577, WorldMath.Noise(ulong.MaxValue, 577));
    }

    [Fact]
    public void TheLongRunSpreadOfTheTemperatureIsSigmaEvenWhenTheCarryOverIsStrong()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            foreach (var season in Tiny.Zone(root)["seasons"]!.AsArray())
            {
                season!["phi"] = 900;
                season["sigma"] = 300;
                season["temp"] = 1000;
            }
        });
        var temps = new List<double>();
        for (var step = 0; step < 40_000; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            temps.Add(runtime.Regions[0].Anomaly);
        }
        var mean = temps.Average();
        var spread = Math.Sqrt(temps.Sum(t => (t - mean) * (t - mean)) / temps.Count);
        Assert.InRange(spread, 250, 350);                     // the innovation is scaled by the square root of one minus phi squared; without it the spread would be 690
    }

    [Fact]
    public void TheWindIsTheZonesMeanPlusTheWeathersOwn()
    {
        var runtime = Fixture.MiniWorld();
        long sum = 0;
        for (var step = 0; step < 20_000; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            sum += runtime.Regions[0].Wind;
        }
        Assert.InRange((double)sum / 20_000, 52, 58);          // 30 and, in the wet half of the time, 50 more
    }

    [Fact]
    public void ACallMayLiveExactlyTheMostStepsAndNotOneMore()
    {
        var runtime = Fixture.MiniWorld();
        runtime.Advance(WorldRuntime.MaxAdvance - 1, runtime.CycleSeasonAt, Fixture.Host());
        Assert.Equal(WorldRuntime.MaxAdvance - 1, runtime.Step);
        var over = Fixture.MiniWorld();
        Assert.Equal(WorldCodes.StepOrder, Fixture.Refusal(() => over.Advance(WorldRuntime.MaxAdvance, over.CycleSeasonAt, Fixture.Host())));
    }

    [Fact]
    public void AHostFactAtTheEdgeOfItsRangeIsAccepted()
    {
        var runtime = Fixture.MiniWorld();
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host(10));
        runtime.Advance(1, runtime.CycleSeasonAt, Fixture.Host(0));
        Assert.Equal(1, runtime.Step);
    }

    [Fact]
    public void ARegimeChangeIsAlwaysToADifferentRegime()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            Tiny.Array(root, "regimes")[0]!["persistence"] = 200;
            var second = Tiny.Copy(Tiny.Array(root, "regimes")[0]!).AsObject();
            second["id"] = "r2";
            second["key"] = "r.r2";
            Tiny.Array(root, "regimes").Add(second);
        });
        var changes = Fixture.Live(runtime, 600).Of(ChangeKind.Regime);
        Assert.NotEmpty(changes);
        Assert.All(changes, c => Assert.NotEqual(c.Index, c.Sub));
    }

    // ---- diseases --------------------------------------------------------------------------------------------------------------------

    private static WorldRuntime Held(int seeds, Action<JsonObject>? more = null)
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            OneRegion(root);
            Tiny.Region(root, "a")["population"] = 10_000;
            var disease = Tiny.Disease(root);
            disease["incubation"] = 3660;
            disease["infectious"] = 3660;
            disease["betaPpm"] = 0;
            more?.Invoke(root);
        });
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.SeedOutbreak("a", "d", seeds);
        return runtime;
    }

    [Fact]
    public void AnAutomaticInterventionStartsAtExactlyItsLineAndAnOutbreakIsDeclaredAtExactlyTheThreshold()
    {
        var auto = Held(1000, root => Tiny.Array(root, "interventions")[2]!["autoAt"] = 100);          // 1,000 of 10,000 exposed: exactly 100 per mille
        var changes = Fixture.Live(auto, 1).Of(ChangeKind.Intervention);
        Assert.Single(changes, c => c.Sub == 1 && c.Sub2 == 1);
        var outbreak = Held(500);                                                                         // 50 per mille: exactly the line of 50
        Assert.Contains(Fixture.Live(outbreak, 1).Of(ChangeKind.Outbreak), c => c.Sub2 == (int)OutbreakChange.Outbreak);
    }

    private static string Doc(Action<JsonObject> edit)
    {
        var root = Tiny.Root();
        edit(root);
        return root.ToJsonString();
    }

    [Fact]
    public void ADifficultyScalesTheTransmissionBySeasonAndTheSeasonsOwnMultiplierIsTheOneUsed()
    {
        var hard = Held(2000, root => { Tiny.Disease(root)["betaPpm"] = 200_000; Tiny.Disease(root)["incubation"] = 1; Tiny.Disease(root)["infectious"] = 4; Tiny.Disease(root)["model"] = "sir"; Tiny.Disease(root)["incubation"] = 0; });
        var normal = Held(2000, root => { Tiny.Disease(root)["betaPpm"] = 200_000; Tiny.Disease(root)["incubation"] = 0; Tiny.Disease(root)["infectious"] = 4; Tiny.Disease(root)["model"] = "sir"; });
        hard.UseDifficulty("hard");
        Fixture.Live(hard, 6);
        Fixture.Live(normal, 6);
        // The hard difficulty doubles the transmission: more were infected than a run at the ordinary one.
        Assert.True(hard.Regions[0].Susceptible[0] < normal.Regions[0].Susceptible[0] - 200, hard.Regions[0].Susceptible[0] + " against " + normal.Regions[0].Susceptible[0]);
        var winter = Held(2000, root => { Tiny.Disease(root)["betaPpm"] = 300_000; Tiny.Disease(root)["incubation"] = 0; Tiny.Disease(root)["infectious"] = 4; Tiny.Disease(root)["model"] = "sir"; Tiny.Disease(root)["seasonal"] = new JsonArray(0, 1000); });
        var before = winter.Regions[0].Susceptible[0];
        for (var step = 1; step <= 9; step++) winter.Advance(step, winter.CycleSeasonAt, Fixture.Host());        // steps 1 to 9 are in the first season: a multiplier of nothing
        Assert.Equal(before, winter.Regions[0].Susceptible[0]);
        for (var step = 10; step <= 14; step++) winter.Advance(step, winter.CycleSeasonAt, Fixture.Host());       // and 10 on are in the second
        Assert.True(winter.Regions[0].Susceptible[0] < before);
    }

    [Fact]
    public void AnInterventionsTransmissionTravelTreatmentAndDeathFactorsAreEachItsOwn()
    {
        // Beta, treatment and death: an integration of the same equations with the intervention's own numbers.
        var runtime = Fixture.MiniWorld(root =>
        {
            OneRegion(root);
            Tiny.Region(root, "a")["population"] = 1_000_000;
            var d = Tiny.Disease(root);
            d["model"] = "sir"; d["incubation"] = 0; d["betaPpm"] = 500_000; d["infectious"] = 4; d["fatality"] = 100;
            var quar = Tiny.Array(root, "interventions")[0]!;
            quar["betaPermille"] = 600; quar["flowPermille"] = 0; quar["treat"] = 2000; quar["fatalityPermille"] = 500;
        });
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.Intervene("a", "quar", true);
        runtime.SeedOutbreak("a", "d", 5000);
        double s = 995_000, i = 5000, r = 0, dead = 0;
        for (var step = 1; step <= 40; step++)
        {
            var n = s + i + r;
            var infections = s * i * 0.5 * 0.6 / n;
            var leave = i * 2.0 / 4;
            var die = leave * 0.1 * 0.5;
            s -= infections; i += infections - leave; r += leave - die; dead += die;
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
        }
        var state = runtime.Regions[0];
        Assert.True(Math.Abs(state.Infectious[0] - i) <= 0.03 * i + 200, "infectious " + state.Infectious[0] + " against " + i);
        Assert.True(Math.Abs(state.Dead[0] - dead) <= 0.05 * dead + 100, "dead " + state.Dead[0] + " against " + dead);
    }

    [Fact]
    public void AnInterventionsTravelFactorCutsTheMixingAlongLinksAndNotTheTransmissionInsideTheRegion()
    {
        long InB(int flowPermille)
        {
            var runtime = Fixture.MiniWorld(root =>
            {
                root["start"]!["storyteller"] = "calm";
                Tiny.Array(root, "interventions")[2]!["autoAt"] = 0;
                Tiny.Array(root, "links")[0]!["flowPpm"] = 300_000;
                Tiny.Disease(root)["betaPpm"] = 500_000;
                Tiny.Region(root, "b")["immune"] = new JsonArray();
                var quar = Tiny.Array(root, "interventions")[0]!;
                quar["betaPermille"] = 1000; quar["flowPermille"] = flowPermille;
            });
            runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
            runtime.Intervene("a", "quar", true);
            runtime.SeedOutbreak("a", "d", 200);
            Fixture.Live(runtime, 12);
            return runtime.Regions[1].Exposed[0] + runtime.Regions[1].Infectious[0] + runtime.Regions[1].Recovered[0];
        }
        Assert.Equal(0, InB(0));
        Assert.True(InB(1000) > 50);
    }

    // ---- hazards and the director ----------------------------------------------------------------------------------------------------

    [Fact]
    public void OnlyTheOriginOfAnOnsetAddsStressAndNotEachRegionItReaches()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            var c = Tiny.Copy(Tiny.Region(root, "a")).AsObject();
            c["id"] = "c"; c["key"] = "g.c";
            Tiny.Array(root, "regions").Add(c);
            Tiny.Array(root, "links").Add(new JsonObject { ["a"] = "b", ["b"] = "c", ["flowPpm"] = 1 });
            Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 2, ["falloff"] = 500 };
            Tiny.Teller(root, "t")["decay"] = 0;
        });
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.BeginHazard("a", "h", "low");
        Assert.Equal(3, runtime.ActiveHazards.Count);
        Assert.Equal(100, runtime.Stress);
    }

    [Fact]
    public void ARungWithNoRecoveryEndsStraightAfterItsPeakAndNeverRecedes()
    {
        var runtime = Fixture.MiniWorld(root => Tiny.Rung(root, 0)["recoverySteps"] = 0);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.BeginHazard("a", "h", "low");
        var changes = Fixture.Live(runtime, 12).Of(ChangeKind.Hazard).Where(c => c.Region == 0).ToList();
        Assert.DoesNotContain(changes, c => c.Sub2 == (int)HazardChange.Receding);
        Assert.Single(changes, c => c.Sub2 == (int)HazardChange.Ended);
    }

    [Fact]
    public void ACooldownOfNSteps_RestsExactlyThatManyStepsAfterTheEnd()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            Tiny.Certain(root);
            Tiny.Hazard(root)["cooldown"] = 4;
            Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 0, ["falloff"] = 0 };
            Tiny.Hazard(root)["ladder"] = new JsonArray(new JsonObject
            {
                ["id"] = "only", ["key"] = "r.only", ["tailPpm"] = 1_000_000, ["minDuration"] = 1, ["maxDuration"] = 1, ["onsetSteps"] = 0, ["recoverySteps"] = 0, ["stress"] = 0, ["escalatePpm"] = 0, ["effects"] = new JsonArray(),
            });
            Tiny.Region(root, "b")["exposure"] = new JsonArray();
        });
        var starts = Fixture.Live(runtime, 40).Of(ChangeKind.Hazard).Where(c => c.Sub2 == (int)HazardChange.Started).Select(c => c.Step).ToList();
        // Started at 0, lives steps 0 and 1 (peak of one step, then over at step 1), rests: ready at 1 + 1 + 4 = 6.
        Assert.Equal([0L, 6, 12, 18, 24, 30, 36], starts);
    }

    private static WorldRuntime Director(Action<JsonObject> edit)
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            root["start"]!["storyteller"] = "calm";
            edit(root);
        });
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        return runtime;
    }

    [Fact]
    public void TheDirectorPeaksAtExactlyItsLineAndRestsMoreThanItsMinimum()
    {
        var runtime = Director(root =>
        {
            var calm = Tiny.Teller(root, "calm");
            calm["peakAt"] = 300; calm["relaxBelow"] = 299; calm["decay"] = 500; calm["peakHold"] = 1; calm["restMin"] = 5;
        });
        runtime.PlayCard("c-bad", "a");
        runtime.PlayCard("c-bad", "a");                      // 600
        var phases = new List<(long Step, DirectorPhase To)>();
        for (var step = 1; step <= 12; step++)
            foreach (var c in runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host()).Items.Where(c => c.Kind == ChangeKind.Director)) phases.Add((c.Step, (DirectorPhase)c.Index));
        // Step 1: 600 decays to 300, which is the line: Peak. Held one step: Relax at 2. Rest more than five steps: elapsed at step s is s - 2 + 1 > 5, so Build at 7.
        Assert.Equal([(1L, DirectorPhase.Peak), (2L, DirectorPhase.Relax), (7L, DirectorPhase.Build)], phases);
    }

    private static List<long> DrawSteps(Action<JsonObject> edit, int steps = 40)
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            root["start"]!["storyteller"] = "calm";
            foreach (var card in Tiny.Array(root, "cards")) card!["weight"] = card["id"]!.GetValue<string>() == "c-global" ? 100 : 0;
            Tiny.Card(root, "c-global")["effects"] = new JsonArray();
            Tiny.Teller(root, "calm")["decay"] = 1000;
            Tiny.Teller(root, "calm")["peakAt"] = 1001;
            edit(root);
        });
        return Fixture.Live(runtime, steps).Of(ChangeKind.Card).Select(c => c.Step).ToList();
    }

    [Fact]
    public void CardsComeAtTheExactStepsTheGapTheCooldownAndTheGroupAllow()
    {
        // A gap of exactly four steps: the first card when four steps have passed since the start (-1), then every four.
        Assert.Equal([3L, 7, 11, 15, 19], DrawSteps(r => { Tiny.Teller(r, "calm")["gapMin"] = 4; Tiny.Teller(r, "calm")["gapMax"] = 4; Tiny.Card(r, "c-global")["cooldown"] = 0; }, 20));
        // A card that rests five steps after it is played, drawn as soon as it may be.
        Assert.Equal([0L, 6, 12, 18], DrawSteps(r => { Tiny.Teller(r, "calm")["gapMin"] = 0; Tiny.Teller(r, "calm")["gapMax"] = 1; Tiny.Card(r, "c-global")["cooldown"] = 5; }, 20));
        // A card whose group is held three steps: allowed again on the third.
        Assert.Equal([0L, 3, 6, 9], DrawSteps(r =>
        {
            Tiny.Teller(r, "calm")["gapMin"] = 0; Tiny.Teller(r, "calm")["gapMax"] = 1;
            Tiny.Card(r, "c-global")["cooldown"] = 0; Tiny.Card(r, "c-global")["group"] = "held"; Tiny.Card(r, "c-global")["duration"] = 3;
        }, 11));
    }

    [Fact]
    public void ADeckOfOneCardIsDrawnFrom()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            Tiny.Array(root, "cards").Clear();
            var only = JsonNode.Parse("""{ "id": "solo", "key": "c.solo", "tier": "neutral", "scope": "global", "weight": 10, "cooldown": 0, "group": "", "duration": 0, "intensity": 0, "effects": [], "chain": [] }""")!;
            Tiny.Array(root, "cards").Add(only);
        });
        Assert.NotEmpty(Fixture.Live(runtime, 60).Of(ChangeKind.Card));
    }

    [Fact]
    public void ADifficultyOfNoBadCardsDrawsNoBadCardAndItsOtherFactorsAreNotMixedWithIt()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            var hard = root["difficulties"]!.AsArray()[1]!;
            hard["bad"] = 0; hard["hazard"] = 3000;
        });
        runtime.UseDifficulty("hard");
        var played = Fixture.Live(runtime, 1500).Of(ChangeKind.Card);
        Assert.True(played.Count > 100);
        Assert.DoesNotContain(played, c => runtime.Set.Cards[c.Index].Tier == Tier.Bad);
    }

    [Fact]
    public void ACardWithADurationButNoGroupHoldsNothingAndTheEffectsKeepTheirSmallestAmounts()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            var timed = Tiny.Card(root, "c-global");
            timed["duration"] = 3;                                                              // no group
            timed["effects"] = new JsonArray(new JsonObject { ["shock"] = "sh1", ["scale"] = 1 }, new JsonObject { ["outbreak"] = "d", ["cases"] = 1 }) ;
            timed["scope"] = "region";
        });
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        var changes = runtime.PlayCard("c-global", "a").Items;
        Assert.Contains(changes, c => c.Kind == ChangeKind.Effect && c.Name == "sh1" && c.Amount == 1);      // a shock of one per mille is still asked
        Assert.Equal(1, runtime.Regions[0].Exposed[0]);                                                      // one case is one case
        Assert.All(runtime.GroupLockedUntil, until => Assert.Equal(0, until));
    }

    [Fact]
    public void AChainedCardInTheFirstRegionIsPlayedThereAndNotWhereverItsConditionHolds()
    {
        var runtime = Fixture.MiniWorld(root => root["start"]!["storyteller"] = "calm");
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.PlayCard("c-bad", "a");
        var after = Fixture.Live(runtime, 4).Of(ChangeKind.Card).Single(c => runtime.Set.Cards[c.Index].Id == "c-after");
        Assert.Equal(0, after.Region);
    }

    // ---- saves -----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheEdgesOfEveryRangeOfTheStateAreTheEdgesAndNotOneMore()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = WorldSave.Write(runtime);
        void Refused(Action<WorldImage> edit, string what) => Assert.True(WorldCodes.SaveState == Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, edit)), what), what);
        Refused(i => i.Season = -2, "a season below the first step's none");
        Refused(i => i.LastCard = -2, "a last card before never");
        Refused(i => i.Regions[0].SoilTenths = set.Hydrology.SoilCapacity + 1, "more water than the bucket holds by one");
        Refused(i => i.Regions[0].Susceptible[0] = WorldFacts.Saturate + 1, "a compartment one beyond the saturation");
    }

    [Fact]
    public void TheListsOfTheStateAreBoundedByTheLimitsOfTheDocumentExactly()
    {
        var set = Fixture.Mini(root =>
        {
            Tiny.Certain(root);
            root["limits"]!["maxHazards"] = 1; root["limits"]!["maxEffects"] = 1; root["limits"]!["maxAgenda"] = 1;
            Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 0, ["falloff"] = 0 };
            Tiny.Region(root, "b")["exposure"] = new JsonArray();
            Tiny.Teller(root, "t")["decay"] = 0;
        });
        var runtime = WorldRuntime.Create(set);
        Fixture.Live(runtime, 1);
        runtime.PlayCard("c-bad", "a");
        var bytes = WorldSave.Write(runtime);
        var image = Tamper.Image(bytes);
        Assert.Single(image.Hazards);
        Assert.Single(image.Timed);
        Assert.Single(image.Agenda);
        WorldSave.Open(set, bytes);
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, i => i.Hazards.Add(new ImageHazard { Hazard = 0, Region = 1, Rung = i.Hazards[0].Rung, Phase = i.Hazards[0].Phase, PhaseStart = i.Hazards[0].PhaseStart, PhaseLength = i.Hazards[0].PhaseLength, PeakLength = i.Hazards[0].PeakLength, Power = 1000 })))));
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, i => i.Timed.Add(new ImageTimed { Card = i.Timed[0].Card, Region = 1, Modifier = i.Timed[0].Modifier, Delta = i.Timed[0].Delta, Until = i.Timed[0].Until })))));
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, i => i.Agenda.Add(new ImageAgenda { Due = i.Agenda[0].Due, Card = i.Agenda[0].Card, Region = 1, Parent = i.Agenda[0].Parent, Link = i.Agenda[0].Link })))));
    }

    // ---- documents -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AFrequencyThatSumsToOneAndALadderOfEightRungsAreAcceptedAndNineAreNot()
    {
        Fixture.Mini(root =>
        {
            Tiny.Zone(root)["seasons"]![0]!["frequency"] = new JsonArray(1, 0);
            Tiny.Zone(root)["seasons"]![1]!["frequency"] = new JsonArray(0, 1);
        });
        JsonArray Ladder(int rungs)
        {
            var ladder = new JsonArray();
            for (var k = 0; k < rungs; k++)
                ladder.Add(new JsonObject
                {
                    ["id"] = "r" + k, ["key"] = "r.low", ["tailPpm"] = 1_000_000 - k * 100_000, ["minDuration"] = 1, ["maxDuration"] = 1, ["onsetSteps"] = 0, ["recoverySteps"] = 0, ["stress"] = 0, ["escalatePpm"] = 0, ["effects"] = new JsonArray(),
                });
            return ladder;
        }
        Assert.Equal(8, Fixture.Mini(root => Tiny.Hazard(root)["ladder"] = Ladder(8)).Hazards[0].Ladder.Count);
        Assert.Equal(WorldCodes.Range, Fixture.Refusal(() => Fixture.Mini(root => Tiny.Hazard(root)["ladder"] = Ladder(9))));
    }

    [Fact]
    public void TheDirectorRelaxesAtExactlyItsRelaxLine()
    {
        var runtime = Director(root =>
        {
            var calm = Tiny.Teller(root, "calm");
            calm["peakAt"] = 300; calm["relaxBelow"] = 100; calm["decay"] = 0; calm["peakHold"] = 1; calm["restMin"] = 0;
        });
        runtime.PlayCard("c-bad", "a");                       // 300
        runtime.Advance(1, runtime.CycleSeasonAt, Fixture.Host());
        Assert.Equal(DirectorPhase.Peak, runtime.Phase);      // exactly the line
        runtime.Advance(2, runtime.CycleSeasonAt, Fixture.Host());
        Assert.Equal(DirectorPhase.Relax, runtime.Phase);
        runtime.PlayCard("c-good", "a");                      // -200: exactly 100, the relax line
        Assert.Equal(100, runtime.Stress);
        runtime.Advance(3, runtime.CycleSeasonAt, Fixture.Host());
        Assert.Equal(DirectorPhase.Build, runtime.Phase);
    }

    [Fact]
    public void TheChanceOfADrawRisesOneStepAtATimeFromTheMinimumGapSoTheMeanGapIsTheRampsOwn()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            root["start"]!["storyteller"] = "calm";
            foreach (var card in Tiny.Array(root, "cards")) card!["weight"] = card["id"]!.GetValue<string>() == "c-global" ? 100 : 0;
            Tiny.Card(root, "c-global")["effects"] = new JsonArray();
            Tiny.Card(root, "c-global")["cooldown"] = 0;
            var calm = Tiny.Teller(root, "calm");
            calm["gapMin"] = 1; calm["gapMax"] = 5; calm["decay"] = 1000; calm["peakAt"] = 1001;
        });
        var steps = Fixture.Live(runtime, 30_000).Of(ChangeKind.Card).Select(c => c.Step).ToList();
        var gaps = steps.Zip(steps.Skip(1), (a, b) => (double)(b - a)).ToList();
        // The chance at a gap of s steps is (s - 1 + 1) / 5 from 1 to 5, so a gap of s is drawn with probability s/5 given it was not drawn before.
        double survive = 1, expected = 0;
        for (var s = 1; s <= 5; s++)
        {
            var p = s / 5.0;
            expected += s * survive * p;
            survive *= 1 - p;
        }
        Assert.InRange(gaps.Average(), expected * 0.97, expected * 1.03);
    }

    [Fact]
    public void AHazardPhaseThatIsShorterThanItsRungsIsRefusedEvenWhenItHasNotRunOutYet()
    {
        var set = Fixture.Mini();
        var runtime = WorldRuntime.Create(set);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.BeginHazard("a", "h", "low");                  // an onset of two steps, one of which has run
        var bytes = WorldSave.Write(runtime);
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, i => i.Hazards[0].PhaseLength = 1))));
        WorldSave.Open(set, bytes);
    }

    [Fact]
    public void ExplainingACardAtTheStepItBecomesFreeSaysItIsFree()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            Tiny.Card(root, "c-bad")["cooldown"] = 0;
            Tiny.Card(root, "c-bad")["duration"] = 1;
        });
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.PlayCard("c-bad", "a");                       // free again at the next step, 1
        var text = WorldExplain.Card(runtime, "c-bad", "", Fixture.Host());
        Assert.Contains("cooldown over", text);
        Assert.Contains("group g is free", text);
    }

    [Fact]
    public void ExplainingAHazardAtTheStepItMayStartAgainSaysTheCooldownIsOver()
    {
        var runtime = Fixture.MiniWorld(root => { Tiny.Hazard(root)["cooldown"] = 0; Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 0, ["falloff"] = 0 }; });
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.BeginHazard("a", "h", "low");
        var step = 0;
        while (runtime.ActiveAt(0, 0) is not null) runtime.Advance(++step, runtime.CycleSeasonAt, Fixture.Host());
        Assert.Contains("cooldown over", WorldExplain.Hazard(runtime, "h", "a", Fixture.Host()));
    }
}
