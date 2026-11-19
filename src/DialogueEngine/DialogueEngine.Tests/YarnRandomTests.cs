using EngineCommon;
using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>
/// Yarn Spinner's random functions on the engine's seeded roll (<c>random()</c>, <c>random_range(a, b)</c>, <c>dice(n)</c>), and visit counts shared by all start nodes. Yarn Spinner documents (Functions,
/// read 2026-10-28): random is "between 0 and 1", random_range is "between a and b, inclusive", dice(n) is "an integer between 1 and sides, inclusive". The scripts are written from those descriptions; the
/// distribution tests draw many seeds and bound the counts loosely (a fair roll falls inside the bounds with overwhelming probability, and the draws are deterministic).
/// </summary>
public sealed class YarnRandomTests
{
    private static readonly ImportOptions Options = new("imported.random", "en");

    private static DialogueRuntime Play(string script, ulong seed, ImportOptions? options = null)
    {
        var result = YarnImporter.Import(script, options ?? Options);
        var runtime = new DialogueRuntime(DialogueLoader.Load(result.Document, new Speakers(result.Speakers.ToArray())), seed);
        var facts = new Dictionary<string, FactValue>();
        var step = runtime.Start(ImportOptions.Conversation, facts);
        while (step.Kind == StepKind.Line) step = runtime.Next(facts);
        return runtime;
    }

    private static string Script(string body) => "title: Start\n---\n<<declare $r = 0>>\n<<declare $hit = false>>\n" + body + "\n===\n";

    private static long Rolled(string body, ulong seed) => Play(Script(body), seed).State.Variables["r"].Integer;

    // ---- <<set $x to dice(n)>> ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void DiceGivesEveryValueFromOneToTheSidesAboutEquallyOften()
    {
        var counts = new int[7];
        for (ulong seed = 1; seed <= 600; seed++) counts[Rolled("<<set $r to dice(6)>>\nAnn: Done.", seed)]++;
        Assert.Equal(0, counts[0]);
        for (var side = 1; side <= 6; side++) Assert.InRange(counts[side], 60, 140);   // expected 100
    }

    [Fact]
    public void ADiceOfMoreThanSixteenSidesRollsInTwoStagesAndStaysEvenlySpread()
    {
        var counts = new int[41];
        for (ulong seed = 1; seed <= 2000; seed++) counts[Rolled("<<set $r to dice(40)>>\nAnn: Done.", seed)]++;
        Assert.Equal(0, counts[0]);
        for (var side = 1; side <= 40; side++) Assert.InRange(counts[side], 28, 75);   // expected 50
    }

    [Fact]
    public void ARandomRangeIsInclusiveOnBothEndsAndMayStartBelowZero()
    {
        var seen = new SortedSet<long>();
        for (ulong seed = 1; seed <= 200; seed++) seen.Add(Rolled("<<set $r to random_range(-2, 2)>>\nAnn: Done.", seed));
        Assert.Equal([-2L, -1L, 0L, 1L, 2L], seen);
    }

    [Fact]
    public void ARangeOfOneValueNeedsNoRollAndAOneSidedDiceIsAlwaysOne()
    {
        var one = Play(Script("<<set $r to random_range(3, 3)>>\nAnn: Done."), 7);
        Assert.Equal(3, one.State.Variables["r"].Integer);
        Assert.Equal(0, one.State.RollCounter);
        Assert.Equal(1, Rolled("<<set $r to dice(1)>>\nAnn: Done.", 9));
    }

    [Fact]
    public void ARollUsesTheSeededRollCounterSoTheSameSeedGivesTheSameValuesAndTwoRollsDiffer()
    {
        var first = Play(Script("<<set $r to dice(12)>>\n<<set $hit to true>>\nAnn: Done."), 5);
        var again = Play(Script("<<set $r to dice(12)>>\n<<set $hit to true>>\nAnn: Done."), 5);
        Assert.Equal(first.State.Variables["r"].Integer, again.State.Variables["r"].Integer);
        Assert.Equal(1, first.State.RollCounter);
        var other = Enumerable.Range(1, 30).Select(s => Rolled("<<set $r to random_range(1, 100)>>\nAnn: Done.", (ulong)s)).Distinct().Count();
        Assert.True(other > 15, "thirty seeds gave only " + other + " different values");
        Assert.True(Enumerable.Range(1, 30).Select(s => Play(Script("<<set $r to dice(100)>>\n<<set $r to dice(100)>>\nAnn: Done."), (ulong)s).State.RollCounter).All(c => c >= 2));
    }

    [Fact]
    public void ARollSurvivesASaveBetweenTheRollAndWhatFollows()
    {
        // The roll counter is part of the save: a session saved after the first roll and reopened rolls the second exactly as the one that went straight on.
        const string script = "title: Start\n---\n<<declare $a = 0>>\n<<declare $b = 0>>\n<<set $a to dice(100)>>\nAnn: Between.\n<<set $b to dice(100)>>\nAnn: Done.\n===\n";
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var result = YarnImporter.Import(script, Options);
            var set = DialogueLoader.Load(result.Document, new Speakers(result.Speakers.ToArray()));
            var facts = new Dictionary<string, FactValue>();
            var straight = new DialogueRuntime(set, seed);
            straight.Start(ImportOptions.Conversation, facts);
            var saved = DialogueSave.Open(set, DialogueSave.Write(straight));
            while (straight.State.Conversation is not null) straight.Next(facts);
            while (saved.State.Conversation is not null) saved.Next(facts);
            Assert.Equal(straight.State.Variables["b"].Integer, saved.State.Variables["b"].Integer);
            Assert.Equal(DialogueSave.StateDigest(straight.State), DialogueSave.StateDigest(saved.State));
            Assert.Equal(4, saved.State.RollCounter);   // 200 values roll in two stages, so each assignment takes two rolls
        }
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(16, 1)]
    [InlineData(17, 2)]
    [InlineData(100, 8)]
    public void ADiceIsOneRandomNodeUpToSixteenValuesAndTwoStagesAboveThat(int sides, int randomNodes)
    {
        var document = System.Text.Json.Nodes.JsonNode.Parse(YarnImporter.Import(Script("<<set $r to dice(" + sides + ")>>\nAnn: Done."), Options).Document)!;
        var nodes = document["conversations"]![0]!["nodes"]!.AsArray();
        Assert.Equal(randomNodes, nodes.Count(n => n!["kind"]!.GetValue<string>() == "random"));
    }

    [Fact]
    public void ConvertingAScriptWithFourHundredSidedRollsIsFastAndTheDocumentFitsAConversation()
    {
        var script = Script("<<set $r to dice(100)>>\n<<set $r to dice(100)>>\n<<set $r to dice(100)>>\n<<set $r to dice(100)>>\nAnn: Done.");
        YarnImporter.Import(script, Options);   // warm up
        var times = new List<double>();
        for (var repeat = 0; repeat < 7; repeat++)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 20; i++) YarnImporter.Import(script, Options);
            times.Add(watch.Elapsed.TotalMilliseconds / 20);
        }
        Console.Error.WriteLine("import of four d100 rolls: " + string.Join(", ", times.Select(t => t.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " ms")));
        Assert.True(times.Min() < 200, "converting took " + times.Min() + " ms");
    }

    // ---- conditions ---------------------------------------------------------------------------------------------------------------------

    private static int TrueCount(string condition, int seeds)
    {
        var hits = 0;
        for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            if (Play(Script("<<if " + condition + ">>\n<<set $hit to true>>\n<<endif>>\nAnn: Done."), seed).State.Variables["hit"].Boolean) hits++;
        return hits;
    }

    [Theory]
    [InlineData("dice(6) >= 5", 600, 150, 250)]      // 1/3, expected 200
    [InlineData("dice(6) > 5", 600, 70, 130)]        // 1/6, expected 100
    [InlineData("4 < dice(6)", 600, 150, 250)]       // the roll on the right: dice(6) > 4, 1/3
    [InlineData("dice(6) == 3", 600, 70, 130)]       // 1/6
    [InlineData("dice(6) != 3", 600, 440, 530)]      // 5/6, expected 500
    [InlineData("random_range(1, 10) <= 4", 600, 200, 280)]   // 4/10, expected 240
    [InlineData("random() < 0.25", 1000, 190, 310)]  // expected 250
    [InlineData("random() >= 0.25", 1000, 690, 810)] // expected 750
    [InlineData("0.9 < random()", 1000, 60, 140)]    // random() > 0.9, expected 100
    public void ARollInAConditionIsTrueAboutAsOftenAsTheComparisonIsLikely(string condition, int seeds, int low, int high) =>
        Assert.InRange(TrueCount(condition, seeds), low, high);

    [Theory]
    [InlineData("dice(6) > 6", 0)]
    [InlineData("dice(6) >= 1", 100)]
    [InlineData("dice(6) == 9", 0)]
    [InlineData("random() < 0", 0)]
    [InlineData("random() < 1", 100)]
    [InlineData("random() <= 1.0", 100)]
    [InlineData("random() > 1", 0)]
    [InlineData("random() < 5", 100)]
    [InlineData("random() > -1", 100)]
    [InlineData("random() < -1", 0)]
    [InlineData("random() > 7", 0)]
    public void AComparisonThatCannotFailOrCannotHoldNeedsNoRollAndIsDecidedAlike(string condition, int hits) =>
        Assert.Equal(hits, TrueCount(condition, 100));

    [Fact]
    public void AConditionWithRollsInAnElseifChainRollsAllOfThemBeforeTheFirstBranchAndTheyAreIndependent()
    {
        const string body = "<<if dice(2) == 1 and dice(2) == 1>>\n<<set $r to 1>>\n<<elseif dice(2) == 1>>\n<<set $r to 2>>\n<<else>>\n<<set $r to 3>>\n<<endif>>\nAnn: Done.";
        var counts = new int[4];
        for (ulong seed = 1; seed <= 800; seed++)
        {
            var runtime = Play(Script(body), seed);
            Assert.Equal(3, runtime.State.RollCounter);
            counts[runtime.State.Variables["r"].Integer]++;
        }
        Assert.InRange(counts[1], 140, 260);   // 1/4 of 800
        Assert.InRange(counts[2], 210, 330);   // 3/4 * 1/2
        Assert.InRange(counts[3], 210, 330);
    }

    [Fact]
    public void ARollInAnOptionConditionAndInAOnceIfIsRolledBeforeThatStatement()
    {
        const string script = "title: Start\n---\n<<declare $r = 0>>\n-> Lucky <<if dice(2) == 1>>\n    <<set $r to 1>>\n-> Always\n    <<set $r to 2>>\n===\n";
        var shown = new SortedSet<int>();
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var result = YarnImporter.Import(script, Options);
            var runtime = new DialogueRuntime(DialogueLoader.Load(result.Document, new Speakers(result.Speakers.ToArray())), seed);
            var step = runtime.Start(ImportOptions.Conversation, new Dictionary<string, FactValue>());
            Assert.Equal(StepKind.Choices, step.Kind);
            shown.Add(step.Choices!.Options.Count);
            Assert.Equal(1, runtime.State.RollCounter);
        }
        Assert.Equal([1, 2], shown);
        const string once = "title: Start\n---\n<<declare $r = 0>>\nAnn: Maybe. <<once if dice(2) == 1>>\nAnn: Done.\n===\n";
        var lines = new SortedSet<int>();
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var result = YarnImporter.Import(once, Options);
            var runtime = new DialogueRuntime(DialogueLoader.Load(result.Document, new Speakers(result.Speakers.ToArray())), seed);
            var facts = new Dictionary<string, FactValue>();
            var count = 0;
            for (var step = runtime.Start(ImportOptions.Conversation, facts); step.Kind == StepKind.Line; step = runtime.Next(facts)) count++;
            lines.Add(count);
        }
        Assert.Equal([1, 2], lines);
    }

    [Fact]
    public void TheReportSaysARollIsApproximatedBeforeItsStatementAndTheDocumentIsFormat3()
    {
        var result = YarnImporter.Import(Script("<<if dice(6) >= 5>>\nAnn: Lucky.\n<<endif>>"), Options);
        Assert.Contains(result.Report.Notes, n => n is { Status: ImportStatus.Approximated, Feature: "random in conditions" });
        Assert.Contains("\"format\": 3", result.Document, StringComparison.Ordinal);
    }

    [Fact]
    public void AProbabilityTooFineForTheWeightsOfARandomNodeIsKeptToTheThousandthAndSaidSo()
    {
        var result = YarnImporter.Import(Script("<<if dice(1000000) == 7>>\n<<set $hit to true>>\n<<endif>>\nAnn: Done."), Options);
        Assert.Contains(result.Report.Notes, n => n is { Status: ImportStatus.Approximated, Feature: "random in conditions" } && n.Detail.Contains("thousandth", StringComparison.Ordinal));
        DialogueLoader.Load(result.Document, new Speakers(result.Speakers.ToArray()));
    }

    // ---- refusals -----------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("<<set $r to random()>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<set $r to dice(101)>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<set $r to random_range(0, 100)>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<set $r to dice(\"six\")>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<set $r to dice(0)>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<set $r to random_range(5, 1)>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<set $r to random_range(1)>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<set $hit to dice(6)>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<set $r to dice(6)>>\n<<set $r to dice(6) + 1>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<if random() == 0.5>>\nAnn: x\n<<endif>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<if random() != 0.5>>\nAnn: x\n<<endif>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<if random() < 0.1234>>\nAnn: x\n<<endif>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<if random()>>\nAnn: x\n<<endif>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<if dice(6) > $r>>\nAnn: x\n<<endif>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<if dice(6) > dice(6)>>\nAnn: x\n<<endif>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<if dice(6) > 2.5>>\nAnn: x\n<<endif>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<if $r > 0.5>>\nAnn: x\n<<endif>>", DialogueCodes.ImportUnsupported)]
    [InlineData("Ann: I rolled {random()}.", DialogueCodes.ImportUnsupported)]
    [InlineData("<<if random(3) < 0.5>>\nAnn: x\n<<endif>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<if dice(6) >= 1.5 >>\nAnn: x\n<<endif>>", DialogueCodes.ImportUnsupported)]
    public void ARandomCallThisConverterCannotSayIsRefusedWithItsCode(string body, string code) =>
        Assert.Equal(code, Fixture.Refusal(() => YarnImporter.Import(Script(body), Options)));

    // ---- visit counts across several start nodes ----------------------------------------------------------------------------------------

    private const string TwoStarts =
        "title: A\n---\nAnn: In A.\n<<jump Shop>>\n===\n" +
        "title: B\n---\n<<if visited(\"Shop\")>>\nAnn: B knows the shop was seen.\n<<else>>\nAnn: B finds the shop new.\n<<endif>>\n<<if visited_count(\"Shop\") >= 2>>\nAnn: Twice.\n<<endif>>\n<<jump Shop>>\n===\n" +
        "title: Shop\n---\nBob: The shop.\n===\n";

    [Fact]
    public void ANodeEnteredThroughOneStartCountsForEveryOtherStartOfTheSameDocument()
    {
        // Yarn Spinner looks a node up by its title in the whole project, so a visit made while one start runs is seen by the others.
        var result = YarnImporter.Import(TwoStarts, Options with { Starts = ["A", "B"] });
        var set = DialogueLoader.Load(result.Document, new Speakers(result.Speakers.ToArray()));
        var table = LocaleTable.Load(result.Strings);
        var runtime = new DialogueRuntime(set, 1);
        var facts = new Dictionary<string, FactValue>();
        List<string> Run(string name)
        {
            var conversation = result.Entries.Single(e => e.Name == name).Conversation;
            var said = new List<string>();
            for (var step = runtime.Start(conversation, facts); step.Kind == StepKind.Line; step = runtime.Next(facts)) said.Add(LocaleRenderer.Render(table, step.Line!.Key, step.Line.Args));
            return said;
        }
        Assert.Equal(["B finds the shop new.", "The shop."], Run("b"));   // nothing has entered the shop yet
        Assert.Equal(["In A.", "The shop."], Run("a"));
        Assert.Equal(["B knows the shop was seen.", "Twice.", "The shop."], Run("b"));   // entered by B, then by A: both count
    }
}
