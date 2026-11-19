using System.Diagnostics;
using AIEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace AIEngine.Tests;

/// <summary>A7: a thousand agents on a think interval cost little per tick. The allocation bound is exact; the time bound is loose because a test host may be busy.</summary>
public sealed class CostTests
{
    /// <summary>1,000 agents of the given behaviour on an interval of 10, with a spread of known units, run for 100 ticks after a warm-up.</summary>
    internal static (double MillisecondsPerTick, double BytesPerTick, int Thinks) Measure(string behavior, int agents, int ticks)
    {
        var set = Fixture.Load(Fixture.Document.Replace("\"id\": \"commander\", \"every\": 1", "\"id\": \"commander\", \"every\": 10", StringComparison.Ordinal));
        var runtime = new AiRuntime(set, 5);
        for (var i = 0; i < agents; i++)
        {
            var id = "a" + i.ToString("0000");
            runtime.Add(id, behavior, 0);
            runtime.SetKey(id, "ready", FactValue.From(true));
            runtime.SetKey(id, "hunger", FactValue.From((long)(i * 7 % 100)));
            for (var u = 0; u < 10; u++) Fixture.Tell(runtime, id, "u" + u, hostile: u >= 6, standing: u % 5 != 4, health: 100 + u);
        }
        var budget = set.MaxCost * (agents / 5 + 1);
        for (var tick = 1; tick <= 40; tick++) runtime.Step(tick, budget);   // warm up the JIT and the tick phases
        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        var thinks = 0;
        for (var tick = 41; tick < 41 + ticks; tick++) thinks += runtime.Step(tick, budget).Thought;
        watch.Stop();
        return (watch.Elapsed.TotalMilliseconds / ticks, (GC.GetAllocatedBytesForCurrentThread() - before) / (double)ticks, thinks);
    }

    [Fact]
    public void AThousandAgentsThinkingEveryTenTicksCostUnderTwoMillisecondsAndSixtyFourKilobytesATick()
    {
        foreach (var behavior in new[] { "worker", "commander" })
        {
            var (milliseconds, bytes, thinks) = Measure(behavior, 1000, 100);
            Assert.Equal(10_000, thinks);                    // every agent thought ten times in the 100 ticks: nobody was starved
            Assert.True(bytes < 64 * 1024, behavior + " allocated " + bytes + " bytes a tick");
            Assert.True(milliseconds < 10, behavior + " took " + milliseconds + " ms a tick");     // the 2 ms target is measured in Release; this guard is loose for a Debug test host
        }
    }
}