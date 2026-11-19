using System.Diagnostics;
using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// What a session with the dialogue channel registry and one dialogue component costs per tick when nobody is talking, and what the new importer code costs. The bounds are loose (a hundred times what was measured
/// on the development machine) so they fail on a mistake like a per-tick allocation or a quadratic walk, not on a slower machine; the measured numbers are in docs/DIALOGUE_ENGINE.md section 10.
/// </summary>
public sealed class DialogueTickCostTests
{
    private sealed class Cast : ISpeakerCatalog
    {
        public bool HasSpeaker(string id) => id == "cmdr";
    }

    private sealed class Quiet : IRuntimeReporter
    {
        public void Report(string code, string message, Exception? exception) => throw new InvalidOperationException(code + ": " + message);
    }

    private const string Document = """
    { "format": 3, "id": "cost", "variables": [], "externalFacts": [], "speakers": [ { "id": "cmdr", "portraits": ["calm"] } ], "quests": [],
      "conversations": [ { "id": "chat", "start": "a", "nodes": [ { "id": "a", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.a", "next": "b" }, { "id": "b", "kind": "end", "result": "done" } ] } ] }
    """;

    internal static string Measure(bool dialogue, int repeats, int ticks, out double bestTickNanoseconds, out long allocatedPerTick)
    {
        var cast = new Cast();
        var session = new RuntimeSession(new RuntimeClock(30, 1_000_000), new Quiet());
        if (dialogue)
        {
            var channels = new DialogueChannels();
            channels.Install(session);
            session.AddComponent(new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(Document, cast), 1), cast, () => new Dictionary<string, FactValue>(), channels), 100);
        }
        session.Init();
        var one = (RuntimeClock.MicrosecondsPerSecond + 29) / 30;
        for (var i = 0; i < 2000; i++) session.Update(one);   // warm up
        var times = new List<double>();
        allocatedPerTick = 0;
        for (var r = 0; r < repeats; r++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < ticks; i++) session.Update(one);
            watch.Stop();
            allocatedPerTick = (GC.GetAllocatedBytesForCurrentThread() - before) / ticks;
            times.Add(watch.Elapsed.TotalMilliseconds * 1_000_000 / ticks);
        }
        bestTickNanoseconds = times.Min();
        return string.Join(", ", times.Select(t => t.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " ns/tick"));
    }

    [Fact]
    public void AnIdleSessionWithTheRegistryAndOneDialogueComponentCostsLittlePerTickAndAllocatesNothingThatGrows()
    {
        var report = Measure(true, 5, 200_000, out var best, out var allocated);
        var without = Measure(false, 5, 200_000, out var bare, out var bareAllocated);
        Assert.True(best < 50_000, "an idle tick took " + report);
        Assert.True(allocated < 20_000, "an idle tick allocated " + allocated + " bytes");
        Console.Error.WriteLine("idle tick with registry and component: " + report + "; allocated per tick: " + allocated + " bytes. Session alone: " + without + "; allocated per tick: " + bareAllocated + " bytes");
    }
}
