using System.Diagnostics;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>
/// Cost (R17): 1,000 actors with 20 edges each. The budgets are set from the measurements recorded in docs/RELATIONSHIP_ENGINE.md section 10.3 (median of 15 repeats on this machine, with a margin for a loaded
/// machine); a budget is a test only for the things that must stay cheap in a game: one ordinary step, the first step of a day, one interaction, and the allocation of a step.
/// </summary>
public sealed class ScaleTests
{
    private const int Actors = 1000;
    private const int EdgesEach = 20;

    internal static RelationshipRuntime Big(Action<System.Text.Json.Nodes.JsonObject>? edit = null)
    {
        var set = RelationshipLoader.Load(Edit.Doc(r => { r["limits"]!["maxActors"] = 2000; r["limits"]!["maxEdgesPerActor"] = 24; edit?.Invoke(r); }));
        var runtime = RelationshipRuntime.Create(set);
        var traits = new[] { "kind", "grumpy", "glutton", "sociable", "stoic" };
        for (var i = 0; i < Actors; i++) runtime.AddActor("p" + i.ToString("0000"), i % 3 == 0 ? [traits[i % 5]] : i % 3 == 1 ? [traits[i % 5], traits[(i + 2) % 5]] : []);
        var activities = new[] { "", "eat", "sleep", "idle", "celebrate", "worship", "work" };
        for (var i = 0; i < Actors; i++)
        {
            runtime.SetActivity("p" + i.ToString("0000"), activities[i % activities.Length]);
            for (var k = 1; k <= EdgesEach; k++)
            {
                var other = "p" + ((i + k * 37) % Actors).ToString("0000");
                runtime.AdjustAxis("p" + i.ToString("0000"), other, k % 3 == 0 ? "trust" : "affinity", (i * 31 + k * 17) % 600 - 200);
                if (k % 5 == 0) runtime.Remember("p" + i.ToString("0000"), other, k % 2 == 0 ? "was-gifted" : "was-insulted", 1000);
            }
        }
        return runtime;
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

    [Fact]
    public void ThousandActorsWithTwentyEdgesEachStepWithinTheBudgetAndKeepTheirLimits()
    {
        var runtime = Big();
        Assert.Equal(Actors * EdgesEach, runtime.EdgeCount);
        var host = Fixture.Host(season: "winter", raining: true);
        // Warm up on a day, then measure ordinary steps (not the first of a day) and days.
        runtime.Advance(runtime.Step + 24, host);
        var ordinary = Time(15, () => runtime.Advance(runtime.Step + 1, host));
        while ((runtime.Step + 1) % 24 != 0) runtime.Advance(runtime.Step + 1, host);
        var boundary = new List<double>();
        long boundaryBytes = 0;
        for (var repeat = 0; repeat < 15; repeat++)
        {
            while ((runtime.Step + 1) % 24 != 0) runtime.Advance(runtime.Step + 1, host);
            var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            runtime.Advance(runtime.Step + 1, host);
            boundary.Add(watch.Elapsed.TotalMilliseconds);
            boundaryBytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
        }
        boundary.Sort();
        var ordinaryBefore = GC.GetAllocatedBytesForCurrentThread();
        runtime.Advance(runtime.Step + 1, host);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - ordinaryBefore;
        var interaction = Time(15, () =>
        {
            runtime.Advance(runtime.Step + 12, host);
            for (var i = 0; i < 100; i++) runtime.Perform(new InteractionRequest("share-meal", "p" + (i * 7).ToString("0000"), "p" + ((i * 7 + 1) % Actors).ToString("0000"), [], ""), Fixture.Host());
        });
        Console.WriteLine($"scale: ordinary step median {ordinary.Median:F2} ms (min {ordinary.Min:F2}, max {ordinary.Max:F2}), {allocated} bytes allocated; first step of a day median {boundary[boundary.Count / 2]:F1} ms (min {boundary[0]:F1}, max {boundary[^1]:F1}), {boundaryBytes} bytes allocated; 100 share-meals + 12 steps median {interaction.Median:F1} ms");
        Assert.True(ordinary.Median < 15, "an ordinary step of 1,000 actors took a median of " + ordinary.Median + " ms");
        // With communities, standing and balance on (the sample document) the first step of a day is about 40 ms in Release on this machine (docs/RELATIONSHIP_ENGINE.md section 11); Debug is several times slower and a loaded machine slower again.
#if DEBUG
        Assert.True(boundary[boundary.Count / 2] < 1500, "the first step of a day took a median of " + boundary[boundary.Count / 2] + " ms");
#else
        Assert.True(boundary[boundary.Count / 2] < 150, "the first step of a day took a median of " + boundary[boundary.Count / 2] + " ms");
#endif
        Assert.True(allocated < 400_000 && boundaryBytes < 16_000_000, "an ordinary step allocated " + allocated + " bytes and a boundary step " + boundaryBytes);
        foreach (var actor in runtime.Actors.Values) Assert.True(actor.Edges.Count <= 24);
    }

    [Fact]
    public void ASaveOfThousandActorsWritesAndOpensWithinTheBudget()
    {
        var runtime = Big();
        runtime.Advance(runtime.Step + 24, Fixture.Spring);
        byte[] bytes = [];
        var write = Time(5, () => bytes = RelationshipSave.Write(runtime));
        RelationshipRuntime? opened = null;
        var open = Time(5, () => opened = RelationshipSave.Open(runtime.Set, bytes));
        Console.WriteLine($"save: {bytes.Length / 1024} KB; write median {write.Median:F0} ms (min {write.Min:F0}, max {write.Max:F0}); open median {open.Median:F0} ms (min {open.Min:F0}, max {open.Max:F0})");
        Assert.Equal(runtime.StateDigest(), opened!.StateDigest());
        Assert.True(write.Median < 2000 && open.Median < 3000, $"write {write.Median} ms, open {open.Median} ms");
    }
}
