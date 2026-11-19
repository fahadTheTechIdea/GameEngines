using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>
/// Cost: 100 regions in a ring with chords, 20 hazards and 20 diseases (each with a response) all going, a year of daily steps. The budgets are set from the measurements recorded in docs/WORLD_EVENTS_ENGINE.md
/// section 10 and docs/measurements (median of repeats on this machine in Release, with a large margin for a loaded machine and a Debug build). A run writes its numbers to <c>scale-results.csv</c> beside the test assembly.
/// </summary>
public sealed class ScaleTests
{
    public const int Regions = 100;
    public const int Hazards = 20;
    public const int Diseases = 20;

    /// <summary>
    /// The big document. <paramref name="excited"/>: every one of the twenty hazards excites itself (a jump of 2% of an onset, falling by 20% a step, at most 20%) and the zones tilt their weather by their neighbours (the sample's spill);
    /// without it the document is what pass 38 measured: no excitation, no spill, the quake's cooldown of 90.
    /// </summary>
    internal static string BigText(bool excited = true)
    {
        var root = JsonNode.Parse(Fixture.ValleyText)!.AsObject();
        root["id"] = "big";
        root["limits"] = new JsonObject { ["maxHazards"] = 4000, ["maxEffects"] = 2000, ["maxAgenda"] = 500 };
        var hazards = root["hazards"]!.AsArray();
        var baseHazards = hazards.Count;
        for (var k = baseHazards; k < Hazards; k++)
        {
            var clone = JsonNode.Parse(hazards[k % baseHazards]!.ToJsonString())!.AsObject();
            clone["id"] = "hz" + k;
            clone["key"] = "hazard." + hazards[k % baseHazards]!["id"]!.GetValue<string>();
            clone["excludes"] = new JsonArray();
            hazards.Add(clone);
        }
        foreach (var hazard in hazards) hazard!["excludes"] = new JsonArray();         // nothing excludes anything: all twenty may go together
        foreach (var hazard in hazards)
        {
            hazard!["excite"] = excited ? new JsonObject { ["jumpPpm"] = 20_000, ["decayPermille"] = 200, ["capPpm"] = 200_000 } : new JsonObject { ["jumpPpm"] = 0, ["decayPermille"] = 1000, ["capPpm"] = 0 };
            if (!excited && hazard["kind"]!.GetValue<string>() == "quake") hazard["cooldown"] = 90;
        }
        if (!excited)
            foreach (var zone in root["zones"]!.AsArray()) zone!["spill"] = 0;
        var diseases = root["diseases"]!.AsArray();
        var baseDiseases = diseases.Count;
        for (var k = baseDiseases; k < Diseases; k++)
        {
            var clone = JsonNode.Parse(diseases[k % baseDiseases]!.ToJsonString())!.AsObject();
            clone["id"] = "dz" + k;
            clone["key"] = diseases[k % baseDiseases]!["key"]!.GetValue<string>();
            clone["seedPpm"] = 3000;
            diseases.Add(clone);
        }
        foreach (var disease in diseases) disease!["seedPpm"] = 3000;
        var interventions = root["interventions"]!.AsArray();
        for (var k = baseDiseases; k < Diseases; k++)
            interventions.Add(new JsonObject
            {
                ["id"] = "q" + k, ["key"] = "intervention.quarantine", ["disease"] = "dz" + k, ["betaPermille"] = 400, ["flowPermille"] = 200, ["vaccinate"] = 0, ["treat"] = 1000, ["fatalityPermille"] = 1000, ["autoAt"] = 25,
            });
        var regions = new JsonArray();
        for (var r = 0; r < Regions; r++)
        {
            var exposure = new JsonArray();
            for (var h = 0; h < Hazards; h++) exposure.Add(new JsonObject { ["hazard"] = hazards[h]!["id"]!.GetValue<string>(), ["permille"] = 400 + (r * 7 + h * 13) % 1000 });
            var immune = new JsonArray();
            if (r % 3 == 0) immune.Add(new JsonObject { ["disease"] = "flu", ["permille"] = 100 });
            regions.Add(new JsonObject
            {
                ["id"] = "r" + r.ToString("000", CultureInfo.InvariantCulture), ["key"] = "region.riverton", ["zone"] = r % 2 == 0 ? "temperate" : "coast", ["market"] = "",
                ["population"] = 5000 + (r * 977) % 45_000, ["exposure"] = exposure, ["immune"] = immune,
            });
        }
        root["regions"] = regions;
        var links = new JsonArray();
        for (var r = 0; r < Regions; r++)
        {
            links.Add(new JsonObject { ["a"] = "r" + r.ToString("000", CultureInfo.InvariantCulture), ["b"] = "r" + ((r + 1) % Regions).ToString("000", CultureInfo.InvariantCulture), ["flowPpm"] = 40_000 });
            links.Add(new JsonObject { ["a"] = "r" + r.ToString("000", CultureInfo.InvariantCulture), ["b"] = "r" + ((r + 10) % Regions).ToString("000", CultureInfo.InvariantCulture), ["flowPpm"] = 15_000 });
        }
        root["links"] = links;
        return root.ToJsonString();
    }

    private static (double Median, double Min, double Max) Time(int repeats, Action action)
    {
        var times = new List<double>();
        for (var i = 0; i < repeats; i++)
        {
            var watch = Stopwatch.StartNew();
            action();
            times.Add(watch.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        return (times[times.Count / 2], times[0], times[^1]);
    }

    private static string Ms(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    private static readonly IReadOnlyDictionary<string, ScenarioEngine.Contracts.FactValue> Host = Fixture.ValleyHost();

    [Fact]
    public void AHundredRegionsTwentyHazardsAndTwentyDiseasesLiveAYearWithinTheBudgetAndKeepTheirInvariants()
    {
        var csv = new StringBuilder("measurement,median,min,max,unit,repeats\n");
        var watch = Stopwatch.StartNew();
        var text = BigText();
        var set = WorldLoader.Load(text);
        var load = watch.Elapsed.TotalMilliseconds;
        Assert.Equal(Regions, set.Regions.Count);
        Assert.Equal(Hazards, set.Hazards.Count);
        Assert.Equal(Diseases, set.Diseases.Count);
        Assert.Equal(3 + Diseases - 2, set.Interventions.Count);
        csv.Append(CultureInfo.InvariantCulture, $"load document ({text.Length / 1024} KB),{Ms(load)},{Ms(load)},{Ms(load)},ms,1\n");

        var runtime = WorldRuntime.Create(set);
        var firstStep = Stopwatch.StartNew();
        runtime.Advance(0, runtime.CycleSeasonAt, Host);
        var first = firstStep.Elapsed.TotalMilliseconds;
        csv.Append(CultureInfo.InvariantCulture, $"first step,{Ms(first)},{Ms(first)},{Ms(first)},ms,1\n");

        var stepTimes = new List<double>();
        var peakHazards = 0;
        var peakDiseases = 0;
        var changes = 0L;
        var year = Stopwatch.StartNew();
        for (var step = 1; step < 365; step++)
        {
            var one = Stopwatch.StartNew();
            changes += runtime.Advance(step, runtime.CycleSeasonAt, Host).Items.Count;
            stepTimes.Add(one.Elapsed.TotalMilliseconds);
            peakHazards = Math.Max(peakHazards, runtime.ActiveHazards.Count);
            var ill = 0;
            for (var d = 0; d < Diseases; d++)
                if (runtime.Regions.Any(r => r.Exposed[d] + r.Infectious[d] > 0)) ill++;
            peakDiseases = Math.Max(peakDiseases, ill);
            if (step % 60 == 0)
                foreach (var region in set.Regions)
                    for (var d = 0; d < Diseases; d++)
                    {
                        var state = runtime.Regions[region.Index];
                        Assert.Equal(region.Population, state.Susceptible[d] + state.Exposed[d] + state.Infectious[d] + state.Recovered[d] + state.Dead[d]);
                    }
        }
        var total = year.Elapsed.TotalMilliseconds;
        stepTimes.Sort();
        Assert.True(peakHazards >= 15, "at most " + peakHazards + " hazards were going at once");
        Assert.True(peakDiseases >= 15, "at most " + peakDiseases + " diseases were going at once");
        csv.Append(CultureInfo.InvariantCulture, $"ordinary step (100 regions 20 hazards 20 diseases),{Ms(stepTimes[stepTimes.Count / 2])},{Ms(stepTimes[0])},{Ms(stepTimes[^1])},ms,364\n");
        csv.Append(CultureInfo.InvariantCulture, $"a year of daily steps,{Ms(total)},{Ms(total)},{Ms(total)},ms,1\n");
        csv.Append(CultureInfo.InvariantCulture, $"changes in the year,{changes},{changes},{changes},count,1\n");
        csv.Append(CultureInfo.InvariantCulture, $"most hazards at once,{peakHazards},{peakHazards},{peakHazards},count,1\n");
        csv.Append(CultureInfo.InvariantCulture, $"most diseases at once,{peakDiseases},{peakDiseases},{peakDiseases},count,1\n");

        var bytes = Array.Empty<byte>();
        var write = Time(7, () => bytes = WorldSave.Write(runtime));
        csv.Append(CultureInfo.InvariantCulture, $"save write ({bytes.Length / 1024} KB),{Ms(write.Median)},{Ms(write.Min)},{Ms(write.Max)},ms,7\n");
        WorldRuntime? reopened = null;
        var open = Time(7, () => reopened = WorldSave.Open(set, bytes));
        csv.Append(CultureInfo.InvariantCulture, $"save open,{Ms(open.Median)},{Ms(open.Min)},{Ms(open.Max)},ms,7\n");
        Assert.Equal(runtime.StateDigest(), reopened!.StateDigest());

        var before = GC.GetAllocatedBytesForCurrentThread();
        runtime.Advance(365, runtime.CycleSeasonAt, Host);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        csv.Append(CultureInfo.InvariantCulture, $"allocated by one step,{allocated},{allocated},{allocated},bytes,1\n");

        // The control: the same hundred regions without excitation or spill (what pass 38 measured), so the cost of the new rules is a difference and not a comparison with another day's measurement.
        var control = WorldRuntime.Create(WorldLoader.Load(BigText(excited: false)));
        control.Advance(0, control.CycleSeasonAt, Host);
        var controlTimes = new List<double>();
        for (var step = 1; step < 365; step++)
        {
            var one = Stopwatch.StartNew();
            control.Advance(step, control.CycleSeasonAt, Host);
            controlTimes.Add(one.Elapsed.TotalMilliseconds);
        }
        controlTimes.Sort();
        csv.Append(CultureInfo.InvariantCulture, $"control step (no excitation, no spill),{Ms(controlTimes[controlTimes.Count / 2])},{Ms(controlTimes[0])},{Ms(controlTimes[^1])},ms,364\n");

        // The same world a second time gives the same state: nothing in a year depends on anything but the document.
        var again = WorldRuntime.Create(set);
        for (var step = 0; step <= 365; step++) again.Advance(step, again.CycleSeasonAt, Host);
        Assert.Equal(runtime.StateDigest(), again.StateDigest());

        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "scale-results.csv"), csv.ToString());
        // Budgets: ten times the Release medians recorded in the document, so a Debug build or a loaded machine still passes and an accidental quadratic does not.
        Assert.True(stepTimes[stepTimes.Count / 2] < 400, "a step took " + stepTimes[stepTimes.Count / 2] + " ms");
        Assert.True(total < 60_000, "the year took " + total + " ms");
        Assert.True(bytes.Length < 8 * 1024 * 1024, "a save is " + bytes.Length + " bytes");
    }

    [Fact]
    public void ABigDocumentIsRefusedOnlyForWhatIsWrongInIt()
    {
        var text = BigText();
        Assert.Equal(WorldCodes.Reference, Fixture.Refusal(() => WorldLoader.Load(text.Replace("\"disease\":\"dz19\"", "\"disease\":\"nope\"", StringComparison.Ordinal))));
        Assert.True(text.Length < WorldLoader.MaxCharacters);
    }
}
