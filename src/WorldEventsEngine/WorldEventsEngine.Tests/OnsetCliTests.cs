using System.Globalization;
using System.Text.Json.Nodes;
using EngineCommon.Balance;
using ScenarioEngine.Contracts;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>
/// The per-onset magnitudes of the sweep (<c>simulate ... --seeds N --onsets</c>): for every natural hazard onset its highest rung, how many steps it went and the area under its intensity curve, and for every outbreak its deaths, peak
/// and length, as whole numbers, so the offline tail tool can fit a sample of onsets and not only one value per seed. Checked against what the engine itself returns: the changes of a run, the intensity the runtime reports, and the
/// single-run totals.
/// </summary>
public sealed class OnsetCliTests
{
    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "samples", name);

    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = WorldCli.Run(args, output, error);
        return (code, output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), error.ToString());
    }

    private static (int Code, string Out, string Err) Valley(params string[] extra) => Run(["simulate", Sample("valley.world.json"), Sample("valley.world.scenario.json"), .. extra]);

    private static long EventCount(string report, string name)
    {
        var words = report.Split('\n').Single(l => l.StartsWith("events " + name + " ", StringComparison.Ordinal)).Split(' ');
        return long.Parse(words[Array.IndexOf(words, "n") + 1], CultureInfo.InvariantCulture);
    }

    // ---- the rule for a natural onset -----------------------------------------------------------------------------------------------

    private static WorldChange Change(ChangeKind kind, int sub2, long amount) => new(kind, 0, 0, 0, 0, sub2, amount, 0, "", "");

    [Fact]
    public void ANaturalOnsetIsAStartedChangeAtFullPowerAmongTheStepsOwnChanges()
    {
        var started = Change(ChangeKind.Hazard, (int)HazardChange.Started, 1000);
        Assert.True(OriginRule.IsNaturalOnset(started, 2, 2));                                                         // the first change after the scripted ones
        Assert.False(OriginRule.IsNaturalOnset(started, 1, 2));                                                        // a scripted call chose that one
        Assert.False(OriginRule.IsNaturalOnset(Change(ChangeKind.Hazard, (int)HazardChange.Started, 600), 5, 2));      // it spread here weaker
        Assert.False(OriginRule.IsNaturalOnset(Change(ChangeKind.Hazard, (int)HazardChange.Escalated, 1000), 5, 2));
        Assert.False(OriginRule.IsNaturalOnset(Change(ChangeKind.Weather, (int)HazardChange.Started, 1000), 5, 2));
    }

    [Fact]
    public void TheValleySpreadsWeakerSoItsOnsetsCanBeToldAndAFullPowerSpreaderCannot()
    {
        OriginRule.Require(Fixture.Valley);
        Assert.Equal(EngineCommon.Balance.BalanceCodes.OnsetAmbiguous, Fixture.Refusal(() => OriginRule.Require(Fixture.Mini(root => Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 2, ["falloff"] = 1000 })), "full-power spread"));
        OriginRule.Require(Fixture.Mini(root => Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 0, ["falloff"] = 1000 }));      // no spread at all: nothing to confuse
    }

    // ---- the tracker against the engine ---------------------------------------------------------------------------------------------

    [Fact]
    public void TheTrackerReportsEachOnsetOnceWithTheEnginesOwnIntensity()
    {
        var root = Lab.Hawkes(7, 150_000, 0, 1000, 0);
        var ladder = Tiny.Hazard(root)["ladder"]!.AsArray();
        var low = ladder[0]!.AsObject();
        low["minDuration"] = 3;
        low["maxDuration"] = 3;
        low["onsetSteps"] = 1;
        low["recoverySteps"] = 2;
        var set = WorldLoader.Load(root.ToJsonString());
        var runtime = WorldRuntime.Create(set);
        var tracker = new OnsetTracker(runtime);
        var facts = (Dictionary<string, FactValue>)new Dictionary<string, FactValue>(Fixture.Host());
        long started = 0, ended = 0, intensity = 0;
        for (long step = 0; step < 400; step++)
        {
            var changes = runtime.Advance(step, runtime.CycleSeasonAt, facts).Items.ToList();
            started += changes.Count(c => c.Kind == ChangeKind.Hazard && c.Sub2 == (int)HazardChange.Started);
            ended += changes.Count(c => c.Kind == ChangeKind.Hazard && c.Sub2 == (int)HazardChange.Ended);
            intensity += runtime.ActiveHazards.Sum(h => runtime.Intensity(h));
            tracker.Observe(step, changes, 0);
        }
        var metrics = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var seen = tracker.Finish(metrics);
        Assert.True(started > 20, "the lab should have many onsets");
        Assert.Equal(started, metrics["onset.hazard.h.natural"]);
        Assert.Equal(started, metrics["onset.hazard.h.drawn-at-least.low"]);        // every natural onset was drawn at the lowest rung or worse
        Assert.Equal(started, seen.Events["hazard.h.size"].Count);
        Assert.Equal(started - ended, metrics["onset.hazard.h.open-at-end"]);
        Assert.Equal(started - ended, seen.Events["hazard.h.open"].Sum());
        Assert.Equal(intensity, seen.Events["hazard.h.size"].Sum());               // the areas add up to all the intensity the runtime reported, step by step
        Assert.All(seen.Events["hazard.h.steps"], steps => Assert.InRange(steps, 1, 6));
        Assert.All(seen.Events["hazard.h.rung"], rung => Assert.Equal(0, rung));
    }

    [Fact]
    public void AnEscalationRaisesTheRungAnOnsetIsReportedAt()
    {
        var root = Lab.Hawkes(9, 100_000, 0, 1000, 0);
        var ladder = Tiny.Hazard(root)["ladder"]!.AsArray();
        var low = ladder[0]!.AsObject();
        low["minDuration"] = 3;
        low["maxDuration"] = 3;
        low["escalatePpm"] = 600_000;
        var high = Tiny.Copy(low).AsObject();
        high["id"] = "worse";
        high["key"] = "r.worse";
        high["tailPpm"] = 10;
        high["escalatePpm"] = 0;
        high["minDuration"] = 2;
        high["maxDuration"] = 2;
        ladder.Add(high);
        var set = WorldLoader.Load(root.ToJsonString());
        var runtime = WorldRuntime.Create(set);
        var tracker = new OnsetTracker(runtime);
        var facts = new Dictionary<string, FactValue>(Fixture.Host());
        for (long step = 0; step < 400; step++) tracker.Observe(step, runtime.Advance(step, runtime.CycleSeasonAt, facts).Items.ToList(), 0);
        var metrics = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var rungs = tracker.Finish(metrics).Events["hazard.h.rung"];
        // Almost every onset is drawn at the first rung (tail 10 ppm for the second), so any onset reported at the second rung got there by escalating.
        Assert.True(rungs.Count(r => r == 1) > metrics["onset.hazard.h.drawn-at-least.worse"] + 3, "escalated onsets should be reported at their highest rung");
    }

    // ---- the sweep ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void WithoutTheFlagTheSweepPrintsNoOnsetsAndWithItTheMetricsAreTheSameAndMoreAreAdded()
    {
        var plain = Valley("--seeds", "4", "--from-seed", "1", "365");
        Assert.DoesNotContain("\nevents ", plain.Out, StringComparison.Ordinal);
        var onsets = Valley("--seeds", "4", "--from-seed", "1", "365", "--onsets");
        Assert.Equal(0, onsets.Code);
        Assert.EndsWith(" onsets", onsets.Out.Split('\n')[0], StringComparison.Ordinal);
        foreach (var line in plain.Out.Split('\n').Where(l => l.StartsWith("metric ", StringComparison.Ordinal)))
            Assert.Contains("\n" + line + "\n", onsets.Out, StringComparison.Ordinal);                // every metric line is the same
        Assert.Contains("\nmetric onset.hazard.storm.natural ", onsets.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryNaturalOnsetIsReportedOnceAndNoMoreThanTheHazardsStartsAreNatural()
    {
        var run = Valley("--seeds", "6", "--from-seed", "1", "365", "--onsets", "--rows");
        foreach (var id in new[] { "storm", "flood", "drought", "fire", "quake" })
        {
            var natural = SumOf(run.Out, "onset.hazard." + id + ".natural");
            Assert.Equal(natural, EventCount(run.Out, "hazard." + id + ".rung"));
            Assert.Equal(natural, EventCount(run.Out, "hazard." + id + ".size"));
            Assert.Equal(natural, EventCount(run.Out, "hazard." + id + ".open"));
            Assert.True(natural <= SumOf(run.Out, "hazard." + id + ".started"), id + ": the natural onsets are some of the starts (the rest spread from a neighbour)");
        }
        Assert.True(SumOf(run.Out, "hazard.storm.started") > SumOf(run.Out, "onset.hazard.storm.natural"), "storms spread to the next region, so the starts are more than the natural onsets");
        Assert.Contains("\nevents outbreak.plague.dead n ", run.Out, StringComparison.Ordinal);
    }

    /// <summary>The sum over seeds of a metric, from the per-seed <c>sample</c> rows of a report written with <c>--rows</c>.</summary>
    private static long SumOf(string report, string metric) =>
        report.Split('\n').Where(l => l.StartsWith("sample " + metric + " ", StringComparison.Ordinal)).Sum(l => long.Parse(l.Split(' ')[3], CultureInfo.InvariantCulture));

    [Fact]
    public void TheOnsetRowsRepeatByteForByteAndFeedTheTailTool()
    {
        var first = Valley("--seeds", "30", "--from-seed", "1", "365", "--onsets", "--rows");
        Assert.Equal(first.Out, Valley("--seeds", "30", "--from-seed", "1", "365", "--onsets", "--rows").Out);
        var onsets = first.Out.Split('\n').Where(l => l.StartsWith("onset outbreak.plague.dead ", StringComparison.Ordinal)).ToList();
        Assert.True(onsets.Count >= 30, "the scripted plague should reach several regions");
        // The tail tool reads exactly these rows (its own tests drive it on them; this project does not reference it): "onset <name> <seed> <whole value>".
        Assert.All(onsets, row =>
        {
            var parts = row.Split(' ');
            Assert.Equal(4, parts.Length);
            Assert.True(long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out _));
            Assert.True(long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out _));
        });
        Assert.Equal(onsets.Count, long.Parse(first.Out.Split('\n').Single(l => l.StartsWith("events outbreak.plague.dead ", StringComparison.Ordinal)).Split(' ')[3], CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ADeathsMetricForAnOutbreakAddsUpToTheRegionsDeathsOfTheRun()
    {
        // The scripted plague is the only plague in the valley; each region's plague outbreaks (closed or cut off at the horizon) together hold every death the engine counted for that disease.
        var seeds = Valley("--seeds", "5", "--from-seed", "1", "365", "--onsets", "--rows", "--metrics", "disease.plague.dead");
        var deaths = seeds.Out.Split('\n').Where(l => l.StartsWith("sample disease.plague.dead ", StringComparison.Ordinal)).Sum(l => long.Parse(l.Split(' ')[3], CultureInfo.InvariantCulture));
        var perOutbreak = seeds.Out.Split('\n').Where(l => l.StartsWith("onset outbreak.plague.dead ", StringComparison.Ordinal)).Sum(l => long.Parse(l.Split(' ')[3], CultureInfo.InvariantCulture));
        Assert.True(perOutbreak <= deaths, "outbreak deaths " + perOutbreak + " cannot exceed the disease's deaths " + deaths);
        Assert.True(perOutbreak * 100 >= deaths * 90, "outbreak deaths " + perOutbreak + " should be nearly all of the disease's " + deaths);
    }
}
