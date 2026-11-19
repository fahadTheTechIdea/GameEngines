using DialogueEngine;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>D1 and D2: the document is read strictly and every reference in it must resolve.</summary>
public sealed class FormatTests
{
    [Fact]
    public void TheFixtureLoadsAndHasTheShapeItDescribes()
    {
        var set = Fixture.Load();
        Assert.Equal("demo.briefing", set.Id);
        Assert.Equal(3, set.Variables.Count);
        Assert.Equal(2, set.Externals.Count);
        Assert.Single(set.Conversations);
        Assert.Equal(8, set.Conversations["briefing"].Nodes.Count);
        Assert.Equal(["brief.accept", "brief.hello", "brief.lucky", "brief.plain", "brief.refuse", "brief.secret", "quest.hold.done", "quest.hold.order", "quest.hold.title"], set.Keys.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["count"], set.Keys["brief.hello"]);
        Assert.Equal(64, set.Digest.Length);
    }

    public static IEnumerable<object[]> Refusals() => new (string Old, string New, string Code)[]
    {
        ("\"format\": 3,", "\"format\": 1,", DialogueCodes.Format),                 // the old format is refused
        ("\"format\": 3,", "\"format\": 2,", DialogueCodes.Format),
        ("\"format\": 3,", "\"format\": 3, \"extra\": 1,", DialogueCodes.UnknownField),
        ("\"id\": \"demo.briefing\",", "\"id\": \"demo.briefing\", \"id\": \"again\",", DialogueCodes.Duplicate),
        ("\"externalFacts\": [ { \"id\": \"hold-status\", \"type\": \"string\" }, { \"id\": \"squads\", \"type\": \"int\", \"min\": 0, \"max\": 1000 } ],", "", DialogueCodes.MissingField),
        ("\"id\": \"demo.briefing\"", "\"id\": \"Demo.Briefing\"", DialogueCodes.Format),
        ("\"initial\": 2, \"min\": 0, \"max\": 5", "\"initial\": 9, \"min\": 0, \"max\": 5", DialogueCodes.Range),
        ("\"id\": \"briefed\", \"type\": \"bool\", \"initial\": false", "\"id\": \"briefed\", \"type\": \"bool\", \"initial\": 3", DialogueCodes.ValueType),
        ("\"initial\": 2, \"min\": 0, \"max\": 5", "\"initial\": 2", DialogueCodes.MissingField),
        ("\"id\": \"briefed\", \"type\": \"bool\", \"initial\": false", "\"id\": \"briefed\", \"type\": \"bool\", \"initial\": false, \"allowed\": [\"a\"]", DialogueCodes.UnknownField),
        ("{ \"id\": \"briefed\"", "{ \"id\": \"trust\"", DialogueCodes.Duplicate),
        ("\"type\": \"bool\"", "\"type\": \"flag\"", DialogueCodes.Format),
        ("\"speakers\": [ { \"id\": \"cmdr\"", "\"speakers\": [ { \"id\": \"ghost\"", DialogueCodes.UnknownSpeaker),
        ("\"portrait\": \"grim\", \"line\": \"brief.plain\"", "\"portrait\": \"angry\", \"line\": \"brief.plain\"", DialogueCodes.UnknownPortrait),
        ("\"line\": \"brief.plain\", \"next\": \"fin\"", "\"line\": \"brief.plain\", \"next\": \"nowhere\"", DialogueCodes.Reference),
        ("{ \"id\": \"bye\", \"kind\": \"end\"", "{ \"id\": \"orphan\", \"kind\": \"end\", \"result\": \"x\" }, { \"id\": \"bye\", \"kind\": \"end\"", DialogueCodes.Unreachable),
        ("\"otherwise\": \"bye\"", "\"otherwise\": \"check\"", DialogueCodes.Loop),
        ("\"fact\": \"var.trust\", \"op\": \"gte\"", "\"fact\": \"var.missing\", \"op\": \"gte\"", DialogueCodes.Reference),
        ("\"fact\": \"var.briefed\", \"op\": \"eq\", \"value\": true", "\"fact\": \"quest.hold.status\", \"op\": \"eq\", \"value\": \"finished\"", DialogueCodes.Reference),
        ("\"fact\": \"var.briefed\", \"op\": \"eq\", \"value\": true", "\"fact\": \"quest.hold.stage\", \"op\": \"eq\", \"value\": \"nowhere\"", DialogueCodes.Reference),
        ("\"fact\": \"var.briefed\", \"op\": \"eq\", \"value\": true", "\"fact\": \"var.stance\", \"op\": \"eq\", \"value\": \"timid\"", DialogueCodes.Reference),
        ("{ \"fact\": \"var.trust\", \"op\": \"gte\", \"value\": 4 }", "{ \"fact\": \"var.trust\", \"op\": \"gte\", \"value\": 4, \"x\": 1 }", DialogueCodes.UnknownField),
        ("\"fact\": \"var.trust\", \"op\": \"gte\", \"value\": 4", "\"fact\": \"var.trust\", \"op\": \"gte\", \"value\": \"four\"", DialogueCodes.Format),
        ("\"fact\": \"var.trust\", \"op\": \"gte\", \"value\": 4", "\"fact\": \"var.trust\", \"op\": \"gte\", \"value\": 40", DialogueCodes.Format),
        ("{ \"add\": \"trust\", \"by\": 1 }", "{ \"add\": \"trust\", \"by\": 1, \"signal\": \"x\" }", DialogueCodes.Format),
        ("\"set\": \"stance\", \"value\": \"bold\"", "\"set\": \"stance\", \"value\": \"timid\"", DialogueCodes.Range),
        ("{ \"add\": \"trust\"", "{ \"add\": \"briefed\"", DialogueCodes.ValueType),
        ("{ \"add\": \"trust\"", "{ \"add\": \"nothing\"", DialogueCodes.Reference),
        ("{ \"startQuest\": \"hold\" }", "{ \"startQuest\": \"other\" }", DialogueCodes.Reference),
        ("\"kind\": \"random\"", "\"kind\": \"dice\"", DialogueCodes.Format),
        ("\"start\": \"hello\"", "\"start\": \"nope\"", DialogueCodes.Reference),
        ("\"pick\": \"player\"", "\"pick\": \"seeded\"", DialogueCodes.MissingField),
        ("\"pick\": \"player\"", "\"pick\": \"nobody\"", DialogueCodes.Format),
        ("\"line\": \"brief.lucky\"", "\"line\": \"brief.hello\"", DialogueCodes.Duplicate),
        ("\"line\": \"brief.lucky\"", "\"line\": \"Hello there\"", DialogueCodes.Format),
        ("], \"otherwise\": \"bye\" }", "] }", DialogueCodes.MissingField),
        ("{ \"id\": \"done\", \"journal\": \"quest.hold.done\", \"advance\": [], \"ends\": \"completed\" }", "{ \"id\": \"done\", \"journal\": \"quest.hold.done\", \"advance\": [], \"ends\": \"completed\" }, { \"id\": \"lost\", \"journal\": \"quest.hold.done\", \"advance\": [] }", DialogueCodes.Unreachable),
        ("\"ends\": \"completed\"", "\"ends\": \"won\"", DialogueCodes.Format),
        ("\"start\": \"order\"", "\"start\": \"missing\"", DialogueCodes.Reference),
        ("\"args\": { \"count\": \"ext.squads\" }", "\"args\": { \"count\": \"ext.nothing\" }", DialogueCodes.Reference),
        ("\"unavailable\": \"show\"", "\"unavailable\": \"maybe\"", DialogueCodes.Format),
        ("\"weight\": 1, \"next\": \"lucky\"", "\"weight\": 0, \"next\": \"lucky\"", DialogueCodes.Range),
        ("{ \"id\": \"bye\", \"kind\": \"end\", \"result\": \"done\" }", "{ \"id\": \"bye\", \"kind\": \"end\", \"result\": \"done\" }, { \"id\": \"bye\", \"kind\": \"end\", \"result\": \"again\" }", DialogueCodes.Duplicate),
    }.Select(r => new object[] { r.Old, r.New, r.Code });

    [Theory]
    [MemberData(nameof(Refusals))]
    public void EachKindOfMistakeInTheDocumentIsRefusedWithItsOwnCode(string old, string replacement, string code) =>
        Assert.Equal(code, Fixture.Refusal(() => Fixture.Load(Fixture.With(old, replacement))));

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"format\": 3,}")]
    public void TextThatIsNotADocumentIsRefused(string text) =>
        Assert.Equal(DialogueCodes.Format, Fixture.Refusal(() => DialogueLoader.Load(text, Fixture.Catalog)));

    [Fact]
    public void ADocumentWithNoConversationIsRefused() =>
        Assert.Equal(DialogueCodes.Range, Fixture.Refusal(() => DialogueLoader.Load("{\"format\":3,\"id\":\"x\",\"variables\":[],\"externalFacts\":[],\"speakers\":[],\"quests\":[],\"conversations\":[]}", Fixture.Catalog)));

    [Fact]
    public void TheDigestIsOfTheExactTextSoReformattingChangesIt()
    {
        var a = Fixture.Load();
        var b = Fixture.Load(Fixture.Document + "\n");
        Assert.NotEqual(a.Digest, b.Digest);
        Assert.Equal(a.Digest, Fixture.Load().Digest);
    }

    [Fact]
    public void ADocumentWithoutAnyConditionStillNeedsEveryBranchToHaveAnOtherwise()
    {
        // The fixture branch is valid; dropping the only case leaves an empty list, which is refused rather than read as "always otherwise".
        Assert.Equal(DialogueCodes.Range, Fixture.Refusal(() => Fixture.Load(Fixture.With("\"cases\": [ { \"when\": { \"fact\": \"var.briefed\", \"op\": \"eq\", \"value\": true }, \"next\": \"bye\" } ]", "\"cases\": []"))));
    }

    [Fact]
    public void ADocumentNeverHoldsLiteralTextOnlyKeysAndIds()
    {
        // Every string the engine shows comes from a key; the alphabet of a key has no space, capital or punctuation, so a sentence cannot be written in its place.
        var set = Fixture.Load();
        Assert.All(set.Keys.Keys, key => Assert.Matches("^[a-z0-9._-]+$", key));
    }

    // ---- D5: one condition grammar, owned by ScenarioEngine ----------------------------------------------------------------------------

    [Fact]
    public void TheDialogueProjectDefinesNoConditionTypeOfItsOwn()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "DialogueEngine", "DialogueEngine.Core"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var sources = Directory.GetFiles(Path.Combine(directory!.FullName, "src", "DialogueEngine", "DialogueEngine.Core"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(sources);
        foreach (var file in sources)
        {
            var text = File.ReadAllText(file);
            foreach (var forbidden in new[] { "class AllCondition", "record AllCondition", "enum FactKind", "record FactValue", "abstract record Condition", "class ConditionLogic" })
                Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
        }
        // And it does use the shared one.
        Assert.Contains(sources, f => File.ReadAllText(f).Contains("Conditions.Evaluate", StringComparison.Ordinal));
    }
}