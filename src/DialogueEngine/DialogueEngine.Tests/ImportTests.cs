using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>
/// The Yarn Spinner and Ink converters. The capability matrices are run, not read: every row's sample is converted (and its report note checked) or refused with the code the row promises, and
/// the converted documents are played to show they do what the source does.
/// </summary>
public sealed class ImportTests
{
    private static readonly ImportOptions Options = new("imported.test", "en");

    public static IEnumerable<object[]> YarnRows() => ImportCapabilities.YarnSpinner.Select(r => new object[] { r });

    public static IEnumerable<object[]> InkRows() => ImportCapabilities.InkJson.Select(r => new object[] { r });

    private static void Check(Capability row, Func<string, ImportOptions, ImportResult> convert)
    {
        if (row.Status == CapabilityStatus.Refused)
        {
            Assert.Equal(row.Code, Fixture.Refusal(() => convert(row.Sample, row.OptionsFor(Options))));
            return;
        }
        var result = convert(row.Sample, row.OptionsFor(Options));
        var wanted = Enum.Parse<ImportStatus>(row.Status.ToString());
        Assert.Contains(result.Report.Notes, n => n.Feature == row.Key && n.Status == wanted);
        // Whatever was converted is a document the loader accepts with a complete strings table.
        var set = DialogueLoader.Load(result.Document, new Speakers(result.Speakers.ToArray()));
        Assert.Equal(set.Conversations.Keys.Order(StringComparer.Ordinal), result.Entries.Select(e => e.Conversation).Concat(set.Conversations.Keys.Where(k => k.StartsWith("detour-", StringComparison.Ordinal) || k.StartsWith("tunnel-", StringComparison.Ordinal))).Distinct().Order(StringComparer.Ordinal));
        LocaleCoverage.Check(set, LocaleTable.Load(result.Strings));
    }

    [Theory, MemberData(nameof(YarnRows))]
    public void EveryYarnRowOfTheMatrixDoesWhatItSays(Capability row) => Check(row, YarnImporter.Import);

    [Theory, MemberData(nameof(InkRows))]
    public void EveryInkRowOfTheMatrixDoesWhatItSays(Capability row) => Check(row, InkImporter.Import);

    [Fact]
    public void TheMatricesCoverEveryStatusAndEveryRefusalIsReachedByItsOwnSample()
    {
        foreach (var matrix in new[] { ImportCapabilities.YarnSpinner, ImportCapabilities.InkJson })
        {
            Assert.Equal(Enum.GetValues<CapabilityStatus>().Length, matrix.Select(r => r.Status).Distinct().Count());
            Assert.All(matrix.Where(r => r.Status == CapabilityStatus.Refused), r => Assert.Null(r.Key));
            Assert.All(matrix.Where(r => r.Status != CapabilityStatus.Refused), r => Assert.NotNull(r.Key));
            Assert.Equal(matrix.Count, matrix.Select(r => r.Feature).Distinct().Count());
        }
    }

    // ---- playing a converted Yarn script ---------------------------------------------------------------------------------------------------

    private const string Script = """
    // a small shop
    title: Start
    ---
    <<declare $gold = 5>>
    <<declare $met = false>>
    Ann: Welcome, I have {$gold} coins. #happy
    -> Take one
        <<set $gold to $gold - 1>>
        Ann: Here.
    -> Leave <<if $gold > 3>>
        Ann: Bye.
        <<stop>>
    <<if $gold >= 5 and not $met>>
        Ann: You are rich.
    <<elseif $gold == 4>>
        Ann: Fine.
    <<else>>
        Ann: Poor.
    <<endif>>
    <<jump Next>>
    ===
    title: Next
    color: red
    ---
    <<set $met to true>>
    Bob: Last.
    ===
    title: Alone
    ---
    Bob: Nobody comes here.
    ===
    """;

    private static List<string> Drive(ImportResult result, params string[] answers)
    {
        var set = DialogueLoader.Load(result.Document, new Speakers(result.Speakers.ToArray()));
        var table = LocaleTable.Load(result.Strings);
        var runtime = new DialogueRuntime(set, 1);
        var seen = new List<string>();
        var facts = new Dictionary<string, FactValue>();
        var step = runtime.Start(ImportOptions.Conversation, facts);
        var next = 0;
        for (var guard = 0; guard < 100; guard++)
        {
            foreach (var e in step.Events) seen.Add("event " + e);
            switch (step.Kind)
            {
                case StepKind.Line:
                    seen.Add(step.Line!.Speaker + ": " + LocaleRenderer.Render(table, step.Line.Key, step.Line.Args) + (step.Line.Tags.Count > 0 ? " #" + string.Join(",#", step.Line.Tags) : ""));
                    step = runtime.Next(facts);
                    break;
                case StepKind.Choices:
                    seen.Add("choices " + string.Join(" | ", step.Choices!.Options.Select(o => LocaleRenderer.Render(table, o.Key, []) + (o.Enabled ? "" : "!"))));
                    step = runtime.Choose(answers[next++], facts);
                    break;
                case StepKind.Ended:
                    seen.Add("ended " + step.Result);
                    return seen;
            }
        }
        throw new InvalidOperationException("the conversation did not end");
    }

    [Fact]
    public void AYarnScriptBecomesADocumentThatPlaysTheWayTheScriptReads()
    {
        var result = YarnImporter.Import(Script, Options);
        Assert.Equal(["ann", "bob"], result.Speakers);
        var played = Drive(result, "o1");
        Assert.Equal(
        [
            "ann: Welcome, I have 5 coins. #happy",
            "choices Take one | Leave",
            "event " + new VariableEvent("gold", FactValue.From(4L)),
            "ann: Here.",
            "ann: Fine.",
            "event " + new VariableEvent("met", FactValue.From(true)),
            "bob: Last.",
            "ended done",
        ], played);
        Assert.Equal(["ann: Welcome, I have 5 coins. #happy", "choices Take one | Leave", "ann: Bye.", "ended done"], Drive(result, "o2"));
        Assert.Contains(result.Report.Notes, n => n is { Status: ImportStatus.Dropped, Feature: "nodes not reachable from the start" });
        Assert.Contains(result.Report.Notes, n => n is { Status: ImportStatus.Dropped, Feature: "headers other than title" });
    }

    [Fact]
    public void EveryComparisonAndLogicalOperatorMeansWhatItSaysWithTheVariableOnEitherSide()
    {
        var checks = new (string Condition, bool Holds)[]
        {
            ("3 < $a", true), ("9 > $a", true), ("7 <= $a", false), ("5 >= $a", true), ("5 <= $a", true), ("6 >= $a", true), ("6 < $a", false),
            ("$a != 5", false), ("$a == 5", true), ("$a neq 4", true), ("$a is 5", true), ("$a eq 4", false),
            ("$a gt 4 and $a lt 6", true), ("$a gte 6 or $a lte 4", false), ("$a <= 4 or $b", true), ("not $b", false), ("!$b", false), ("$b", true),
            ("$s == \"x\" and ($a >= 5)", true), ("$s != \"x\" || $a > 100", false), ("$a > 4 && $a < 6 && $b", true),
        };
        var script = new System.Text.StringBuilder("title: Start\n---\n<<declare $a = 5>>\n<<declare $b = true>>\n<<declare $s = \"x\">>\n");
        for (var i = 0; i < checks.Length; i++) script.Append($"<<if {checks[i].Condition}>>\nAnn: T{i}.\n<<endif>>\n");
        script.Append("===\n");
        var played = Drive(YarnImporter.Import(script.ToString(), Options));
        var expected = checks.Select((c, i) => (c, i)).Where(x => x.c.Holds).Select(x => "ann: T" + x.i + ".").Append("ended done");
        Assert.Equal(expected, played);
    }

    [Fact]
    public void EverySetFormChangesTheVariableTheWayItReads()
    {
        const string script = "title: Start\n---\n<<declare $a = 5>>\n<<declare $b = false>>\n<<declare $s = \"x\">>\n" +
            "<<set $a += 3>>\nAnn: {$a}\n<<set $a -= 2>>\nAnn: {$a}\n<<set $a = $a - 1>>\nAnn: {$a}\n<<set $a to $a + 10>>\nAnn: {$a}\n<<set $a to 7>>\nAnn: {$a}\n" +
            "<<set $b to true>>\nAnn: {$b}\n<<set $s to \"y z\">>\nAnn: {$s}\n===\n";
        Assert.Equal(["ann: 8", "ann: 6", "ann: 5", "ann: 15", "ann: 7", "ann: true", "ann: y z", "ended done"], Drive(YarnImporter.Import(script, Options)).Where(l => !l.StartsWith("event", StringComparison.Ordinal)));
    }

    [Fact]
    public void AnOptionWhoseConditionIsFalseIsHiddenAndWhenNoneIsLeftTheConversationGoesOnAfterTheGroup()
    {
        var some = YarnImporter.Import("title: Start\n---\n<<declare $k = false>>\n-> Hidden <<if $k>>\n    Ann: no.\n-> Shown\n    Ann: yes.\nAnn: after.\n===\n", Options);
        Assert.Equal(["choices Shown", "ann: yes.", "ann: after.", "ended done"], Drive(some, "o2"));
        var none = YarnImporter.Import("title: Start\n---\n<<declare $k = false>>\n-> Hidden <<if $k>>\n    Ann: no.\nAnn: after.\n===\n", Options);
        Assert.Equal(["ann: after.", "ended done"], Drive(none));
    }

    [Fact]
    public void StatementsAfterAJumpOrStopAreDroppedAndReportedAndLongRunsOfSetsAreMergedUpToTheLimitOfAnEffectsNode()
    {
        var dead = YarnImporter.Import("title: Start\n---\nAnn: one.\n<<stop>>\nAnn: never.\n===\n", Options);
        Assert.Equal(["ann: one.", "ended done"], Drive(dead));
        Assert.Contains(dead.Report.Notes, n => n is { Status: ImportStatus.Dropped, Feature: "statements after a jump or stop" });
        Assert.DoesNotContain("never", dead.Strings, StringComparison.Ordinal);
        var many = "title: Start\n---\n<<declare $a = 0>>\n" + string.Concat(Enumerable.Repeat("<<set $a += 1>>\n", 20)) + "Ann: {$a}\n===\n";
        var result = YarnImporter.Import(many, Options);
        var nodes = System.Text.Json.Nodes.JsonNode.Parse(result.Document)!["conversations"]![0]!["nodes"]!.AsArray().Where(n => n!["kind"]!.GetValue<string>() == "effects").ToList();
        Assert.Equal([16, 4], nodes.Select(n => n!["effects"]!.AsArray().Count).ToList());
        Assert.Equal(["ann: 20", "ended done"], Drive(result).Where(l => !l.StartsWith("event", StringComparison.Ordinal)));
    }

    [Fact]
    public void TagsKeepTheirOrderAreLowerCasedAndAreNotRepeated()
    {
        var result = YarnImporter.Import("title: Start\n---\nAnn: Hi. #Beta #alpha #beta\n===\n", Options);
        Assert.Equal(["ann: Hi. #alpha,#beta", "ended done"], Drive(result));   // a repeated tag counts where it was last written
    }

    [Fact]
    public void ContentThatNothingLeadsToIsLeftOutWithItsTextSoTheStringsTableHasNoOrphanKeys()
    {
        // Both branches jump away, so the line after the if is made but can never be reached.
        const string script = "title: Start\n---\n<<declare $k = true>>\n<<if $k>>\n<<jump Other>>\n<<else>>\n<<jump Other>>\n<<endif>>\nAnn: orphan.\n===\ntitle: Other\n---\nAnn: kept.\n===\n";
        var result = YarnImporter.Import(script, Options);
        Assert.DoesNotContain("orphan", result.Strings, StringComparison.Ordinal);
        Assert.Contains("kept", result.Strings, StringComparison.Ordinal);
        Assert.Contains(result.Report.Notes, n => n is { Status: ImportStatus.Dropped, Feature: "unreachable content" });
        Assert.Equal(["ann: kept.", "ended done"], Drive(result));
    }

    [Fact]
    public void AnotherStartConvertsThatPartOfTheScript()
    {
        var result = YarnImporter.Import(Script, Options with { Start = "Alone" });
        Assert.Equal(["bob: Nobody comes here.", "ended done"], Drive(result));
        Assert.Equal(["bob"], result.Speakers);
    }

    [Fact]
    public void ALoopThroughAJumpPlaysRoundAgain()
    {
        var result = YarnImporter.Import("title: Start\n---\nAnn: Again?\n-> Yes\n    <<jump Start>>\n-> No\nAnn: Done.\n===\n", Options);
        Assert.Equal(["ann: Again?", "choices Yes | No", "ann: Again?", "choices Yes | No", "ann: Done.", "ended done"], Drive(result, "o1", "o2"));
    }

    [Fact]
    public void ConvertingTwiceGivesTheSameBytes()
    {
        var one = YarnImporter.Import(Script, Options);
        var two = YarnImporter.Import(Script, Options);
        Assert.Equal(one.Document, two.Document);
        Assert.Equal(one.Strings, two.Strings);
        var ink = ImportCapabilities.InkJson[7].Sample;
        Assert.Equal(InkImporter.Import(ink, Options).Document, InkImporter.Import(ink, Options).Document);
    }

    [Fact]
    public void TextWithBracesAndEscapesReachesTheStringsTableAsTheLocaleTablesReadIt()
    {
        var result = YarnImporter.Import("title: Start\n---\nAnn: Use \\{braces} and {$n} please.\n<<declare $n = 3>>\n===\n", Options);
        var table = LocaleTable.Load(result.Strings);
        var played = Drive(result);
        Assert.Equal(["ann: Use {braces} and 3 please.", "ended done"], played);
        Assert.Contains("{{braces}}", table.Messages.Values.Single().Text);
    }

    [Theory]
    [InlineData("", DialogueCodes.ImportSyntax)]
    [InlineData("title: Start\n---\nAnn: Hi.\n", DialogueCodes.ImportSyntax)]                                           // never closed
    [InlineData("---\nAnn: Hi.\n===\n", DialogueCodes.ImportSyntax)]                                                    // no header
    [InlineData("color: red\n---\nAnn: Hi.\n===\n", DialogueCodes.ImportSyntax)]                                        // no title
    [InlineData("title: Start\n---\n<<jump Nowhere>>\n===\n", DialogueCodes.ImportSyntax)]
    [InlineData("title: Start\n---\n<<set $x to 1>>\nAnn: Hi.\n===\n", DialogueCodes.ImportSyntax)]                     // undeclared
    [InlineData("title: Start\n---\nAnn: Hi {$x}.\n===\n", DialogueCodes.ImportSyntax)]
    [InlineData("title: Start\n---\n<<if true>>\nAnn: Hi.\n===\n", DialogueCodes.ImportSyntax)]                        // unclosed if
    [InlineData("title: Start\n---\n<<endif>>\nAnn: Hi.\n===\n", DialogueCodes.ImportSyntax)]
    [InlineData("title: Start\n---\nAnn: Hi.\n===\ntitle: start\n---\nAnn: Hi.\n===\n", DialogueCodes.ImportSyntax)]    // titles compared without case
    [InlineData("title: Start\n---\n<<jump Start>>\n===\n", DialogueCodes.ImportSyntax)]                               // a loop with nothing in it
    [InlineData("title: Start\n---\n<<declare $g = 1>>\n<<declare $G = 2>>\nAnn: Hi.\n===\n", DialogueCodes.ImportSyntax)]
    [InlineData("title: Bad Name\n---\nAnn: Hi.\n===\n", DialogueCodes.ImportSyntax)]
    [InlineData("title: Start\n---\n<<declare $g = 1>>\n<<if $g == \"a\">>\nAnn: Hi.\n<<endif>>\n===\n", DialogueCodes.ImportSyntax)]    // a number against a text
    [InlineData("title: Start\n---\n<<declare $g = \"a\">>\n<<if $g > \"a\">>\nAnn: Hi.\n<<endif>>\n===\n", DialogueCodes.ImportSyntax)]  // only numbers compare in order
    [InlineData("title: Start\n---\nAnn: Hi. #Bad:Tag\n===\n", DialogueCodes.ImportUnsupported)]
    public void AScriptThatIsNotYarnOrCannotBeConvertedIsRefusedWithTheRightCode(string script, string code) =>
        Assert.Equal(code, Fixture.Refusal(() => YarnImporter.Import(script, Options)));

    [Fact]
    public void AValueOutOfItsRangeOrAnOverlongTextIsRefusedByTheLoaderCheckOfTheOutput()
    {
        Assert.Equal(DialogueCodes.ImportInvalid, Fixture.Refusal(() => YarnImporter.Import("title: Start\n---\n<<declare $g = 5000000000>>\nAnn: Hi.\n===\n", Options)));
        Assert.Equal(DialogueCodes.ImportUnsupported, Fixture.Refusal(() => YarnImporter.Import("title: Start\n---\nAnn: " + new string('x', 1100) + "\n===\n", Options)));
    }

    // ---- Ink ----------------------------------------------------------------------------------------------------------------------------

    private static string Story(string flow, string? knots = null, bool closed = false) =>
        "{\"inkVersion\":21,\"root\":[[" + flow + (closed ? "" : ",null") + "],\"done\"," + (knots ?? "null") + "],\"listDefs\":{}}";

    [Fact]
    public void AnInkStoryWithKnotsTextAndTagsPlaysAsTheStoryReads()
    {
        var result = InkImporter.Import(Story("\"^Hello there.\",\"#\",\"^warm\",\"/#\",\"\\n\",\"^Come in.\",\"\\n\",{\"->\":\"hall\"}", "{\"hall\":[\"^The hall is {big}.\",\"\\n\",\"end\",null]}"), Options);
        Assert.Equal(["narrator: Hello there. #warm", "narrator: Come in.", "narrator: The hall is {big}.", "ended done"], Drive(result));
        Assert.Equal(["narrator"], result.Speakers);
    }

    /// <summary>A once-only answer sets its own variable when taken; tests that are not about that look at the rest.</summary>
    private static List<string> WithoutSpentMarks(List<string> played) => played.Where(l => !l.StartsWith("event VariableEvent { Id = ink-taken", StringComparison.Ordinal)).ToList();

    [Fact]
    public void InkChoicesShowTheirStartAndChoiceOnlyTextAndTheBranchEchoesWhatWasSaid()
    {
        var start = ImportCapabilities.InkJson.Single(r => r.Feature.StartsWith("choices with start content", StringComparison.Ordinal)).Sample;
        Assert.Equal(["choices Hello", "narrator: Hello there.", "ended done"], WithoutSpentMarks(Drive(InkImporter.Import(start, Options), "o1")));
        var choiceOnly = ImportCapabilities.InkJson.Single(r => r.Feature.StartsWith("sticky choices", StringComparison.Ordinal)).Sample;
        Assert.Equal(["choices Take the road", "narrator: You walk.", "ended done"], Drive(InkImporter.Import(choiceOnly, Options), "o1"));
        // Start content and choice-only content together: "Hello [there]" shows both in the answer and the branch echoes the start content only.
        var both = start.Replace("\"/str\",\"/ev\",{\"*\"", "\"^ there\",\"/str\",\"/ev\",{\"*\"", StringComparison.Ordinal);
        Assert.NotEqual(start, both);
        Assert.Equal(["choices Hello there", "narrator: Hello there.", "ended done"], WithoutSpentMarks(Drive(InkImporter.Import(both, Options), "o1")));
    }

    private const string Menu =
        "{\"inkVersion\":21,\"root\":[[\"^Pick.\",\"\\n\",{\"->\":\"menu\"},null],\"done\",{\"menu\":[" +
        "\"ev\",\"str\",\"^A\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":20}," +
        "\"ev\",\"str\",\"^B\",\"/str\",\"/ev\",{\"*\":\".^.c-1\",\"flg\":20}," +
        "{\"c-0\":[\"^a\",\"\\n\",{\"->\":\"menu\"},{\"#f\":5}],\"c-1\":[\"^b\",\"\\n\",\"end\",{\"#f\":5}]}]}],\"listDefs\":{}}";

    [Fact]
    public void AnInkOnceOnlyChoiceIsSpentWhenTakenAndAStickyOneIsNot()
    {
        var once = InkImporter.Import(Menu, Options);
        Assert.Equal(["narrator: Pick.", "choices A | B", "event " + new VariableEvent("ink-taken-1", FactValue.From(true)), "narrator: a", "choices B", "event " + new VariableEvent("ink-taken-2", FactValue.From(true)), "narrator: b", "ended done"], Drive(once, "o1", "o2"));
        var sticky = InkImporter.Import(Menu.Replace("\"flg\":20", "\"flg\":4", StringComparison.Ordinal), Options);
        Assert.Equal(["narrator: Pick.", "choices A | B", "narrator: a", "choices A | B", "narrator: a", "choices A | B", "narrator: b", "ended done"], Drive(sticky, "o1", "o1", "o2"));
    }

    [Fact]
    public void WhenEveryOnceOnlyChoiceIsSpentTheStoryEndsInsteadOfBeingStuck()
    {
        var onlyA = Menu.Replace("\"ev\",\"str\",\"^B\",\"/str\",\"/ev\",{\"*\":\".^.c-1\",\"flg\":20},", "", StringComparison.Ordinal);
        var played = Drive(InkImporter.Import(onlyA, Options), "o1");
        Assert.Equal(["narrator: Pick.", "choices A", "event " + new VariableEvent("ink-taken-1", FactValue.From(true)), "narrator: a", "ended done"], played);
    }

    [Theory]
    [InlineData("not json", DialogueCodes.ImportSyntax)]
    [InlineData("[]", DialogueCodes.ImportSyntax)]
    [InlineData("{\"inkVersion\":21}", DialogueCodes.ImportSyntax)]
    [InlineData("{\"inkVersion\":21,\"root\":[[\"done\",null],\"done\",null],\"extra\":1}", DialogueCodes.ImportSyntax)]
    [InlineData("{\"inkVersion\":99,\"root\":[[\"done\",null],\"done\",null]}", DialogueCodes.ImportUnsupported)]
    [InlineData("{\"inkVersion\":21,\"root\":[[\"^x\",{\"->\":\"nowhere\"},null],\"done\",null]}", DialogueCodes.ImportSyntax)]
    [InlineData("{\"inkVersion\":21,\"root\":[[\"^x\",\"\\n\",{\"->\":\"0.5\"},null],\"done\",null]}", DialogueCodes.ImportSyntax)]
    [InlineData("{\"inkVersion\":21,\"root\":[[\"ev\",\"str\",\"^x\",\"/ev\",null],\"done\",null]}", DialogueCodes.ImportSyntax)]
    [InlineData("{\"inkVersion\":21,\"root\":[[\"#\",\"^x\",null],\"done\",null]}", DialogueCodes.ImportSyntax)]
    [InlineData("{\"inkVersion\":21,\"root\":[[7,null],\"done\",null]}", DialogueCodes.ImportUnsupported)]
    public void AStoryThatIsNotInkOrCannotBeConvertedIsRefusedWithTheRightCode(string story, string code) =>
        Assert.Equal(code, Fixture.Refusal(() => InkImporter.Import(story, Options)));

    [Fact]
    public void AnInkStartThatTheStoryDoesNotHaveIsRefusedAndAKnotCanBeTheStart()
    {
        var story = Story("\"^Out.\",\"\\n\",\"done\"", "{\"cave\":[\"^In the cave.\",\"\\n\",\"end\",null]}");
        Assert.Equal(DialogueCodes.ImportSyntax, Fixture.Refusal(() => InkImporter.Import(story, Options with { Start = "swamp" })));
        Assert.Equal(["narrator: In the cave.", "ended done"], Drive(InkImporter.Import(story, Options with { Start = "cave" })));
    }
}
