using System.Collections;
using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>D12: a condition sees only what the document declared and the host exposed. It cannot read an undeclared host fact, a fact the host dresses up as the conversation's own, or anything outside the call.</summary>
public sealed class CheatFreeTests
{
    /// <summary>The facts a host exposes, remembering every key the runtime asked for.</summary>
    private sealed class Recording(IReadOnlyDictionary<string, FactValue> inner) : IReadOnlyDictionary<string, FactValue>
    {
        public HashSet<string> Asked { get; } = new();
        public FactValue this[string key] { get { Asked.Add(key); return inner[key]; } }
        public IEnumerable<string> Keys { get { Asked.Add("<enumerated>"); return inner.Keys; } }
        public IEnumerable<FactValue> Values { get { Asked.Add("<enumerated>"); return inner.Values; } }
        public int Count => inner.Count;
        public bool ContainsKey(string key) { Asked.Add(key); return inner.ContainsKey(key); }
        public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out FactValue value) { Asked.Add(key); return inner.TryGetValue(key, out value); }
        public IEnumerator<KeyValuePair<string, FactValue>> GetEnumerator() { Asked.Add("<enumerated>"); return inner.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static Dictionary<string, FactValue> Crowded()
    {
        var facts = new Dictionary<string, FactValue>(Fixture.Facts());
        facts["ext.secret"] = FactValue.From(99L);                     // not declared by the document
        facts["var.trust"] = FactValue.From(5L);                       // dressed as the conversation's own variable
        facts["quest.hold.status"] = FactValue.From("completed");      // dressed as the quest's own state
        facts["visits.briefing/hello"] = FactValue.From(40L);          // dressed as a visit count
        return facts;
    }

    [Fact]
    public void OnlyTheExternalFactsTheDocumentDeclaresAreEverAskedFor()
    {
        var facts = new Recording(Crowded());
        var seen = Fixture.Play(new DialogueRuntime(Fixture.Load(), 1), "accept", facts);
        Assert.Equal("ended done", seen[^1]);
        Assert.NotEmpty(facts.Asked);
        Assert.All(facts.Asked, key => Assert.Contains(key, new[] { "ext.squads", "ext.hold-status" }));
    }

    [Fact]
    public void AHostCannotPassOffItsOwnValueAsAVariableAQuestStateOrAVisitCount()
    {
        var runtime = new DialogueRuntime(Fixture.Load(), 1);
        var facts = Crowded();
        var step = runtime.Start("briefing", facts);
        step = runtime.Next(facts);
        // trust is 2 in the state; the host's claim of 5 would open the answer that needs 4.
        Assert.Equal(StepKind.Choices, step.Kind);
        Assert.False(step.Choices!.Options.Single(o => o.Id == "secret").Enabled);
        Assert.Throws<EngineCommon.EngineRefusedException>(() => runtime.Choose("secret", facts));
        runtime.Choose("accept", facts);
        Assert.Equal(new QuestProgress(QuestStatus.Active, "order"), runtime.State.Quests["hold"]);   // the host's "completed" did not finish the quest
        Assert.Equal(1, runtime.State.Visits["briefing/hello"]);                                                                       // nor did its claim of 40 visits count
    }

    [Theory]
    [InlineData("var.trust\", \"op\": \"gte\", \"value\": 4", "var.trust")]
    [InlineData("quest.hold.status\", \"op\": \"eq\", \"value\": \"completed\"", "quest.hold.status")]
    [InlineData("visits.briefing/hello\", \"op\": \"gte\", \"value\": 2", "visits.briefing/hello")]
    public void AnAnswerGuardedByAFactOfTheConversationReadsItFromTheStateAndNeverFromTheHost(string condition, string fact)
    {
        var document = Fixture.With("var.trust\", \"op\": \"gte\", \"value\": 4", condition);
        var facts = new Recording(Crowded());
        var runtime = new DialogueRuntime(Fixture.Load(document), 1);
        runtime.Start("briefing", facts);
        var step = runtime.Next(facts);
        Assert.False(step.Choices!.Options.Single(o => o.Id == "secret").Enabled, fact + " was taken from the host");
        Assert.DoesNotContain(fact, facts.Asked);
    }

    [Fact]
    public void AConditionOnAFactTheDocumentDidNotDeclareIsRefusedAtLoad()
    {
        Assert.Equal(DialogueCodes.Reference, Fixture.Refusal(() => Fixture.Load(Fixture.With("\"fact\": \"var.trust\", \"op\": \"gte\"", "\"fact\": \"ext.secret\", \"op\": \"gte\""))));
        Assert.Equal(DialogueCodes.Reference, Fixture.Refusal(() => Fixture.Load(Fixture.With("\"fact\": \"var.trust\", \"op\": \"gte\"", "\"fact\": \"world.gold\", \"op\": \"gte\""))));
    }

    [Fact]
    public void TheCoreReadsNoClockNoRandomSourceNoEnvironmentAndNoGameType()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "DialogueEngine", "DialogueEngine.Core"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var files = Directory.GetFiles(Path.Combine(directory!.FullName, "src", "DialogueEngine", "DialogueEngine.Core"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(files);
        var text = string.Join('\n', files.Select(File.ReadAllText));
        foreach (var forbidden in new[] { "DateTime", "Stopwatch", "Environment.", "System.Random", "new Random(", "Random.Shared", "GameRuntime", "Guid.NewGuid", "File.Read", "Process." })
            Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
    }
}
