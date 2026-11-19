using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>A hot reload that follows renames: the reload map moves variables, quests, stages, the open conversation, the calls it is inside and the visit counts to their new names, and is refused whole when it does not fit.</summary>
public sealed class ReloadMapTests
{
    private const string Map = """
    { "format": 1, "conversations": { "briefing": "intro" }, "nodes": { "briefing/ask": "intro/question" },
      "variables": { "trust": "rapport" }, "quests": { "hold": "defend" }, "stages": { "hold/order": "orders" } }
    """;

    private static string Renamed() => Fixture.With("\"order\"", "\"orders\"",
        Fixture.With("\"hold\"", "\"defend\"",
            Fixture.With("trust", "rapport",
                Fixture.With("\"ask\"", "\"question\"",
                    Fixture.With("\"id\": \"briefing\"", "\"id\": \"intro\"")))));

    private static DialogueRuntime Midway()
    {
        var runtime = new DialogueRuntime(Fixture.Load(), 5);
        runtime.Start("briefing", Fixture.Facts());
        runtime.Next(Fixture.Facts());
        var step = runtime.Choose("accept", Fixture.Facts());   // the random node picks a line; the quest has started
        Assert.Equal(StepKind.Line, step.Kind);
        return runtime;
    }

    private static List<string> Rest(DialogueRuntime runtime)
    {
        var seen = new List<string>();
        var step = runtime.Next(Fixture.Facts());
        while (true)
        {
            seen.Add(step.Kind + " " + step.Line?.Key + string.Join(",", step.Events.Select(e => e.GetType().Name)));
            if (step.Kind == StepKind.Ended) return seen;
            step = runtime.Next(Fixture.Facts());
        }
    }

    [Fact]
    public void AMapMovesTheStateToTheNewNamesAndTheConversationGoesOnExactlyAsItWouldHave()
    {
        var follower = Midway();
        var twin = Midway();
        var visits = follower.State.Visits.Count;
        Assert.True(visits >= 4);
        var report = follower.Reload(Fixture.Load(Renamed()), ReloadMode.Refuse, ReloadMap.Parse(Map));

        Assert.Equal(visits, report.RenamedVisits);
        Assert.Equal(0, report.DroppedVisits);
        Assert.Empty(report.AddedVariables);
        Assert.Empty(report.AddedQuests);
        Assert.Equal("intro", follower.State.Conversation);
        Assert.Equal(Waiting.Line, follower.State.WaitingFor);
        Assert.Equal(3L, follower.State.Variables["rapport"].Integer);
        Assert.Equal(new QuestProgress(QuestStatus.Active, "orders"), follower.State.Quests["defend"]);
        Assert.Equal(1, follower.State.Visits["intro/question"]);
        Assert.All(follower.State.Visits.Keys, k => Assert.StartsWith("intro/", k, StringComparison.Ordinal));
        Assert.Equal(Rest(twin), Rest(follower));
    }

    [Fact]
    public void WithoutTheMapTheSameEditIsRefusedAndAnEmptyMapChangesNothing()
    {
        var runtime = Midway();
        var digest = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.ReloadIncompatible, Fixture.Refusal(() => runtime.Reload(Fixture.Load(Renamed()))));
        // The map has to name every rename: this one leaves the variable out.
        var partial = ReloadMap.Parse("{ \"format\": 1, \"conversations\": { \"briefing\": \"intro\" } }");
        Assert.Equal(DialogueCodes.ReloadIncompatible, Fixture.Refusal(() => runtime.Reload(Fixture.Load(Renamed()), ReloadMode.Refuse, partial)));
        Assert.Equal(digest, DialogueSave.StateDigest(runtime.State));
        Assert.Equal("briefing", runtime.State.Conversation);
        var same = runtime.Reload(Fixture.Load(), ReloadMode.Refuse, ReloadMap.Empty);
        Assert.Equal(0, same.RenamedVisits);
        Assert.Equal(digest, DialogueSave.StateDigest(runtime.State));
    }

    [Theory]
    [InlineData("""{ "format": 1, "variables": { "nothing": "rapport" } }""", DialogueCodes.MapSource)]
    [InlineData("""{ "format": 1, "variables": { "trust": "nothing" } }""", DialogueCodes.MapTarget)]
    [InlineData("""{ "format": 1, "conversations": { "nothing": "intro" } }""", DialogueCodes.MapSource)]
    [InlineData("""{ "format": 1, "conversations": { "briefing": "nothing" } }""", DialogueCodes.MapTarget)]
    [InlineData("""{ "format": 1, "nodes": { "briefing/nothing": "intro/question" } }""", DialogueCodes.MapSource)]
    [InlineData("""{ "format": 1, "nodes": { "briefing/ask": "intro/nothing" } }""", DialogueCodes.MapTarget)]
    [InlineData("""{ "format": 1, "quests": { "hold": "nothing" } }""", DialogueCodes.MapTarget)]
    [InlineData("""{ "format": 1, "stages": { "hold/nothing": "orders" } }""", DialogueCodes.MapSource)]
    [InlineData("""{ "format": 1, "quests": { "hold": "defend" }, "stages": { "hold/order": "nothing" } }""", DialogueCodes.MapTarget)]
    [InlineData("""{ "format": 1, "conversations": { "briefing": "intro" }, "nodes": { "briefing/ask": "intro/hello" } }""", DialogueCodes.MapConflict)]
    [InlineData("""{ "format": 1, "variables": { "trust": "rapport", "briefed": "rapport" } }""", DialogueCodes.MapConflict)]
    [InlineData("""{ "format": 1, "quests": { "hold": "defend" }, "stages": { "hold/order": "done" } }""", DialogueCodes.MapConflict)]
    public void AMapThatDoesNotFitTheTwoDocumentsIsRefusedWithItsCodeAndTheSessionIsUntouched(string map, string code)
    {
        var runtime = Midway();
        var digest = DialogueSave.StateDigest(runtime.State);
        var set = runtime.Set;
        var parsed = ReloadMap.Parse(map);
        Assert.Equal(code, Fixture.Refusal(() => runtime.Reload(Fixture.Load(Renamed()), ReloadMode.Refuse, parsed)));
        Assert.Equal(digest, DialogueSave.StateDigest(runtime.State));
        Assert.Same(set, runtime.Set);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("""{ "conversations": {} }""")]                                                // no format
    [InlineData("""{ "format": 2 }""")]
    [InlineData("""{ "format": 1, "other": {} }""")]
    [InlineData("""{ "format": 1, "variables": [] }""")]
    [InlineData("""{ "format": 1, "variables": { "a": 1 } }""")]
    [InlineData("""{ "format": 1, "variables": { "A": "b" } }""")]                             // ids are lower case
    [InlineData("""{ "format": 1, "variables": { "a": "b/c" } }""")]                           // one id, not two
    [InlineData("""{ "format": 1, "nodes": { "a": "b/c" } }""")]                               // two ids
    [InlineData("""{ "format": 1, "variables": { "a": "b", "a": "c" } }""")]                   // the same old name twice
    [InlineData("""{ "format": 1, "stages": { "a/b": "c/d" } }""")]                            // a stage is one id
    public void AMapThatIsNotAMapIsRefusedAsAFormatProblem(string map) => Assert.Equal(DialogueCodes.MapFormat, Fixture.Refusal(() => ReloadMap.Parse(map)));

    [Fact]
    public void AMapWithMoreEntriesThanAnyDocumentCouldNeedIsRefused()
    {
        var entries = string.Join(",", Enumerable.Range(0, 4097).Select(i => $"\"a{i}\":\"b{i}\""));
        Assert.Equal(DialogueCodes.MapFormat, Fixture.Refusal(() => ReloadMap.Parse("{ \"format\": 1, \"variables\": { " + entries + " } }")));
    }

    [Fact]
    public void ACallFrameFollowsTheRenamesOfTheCallerAndTheConversationItIsInside()
    {
        var runtime = new DialogueRuntime(FeatureFixture.Load(), 1);
        var step = runtime.Start("main", FeatureFixture.Facts());
        step = runtime.Next(FeatureFixture.Facts());   // hi -> call detour: its first line is showing
        Assert.Equal("d1", step.Line!.Node);
        Assert.Single(runtime.State.Frames);
        var edited = FeatureFixture.With("\"sub\"", "\"gosub\"").Replace("\"detour\"", "\"side\"", StringComparison.Ordinal);
        var map = ReloadMap.Parse("""{ "format": 1, "conversations": { "detour": "side" }, "nodes": { "main/sub": "main/gosub" } }""");
        var report = runtime.Reload(FeatureFixture.Load(edited), ReloadMode.Refuse, map);
        Assert.Equal(1, runtime.State.Visits.Count(v => v.Key.StartsWith("side/", StringComparison.Ordinal)));
        Assert.Equal(2, report.RenamedVisits);
        Assert.Equal("side", runtime.State.Conversation);
        Assert.Equal(new DialogueFrame("main", "gosub", false), runtime.State.Frames.Single());
        step = runtime.Next(FeatureFixture.Facts());   // d2 raises mood, the return comes back to the choice in main
        Assert.Equal(StepKind.Choices, step.Kind);
        Assert.Equal("main", runtime.State.Conversation);
        Assert.Equal(1L, runtime.State.Variables["mood"].Integer);
    }

    [Fact]
    public void WithoutTheMapARenamedCalleeInTheMiddleOfACallIsRefusedAndRestartClosesIt()
    {
        var runtime = new DialogueRuntime(FeatureFixture.Load(), 1);
        runtime.Start("main", FeatureFixture.Facts());
        runtime.Next(FeatureFixture.Facts());
        var edited = FeatureFixture.With("\"detour\"", "\"side\"");
        Assert.Equal(DialogueCodes.ReloadIncompatible, Fixture.Refusal(() => runtime.Reload(FeatureFixture.Load(edited))));
        var report = runtime.Reload(FeatureFixture.Load(edited), ReloadMode.RestartConversation);
        Assert.Equal("detour/d1", report.ClosedConversation);
        Assert.False(runtime.InConversation);
    }
}
