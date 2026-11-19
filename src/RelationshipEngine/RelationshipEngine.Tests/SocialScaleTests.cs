using System.Diagnostics;
using System.Globalization;
using System.Text;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>
/// What the derived social state costs (1,000 actors with 20 edges each, the size ScaleTests uses), step by step through a whole day. The same village is built four times: the passes off (the old day), all on atomic (the
/// whole pass in the first step of the day), all on spread (one stage in each of the first five steps), and the passes off with the spread schedule (the control: upkeep and gossip alone). Round after round each is lived
/// through one whole day, every step timed (a collection before each, outside the timing), the four in turn so that a machine that gets busier during the test is busier for all four. What matters for a game at 60 frames
/// a second is the <b>worst step of a day</b>, not the first. The rows are written to relationship-social-scale-results.csv beside the test assembly. The budget is checked in Release only.
/// </summary>
public sealed class SocialScaleTests
{
    private const int Rounds = 25;
    private const int Steps = 24;

    private sealed record Variant(string Name, bool Passes, string Schedule)
    {
        public RelationshipRuntime Runtime { get; } = ScaleTests.Big(r =>
        {
            foreach (var block in new[] { "communities", "standing", "balance" }) r["social"]![block]!["enabled"] = Passes;
            r["social"]!["schedule"] = Schedule;
        });

        public List<double> Worst { get; } = new();
        public List<double> Total { get; } = new();
        public List<double>[] ByOffset { get; } = Enumerable.Range(0, Steps).Select(_ => new List<double>()).ToArray();
        public long WorstBytes { get; set; }
    }

    private static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static double Median(List<double> times) => times.Order().ElementAt(times.Count / 2);

    [Fact]
    public void TheDerivedPassesCostWhatTheDocumentSaysAndTheSpreadScheduleKeepsEveryStepSmall()
    {
        var host = Fixture.Host(season: "winter", raining: true);
        var variants = new[] { new Variant("passes-off", false, "atomic"), new Variant("atomic", true, "atomic"), new Variant("spread", true, "spread"), new Variant("spread-passes-off", false, "spread") };
        foreach (var variant in variants) variant.Runtime.Advance(variant.Runtime.Step + 48, host);   // two days first, so JIT is not measured
        for (var round = 0; round < Rounds; round++)
            foreach (var variant in variants)
            {
                var runtime = variant.Runtime;
                while ((runtime.Step + 1) % Steps != 0) runtime.Advance(runtime.Step + 1, host);
                double worst = 0, total = 0;
                for (var offset = 0; offset < Steps; offset++)                         // the first step lived is step 24k (offset 0), the last 24k+23
                {
                    GC.Collect();
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    var watch = Stopwatch.StartNew();
                    runtime.Advance(runtime.Step + 1, host);
                    var elapsed = watch.Elapsed.TotalMilliseconds;
                    var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                    variant.ByOffset[offset].Add(elapsed);
                    total += elapsed;
                    if (elapsed > worst) { worst = elapsed; variant.WorstBytes = bytes; }
                }
                variant.Worst.Add(worst);
                variant.Total.Add(total);
            }
        var csv = new StringBuilder("variant,worst_step_median_ms,worst_step_min_ms,worst_step_max_ms,day_total_median_ms,worst_step_bytes,step0_median_ms,step1_median_ms,step2_median_ms,step3_median_ms,step4_median_ms,step5_median_ms,ordinary_median_ms,rounds\n");
        foreach (var v in variants)
        {
            var ordinary = Median(v.ByOffset.Skip(6).SelectMany(x => x).ToList());
            csv.Append(string.Join(',', v.Name, F(Median(v.Worst)), F(v.Worst.Min()), F(v.Worst.Max()), F(Median(v.Total)), v.WorstBytes.ToString(CultureInfo.InvariantCulture), string.Join(',', Enumerable.Range(0, 6).Select(o => F(Median(v.ByOffset[o])))), F(ordinary), Rounds.ToString(CultureInfo.InvariantCulture))).Append('\n');
            Console.WriteLine($"social scale: {v.Name}: worst step of a day median {Median(v.Worst):F2} ms (min {v.Worst.Min():F2}, max {v.Worst.Max():F2}), whole day {Median(v.Total):F1} ms; steps 0..5 medians {string.Join(' ', Enumerable.Range(0, 6).Select(o => F(Median(v.ByOffset[o]))))}; ordinary {ordinary:F2} ms");
        }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "relationship-social-scale-results.csv"), csv.ToString());
        var off = variants[0];
        var atomic = variants[1];
        var spread = variants[2];
        Assert.True(Median(atomic.Worst) > Median(off.Worst), "the passes cost something: they did run");
        Assert.True(Median(spread.ByOffset[3]) > Median(spread.ByOffset[8]) && Median(spread.ByOffset[2]) > Median(spread.ByOffset[8]), "the stages ran in their own steps");
#if !DEBUG
        Assert.True(Median(atomic.Worst) < 120, "the first step of a day with every derived pass took a median of " + Median(atomic.Worst) + " ms");
        // The point of the schedule: no step of the day is as long as the atomic one, and none comes near two frames.
        Assert.True(Median(spread.Worst) < Median(atomic.Worst) * 0.75, "spread worst step " + Median(spread.Worst) + " ms against atomic " + Median(atomic.Worst) + " ms");
        Assert.True(Median(spread.Worst) < 40, "the worst step of a spread day took a median of " + Median(spread.Worst) + " ms");
        // An ordinary step does not run the passes: with all three on it costs what it costs with all three off, within the noise.
        Assert.True(Median(spread.ByOffset.Skip(6).SelectMany(x => x).ToList()) < Median(off.ByOffset.Skip(6).SelectMany(x => x).ToList()) * 2 + 1, "an ordinary step with the passes on is slower than with them off");
#endif
    }
}
