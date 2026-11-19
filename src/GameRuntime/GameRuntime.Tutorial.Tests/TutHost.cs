using System.Text.RegularExpressions;
using EngineCommon;
using ScenarioEngine.Contracts;
using TutorialEngine;
using Xunit;

namespace GameRuntime.Tutorial.Tests;

internal sealed class Reporter : IRuntimeReporter
{
    public List<string> Reports { get; } = new();
    public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
}

/// <summary>The documents the tests use: the sample, and a small document whose steps and tips are a few steps long.</summary>
internal static class Samples
{
    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", name));

    public static readonly TutorialSet Valley = TutorialLoader.Load(Read("valley.tutorial.json"));

    public const string SmallText = """
    { "format": 1, "id": "small", "time": { "stepsPerDay": 100 }, "limits": { "maxActiveSteps": 1, "maxVisibleTips": 2, "tipGap": 0 },
      "facts": [ { "id": "flag", "type": "bool" } ],
      "events": [ { "id": "e1" }, { "id": "e2" }, { "id": "e3" } ],
      "inputs": [ { "action": "press.one", "event": "e1" } ],
      "skills": [ { "id": "sk", "key": "k.sk", "mastered": 3, "evidence": [ { "event": "e1", "points": 1 } ] } ],
      "journey": { "languages": [ "en", "fr" ], "defaultLanguage": "en",
        "pages": [ { "id": "p1", "key": "k.p1", "body": "k.p1b", "art": "k.p1a" }, { "id": "p2", "key": "k.p2", "body": "k.p2b", "art": "k.p2a" } ], "entries": [ "host", "join" ] },
      "steps": [
        { "id": "s1", "key": "k.s1", "body": "k.s1b", "target": "t1", "motion": "pulse", "skippable": true, "skill": "sk", "requires": [], "requiresAny": [],
          "complete": { "fact": "event.e1.since_step", "op": "gte", "value": 1 }, "hints": [ { "idle": 5, "kind": "nudge", "key": "k.h1" }, { "idle": 10, "kind": "explain", "key": "k.h2" } ] },
        { "id": "s2", "key": "k.s2", "body": "k.s2b", "target": "", "motion": "none", "skippable": false, "skill": "", "requires": [ "s1" ], "requiresAny": [],
          "complete": { "fact": "event.e2.since_step", "op": "gte", "value": 1 }, "hints": [] },
        { "id": "s3", "key": "k.s3", "body": "k.s3b", "target": "", "motion": "none", "skippable": true, "skill": "", "requires": [ "s2" ], "requiresAny": [],
          "complete": { "fact": "flag", "op": "eq", "value": true }, "hints": [] } ],
      "tips": [
        { "id": "t1", "key": "k.t1", "when": { "fact": "flag", "op": "eq", "value": true }, "cooldown": 5, "maxShows": 2, "duration": 3, "priority": 1, "skill": "", "afterStep": "", "motion": "pulse" } ] }
    """;

    public static readonly TutorialSet Small = TutorialLoader.Load(SmallText);

    public static Dictionary<string, FactValue> Flag(bool value = false) => new(StringComparer.Ordinal) { ["flag"] = FactValue.From(value) };

    public static Dictionary<string, FactValue> ValleyHost(bool gamepad = false, bool menu = false, bool paused = false, long population = 0) => new(StringComparer.Ordinal)
    {
        ["input.gamepad"] = FactValue.From(gamepad), ["ui.menu_open"] = FactValue.From(menu), ["game.paused"] = FactValue.From(paused), ["game.population"] = FactValue.From(population),
    };

    public static string Code(Action action) => Assert.Throws<EngineRefusedException>(action).Code;

    /// <summary>A runtime with the journey done, ready for the tutorial to be started.</summary>
    public static TutorialRuntime Done(TutorialSet set, long step = 0)
    {
        var runtime = TutorialRuntime.Create(set, step);
        runtime.SelectLanguage("en");
        while (runtime.Journey.State == JourneyState.Story) runtime.StoryNext();
        runtime.ChooseEntry(set.Journey.Entries[0]);
        return runtime;
    }
}

/// <summary>A session with a tutorial component, a channel registry and a record of everything the component raised.</summary>
internal sealed class TutHost
{
    public const int TicksPerSecond = 50;
    public const long TickMicroseconds = 1_000_000 / TicksPerSecond;

    public Reporter Faults { get; } = new();
    public RuntimeSession Session { get; }
    public TutorialChannels Channels { get; }
    public TutorialComponent Component { get; }
    public TutorialRuntime Runtime { get; }
    public TutorialTranscript Transcript { get; }
    public TutorialJournal Journal { get; } = new();
    public Dictionary<string, FactValue> HostFacts { get; } = Samples.Flag();
    public List<object> All { get; } = new();
    private long _requests;

    public TutHost(TutorialSet? set = null, int maxEvents = 2000, string channel = "", bool journal = false, TutorialRuntime? runtime = null, long startTick = 0,
        IReadOnlyList<(IGameComponent Component, int Order)>? extra = null, ITutorialClock? clock = null, int maxOwed = TutorialComponent.DefaultMaxOwed, bool start = true)
    {
        Runtime = runtime ?? Samples.Done(set ?? Samples.Small);
        if (runtime is null && start) Runtime.Start();
        Session = new RuntimeSession(new RuntimeClock(TicksPerSecond, 8, startTick), Faults);
        Channels = new TutorialChannels();
        Channels.Install(Session);
        Component = new TutorialComponent(Runtime, clock ?? new TickTutorialClock(), () => HostFacts, Channels, channel: channel, maxEvents: maxEvents, journal: journal ? Journal : null, maxOwed: maxOwed);
        Session.AddComponent(Component, 10);
        foreach (var (component, order) in extra ?? []) Session.AddComponent(component, order);
        Transcript = new TutorialTranscript(Session);
        Session.Subscribe<TutorialApplied>(e => All.Add(e));
        Session.Subscribe<TutorialRefused>(e => All.Add(e));
        Session.Subscribe<TutorialProgressAnswered>(e => All.Add(e));
        Session.Subscribe<TutorialStepChanged>(e => All.Add(e));
        Session.Subscribe<TutorialHintChanged>(e => All.Add(e));
        Session.Subscribe<TutorialTipChanged>(e => All.Add(e));
        Session.Subscribe<TutorialSkillMastered>(e => All.Add(e));
        Session.Subscribe<TutorialPhaseChanged>(e => All.Add(e));
        Session.Subscribe<TutorialJourneyChanged>(e => All.Add(e));
        Session.Subscribe<TutorialSettingChanged>(e => All.Add(e));
        Session.Subscribe<TutorialSaveNeeded>(e => All.Add(e));
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
    public string Ask(string kind, string text = "", long number = 1, string channel = "")
    {
        var id = NextId();
        Session.Submit(new TutorialRequested(id, kind, text, number, channel));
        return id;
    }

    /// <summary>The transcript lines with the tick taken out, so two runs that raise the same events at different ticks can be compared.</summary>
    public IReadOnlyList<string> LinesWithoutTicks() => Transcript.Lines.Select(l => Regex.Replace(l, @"Tick=\d+,?", "")).ToList();
}
