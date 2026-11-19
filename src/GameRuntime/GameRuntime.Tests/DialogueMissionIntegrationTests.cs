using DialogueEngine;
using EngineCommon;
using GameRuntime.Headless;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// The narrative quest and the ScenarioEngine objective, side by side in one running game. The objective is owned by the mission; the dialogue quest only reads its status through a fact the
/// host supplies, at the moment the dialogue component answers a request in its Update. Nothing in the dialogue project holds, copies or sets an objective status.
/// </summary>
public sealed class DialogueMissionIntegrationTests
{
    private sealed class Speakers : ISpeakerCatalog
    {
        public bool HasSpeaker(string id) => false;
    }

    private sealed class QuietReporter : IEngineReporter
    {
        public void Report(EngineNotice notice) { }
        public void Audit(AuditRecord record) { }
    }

    private sealed class RuntimeReporter : IRuntimeReporter
    {
        public List<string> Codes { get; } = new();
        public void Report(string code, string message, Exception? exception) => Codes.Add(code);
    }

    private const string Document = """
    {
     "format": 3, "id": "demo.goal",
     "variables": [], "externalFacts": [ { "id": "profit-status", "type": "string" } ],
     "speakers": [],
     "quests": [ { "id": "goal", "title": "quest.goal.title", "start": "chase", "stages": [
        { "id": "chase", "journal": "quest.goal.chase", "advance": [ { "when": { "fact": "ext.profit-status", "op": "eq", "value": "completed" }, "to": "done" } ] },
        { "id": "done", "journal": "quest.goal.done", "advance": [], "ends": "completed" } ] } ],
     "conversations": [ { "id": "hello", "start": "bye", "nodes": [ { "id": "bye", "kind": "end", "result": "done" } ] } ]
    }
    """;

    /// <summary>Asks for the quest to start on its first Update, then for it to look at the facts again on every Update.</summary>
    private sealed class QuestWatcher(string id) : IGameComponent
    {
        private IEventPort? _events;
        private int _request;
        public string Id { get; } = id;
        public void Init(in ComponentContext context) => _events = context.Events;
        public void Update(in UpdateContext context)
        {
            var name = "w" + (_request++);
            if (context.Tick == 1) _events!.Emit(new DialogueQuestStartRequested(name, "goal"));
            else _events!.Emit(new DialogueQuestsUpdateRequested(name));
        }
    }

    private sealed class Result
    {
        public long ObjectiveCompletedAt { get; set; } = -1;
        public long QuestDoneAt { get; set; } = -1;
        public long QuestStartedAt { get; set; } = -1;
        public List<string> Refusals { get; } = new();
        public List<string> RefusedRequests { get; } = new();
        public DialogueComponent Dialogue { get; set; } = null!;
        public HeadlessSession Session { get; set; } = null!;
    }

    private static async Task<Result> Play(bool watcherFirst, int ticks = 4000)
    {
        var environment = await HeadlessEnvironment.LoadAsync(new QuietReporter());
        var result = new Result();
        var speakers = new Speakers();
        HeadlessSession? session = null;
        IReadOnlyDictionary<string, FactValue> Facts() => new Dictionary<string, FactValue>
        {
            ["ext.profit-status"] = FactValue.From(session!.Mission.Snapshot.Objectives.Single().Value.ToString().ToLowerInvariant()),
        };
        var channels = new DialogueChannels();
        var dialogue = new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(Document, speakers), 1), speakers, Facts, channels);
        var watcher = new QuestWatcher("watcher");
        session = HeadlessSession.Create(environment, 1, extra: watcherFirst ? [watcher, dialogue] : [dialogue, watcher], bot: TycoonBots.Create("greedy", 1, environment.GoalProfit));
        channels.Install(session.Runtime);
        session.Runtime.Subscribe<MissionEvent>(e => { if (e.Kind == "core.objective_changed" && e.PayloadJson.Contains("\"Completed\"", StringComparison.Ordinal) && result.ObjectiveCompletedAt < 0) result.ObjectiveCompletedAt = e.Tick; });
        session.Runtime.Subscribe<DialogueQuestChanged>(e =>
        {
            if (e.Status == "active" && result.QuestStartedAt < 0) result.QuestStartedAt = e.Tick;
            if (e.Status == "completed" && result.QuestDoneAt < 0) result.QuestDoneAt = e.Tick;
        });
        session.Runtime.Subscribe<DialogueRefused>(e => { result.Refusals.Add($"t{e.Tick} {e.Code}"); result.RefusedRequests.Add(e.RequestId); });
        session.Runtime.Init();
        var one = (RuntimeClock.MicrosecondsPerSecond + session.Clock.TicksPerSecond - 1) / session.Clock.TicksPerSecond;
        for (var i = 0; i < ticks && session.Runtime.State == RuntimeState.Running; i++) session.Runtime.Update(one);
        result.Dialogue = dialogue;
        result.Session = session;
        return result;
    }

    [Fact]
    public async Task TheQuestAdvancesInTheSameTickTheObjectiveCompletesWhenTheRequestIsRaisedBeforeTheDialogueUpdates()
    {
        var result = await Play(watcherFirst: true);
        Assert.True(result.ObjectiveCompletedAt > 0, "the objective never completed");
        Assert.Equal(result.ObjectiveCompletedAt, result.QuestDoneAt);
        Assert.Empty(result.Refusals);
    }

    [Fact]
    public async Task ARequestRaisedAfterTheDialogueHasUpdatedIsAnsweredInItsNextUpdateWithTheFactsOfThatMoment()
    {
        var first = await Play(watcherFirst: true);
        var second = await Play(watcherFirst: false);
        Assert.Equal(1, first.QuestStartedAt);
        Assert.Equal(2, second.QuestStartedAt);                                   // the same request, answered in the dialogue's next Update
        Assert.Equal(first.ObjectiveCompletedAt, second.ObjectiveCompletedAt);   // the mission does not care who asks
        Assert.Equal(RuntimeState.Ended, second.Session.Runtime.State);
        // The request of tick 14 was answered in the dialogue Update of tick 15, after the mission had completed the objective in that tick: the fact is read when the answer is made, not when it was asked.
        Assert.Equal(second.ObjectiveCompletedAt, second.QuestDoneAt);
        Assert.Equal(QuestStatus.Completed, second.Dialogue.Runtime.State.Quests["goal"].Status);
        Assert.Empty(second.Refusals);
    }
    [Fact]
    public async Task RequestsThatArriveTogetherAreAnsweredInTheOrderTheyArrivedSoTheFirstStartWinsAndTheLaterOnesAreTheRefusedOnes()
    {
        // Two starts submitted from outside arrive before the watcher's own start of the same tick. Answered in arrival order, the first is accepted and the other two are refused, in that order;
        // answered in any other order the refused ones would be different requests.
        var result = await Play(watcherFirst: true, ticks: 0);
        result.Session.Runtime.Submit(new DialogueQuestStartRequested("a", "goal"));
        result.Session.Runtime.Submit(new DialogueQuestStartRequested("b", "goal"));
        result.Session.Runtime.Update((RuntimeClock.MicrosecondsPerSecond + result.Session.Clock.TicksPerSecond - 1) / result.Session.Clock.TicksPerSecond);
        Assert.Equal(["b", "w0"], result.RefusedRequests);
        Assert.Equal(QuestStatus.Active, result.Dialogue.Runtime.State.Quests["goal"].Status);
        Assert.Equal(1, result.QuestStartedAt);
    }

    [Fact]
    public async Task TheDialogueStateHoldsTheNarrativeQuestAndNoObjectiveStatusAndTheMissionIsUntouched()
    {
        var result = await Play(watcherFirst: true);
        var quest = result.Dialogue.Runtime.State.Quests["goal"];
        Assert.Equal((QuestStatus.Completed, "done"), (quest.Status, quest.Stage));
        Assert.Equal(ObjectiveStatus.Completed, result.Session.Mission.Snapshot.Objectives.Single().Value);
        var saved = DialogueSave.Canonical(result.Dialogue.Runtime.State);
        Assert.DoesNotContain("profit-status", saved, StringComparison.Ordinal);               // the fact is read, never stored
        Assert.DoesNotContain(nameof(ObjectiveStatus), saved, StringComparison.Ordinal);
        var members = typeof(DialogueState).GetProperties().Select(p => p.PropertyType.FullName ?? "").ToList();
        Assert.DoesNotContain(members, m => m.Contains("Objective", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARequestForAQuestThatCannotStartIsRefusedAsAnEventAndNothingIsStartedTwice()
    {
        var result = await Play(watcherFirst: true, ticks: 3);
        var events = result.Session.Runtime;
        events.Submit(new DialogueQuestStartRequested("again", "goal"));
        events.Submit(new DialogueQuestStartRequested("ghost", "nobody"));
        var before = DialogueSave.StateDigest(result.Dialogue.Runtime.State);
        events.Update((RuntimeClock.MicrosecondsPerSecond + result.Session.Clock.TicksPerSecond - 1) / result.Session.Clock.TicksPerSecond);
        Assert.Contains(result.Refusals, r => r.EndsWith(DialogueCodes.QuestState, StringComparison.Ordinal));
        Assert.Contains(result.Refusals, r => r.EndsWith(DialogueCodes.Reference, StringComparison.Ordinal));
        Assert.Equal(before, DialogueSave.StateDigest(result.Dialogue.Runtime.State));
    }

    [Fact]
    public async Task AnEarlyRequestChangesNothingUntilTheDialogueUpdatesAndTheFactIsReadAtThatMoment()
    {
        var result = await Play(watcherFirst: true, ticks: 2);
        var before = DialogueSave.StateDigest(result.Dialogue.Runtime.State);
        result.Session.Runtime.Submit(new DialogueQuestsUpdateRequested("early"));
        Assert.Equal(before, DialogueSave.StateDigest(result.Dialogue.Runtime.State));           // submitted from outside a tick: only queued
        var answered = result.Dialogue.Answered;
        result.Session.Runtime.Update((RuntimeClock.MicrosecondsPerSecond + result.Session.Clock.TicksPerSecond - 1) / result.Session.Clock.TicksPerSecond);
        Assert.True(result.Dialogue.Answered >= answered + 2, "the early request and the watcher were both answered in the Update");
    }

    private static string Sources(string project)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", project))) directory = directory.Parent;
        Assert.NotNull(directory);
        return string.Join('\n', Directory.GetFiles(Path.Combine(directory!.FullName, "src", project), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)).Select(File.ReadAllText));
    }

    [Theory]
    [InlineData("DialogueEngine/DialogueEngine.Core")]
    [InlineData("GameRuntime/GameRuntime.Dialogue")]
    public void NoDialogueCodeNamesAnObjectiveOrTheMissionSoThereIsNoSecondOwnerOfObjectiveStatus(string project)
    {
        var text = Sources(project);
        foreach (var forbidden in new[] { "ObjectiveStatus", "ObjectiveDefinition", "IScenarioSimulation", "MissionComponent", "MissionCommandRequested", "Snapshot.Objectives" })
            Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
    }
}
