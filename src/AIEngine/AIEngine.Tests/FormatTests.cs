using AIEngine;
using Xunit;

namespace AIEngine.Tests;

/// <summary>A1 and A2: the document is read strictly, every reference must resolve and every shape is bounded.</summary>
public sealed class FormatTests
{
    [Fact]
    public void TheFixtureLoadsAndHasTheShapeItDescribes()
    {
        var set = Fixture.Load();
        Assert.Equal("demo.ai", set.Id);
        Assert.Equal(["distance", "due", "foes", "hunger", "mode", "phase", "ready", "side", "small", "spare", "target"], set.Keys.Select(k => k.Id));
        Assert.Equal(["stock", "unit"], set.Percepts.Select(p => p.Id));
        Assert.Equal(["fire", "order", "rest"], set.Intents.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["commander", "scout", "worker"], set.Behaviors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(64, set.Digest.Length);
        Assert.Equal(set.Behaviors.Values.Max(b => b.MaxCost), set.MaxCost);
        Assert.True(set.TryKey("ready", out var ready) && ready.HostWrites);
        Assert.True(set.TryKey("foes", out var foes) && !foes.HostWrites);
        // Keys are in id order and a key's index is its place.
        Assert.All(set.Keys, k => Assert.Equal(k.Index, set.Keys.ToList().IndexOf(k)));
    }

    public static IEnumerable<object[]> Refusals() => new (string Old, string New, string Code)[]
    {
        ("\"format\": 1,", "\"format\": 2,", AiCodes.Format),
        ("\"format\": 1,", "\"format\": 1, \"extra\": 1,", AiCodes.UnknownField),
        ("\"id\": \"demo.ai\",", "\"id\": \"demo.ai\", \"id\": \"again\",", AiCodes.Duplicate),
        ("\"id\": \"demo.ai\",", "", AiCodes.MissingField),
        ("\"id\": \"demo.ai\"", "\"id\": \"Demo.AI\"", AiCodes.Format),
        // keys
        ("{ \"id\": \"ready\", \"type\": \"bool\", \"initial\": false, \"source\": \"host\" }", "{ \"id\": \"ready\", \"type\": \"bool\", \"initial\": false, \"source\": \"host\" }, { \"id\": \"ready\", \"type\": \"bool\", \"initial\": true, \"source\": \"host\" }", AiCodes.Duplicate),
        ("\"initial\": false, \"source\": \"host\" }", "\"initial\": 3, \"source\": \"host\" }", AiCodes.ValueType),
        ("\"initial\": 1, \"min\": 0, \"max\": 9", "\"initial\": 12, \"min\": 0, \"max\": 9", AiCodes.Range),
        ("\"initial\": 1, \"min\": 0, \"max\": 9", "\"initial\": 1, \"min\": 5, \"max\": 2", AiCodes.Range),
        ("\"initial\": 1, \"min\": 0, \"max\": 9", "\"initial\": 1, \"min\": 0", AiCodes.MissingField),
        ("\"initial\": 1, \"min\": 0, \"max\": 9", "\"initial\": 1, \"min\": 0, \"max\": 9999999999999", AiCodes.Range),
        ("\"initial\": false, \"source\": \"host\" }", "\"initial\": false, \"min\": 0, \"max\": 1, \"source\": \"host\" }", AiCodes.Format),
        ("\"type\": \"bool\", \"initial\": false, \"source\": \"host\"", "\"type\": \"float\", \"initial\": false, \"source\": \"host\"", AiCodes.Format),
        ("\"initial\": 1, \"min\": 0, \"max\": 9, \"source\": \"host\"", "\"initial\": 1, \"min\": 0, \"max\": 9, \"source\": \"world\"", AiCodes.Format),
        // percepts
        ("\"owned\": false, \"attrs\"", "\"owned\": false, \"capacity\": 3, \"attrs\"", AiCodes.Duplicate),
        ("{ \"id\": \"health\", \"type\": \"int\", \"min\": 0, \"max\": 1000, \"unknown\": 0 }", "{ \"id\": \"id\", \"type\": \"int\", \"min\": 0, \"max\": 1000, \"unknown\": 0 }", AiCodes.Reserved),
        ("{ \"id\": \"health\", \"type\": \"int\", \"min\": 0, \"max\": 1000, \"unknown\": 0 }", "{ \"id\": \"health\", \"type\": \"int\", \"min\": 0, \"max\": 1000, \"unknown\": 5000 }", AiCodes.Range),
        ("{ \"id\": \"health\", \"type\": \"int\", \"min\": 0, \"max\": 1000, \"unknown\": 0 }", "{ \"id\": \"health\", \"type\": \"int\", \"min\": 0, \"max\": 1000 }", AiCodes.MissingField),
        ("\"capacity\": 16, \"memory\": 50", "\"capacity\": 0, \"memory\": 50", AiCodes.Range),
        ("\"ownerCapacity\": 4, ", "", AiCodes.MissingField),                                               // an owned kind must say how many one owner holds
        ("\"ownerCapacity\": 4, ", "\"ownerCapacity\": 0, ", AiCodes.Range),
        ("\"ownerCapacity\": 4, ", "\"ownerCapacity\": 9, ", AiCodes.Range),                                // more than the kind holds
        ("\"owned\": false, \"attrs\"", "\"owned\": false, \"ownerCapacity\": 2, \"attrs\"", AiCodes.Format),   // a kind with no owners
        ("\"capacity\": 16, \"memory\": 50", "\"capacity\": 16, \"memory\": 0", AiCodes.Range),
        ("{ \"id\": \"hostile\", \"type\": \"bool\", \"unknown\": false },", "{ \"id\": \"hostile\", \"type\": \"bool\", \"unknown\": false }, { \"id\": \"hostile\", \"type\": \"bool\", \"unknown\": false },", AiCodes.Duplicate),
        // intents
        ("{ \"id\": \"rest\", \"args\": [] }", "{ \"id\": \"rest\", \"args\": [ { \"id\": \"a\", \"type\": \"int\" } ] }", AiCodes.MissingField),
        ("{ \"id\": \"rest\", \"args\": [] },", "{ \"id\": \"rest\", \"args\": [] }, { \"id\": \"rest\", \"args\": [] },", AiCodes.Duplicate),
        // nodes
        ("\"id\": \"commander\", \"every\": 1", "\"id\": \"commander\", \"every\": 0", AiCodes.Range),
        ("{ \"id\": \"is-ready\", \"kind\": \"condition\"", "{ \"id\": \"is-ready\", \"kind\": \"waits\"", AiCodes.Format),
        ("{ \"id\": \"is-ready\", \"kind\": \"condition\"", "{ \"id\": \"main\", \"kind\": \"condition\"", AiCodes.Duplicate),
        ("{ \"id\": \"is-ready\", \"kind\": \"condition\", \"when\"", "{ \"id\": \"is-ready\", \"kind\": \"condition\", \"extra\": 1, \"when\"", AiCodes.UnknownField),
        ("\"fact\": \"key.ready\", \"op\": \"eq\", \"value\": true", "\"fact\": \"key.missing\", \"op\": \"eq\", \"value\": true", AiCodes.Reference),
        ("\"fact\": \"key.ready\", \"op\": \"eq\", \"value\": true", "\"fact\": \"key.ready\", \"op\": \"eq\", \"value\": 4", AiCodes.Format),
        ("\"fact\": \"key.foes\", \"op\": \"gt\", \"value\": 0", "\"fact\": \"item.health\", \"op\": \"gt\", \"value\": 0", AiCodes.Reference),
        ("\"fact\": \"item.hostile\", \"op\": \"eq\", \"value\": false", "\"fact\": \"item.nothing\", \"op\": \"eq\", \"value\": false", AiCodes.Reference),
        ("\"key\": \"phase\", \"value\": { \"op\": \"mod\"", "\"key\": \"ready\", \"value\": { \"op\": \"mod\"", AiCodes.WrongWriter),
        ("\"key\": \"phase\", \"value\": { \"op\": \"mod\"", "\"key\": \"nothing\", \"value\": { \"op\": \"mod\"", AiCodes.Reference),
        ("\"op\": \"mod\", \"args\": [ { \"op\": \"tick\" }, 15 ]", "\"op\": \"mod\", \"args\": [ { \"op\": \"tick\" }, 30 ]", AiCodes.Range),
        ("\"op\": \"mod\", \"args\": [ { \"op\": \"tick\" }, 15 ]", "\"op\": \"mod\", \"args\": [ { \"op\": \"tick\" }, { \"key\": \"foes\" } ]", AiCodes.Divisor),
        ("\"op\": \"mod\", \"args\": [ { \"op\": \"tick\" }, 15 ]", "\"op\": \"mul\", \"args\": [ { \"op\": \"tick\" }, { \"op\": \"tick\" } ]", AiCodes.Overflow),
        ("\"op\": \"mod\", \"args\": [ { \"op\": \"tick\" }, 15 ]", "\"op\": \"mod\", \"args\": [ true, 15 ]", AiCodes.ExprType),
        ("\"op\": \"mod\", \"args\": [ { \"op\": \"tick\" }, 15 ]", "\"op\": \"power\", \"args\": [ { \"op\": \"tick\" }, 15 ]", AiCodes.Format),
        ("\"op\": \"mod\", \"args\": [ { \"op\": \"tick\" }, 15 ]", "\"op\": \"mod\", \"args\": [ { \"op\": \"tick\" } ]", AiCodes.Range),
        ("\"op\": \"mod\", \"args\": [ { \"op\": \"tick\" }, 15 ]", "\"op\": \"mod\", \"args\": [ { \"op\": \"tick\" }, 15.5 ]", AiCodes.Format),
        ("{ \"op\": \"tick\" }, 15 ]", "{ \"op\": \"tick\", \"args\": [] }, 15 ]", AiCodes.UnknownField),
        ("\"key\": \"due\", \"value\": true", "\"key\": \"due\", \"value\": 1", AiCodes.ExprType),
        ("\"key\": \"due\", \"value\": true", "\"key\": \"mode\", \"value\": 7", AiCodes.ExprType),
        ("\"key\": \"due\", \"value\": false }", "\"key\": \"foes\", \"value\": 99 }", AiCodes.Range),
        ("\"from\": \"unit\", \"into\": \"foes\"", "\"from\": \"ghost\", \"into\": \"foes\"", AiCodes.Reference),
        ("\"from\": \"unit\", \"into\": \"foes\"", "\"from\": \"unit\", \"into\": \"target\"", AiCodes.Range),
        ("\"from\": \"unit\", \"into\": \"foes\"", "\"from\": \"unit\", \"into\": \"ready\"", AiCodes.WrongWriter),
        ("\"from\": \"unit\", \"into\": \"foes\"", "\"from\": \"unit\", \"into\": \"small\"", AiCodes.Range),
        ("\"limit\": 16", "\"limit\": 17", AiCodes.Range),
        ("\"limit\": 16", "\"limit\": 0", AiCodes.Range),
        ("\"intent\": \"fire\", \"args\": { \"attacker\"", "\"intent\": \"volley\", \"args\": { \"attacker\"", AiCodes.Reference),
        ("\"metres\": { \"key\": \"distance\" } } }", "\"metres\": { \"key\": \"distance\" }, \"extra\": 1 } }", AiCodes.UnknownField),
        ("\"defender\": { \"key\": \"target\" }, ", "", AiCodes.MissingField),
        ("\"attacker\": { \"item\": \"id\" }", "\"attacker\": 5", AiCodes.ExprType),
        ("\"metres\": { \"key\": \"distance\" } } }", "\"metres\": { \"op\": \"add\", \"args\": [ { \"key\": \"distance\" }, 1 ] } } }", AiCodes.Range),
        ("\"attacker\": { \"item\": \"id\" }", "\"attacker\": { \"item\": \"nothing\" }", AiCodes.Reference),
        ("\"attacker\": { \"item\": \"id\" }", "\"attacker\": { \"item\": \"owner\" }, \"x\": 1", AiCodes.UnknownField),
        ("\"pick\": { \"nth\"", "\"pick\": { \"min\": \"hostile\", \"nth\"", AiCodes.Format),
        ("\"pick\": { \"nth\": { \"op\": \"mod\", \"args\": [ { \"item\": \"index\" }", "\"pick\": { \"nth\": { \"op\": \"mod\", \"args\": [ { \"item\": \"bogus\" }", AiCodes.Reference),
        ("\"into\": \"target\", \"where\"", "\"into\": \"foes\", \"where\"", AiCodes.ExprType),
        ("\"kind\": \"forEach\", \"from\": \"unit\", \"limit\": 16, \"where\"", "\"kind\": \"forEach\", \"from\": \"unit\", \"limit\": 16, \"of\": \"someone\", \"where\"", AiCodes.Reference),
        // utility
        ("\"momentum\": 150", "\"momentum\": 1500", AiCodes.Range),
        ("{ \"id\": \"work\", \"weight\": 1000", "{ \"id\": \"eat\", \"weight\": 1000", AiCodes.Duplicate),
        ("\"weight\": 1000, \"considerations\": [ { \"input\": { \"key\": \"hunger\" }, \"min\": 0, \"max\": 100, \"curve\": { \"kind\": \"linear\" } } ]", "\"weight\": 0, \"considerations\": [ { \"input\": { \"key\": \"hunger\" }, \"min\": 0, \"max\": 100, \"curve\": { \"kind\": \"linear\" } } ]", AiCodes.Range),
        ("\"min\": 0, \"max\": 100, \"curve\": { \"kind\": \"linear\" } }", "\"min\": 100, \"max\": 100, \"curve\": { \"kind\": \"linear\" } }", AiCodes.Range),
        ("\"curve\": { \"kind\": \"linear\" } }", "\"curve\": { \"kind\": \"wavy\" } }", AiCodes.Format),
        ("\"curve\": { \"kind\": \"linear\" } }", "\"curve\": { \"kind\": \"step\" } }", AiCodes.Format),
        ("\"curve\": { \"kind\": \"linear\" } }", "\"curve\": { \"kind\": \"linear\", \"at\": 5 } }", AiCodes.Format),
        ("\"input\": { \"key\": \"hunger\" }, \"min\": 0, \"max\": 100, \"curve\": { \"kind\": \"linear\" }", "\"input\": { \"key\": \"mode\" }, \"min\": 0, \"max\": 100, \"curve\": { \"kind\": \"linear\" }", AiCodes.ExprType),
        // waiting and shapes
        ("\"until\": { \"fact\": \"key.ready\", \"op\": \"eq\", \"value\": true }, ", "", AiCodes.Format),
        ("\"timeout\": 5", "\"timeout\": 0", AiCodes.Range),
        ("\"ticks\": 20", "\"ticks\": 0", AiCodes.Range),
        ("\"by\": -5", "\"by\": \"five\"", AiCodes.ExprType),
        ("\"attr\": \"held\"", "\"attr\": \"missing\"", AiCodes.Reference),
        ("\"target\": \"depot-a\"", "\"target\": 5", AiCodes.ExprType),
        ("\"weight\": 1, \"child\": { \"id\": \"heads\"", "\"weight\": 1001, \"child\": { \"id\": \"heads\"", AiCodes.Range),
        ("\"behaviors\": [", "\"behaviors\": [ { \"id\": \"worker\", \"every\": 5, \"root\": { \"id\": \"x\", \"kind\": \"condition\", \"when\": { \"fact\": \"tick\", \"op\": \"gte\", \"value\": 0 } } },", AiCodes.Duplicate),
    }.Select(r => new object[] { r.Old, r.New, r.Code });

    [Theory]
    [MemberData(nameof(Refusals))]
    public void EachKindOfMistakeInTheDocumentIsRefusedWithItsOwnCode(string old, string replacement, string code) =>
        Assert.Equal(code, Fixture.Refusal(() => Fixture.Load(Fixture.With(old, replacement))));

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"format\": 1,}")]
    public void TextThatIsNotADocumentIsRefused(string text) => Assert.Equal(AiCodes.Format, Fixture.Refusal(() => AiLoader.Load(text)));

    [Fact]
    public void ALoopBodyCannotWaitAndTheTreeIsBoundedInDepthAndSize()
    {
        var waiting = "{ \"id\": \"go\", \"kind\": \"act\", \"intent\": \"rest\", \"args\": { }, \"until\": { \"fact\": \"tick\", \"op\": \"gt\", \"value\": 3 } }";
        // An act that waits, put straight inside a loop, is refused where it is read; a waiting subtree is refused where the loop body is checked.
        Assert.Equal(AiCodes.CannotWait, Fixture.Refusal(() => Fixture.LoadMini("{ \"id\": \"loop\", \"kind\": \"forEach\", \"from\": \"unit\", \"limit\": 4, \"child\": " + waiting + " }")));
        var viaCooldown = "{ \"id\": \"loop\", \"kind\": \"forEach\", \"from\": \"unit\", \"limit\": 4, \"child\": { \"id\": \"c\", \"kind\": \"invert\", \"child\": { \"id\": \"s\", \"kind\": \"succeed\", \"child\": { \"id\": \"b\", \"kind\": \"condition\", \"when\": { \"fact\": \"tick\", \"op\": \"gt\", \"value\": 1 } } } } }";
        Assert.NotNull(Fixture.LoadMini(viaCooldown));   // conditions never wait, so this is fine
        var deep = "{ \"id\": \"leaf\", \"kind\": \"condition\", \"when\": { \"fact\": \"tick\", \"op\": \"gte\", \"value\": 0 } }";
        for (var i = 0; i < 13; i++) deep = "{ \"id\": \"d" + i + "\", \"kind\": \"succeed\", \"child\": " + deep + " }";
        Assert.Equal(AiCodes.TooDeep, Fixture.Refusal(() => Fixture.LoadMini(deep)));
        var leaves = string.Join(",", Enumerable.Range(0, 32).Select(i => "{ \"id\": \"l" + i + "\", \"kind\": \"condition\", \"when\": { \"fact\": \"tick\", \"op\": \"gte\", \"value\": 0 } }"));
        var wide = string.Join(",", Enumerable.Range(0, 17).Select(g => "{ \"id\": \"g" + g + "\", \"kind\": \"sequence\", \"resume\": false, \"children\": [" + leaves.Replace("\"id\": \"l", "\"id\": \"g" + g + "l") + "] }"));
        Assert.Equal(AiCodes.TooMany, Fixture.Refusal(() => Fixture.LoadMini("{ \"id\": \"top\", \"kind\": \"selector\", \"resume\": false, \"children\": [" + wide + "] }")));
    }

    [Fact]
    public void TextTooLongForItsKeyOrArgumentIsRefusedWhereItIsRead()
    {
        var joined = "{ \"id\": \"set\", \"kind\": \"let\", \"key\": \"mode\", \"value\": { \"op\": \"concat\", \"args\": [ { \"key\": \"mode\" }, { \"key\": \"mode\" } ] } }";
        Assert.Equal(AiCodes.Range, Fixture.Refusal(() => Fixture.LoadMini(joined)));
        var toIntent = "{ \"id\": \"go\", \"kind\": \"act\", \"intent\": \"order\", \"args\": { \"what\": { \"op\": \"concat\", \"args\": [ { \"key\": \"mode\" }, { \"key\": \"mode\" } ] }, \"amount\": 1 } }";
        Assert.Equal(AiCodes.Range, Fixture.Refusal(() => Fixture.LoadMini(toIntent)));
        var fine = "{ \"id\": \"set\", \"kind\": \"let\", \"key\": \"mode\", \"value\": { \"op\": \"concat\", \"args\": [ \"on-\", \"duty\" ] } }";
        Assert.NotNull(Fixture.LoadMini(fine));
    }

    [Fact]
    public void AWaitingActOutsideALoopIsFineAndEveryBehaviourHasAnExactBound()
    {
        var set = Fixture.LoadMini("{ \"id\": \"go\", \"kind\": \"act\", \"intent\": \"rest\", \"args\": { }, \"until\": { \"fact\": \"tick\", \"op\": \"gt\", \"value\": 3 } }");
        Assert.Equal(1, set.Behaviors["t"].MaxCost);
        Assert.Equal(1, set.Behaviors["t"].NodeCount);
        Assert.Equal(1, set.Behaviors["t"].SlotCount);
        Assert.True(set.Behaviors["t"].Root.MayRun);
    }

    [Fact]
    public void TheDigestIsOfTheExactTextSoReformattingChangesIt()
    {
        var a = Fixture.Load();
        Assert.NotEqual(a.Digest, Fixture.Load(Fixture.Document + "\n").Digest);
        Assert.Equal(a.Digest, Fixture.Load().Digest);
    }

    [Fact]
    public void TheEngineLibraryReferencesNothingOfTheGameAndTouchesNeitherClockNorFiles()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "AIEngine", "AIEngine.Core"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var core = Path.Combine(directory!.FullName, "src", "AIEngine", "AIEngine.Core");
        var project = File.ReadAllText(Path.Combine(core, "AIEngine.Core.csproj"));
        var references = System.Text.RegularExpressions.Regex.Matches(project, "ProjectReference Include=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["../../EngineCommon/EngineCommon.Core/EngineCommon.Core.csproj", "../../ScenarioEngine/src/ScenarioEngine.Contracts/ScenarioEngine.Contracts.csproj"], references.Order(StringComparer.Ordinal));
        var sources = Directory.GetFiles(core, "*.cs").ToList();
        Assert.NotEmpty(sources);
        foreach (var file in sources)
        {
            var text = File.ReadAllText(file);
            foreach (var forbidden in new[] { "DateTime", "Stopwatch", "Environment.", "System.Random", "new Random(", "Random.Shared", "Guid.NewGuid", "System.IO", "File.", "Task.", "Thread.", "GameRuntime", "CombatEngine", "SimEngine", "Godot" })
                Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
        }
    }
}