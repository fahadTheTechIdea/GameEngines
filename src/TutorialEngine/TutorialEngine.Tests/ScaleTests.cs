using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace TutorialEngine.Tests;

/// <summary>
/// Cost: a tutorial of 200 steps (a chain, with every tenth step a branch and hints on each) and 50 tips that all show at once and stay (the worst case for the work of one evaluation, which reads every
/// showing tip's context). The budgets are set from the measurements recorded in docs/TUTORIAL_ENGINE.md section 10.4 and docs/measurements (median of 15 repeats on this machine in Release, with a margin
/// for a loaded machine). A run writes its numbers to <c>scale-results.csv</c> beside the test assembly.
/// </summary>
public sealed class ScaleTests
{
    public const int Steps = 200;
    public const int Tips = 50;

    internal static string BigText()
    {
        var root = Tiny.Root();
        root["id"] = "big";
        root["limits"] = new JsonObject { ["maxActiveSteps"] = 1, ["maxVisibleTips"] = Tips, ["tipGap"] = 0 };
        var events = new JsonArray();
        for (var e = 0; e < 100; e++) events.Add(new JsonObject { ["id"] = "e" + e.ToString("000", CultureInfo.InvariantCulture) });
        root["events"] = events;
        root["inputs"] = new JsonArray();
        root["skills"] = new JsonArray { new JsonObject { ["id"] = "sk", ["key"] = "k.sk", ["mastered"] = 1000, ["evidence"] = new JsonArray { new JsonObject { ["event"] = "e000", ["points"] = 1 } } } };
        var steps = new JsonArray();
        for (var s = 0; s < Steps; s++)
        {
            var step = new JsonObject
            {
                ["id"] = "s" + s.ToString("000", CultureInfo.InvariantCulture), ["key"] = "k.s" + s, ["body"] = "k.b" + s, ["target"] = "t", ["motion"] = "pulse", ["skippable"] = true, ["skill"] = "",
                ["requires"] = s == 0 ? new JsonArray() : new JsonArray("s" + (s - 1).ToString("000", CultureInfo.InvariantCulture)), ["requiresAny"] = new JsonArray(),
                ["complete"] = Tiny.Cond("event.e" + (s % 100).ToString("000", CultureInfo.InvariantCulture) + ".since_step", "gte", 1),
                ["hints"] = new JsonArray { new JsonObject { ["idle"] = 100, ["kind"] = "nudge", ["key"] = "k.h" }, new JsonObject { ["idle"] = 300, ["kind"] = "explain", ["key"] = "k.h2" }, new JsonObject { ["idle"] = 900, ["kind"] = "show", ["key"] = "k.h3" } },
            };
            if (s % 10 == 5) step["when"] = Tiny.Cond("flag", "eq", true);
            steps.Add(step);
        }
        root["steps"] = steps;
        var tips = new JsonArray();
        for (var t = 0; t < Tips; t++)
            tips.Add(new JsonObject
            {
                ["id"] = "t" + t.ToString("00", CultureInfo.InvariantCulture), ["key"] = "k.t" + t, ["priority"] = t % 5, ["cooldown"] = 10, ["maxShows"] = 100, ["duration"] = 1_000_000, ["skill"] = "", ["afterStep"] = "",
                ["motion"] = "none", ["when"] = new JsonObject { ["all"] = new JsonArray { Tiny.Cond("flag", "eq", true), Tiny.Cond("level", "gte", t % 10), Tiny.Cond("step.s000.done", "eq", true) } },
            });
        root["tips"] = tips;
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

    [Fact]
    public void TwoHundredStepsAndFiftyShowingTipsEvaluateSaveAndOpenWithinTheBudgetAndKeepTheirInvariants()
    {
        var csv = new StringBuilder("measurement,median,min,max,unit,repeats\n");
        var watch = Stopwatch.StartNew();
        var text = BigText();
        var set = TutorialLoader.Load(text);
        var load = watch.Elapsed.TotalMilliseconds;
        Assert.Equal(Steps, set.Steps.Count);
        Assert.Equal(Tips, set.Tips.Count);
        var runtime = TutorialRuntime.Create(set);
        runtime.SelectLanguage("en");
        while (runtime.Journey.State == JourneyState.Story) runtime.StoryNext();
        runtime.ChooseEntry("host");
        runtime.Start();
        var host = Fixture.Host(flag: true, level: 9);
        watch.Restart();
        runtime.Advance(1, host);
        var first = watch.Elapsed.TotalMilliseconds;
        Assert.Single(runtime.ActiveSteps());
        // Walk the whole tutorial: one observation completes each step, so the 200 steps are taught one after the other with 50 tips showing throughout.
        watch.Restart();
        var step = 1L;
        for (var s = 0; s < Steps; s++)
        {
            step += 2;
            runtime.MoveTo(step);
            var active = runtime.ActiveSteps().First();
            foreach (var evt in set.Steps[active].Since) runtime.Observe(set.Events[evt].Id);
            runtime.Evaluate(host);
        }
        var walk = watch.Elapsed.TotalMilliseconds;
        Assert.Equal(TutorialPhase.Finished, runtime.Phase);
        Assert.Equal(Tips, runtime.VisibleTips().Count);
        // The ordinary evaluation of a running tutorial with every tip showing: a second tutorial held at the middle step.
        var middle = TutorialRuntime.Create(set);
        middle.SelectLanguage("en"); while (middle.Journey.State == JourneyState.Story) middle.StoryNext(); middle.ChooseEntry("host"); middle.Start();
        var held = 1L;
        middle.Advance(held, host);
        for (var s = 0; s < Steps / 2; s++) { held += 2; middle.MoveTo(held); foreach (var evt in set.Steps[middle.ActiveSteps().First()].Since) middle.Observe(set.Events[evt].Id); middle.Evaluate(host); }
        Assert.Equal(Tips, middle.VisibleTips().Count);
        var ordinary = Time(15, () => middle.Advance(held += 1, host));
        long allocated = 0;
        for (var i = 0; i < 5; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            middle.Advance(held += 1, host);
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }
        allocated /= 5;
        var observe = Time(15, () => middle.Observe("e050"));
        byte[] bytes = [];
        var write = Time(5, () => bytes = TutorialSave.Write(middle));
        var open = Time(5, () => TutorialSave.Open(set, bytes));
        var digest = Time(5, () => middle.StateDigest());
        csv.Append("load document,").Append(Ms(load)).Append(',').Append(Ms(load)).Append(',').Append(Ms(load)).Append(",ms,1\n");
        csv.Append("first evaluation,").Append(Ms(first)).Append(',').Append(Ms(first)).Append(',').Append(Ms(first)).Append(",ms,1\n");
        csv.Append("whole walk of 200 steps,").Append(Ms(walk)).Append(',').Append(Ms(walk)).Append(',').Append(Ms(walk)).Append(",ms,1\n");
        csv.Append("ordinary evaluation (50 tips showing),").Append(Ms(ordinary.Median)).Append(',').Append(Ms(ordinary.Min)).Append(',').Append(Ms(ordinary.Max)).Append(",ms,15\n");
        csv.Append("allocation of an evaluation,").Append(allocated).Append(',').Append(allocated).Append(',').Append(allocated).Append(",bytes,5\n");
        csv.Append("one observation,").Append(Ms(observe.Median)).Append(',').Append(Ms(observe.Min)).Append(',').Append(Ms(observe.Max)).Append(",ms,15\n");
        csv.Append("save write,").Append(Ms(write.Median)).Append(',').Append(Ms(write.Min)).Append(',').Append(Ms(write.Max)).Append(",ms,5\n");
        csv.Append("save open,").Append(Ms(open.Median)).Append(',').Append(Ms(open.Min)).Append(',').Append(Ms(open.Max)).Append(",ms,5\n");
        csv.Append("state digest,").Append(Ms(digest.Median)).Append(',').Append(Ms(digest.Min)).Append(',').Append(Ms(digest.Max)).Append(",ms,5\n");
        csv.Append("save size,").Append(bytes.Length / 1024).Append(',').Append(bytes.Length / 1024).Append(',').Append(bytes.Length / 1024).Append(",KB,1\n");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "scale-results.csv"), csv.ToString());

        BoundsTests.Invariants(middle);
        Assert.True(ordinary.Median < Budgets.EvaluateMs, "an evaluation took " + Ms(ordinary.Median) + " ms (median of 15)");
        Assert.True(first < Budgets.FirstMs, "the first evaluation took " + Ms(first) + " ms");
        Assert.True(allocated < Budgets.EvaluateBytes, "an evaluation allocated " + allocated + " bytes");
        Assert.True(observe.Median < Budgets.ObserveMs, "an observation took " + Ms(observe.Median) + " ms");
        Assert.True(write.Median < Budgets.WriteMs, "a save took " + Ms(write.Median) + " ms");
        Assert.True(open.Median < Budgets.OpenMs, "opening a save took " + Ms(open.Median) + " ms");
        Assert.Equal(middle.StateDigest(), TutorialSave.Open(set, bytes).StateDigest());
    }
}

/// <summary>The budgets of <see cref="ScaleTests"/>, in one place so the document and the test cannot disagree.</summary>
internal static class Budgets
{
    public const double EvaluateMs = 1;
    public const double FirstMs = 50;
    public const long EvaluateBytes = 64 * 1024;
    public const double ObserveMs = 0.1;
    public const double WriteMs = 10;
    public const double OpenMs = 20;
}
