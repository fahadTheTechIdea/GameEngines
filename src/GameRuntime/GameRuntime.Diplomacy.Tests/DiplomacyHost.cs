using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DiplomacyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Diplomacy.Tests;

internal sealed class Reporter : IRuntimeReporter
{
    public List<string> Reports { get; } = new();
    public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
}

/// <summary>The documents the tests use: the isles sample, and a copy with nothing happening by itself that a test turns things on in.</summary>
internal static class Samples
{
    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", name));

    public static readonly string IslesText = Read("isles.diplomacy.json");
    public static readonly DipSet Isles = DipLoader.Load(IslesText);

    public static JsonObject Root() => JsonNode.Parse(IslesText)!.AsObject();

    public static JsonObject Realm(JsonObject root, string id) => root["realms"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);
    public static JsonObject Type(JsonObject root, string id) => root["types"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);
    public static JsonObject Rules(JsonObject root, string name) => root["rules"]![name]!.AsObject();

    /// <summary>The isles with no succession by itself, every offer answered at once and certain, and a quiet balance: what a test asks for is all that happens.</summary>
    public static DipSet Calm(Action<JsonObject>? edit = null)
    {
        var root = Root();
        foreach (var realm in root["realms"]!.AsArray()) { realm!["successionPpm"] = 0; realm["control"] = "ai"; }
        Rules(root, "balance")["interval"] = 100000;
        foreach (var type in root["types"]!.AsArray())
        {
            type!["considerDays"] = Math.Min(type["considerDays"]!.GetValue<int>(), 1);
            if (type["consent"]!.GetValue<string>() == "mutual" && type["id"]!.GetValue<string>() != "peace") { type["bias"] = 5000; type["weights"] = new JsonObject(); type["modifiers"] = new JsonArray(); }
        }
        edit?.Invoke(root);
        return DipLoader.Load(root.ToJsonString());
    }

    public static Dictionary<string, FactValue> Crisis(long value = 2) => new(StringComparer.Ordinal) { ["game.crisis"] = FactValue.From(value) };

    public static string Code(Action action) => Assert.Throws<EngineRefusedException>(action).Code;
}

/// <summary>A session with a diplomacy component, a channel registry and a record of everything the component raised.</summary>
internal sealed class DiplomacyHost
{
    public const int TicksPerSecond = 50;
    public const long TickMicroseconds = 1_000_000 / TicksPerSecond;

    public Reporter Faults { get; } = new();
    public RuntimeSession Session { get; }
    public DiplomacyChannels Channels { get; }
    public DiplomacyComponent Component { get; }
    public DipRuntime Runtime { get; }
    public DiplomacyTranscript Transcript { get; }
    public DiplomacyJournal Journal { get; } = new();
    public Dictionary<string, FactValue> HostFacts { get; } = Samples.Crisis();
    public List<object> All { get; } = new();
    private long _requests;

    public DiplomacyHost(DipSet? set = null, int maxEvents = 2000, string channel = "", bool journal = false, DipRuntime? runtime = null, long startTick = 0,
        IReadOnlyList<(IGameComponent Component, int Order)>? extra = null, IDiplomacyClock? clock = null, int maxOwed = DiplomacyComponent.DefaultMaxOwed, int maxSteps = DiplomacyComponent.DefaultMaxStepsPerUpdate)
    {
        Runtime = runtime ?? DipRuntime.Create(set ?? Samples.Calm());
        Session = new RuntimeSession(new RuntimeClock(TicksPerSecond, 8, startTick), Faults);
        Channels = new DiplomacyChannels();
        Channels.Install(Session);
        Component = new DiplomacyComponent(Runtime, clock ?? new TickDiplomacyClock(Runtime.Set), () => HostFacts, Channels, channel: channel, maxEvents: maxEvents, journal: journal ? Journal : null, maxOwed: maxOwed, maxStepsPerUpdate: maxSteps);
        Session.AddComponent(Component, 10);
        foreach (var (component, order) in extra ?? []) Session.AddComponent(component, order);
        Transcript = new DiplomacyTranscript(Session);
        Session.Subscribe<DiplomacyApplied>(e => All.Add(e));
        Session.Subscribe<DiplomacyRefused>(e => All.Add(e));
        Session.Subscribe<DiplomacyEvaluated>(e => All.Add(e));
        Session.Subscribe<DiplomacyStandingAnswered>(e => All.Add(e));
        Session.Subscribe<DiplomacyEstimated>(e => All.Add(e));
        Session.Subscribe<DiplomacySeasonChanged>(e => All.Add(e));
        Session.Subscribe<DiplomacyProposalChanged>(e => All.Add(e));
        Session.Subscribe<DiplomacyTreatyChanged>(e => All.Add(e));
        Session.Subscribe<DiplomacyObligationChanged>(e => All.Add(e));
        Session.Subscribe<DiplomacyWarChanged>(e => All.Add(e));
        Session.Subscribe<DiplomacyCoalitionChanged>(e => All.Add(e));
        Session.Subscribe<DiplomacyMissionChanged>(e => All.Add(e));
        Session.Subscribe<DiplomacyAgentChanged>(e => All.Add(e));
        Session.Subscribe<DiplomacyIntelChanged>(e => All.Add(e));
        Session.Subscribe<DiplomacyPoliticsChanged>(e => All.Add(e));
        Session.Subscribe<DiplomacyGrantChanged>(e => All.Add(e));
        Session.Subscribe<DiplomacyIncidentLogged>(e => All.Add(e));
        Session.Subscribe<DiplomacyEffectIssued>(e => All.Add(e));
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
    public string Ask(string kind, string a = "", string b = "", string c = "", long number = 0, long number2 = 0, string channel = "")
    {
        var id = NextId();
        Session.Submit(new DiplomacyRequested(id, kind, a, b, c, number, number2, channel));
        return id;
    }

    /// <summary>The transcript lines with the tick taken out, so two runs that raise the same events at different ticks can be compared.</summary>
    public IReadOnlyList<string> LinesWithoutTicks() => Transcript.Lines.Select(l => Regex.Replace(l, @"Tick=\d+,?", "")).ToList();
}
