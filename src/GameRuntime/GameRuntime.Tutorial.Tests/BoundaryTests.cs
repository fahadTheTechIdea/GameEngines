using System.Text.Json.Nodes;
using CalendarEngine;
using DialogueEngine;
using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;
using TutorialEngine;
using Xunit;

namespace GameRuntime.Tutorial.Tests;

/// <summary>The edges of the game layer's comparisons and filters. Written from the mutants of the first round that the other tests did not kill.</summary>
public sealed class BoundaryTests
{
    [Theory]
    [InlineData("R|1||observe|id|e1|1|extra")]
    [InlineData("F|1|flag=B:true|extra")]
    [InlineData("S|1|1|extra")]
    public void ALineWithAPartTooManyIsRefused(string text) => Assert.Equal(TutorialJournal.BadJournal, Samples.Code(() => TutorialJournal.Parse(text)));

    [Fact]
    public void AReplayOfNoTicksIsAllowedAndChangesNothing()
    {
        var runtime = Samples.Done(Samples.Small);
        runtime.Start();
        var before = runtime.StateDigest();
        var replay = TutorialReplay.Run(runtime, [], 7, 7, 50);
        Assert.Equal(before, replay.StateDigest);
        Assert.Equal(0, replay.Events);
        Assert.Equal(7, replay.Tick);
    }

    [Fact]
    public void TwoComponentsSharingAJournalReplayEachItsOwnRequestsOnly()
    {
        var journal = new TutorialJournal();
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var channels = new TutorialChannels();
        channels.Install(session);
        var a = new TutorialComponent(Samples.Done(Samples.Small), new TickTutorialClock(), () => Samples.Flag(), channels, "tutorial-a", "a", journal: journal);
        var b = new TutorialComponent(Samples.Done(Samples.Small), new TickTutorialClock(), () => Samples.Flag(), channels, "tutorial-b", "b", journal: journal);
        session.AddComponent(a, 10);
        session.AddComponent(b, 11);
        var transcript = new TutorialTranscript(session);
        session.Init();
        a.Runtime.Start();
        session.Submit(new TutorialRequested("x", "observe", "e1", 3, "a"));
        session.Submit(new TutorialRequested("y", "observe", "e2", 2, "b"));
        session.Update(20_000);
        session.Update(20_000);
        var entries = TutorialJournal.Parse(journal.ToText());
        Assert.Contains(entries, e => e.Channel == "a" && e.Kind == "observe");
        Assert.Contains(entries, e => e.Channel == "b" && e.Kind == "observe");
        var fresh = Samples.Done(Samples.Small);
        fresh.Start();
        var replay = TutorialReplay.Run(fresh, entries, 0, 2, 50, "tutorial-a", "a");
        Assert.Equal(a.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(0, fresh.EventCounts[1]);                                 // b's observation of e2 is not a's
        Assert.Equal(transcript.Lines.Count(l => l.Contains("Source=tutorial-a", StringComparison.Ordinal)), replay.Events);       // and b's request is not submitted to a session where nobody serves it (that would be a refusal event)
    }

    [Fact]
    public void ACalendarWithTwoTicksToAStepGivesHalfTheTicksAsSteps()
    {
        var gregorian = CalendarLoader.Load(Samples.Read("gregorian.calendar.json"));
        var root = JsonNode.Parse(Samples.Read("valley.tutorial.json"))!.AsObject();
        root["time"]!["stepsPerDay"] = 720;
        var set = TutorialLoader.Load(root.ToJsonString());
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var calendarChannels = new CalendarChannels();
        calendarChannels.Install(session);
        var tutorialChannels = new TutorialChannels();
        tutorialChannels.Install(session);
        var weather = new Dictionary<string, FactValue> { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };
        var calendar = new CalendarComponent(CalendarRuntime.Create(gregorian, 0, 1, 1), () => weather, calendarChannels);
        var component = new TutorialComponent(Samples.Done(set), TutorialCalendar.Clock(calendar, set, 0), TutorialCalendar.HostFacts(calendar, () => Samples.ValleyHost()), tutorialChannels);
        session.AddComponent(calendar, 10);
        session.AddComponent(component, 30);
        session.Init();
        for (var i = 0; i < 20; i++) session.Update(20_000);
        Assert.Equal(10, component.Runtime.Step);
    }

    [Fact]
    public void TheInstantTheTutorialStartsAtIsStepZeroNotAClockFault()
    {
        var gregorian = CalendarLoader.Load(Samples.Read("gregorian.calendar.json"));
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var calendarChannels = new CalendarChannels();
        calendarChannels.Install(session);
        var tutorialChannels = new TutorialChannels();
        tutorialChannels.Install(session);
        var weather = new Dictionary<string, FactValue> { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };
        var calendar = new CalendarComponent(CalendarRuntime.Create(gregorian, 0, 1, 1), () => weather, calendarChannels);
        var component = new TutorialComponent(Samples.Done(Samples.Valley), TutorialCalendar.Clock(calendar, Samples.Valley, 1), TutorialCalendar.HostFacts(calendar, () => Samples.ValleyHost()), tutorialChannels);
        session.AddComponent(calendar, 10);
        session.AddComponent(component, 30);
        var refused = new List<TutorialRefused>();
        session.Subscribe<TutorialRefused>(refused.Add);
        session.Init();
        session.Update(20_000);
        Assert.Empty(refused);                                                  // the calendar is at instant 1 after its first tick, which is step 0
        Assert.Equal(0, component.Runtime.Step);
    }

    // ---- the observers listen to what they were told to and nothing else ----------------------------------------------------------------

    [Fact]
    public void AConversationEndingOnAnotherDialogueChannelIsNotThisSpeakersAndOneThatEndedIsNotCountedTwice()
    {
        var set = TutorialLoader.Load(Samples.SmallText.Replace("\"complete\": { \"fact\": \"event.e2.since_step\", \"op\": \"gte\", \"value\": 1 }", "\"complete\": { \"fact\": \"event.e3.since_step\", \"op\": \"gte\", \"value\": 1 }", StringComparison.Ordinal));
        const string Document = """
        { "format": 3, "id": "x", "variables": [], "externalFacts": [], "speakers": [ { "id": "guide", "portraits": [ "calm" ] } ], "quests": [],
          "conversations": [ { "id": "intro", "start": "l", "nodes": [ { "id": "l", "kind": "line", "speaker": "guide", "portrait": "calm", "line": "k", "next": "e" }, { "id": "e", "kind": "end", "result": "done" } ] } ] }
        """;
        var dialogue = DialogueLoader.Load(Document, new Cast());
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var tutorialChannels = new TutorialChannels();
        tutorialChannels.Install(session);
        var dialogueChannels = new DialogueChannels();
        dialogueChannels.Install(session);
        var runtime = Samples.Done(set);
        runtime.Start();
        var tutorial = new TutorialComponent(runtime, new TickTutorialClock(), () => Samples.Flag(), tutorialChannels);
        var speaker = new TutorialDialogueSpeaker("speaker", [new TutorialSpokenStep("s2", "intro", "e3")], set, dialogue, "", "d");
        session.AddComponent(tutorial, 30);
        session.AddComponent(speaker, 40);
        session.Init();
        session.Update(20_000);
        session.Submit(new TutorialRequested("o", "observe", "e1"));
        session.Update(20_000);
        session.Update(20_000);
        Assert.Equal(1, speaker.Started);
        session.Submit(new DialogueEnded("r", 5, "intro", "done", "x"));        // another dialogue channel: not ours
        session.Update(20_000);
        Assert.Equal(0, speaker.Finished);
        session.Submit(new DialogueEnded("r", 6, "intro", "done", "d"));
        session.Update(20_000);
        session.Update(20_000);
        Assert.Equal(1, speaker.Finished);
        Assert.Equal(1, runtime.EventCounts[2]);
        session.Submit(new DialogueEnded("r", 7, "intro", "done", "d"));        // the same conversation ending again: the step is not told twice
        session.Update(20_000);
        session.Update(20_000);
        Assert.Equal(1, speaker.Finished);
        Assert.Equal(1, runtime.EventCounts[2]);
    }

    private sealed class Cast : ISpeakerCatalog
    {
        public bool HasSpeaker(string id) => id == "guide";
    }

    [Fact]
    public void ATradeRuleNamingAGoodIsNotToldOfAnotherGood()
    {
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var tutorialChannels = new TutorialChannels();
        tutorialChannels.Install(session);
        var economyChannels = new EconomyChannels();
        economyChannels.Install(session);
        var runtime = Samples.Done(Samples.Small);
        runtime.Start();
        var observer = new TutorialEconomyObserver("eco", [new TutorialTradeRule("", "iron", "e2")], Samples.Small);
        session.AddComponent(new TutorialComponent(runtime, new TickTutorialClock(), () => Samples.Flag(), tutorialChannels), 30);
        session.AddComponent(observer, 40);
        session.Init();
        session.Update(20_000);
        session.Submit(new EconomyTradeExecuted("economy", "", "t1", 1, "player", "riverton", "grain", "crown", 5, 100, 500, 0, 500));
        session.Update(20_000);
        session.Update(20_000);
        Assert.Equal(0, observer.Observed);
        session.Submit(new EconomyTradeExecuted("economy", "", "t2", 2, "player", "riverton", "iron", "crown", 5, 100, 500, 0, 500));
        session.Update(20_000);
        session.Update(20_000);
        Assert.Equal(1, observer.Observed);
        Assert.Equal(1, runtime.EventCounts[1]);
    }

    [Fact]
    public void ARelationshipRuleIsToldOnlyOfAnAcceptedInteractionByTheNamedActorWithTheNamedInteraction()
    {
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var tutorialChannels = new TutorialChannels();
        tutorialChannels.Install(session);
        var relationshipChannels = new RelationshipChannels();
        relationshipChannels.Install(session);
        var runtime = Samples.Done(Samples.Small);
        runtime.Start();
        var observer = new TutorialRelationshipObserver("rel", [new TutorialRelationshipRule(TutorialRelationshipSignal.InteractionAccepted, "alice", "chat", "e3")], Samples.Small);
        session.AddComponent(new TutorialComponent(runtime, new TickTutorialClock(), () => Samples.Flag(), tutorialChannels), 30);
        session.AddComponent(observer, 40);
        session.Init();
        session.Update(20_000);
        void Performed(string actor, string interaction, bool accepted)
        {
            session.Submit(new RelationshipInteractionPerformed("relationship", "", "r", 1, interaction, actor, "dan", accepted, 0, 0, 0));
            session.Update(20_000);
            session.Update(20_000);
        }
        Performed("alice", "chat", false);                                      // refused
        Performed("bob", "chat", true);                                         // another actor
        Performed("alice", "insult", true);                                     // another interaction
        Assert.Equal(0, observer.Observed);
        Performed("alice", "chat", true);
        Assert.Equal(1, observer.Observed);
        Assert.Equal(1, runtime.EventCounts[2]);
    }
}
