using System.Text.RegularExpressions;
using RelationshipEngine;
using ScenarioEngine.Contracts;

namespace GameRuntime.Relationship.Tests;

internal sealed class Reporter : IRuntimeReporter
{
    public List<string> Reports { get; } = new();
    public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
}

/// <summary>The sample documents the tests use.</summary>
internal static class Samples
{
    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", name));

    /// <summary>The sample as shipped: the day's work spread over its first five steps.</summary>
    public static readonly RelationshipSet Shipped = RelationshipLoader.Load(Read("village.relationship.json"));

    /// <summary>The sample with the atomic schedule, which most tests build on because they test what the day's passes decide at a known step; the spread schedule has its own tests (SocialReplayTests, the engine's DayScheduleTests).</summary>
    public static readonly string VillageText = Read("village.relationship.json").Replace("\"schedule\": \"spread\"", "\"schedule\": \"atomic\"", StringComparison.Ordinal);

    public static readonly RelationshipSet Village = RelationshipLoader.Load(VillageText);

    public static IReadOnlyDictionary<string, FactValue> Spring => Host();

    public static Dictionary<string, FactValue> Host(string season = "spring", bool day = true, bool holiday = false, bool raining = false) => new(StringComparer.Ordinal)
    {
        ["cal.season"] = FactValue.From(season), ["cal.is_day"] = FactValue.From(day), ["cal.is_holiday"] = FactValue.From(holiday), ["weather.raining"] = FactValue.From(raining),
    };
}

/// <summary>A session with a relationship component, a channel registry and a record of everything the component raised.</summary>
internal sealed class RelHost
{
    public const int TicksPerSecond = 50;
    public const long TickMicroseconds = 1_000_000 / TicksPerSecond;

    public Reporter Faults { get; } = new();
    public RuntimeSession Session { get; }
    public RelationshipChannels Channels { get; }
    public RelationshipComponent Component { get; }
    public RelationshipRuntime Runtime { get; }
    public RelationshipTranscript Transcript { get; }
    public RelationshipJournal Journal { get; } = new();
    public Dictionary<string, FactValue> HostFacts { get; } = Samples.Host();
    public List<object> All { get; } = new();
    private long _requests;

    public RelHost(RelationshipSet? set = null, long ticksPerStep = 50, int maxEvents = 2000, string channel = "", bool journal = false, RelationshipRuntime? runtime = null, long startTick = 0,
        IReadOnlyList<(IGameComponent Component, int Order)>? extra = null, IRelationshipClock? clock = null)
    {
        Runtime = runtime ?? RelationshipRuntime.Create(set ?? Samples.Village);
        Session = new RuntimeSession(new RuntimeClock(TicksPerSecond, 8, startTick), Faults);
        Channels = new RelationshipChannels();
        Channels.Install(Session);
        Component = new RelationshipComponent(Runtime, clock ?? new TickRelationshipClock(ticksPerStep), () => HostFacts, Channels, channel: channel, maxEvents: maxEvents, journal: journal ? Journal : null);
        Session.AddComponent(Component, 10);
        foreach (var (component, order) in extra ?? []) Session.AddComponent(component, order);
        Transcript = new RelationshipTranscript(Session);
        Session.Subscribe<RelationshipApplied>(e => All.Add(e));
        Session.Subscribe<RelationshipRefused>(e => All.Add(e));
        Session.Subscribe<RelationshipInteractionPerformed>(e => All.Add(e));
        Session.Subscribe<RelationshipMoodAnswered>(e => All.Add(e));
        Session.Subscribe<RelationshipOpinionAnswered>(e => All.Add(e));
        Session.Subscribe<RelationshipStepped>(e => All.Add(e));
        Session.Subscribe<RelationshipMoodChanged>(e => All.Add(e));
        Session.Subscribe<RelationshipNeedCritical>(e => All.Add(e));
        Session.Subscribe<RelationshipStateChanged>(e => All.Add(e));
        Session.Subscribe<RelationshipBondChanged>(e => All.Add(e));
        Session.Subscribe<RelationshipMemoryAdded>(e => All.Add(e));
        Session.Subscribe<RelationshipMoodletAdded>(e => All.Add(e));
        Session.Subscribe<RelationshipEvicted>(e => All.Add(e));
        Session.Subscribe<RelationshipCommunityChanged>(e => All.Add(e));
        Session.Subscribe<RelationshipStandingChanged>(e => All.Add(e));
        Session.Subscribe<RelationshipBalanceShifted>(e => All.Add(e));
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

    public void Add(string actor, string traits = "", string tastes = "", string channel = "") => Session.Submit(new RelationshipActorAddRequested(NextId(), actor, traits, tastes, channel));
}

/// <summary>Edits of the sample relationship document, for the tests that need a document that is wrong or different in one place.</summary>
internal static class Edit
{
    public static string Doc(Action<System.Text.Json.Nodes.JsonObject> edit)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(Samples.VillageText)!.AsObject();
        edit(root);
        return root.ToJsonString();
    }
}
