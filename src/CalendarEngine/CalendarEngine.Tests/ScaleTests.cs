using System.Diagnostics;
using System.Globalization;
using EngineCommon;
using Xunit;

namespace CalendarEngine.Tests;

/// <summary>
/// The cost of the questions a game asks every tick, measured with repeats (the median, the fastest and the slowest of each are written to <c>perf.txt</c> beside the test output). The assertions are loose
/// bounds meant to catch a change from linear to quadratic or an allocation per call, not to pin a machine; the numbers in docs/CALENDAR_ENGINE.md are from the file.
/// </summary>
public sealed class ScaleTests
{
    private static readonly List<string> Report = new();

    private static (double Median, double Min, double Max) Measure(string name, int repeats, Action work)
    {
        work();   // warm up
        var times = new double[repeats];
        for (var i = 0; i < repeats; i++)
        {
            var start = Stopwatch.GetTimestamp();
            work();
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        Array.Sort(times);
        var result = (times[repeats / 2], times[0], times[^1]);
        lock (Report)
        {
            Report.Add(string.Create(CultureInfo.InvariantCulture, $"{name}: median {result.Item1:F3} ms, min {result.Item2:F3} ms, max {result.Item3:F3} ms over {repeats} runs"));
            File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "perf.txt"), Report);
        }
        return result;
    }

    [Fact]
    public void TenThousandActorsAreAnsweredInAFewMillisecondsAndAllocateLittle()
    {
        var set = Fixture.Gregorian;
        var runtime = CalendarRuntime.Create(set, Fixture.At(set, 2024, "march", 6, 10), 1, 1);
        for (var i = 0; i < 10_000; i++) runtime.AddActor("actor-" + i.ToString("00000", CultureInfo.InvariantCulture), i % 4 == 0 ? "guard" : "shopkeeper");
        var context = runtime.Context(Fixture.Dry);
        var sink = 0;
        var query = Measure("10,000 WhereIs in one context", 30, () => { foreach (var id in runtime.Actors.Keys) sink += runtime.WhereIs(context, id).From; });
        Assert.True(query.Median < 100, "10,000 queries took " + query.Median + " ms");
        var first = runtime.Refresh(runtime.Context(Fixture.Dry), 100_000);
        Assert.Equal(10_000, first.Count);
        var quiet = Measure("10,000 Refresh, nothing changed (context included)", 30, () => Assert.Empty(runtime.Refresh(runtime.Context(Fixture.Dry), 100_000)));
        Assert.True(quiet.Median < 100, "a quiet refresh took " + quiet.Median + " ms");
        // A minute at which everybody changes block: 7:00 breakfast for the shopkeepers.
        var changed = 0;
        var storm = Measure("10,000 Refresh at a block change (7,500 announce)", 20, () =>
        {
            var local = CalendarRuntime.Create(set, Fixture.At(set, 2024, "march", 6, 6, 59), 1, 1);
            for (var i = 0; i < 10_000; i++) local.AddActor("actor-" + i.ToString("00000", CultureInfo.InvariantCulture), i % 4 == 0 ? "guard" : "shopkeeper");
            local.Refresh(local.Context(Fixture.Dry), 100_000);
            local.SkipTo(local.Instant + 1);
            changed = local.Refresh(local.Context(Fixture.Dry), 100_000).Count;
        });
        Assert.Equal(7_500, changed);
        Assert.True(storm.Median < 1000, "the block change took " + storm.Median + " ms");
        Assert.True(sink > 0);

        var before = GC.GetAllocatedBytesForCurrentThread();
        foreach (var id in runtime.Actors.Keys) runtime.WhereIs(context, id);
        var perQuery = (GC.GetAllocatedBytesForCurrentThread() - before) / 10_000;
        lock (Report) { Report.Add("allocation per WhereIs in a context: " + perQuery + " bytes"); File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "perf.txt"), Report); }
        Assert.True(perQuery < 200, "each query allocated " + perQuery + " bytes");
    }

    [Fact]
    public void LongRangeDateConversionIsConstantTimePerCall()
    {
        var set = Fixture.Gregorian;
        var system = set.System;
        var instants = Enumerable.Range(0, 100_000).Select(i => system.MinInstant + (long)(DeterministicRoll.Next(1, "span", i) % (ulong)(system.MaxInstant - system.MinInstant))).ToArray();
        long sum = 0;
        var toDate = Measure("100,000 instant to moment over 200,000 years", 15, () => { foreach (var instant in instants) sum += system.MomentOf(instant).Day.Year; });
        var dates = instants.Select(i => system.MomentOf(i).Day.Date).ToArray();
        var toInstant = Measure("100,000 date to instant over 200,000 years", 15, () => { foreach (var date in dates) sum += system.DayNumber(date); });
        Assert.True(toDate.Median < 200, "conversion took " + toDate.Median + " ms");
        Assert.True(toInstant.Median < 200, "conversion took " + toInstant.Median + " ms");
        // Constant time: the same call near the epoch and 100,000 years away cost about the same.
        var near = Enumerable.Range(0, 100_000).Select(i => (long)i * 7).ToArray();
        var nearTime = Measure("100,000 instant to moment near the epoch", 15, () => { foreach (var instant in near) sum += system.MomentOf(instant).Day.Year; });
        Assert.True(toDate.Median < nearTime.Median * 4 + 5, "far conversions cost " + toDate.Median + " ms against " + nearTime.Median + " ms near the epoch");
        Assert.NotEqual(0, sum);
    }

    [Fact]
    public void FactsHolidaysAndStepsCostLittle()
    {
        var set = Fixture.Gregorian;
        var runtime = CalendarRuntime.Create(set, Fixture.At(set, 2024, "january", 1), 1, 1);
        var moments = Enumerable.Range(0, 10_000).Select(i => set.System.MomentOf(runtime.Instant + i * 997L)).ToArray();
        var total = 0;
        var facts = Measure("10,000 CalendarFacts.Read", 15, () => { foreach (var moment in moments) total += CalendarFacts.Read(set, moment).Count; });
        var holidays = Measure("10,000 HolidaysOn", 15, () => { foreach (var moment in moments) total += set.HolidaysOn(moment.Day).Count; });
        var steps = Measure("100,000 Step calls of one tick", 15, () =>
        {
            var local = CalendarRuntime.Create(set, Fixture.At(set, 2024, "january", 1), 1, 1);
            for (var tick = 1; tick <= 100_000; tick++) local.Step(tick);
        });
        var skip = Measure("365 one-day skips with holiday and season scan", 15, () =>
        {
            var local = CalendarRuntime.Create(set, Fixture.At(set, 2024, "january", 1), 1, 1);
            for (var day = 1; day <= 365; day++) local.SkipTo(local.Instant + 1440);
        });
        Assert.True(facts.Median < 500 && holidays.Median < 500 && steps.Median < 500 && skip.Median < 500, $"{facts.Median} {holidays.Median} {steps.Median} {skip.Median}");
        Assert.True(total > 0);
    }

    [Fact]
    public void ASaveOfTenThousandActorsIsWrittenAndOpenedQuickly()
    {
        var set = Fixture.Gregorian;
        var runtime = CalendarRuntime.Create(set, Fixture.At(set, 2024, "march", 6, 10), 1, 1);
        for (var i = 0; i < 10_000; i++) runtime.AddActor("actor-" + i.ToString("00000", CultureInfo.InvariantCulture), "shopkeeper");
        runtime.Refresh(runtime.Context(Fixture.Dry), 100_000);
        var bytes = CalendarSave.Write(runtime);
        var write = Measure("write a save of 10,000 actors", 10, () => CalendarSave.Write(runtime));
        var open = Measure("open a save of 10,000 actors", 10, () => CalendarSave.Open(set, bytes));
        lock (Report) { Report.Add("save of 10,000 actors: " + bytes.Length + " bytes"); File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "perf.txt"), Report); }
        Assert.True(write.Median < 500 && open.Median < 500, $"{write.Median} {open.Median}");
    }
}
