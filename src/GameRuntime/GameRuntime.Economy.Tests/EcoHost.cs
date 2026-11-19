using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EconomyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Economy.Tests;

internal sealed class Reporter : IRuntimeReporter
{
    public List<string> Reports { get; } = new();
    public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
}

/// <summary>The sample documents the tests use.</summary>
internal static class Samples
{
    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", name));

    public static readonly EconomySet Town = EconomyLoader.Load(Read("town.economy.json"));

    /// <summary>The town with a period of one step and a day of one step: with a clock of one tick a step, every Update closes a period.</summary>
    public static EconomySet Fast(Action<JsonObject>? edit = null) => EconomyLoader.Load(FastText(edit));

    public static string FastText(Action<JsonObject>? edit = null) => Edit.Doc(root =>
    {
        root["time"]!["stepsPerDay"] = 1;
        root["time"]!["periodSteps"] = 1;
        edit?.Invoke(root);
    });

    public static Dictionary<string, FactValue> Host(string season = "spring", int plague = 0) => new(StringComparer.Ordinal)
    {
        ["cal.season"] = FactValue.From(season), ["story.plague"] = FactValue.From(plague),
    };

    public static string Code(Action action) => Assert.Throws<EngineRefusedException>(action).Code;

}

/// <summary>A session with an economy component, a channel registry and a record of everything the component raised.</summary>
internal sealed class EcoHost
{
    public const int TicksPerSecond = 50;
    public const long TickMicroseconds = 1_000_000 / TicksPerSecond;

    public Reporter Faults { get; } = new();
    public RuntimeSession Session { get; }
    public EconomyChannels Channels { get; }
    public EconomyComponent Component { get; }
    public EconomyRuntime Runtime { get; }
    public EconomyTranscript Transcript { get; }
    public EconomyJournal Journal { get; } = new();
    public Dictionary<string, FactValue> HostFacts { get; } = Samples.Host();
    public List<object> All { get; } = new();
    private long _requests;

    public EcoHost(EconomySet? set = null, long ticksPerStep = 1, int maxEvents = 2000, string channel = "", bool journal = false, EconomyRuntime? runtime = null, long startTick = 0,
        IReadOnlyList<(IGameComponent Component, int Order)>? extra = null, IEconomyClock? clock = null, int maxOwed = EconomyComponent.DefaultMaxOwed, Action<RuntimeSession>? declare = null)
    {
        Runtime = runtime ?? EconomyRuntime.Create(set ?? Samples.Fast());
        Session = new RuntimeSession(new RuntimeClock(TicksPerSecond, 8, startTick), Faults);
        declare?.Invoke(Session);
        Channels = new EconomyChannels();
        Channels.Install(Session);
        Component = new EconomyComponent(Runtime, clock ?? new TickEconomyClock(ticksPerStep), () => HostFacts, Channels, channel: channel, maxEvents: maxEvents, journal: journal ? Journal : null, maxOwed: maxOwed);
        Session.AddComponent(Component, 10);
        foreach (var (component, order) in extra ?? []) Session.AddComponent(component, order);
        Transcript = new EconomyTranscript(Session);
        Session.Subscribe<EconomyApplied>(e => All.Add(e));
        Session.Subscribe<EconomyRefused>(e => All.Add(e));
        Session.Subscribe<EconomyTradeExecuted>(e => All.Add(e));
        Session.Subscribe<EconomyQuoted>(e => All.Add(e));
        Session.Subscribe<EconomyPriceAnswered>(e => All.Add(e));
        Session.Subscribe<EconomyStepped>(e => All.Add(e));
        Session.Subscribe<EconomyRateChanged>(e => All.Add(e));
        Session.Subscribe<EconomyShockChanged>(e => All.Add(e));
        Session.Subscribe<EconomyShipmentArrived>(e => All.Add(e));
        Session.Subscribe<EconomyBandChanged>(e => All.Add(e));
        Session.Subscribe<EconomyShortageChanged>(e => All.Add(e));
        Session.Subscribe<EconomyShipmentLaunched>(e => All.Add(e));
        Session.Subscribe<EconomyRouteBlocked>(e => All.Add(e));
        Session.Subscribe<EconomySettled>(e => All.Add(e));
        Session.Subscribe<EconomyLoanChanged>(e => All.Add(e));
        Session.Subscribe<EconomyProductionLimited>(e => All.Add(e));
        Session.Subscribe<EconomyRecipeUnlocked>(e => All.Add(e));
        Session.Init();
    }

    public string NextId() => "r" + ++_requests;

    public void Tick(int ticks = 1)
    {
        for (var i = 0; i < ticks; i++)
            if (Session.Update(TickMicroseconds) != 1) throw new InvalidOperationException("the session did not run (state " + Session.State + "): " + string.Join("; ", Faults.Reports));
    }

    public IEnumerable<T> Of<T>() where T : struct => All.OfType<T>();

    /// <summary>The transcript lines with the tick taken out, so two runs that raise the same events at different ticks can be compared.</summary>
    public IReadOnlyList<string> LinesWithoutTicks() => Transcript.Lines.Select(l => Regex.Replace(l, @"Tick=\d+,?", "")).ToList();

    public void Trade(string market, string good, long quantity, string trader = "player", string channel = "") => Session.Submit(new EconomyTradeRequested(NextId(), trader, market, good, quantity, channel));
}

/// <summary>Edits of the sample economy document, for the tests that need a document that is wrong or different in one place.</summary>
internal static class Edit
{
    public static string Doc(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(Samples.Read("town.economy.json"))!.AsObject();
        edit(root);
        return root.ToJsonString();
    }
}
