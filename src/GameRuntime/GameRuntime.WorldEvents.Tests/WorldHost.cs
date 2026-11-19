using System.Text.RegularExpressions;
using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;
using Xunit;

namespace GameRuntime.WorldEvents.Tests;

internal sealed class Reporter : IRuntimeReporter
{
    public List<string> Reports { get; } = new();
    public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
}

/// <summary>The documents the tests use: the small one (also the engine tests') and the valley sample.</summary>
internal static class Samples
{
    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", name));

    public static readonly string TinyText = Read("tiny.world.json");
    public static readonly WorldSet Tiny = WorldLoader.Load(TinyText);
    public static readonly WorldSet Valley = WorldLoader.Load(Read("valley.world.json"));

    public static WorldSet Edited(Action<System.Text.Json.Nodes.JsonObject> edit)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(TinyText)!.AsObject();
        edit(root);
        return WorldLoader.Load(root.ToJsonString());
    }

    /// <summary>The valley with nothing happening by itself: no hazard has a rate, no card can be drawn, no disease seeds. What a test asks for is all that happens.</summary>
    public static WorldSet Calm()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(Read("valley.world.json"))!.AsObject();
        foreach (var hazard in root["hazards"]!.AsArray()) hazard!["ratePpm"] = 0;
        foreach (var card in root["cards"]!.AsArray()) card!["weight"] = 0;
        foreach (var disease in root["diseases"]!.AsArray()) { disease!["seedPpm"] = 0; disease["seedCases"] = 0; }
        return WorldLoader.Load(root.ToJsonString());
    }

    public static Dictionary<string, FactValue> Alert(long value = 0) => new(StringComparer.Ordinal) { ["ext.alert"] = FactValue.From(value) };

    public static string Code(Action action) => Assert.Throws<EngineRefusedException>(action).Code;
}

/// <summary>A session with a world component, a channel registry and a record of everything the component raised.</summary>
internal sealed class WorldHost
{
    public const int TicksPerSecond = 50;
    public const long TickMicroseconds = 1_000_000 / TicksPerSecond;

    public Reporter Faults { get; } = new();
    public RuntimeSession Session { get; }
    public WorldChannels Channels { get; }
    public WorldComponent Component { get; }
    public WorldRuntime Runtime { get; }
    public WorldTranscript Transcript { get; }
    public WorldJournal Journal { get; } = new();
    public Dictionary<string, FactValue> HostFacts { get; } = Samples.Alert();
    public List<object> All { get; } = new();
    private long _requests;

    public WorldHost(WorldSet? set = null, int maxEvents = 2000, string channel = "", bool journal = false, WorldRuntime? runtime = null, long startTick = 0,
        IReadOnlyList<(IGameComponent Component, int Order)>? extra = null, IWorldClock? clock = null, int maxOwed = WorldComponent.DefaultMaxOwed, int maxSteps = WorldComponent.DefaultMaxStepsPerUpdate)
    {
        Runtime = runtime ?? WorldRuntime.Create(set ?? Samples.Tiny);
        Session = new RuntimeSession(new RuntimeClock(TicksPerSecond, 8, startTick), Faults);
        Channels = new WorldChannels();
        Channels.Install(Session);
        Component = new WorldComponent(Runtime, clock ?? new TickWorldClock(Runtime.Set), () => HostFacts, Channels, channel: channel, maxEvents: maxEvents, journal: journal ? Journal : null, maxOwed: maxOwed, maxStepsPerUpdate: maxSteps);
        Session.AddComponent(Component, 10);
        foreach (var (component, order) in extra ?? []) Session.AddComponent(component, order);
        Transcript = new WorldTranscript(Session);
        Session.Subscribe<WorldApplied>(e => All.Add(e));
        Session.Subscribe<WorldRefused>(e => All.Add(e));
        Session.Subscribe<WorldWeatherAnswered>(e => All.Add(e));
        Session.Subscribe<WorldDiseaseAnswered>(e => All.Add(e));
        Session.Subscribe<WorldSeasonChanged>(e => All.Add(e));
        Session.Subscribe<WorldRegimeChanged>(e => All.Add(e));
        Session.Subscribe<WorldWeatherChanged>(e => All.Add(e));
        Session.Subscribe<WorldHazardChanged>(e => All.Add(e));
        Session.Subscribe<WorldOutbreakChanged>(e => All.Add(e));
        Session.Subscribe<WorldCardPlayed>(e => All.Add(e));
        Session.Subscribe<WorldDirectorChanged>(e => All.Add(e));
        Session.Subscribe<WorldEffectIssued>(e => All.Add(e));
        Session.Subscribe<WorldInterventionChanged>(e => All.Add(e));
        Session.Init();
    }

    public string NextId() => "r" + ++_requests;

    public void Tick(int ticks = 1)
    {
        for (var i = 0; i < ticks; i++)
            if (Session.Update(TickMicroseconds) != 1) throw new InvalidOperationException("the session did not run (state " + Session.State + "): " + string.Join("; ", Faults.Reports));
    }

    public IEnumerable<T> Of<T>() where T : struct => All.OfType<T>();

    /// <summary>Submits a request; the component answers it in its next Update.</summary>
    public string Ask(string kind, string a = "", string b = "", string c = "", long number = 1, string channel = "")
    {
        var id = NextId();
        Session.Submit(new WorldRequested(id, kind, a, b, c, number, channel));
        return id;
    }

    /// <summary>The transcript lines with the tick taken out, so two runs that raise the same events at different ticks can be compared.</summary>
    public IReadOnlyList<string> LinesWithoutTicks() => Transcript.Lines.Select(l => Regex.Replace(l, @"Tick=\d+,?", "")).ToList();
}
