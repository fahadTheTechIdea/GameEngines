using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>The Ink half of <see cref="ImportFeatureTests"/>: sequences, tunnels, threads, glue, lists, allowed external functions, visit counts and the mistakes of the stack language.</summary>
public sealed partial class ImportFeatureTests
{
    private static string Seq(string selector, params string[] texts) => "[" + ImportCapabilities.InkSequence(selector, texts) + "]";

    private static List<string> Said(IEnumerable<string> played) => played.Where(l => l.StartsWith("narrator", StringComparison.Ordinal)).ToList();

    /// <summary>A knot that says a sequence and then offers to go round again or stop.</summary>
    private static string Rounds(string sequence) => Story("{\"->\":\"top\"}",
        "{\"top\":[" + sequence + ",\"ev\",\"str\",\"^Again\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":4},\"ev\",\"str\",\"^Stop\",\"/str\",\"/ev\",{\"*\":\".^.c-1\",\"flg\":4},"
        + "{\"c-0\":[\"\\n\",{\"->\":\"top\"},{\"#f\":5}],\"c-1\":[\"\\n\",\"end\",{\"#f\":5}]}]}");

    private static string[] Again(int times) => Enumerable.Repeat("o1", times).Append("o2").ToArray();

    [Fact]
    public void ASequenceSaysEachBranchInTurnAndThenTheLastOneForEver()
    {
        var played = Said(Ink(Rounds(Seq("2,\"MIN\"", "A", "B", "C")), null, null, Again(4)));
        Assert.Equal(["narrator: A", "narrator: B", "narrator: C", "narrator: C", "narrator: C"], played);
    }

    [Fact]
    public void ACycleGoesRoundAndOnceOnlyContentSaysNothingAfterItsLastBranch()
    {
        Assert.Equal(["narrator: A", "narrator: B", "narrator: A", "narrator: B", "narrator: A"], Said(Ink(Rounds(Seq("2,\"%\"", "A", "B")), null, null, Again(4))));
        var once = Ink(Rounds(Seq("2,\"MIN\"", "A", "B", "")), null, null, Again(3));
        Assert.Equal(["narrator: A", "narrator: B"], Said(once));
        Assert.Equal(4, once.Count(l => l.StartsWith("choices", StringComparison.Ordinal)));   // the story goes on to offer its choice each round
    }

    [Fact]
    public void AStoppingSequenceWhoseLastBranchIsEmptyIsTheSameAsOnceOnlyContent()
    {
        var result = InkImporter.Import(Rounds(Seq("2,\"MIN\"", "A", "B", "")), Options);
        var variant = System.Text.Json.Nodes.JsonNode.Parse(result.Document)!["conversations"]![0]!["nodes"]!.AsArray().Single(n => n!["kind"]!.GetValue<string>() == "variant")!;
        Assert.Equal("once", variant["mode"]!.GetValue<string>());
        Assert.Equal(2, variant["options"]!.AsArray().Count);
    }

    [Fact]
    public void AShuffleSaysEveryBranchOncePerRoundInAnOrderTheSeedFixes()
    {
        var played = Said(Ink(Rounds(Seq("2,\"seq\"", "A", "B")), null, null, Again(3)));
        Assert.Equal(4, played.Count);
        Assert.Equal(["narrator: A", "narrator: B"], played.Take(2).Order());
        Assert.Equal(["narrator: A", "narrator: B"], played.Skip(2).Take(2).Order());
        Assert.Equal(played, Said(Ink(Rounds(Seq("2,\"seq\"", "A", "B")), null, null, Again(3))));
    }

    [Fact]
    public void ASequenceOfMoreThanSixteenBranchesOrWithTheWrongNumberOfBranchesIsRefused()
    {
        Assert.Equal(DialogueCodes.ImportUnsupported, Fixture.Refusal(() => InkImporter.Import(Rounds(Seq("16,\"MIN\"", Enumerable.Range(0, 17).Select(i => "T" + i).ToArray())), Options)));
        Assert.Equal(DialogueCodes.ImportSyntax, Fixture.Refusal(() => InkImporter.Import(Rounds(Seq("5,\"MIN\"", "A", "B")), Options)));
    }

    [Fact]
    public void ATunnelRunsAndComesBackToWhereItWasCalledEachTime()
    {
        var story = Story("{\"->t->\":\"scene\"},\"^Back.\",\"\\n\",{\"->t->\":\"scene\"},\"^Done.\",\"\\n\",\"done\"", "{\"scene\":[\"^In the scene.\",\"\\n\",\"->->\",null]}");
        var result = InkImporter.Import(story, Options);
        Assert.Equal(["narrator: In the scene.", "narrator: Back.", "narrator: In the scene.", "narrator: Done.", "ended done"], Drive(result, "talk", null));
        var conversations = DialogueLoader.Load(result.Document, new Speakers("narrator")).Conversations.Keys;
        Assert.Equal(1, conversations.Count(k => k.StartsWith("tunnel-", StringComparison.Ordinal)));
    }

    [Fact]
    public void ATunnelCanOfferAChoiceAndNestAndItsReturnGoesBackToTheCaller()
    {
        var story = Story("{\"->t->\":\"outer\"},\"^End.\",\"\\n\",\"done\"",
            "{\"outer\":[\"^Outer.\",\"\\n\",{\"->t->\":\"inner\"},\"^Outer done.\",\"\\n\",\"->->\",null],"
            + "\"inner\":[\"ev\",\"str\",\"^Stay\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":4},{\"c-0\":[\"\\n\",\"^Stayed.\",\"\\n\",\"->->\",{\"#f\":5}]}]}");
        Assert.Equal(["narrator: Outer.", "choices Stay", "narrator: Stayed.", "narrator: Outer done.", "narrator: End.", "ended done"], Ink(story, null, null, "o1"));
    }

    [Fact]
    public void ThreadsThatOfferChoicesJoinThemToTheChoicesHereInOrder()
    {
        var story = Story("\"^Pick.\",\"\\n\",\"thread\",{\"->\":\"left\"},\"thread\",{\"->\":\"right\"},\"ev\",\"str\",\"^Stay\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":4},{\"c-0\":[\"\\n\",\"^Stayed.\",\"\\n\",\"end\",{\"#f\":5}]}",
            "{\"left\":[\"ev\",\"str\",\"^Left\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":4},{\"c-0\":[\"\\n\",\"^Went left.\",\"\\n\",\"end\",{\"#f\":5}]}],"
            + "\"right\":[\"ev\",\"str\",\"^Right\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":4},{\"c-0\":[\"\\n\",\"^Went right.\",\"\\n\",\"end\",{\"#f\":5}]}]}");
        Assert.Equal(["narrator: Pick.", "choices Left | Right | Stay", "narrator: Went right.", "ended done"], Ink(story, null, null, "o2"));
        Assert.Equal(["narrator: Pick.", "choices Left | Right | Stay", "narrator: Stayed.", "ended done"], Ink(story, null, null, "o3"));
    }

    [Fact]
    public void GlueMarksTheLineBeforeItWhetherWrittenAfterTheTextOrBeforeTheNext()
    {
        Assert.Equal(["narrator: Hello <glue>", "narrator: there.", "ended done"], Ink(Story("\"^Hello\",\"<>\",\"\\n\",\"^there.\",\"\\n\",\"done\"")));
        Assert.Equal(["narrator: One <glue>", "narrator: two", "ended done"], Ink(Story("\"^One\",\"\\n\",\"<>\",\"^two\",\"\\n\",\"done\"")));
        Assert.Equal(["narrator: We hurried home <glue>", "narrator: to Savile Row", "ended done"], Ink(Story("\"^We hurried home \",\"<>\",{\"->\":\"k\"}", "{\"k\":[\"^to Savile Row\",\"\\n\",\"done\",null]}")));
    }

    private static string Lists(string operations, string check) => Story(operations + "\"ev\"," + check + ",\"/ev\",{\"->\":\"yes\",\"c\":true},\"^No.\",\"\\n\",\"done\"",
        "{\"yes\":[\"^Yes.\",\"\\n\",\"done\",null]," + Globals("{\"list\":{\"mood.calm\":1},\"origins\":[\"mood\"]},{\"VAR=\":\"m\"}") + "}", "{\"mood\":{\"calm\":1,\"angry\":2,\"sad\":3}}");

    private static string Has(params string[] members) => "{\"VAR?\":\"m\"},{\"list\":{" + string.Join(",", members.Select(m => "\"mood." + m + "\":1")) + "}},\"?\"";

    private static string Change(string op, string member) => "\"ev\",{\"VAR?\":\"m\"},{\"list\":{\"mood." + member + "\":1}},\"" + op + "\",{\"VAR=\":\"m\",\"re\":true},\"/ev\",";

    private static string SetTo(string member) => "\"ev\",{\"list\":{\"mood." + member + "\":1},\"origins\":[\"mood\"]},{\"VAR=\":\"m\",\"re\":true},\"/ev\",";

    [Theory]
    [InlineData("", "calm", "Yes.")]
    [InlineData("", "angry", "No.")]
    [InlineData("+angry", "angry", "Yes.")]
    [InlineData("+angry", "calm", "Yes.")]
    [InlineData("-calm", "calm", "No.")]
    [InlineData("=sad", "calm", "No.")]
    [InlineData("=sad", "sad", "Yes.")]
    [InlineData("+angry,+sad,-angry", "angry", "No.")]
    [InlineData("+angry,+sad,-angry", "sad", "Yes.")]
    [InlineData("+angry", "calm,angry", "Yes.")]
    [InlineData("", "calm,angry", "No.")]
    public void AListIsASetOfMembersThatCanBeAddedToTakenFromSetAndAskedAbout(string steps, string members, string expected)
    {
        var operations = string.Concat(steps.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0] == '+' ? Change("+", s[1..]) : s[0] == '-' ? Change("-", s[1..]) : SetTo(s[1..])));
        var story = Lists(operations, Has(members.Split(',')));
        Assert.Equal(expected, Ink(story).First().Split(": ")[1]);
    }

    [Fact]
    public void AHasntTestIsTheOppositeOfHas()
    {
        var story = Lists("", "{\"VAR?\":\"m\"},{\"list\":{\"mood.sad\":1}},\"!?\"");
        Assert.Equal("Yes.", Ink(story).First().Split(": ")[1]);
    }

    [Fact]
    public void AListBecomesOneTrueFalseVariablePerMemberOfItsList()
    {
        var variables = System.Text.Json.Nodes.JsonNode.Parse(InkImporter.Import(Lists("", Has("calm")), Options).Document)!["variables"]!.AsArray().Select(v => v!["id"]!.GetValue<string>() + "=" + v["initial"]!.ToJsonString()).Order().ToList();
        Assert.Equal(["m-angry=false", "m-calm=true", "m-sad=false"], variables);
    }

    [Theory]
    [InlineData("\"ev\",{\"VAR?\":\"m\"},{\"list\":{\"mood.nobody\":9}},\"+\",{\"VAR=\":\"m\",\"re\":true},\"/ev\",", DialogueCodes.ImportSyntax)]
    [InlineData("\"ev\",{\"VAR?\":\"m\"},{\"list\":{\"mood.calm\":1}},\"*\",{\"VAR=\":\"m\",\"re\":true},\"/ev\",", DialogueCodes.ImportUnsupported)]
    [InlineData("\"ev\",3,{\"VAR=\":\"m\",\"re\":true},\"/ev\",", DialogueCodes.ImportUnsupported)]
    public void ABadListOperationIsRefused(string operation, string code) =>
        Assert.Equal(code, Fixture.Refusal(() => InkImporter.Import(Lists(operation, Has("calm")), Options)));

    [Fact]
    public void AnAllowedExternalFunctionIsReadFromTheHostFact()
    {
        var story = Story("\"ev\",\"str\",\"^sword\",\"/str\",{\"x()\":\"has_item\",\"exArgs\":1},\"/ev\",{\"->\":\"yes\",\"c\":true},\"^No sword.\",\"\\n\",\"done\"", "{\"yes\":[\"^Sword!\",\"\\n\",\"done\",null]}");
        var options = Options with { Functions = [ImportFunction.Parse("has_item(\"sword\")=has-sword:bool")] };
        Assert.Equal(["narrator: Sword!", "ended done"], Ink(story, new Dictionary<string, FactValue> { ["ext.has-sword"] = FactValue.From(true) }, options));
        Assert.Equal(["narrator: No sword.", "ended done"], Ink(story, new Dictionary<string, FactValue> { ["ext.has-sword"] = FactValue.From(false) }, options));
        var thrown = Assert.Throws<EngineRefusedException>(() => InkImporter.Import(story, Options));
        Assert.Equal(DialogueCodes.ImportFunction, thrown.Code);
        Assert.Contains("has_item(\"sword\")", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExternalFunctionWithAWholeNumberAnswerCanBeComparedAndPrinted()
    {
        var story = Story("\"^Bank \",\"ev\",{\"x()\":\"bank\",\"exArgs\":0},\"out\",\"/ev\",\"\\n\",\"ev\",{\"x()\":\"bank\",\"exArgs\":0},100,\">=\",\"/ev\",{\"->\":\"rich\",\"c\":true},\"^Poor.\",\"\\n\",\"done\"", "{\"rich\":[\"^Rich.\",\"\\n\",\"done\",null]}");
        var options = Options with { Functions = [ImportFunction.Parse("bank()=bank:int:0..100000")] };
        Assert.Equal(["narrator: Bank 250", "narrator: Rich.", "ended done"], Ink(story, new Dictionary<string, FactValue> { ["ext.bank"] = FactValue.From(250L) }, options));
        Assert.Equal(["narrator: Bank 5", "narrator: Poor.", "ended done"], Ink(story, new Dictionary<string, FactValue> { ["ext.bank"] = FactValue.From(5L) }, options));
    }

    [Fact]
    public void AVisitCountReadsHowOftenAContainerWasEnteredAtItsStart()
    {
        var story = Story("{\"->\":\"hall\"}",
            "{\"hall\":[\"ev\",{\"CNT?\":\"hall\"},1,\"==\",\"/ev\",{\"->\":\".^.first\",\"c\":true},\"^Again.\",\"\\n\",\"ev\",\"str\",\"^Once more\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":4},\"ev\",\"str\",\"^Stop\",\"/str\",\"/ev\",{\"*\":\".^.c-1\",\"flg\":4},"
            + "{\"first\":[\"^First visit.\",\"\\n\",{\"->\":\"hall\"},null],\"c-0\":[\"\\n\",{\"->\":\"hall\"},{\"#f\":5}],\"c-1\":[\"\\n\",\"end\",{\"#f\":5}]}]}");
        Assert.Equal(["narrator: First visit.", "narrator: Again.", "choices Once more | Stop", "narrator: Again.", "choices Once more | Stop", "ended done"], Ink(story, null, null, "o1", "o2"));
    }

    [Theory]
    [InlineData("\"ev\",{\"VAR?\":\"nope\"},\"out\",\"/ev\",\"done\"", DialogueCodes.ImportSyntax)]                                    // read before it was declared
    [InlineData("\"ev\",{\"VAR?\":\"gold\"},{\"VAR?\":\"gold\"},\"+\",{\"VAR=\":\"gold\",\"re\":true},\"/ev\",\"done\"", DialogueCodes.ImportUnsupported)]   // x = x + x
    [InlineData("\"ev\",{\"VAR?\":\"gold\"},\"str\",\"^a\",\"/str\",\"==\",\"/ev\",{\"->\":\"x\",\"c\":true},\"done\"", DialogueCodes.ImportSyntax)]       // a number against a text
    [InlineData("\"ev\",{\"VAR?\":\"gold\"},\"str\",\"^a\",\"/str\",\"<\",\"/ev\",{\"->\":\"x\",\"c\":true},\"done\"", DialogueCodes.ImportSyntax)]
    [InlineData("\"ev\",true,\"/ev\",{\"->\":\"x\",\"c\":true},\"done\"", DialogueCodes.ImportUnsupported)]                              // a condition that is always true
    [InlineData("\"ev\",{\"VAR?\":\"gold\"},\"visit\",\"+\",\"/ev\",{\"->\":\"x\",\"c\":true},\"done\"", DialogueCodes.ImportUnsupported)]   // a visit count outside a sequence
    [InlineData("\"ev\",\"du\",\"/ev\",\"done\"", DialogueCodes.ImportSyntax)]
    [InlineData("\"ev\",\"+\",\"/ev\",\"done\"", DialogueCodes.ImportSyntax)]
    public void AnInkMistakeInTheStackLanguageIsRefusedWithTheRightCode(string flow, string code)
    {
        var story = Story(flow, "{\"x\":[\"done\",null]," + Globals("1,{\"VAR=\":\"gold\"}") + "}");
        Assert.Equal(code, Fixture.Refusal(() => InkImporter.Import(story, Options)));
    }

    [Fact]
    public void AWholeNumberOutsideTheRangeOfAVariableIsRefused()
    {
        var story = Story("\"done\"", "{" + Globals("9999999999,{\"VAR=\":\"big\"}") + "}");
        Assert.Equal(DialogueCodes.ImportUnsupported, Fixture.Refusal(() => InkImporter.Import(story, Options)));
    }

    [Fact]
    public void InkVariableNamesThatDifferOnlyInCaseAreRefusedBecauseIdsHaveNoCase()
    {
        var story = Story("\"done\"", "{" + Globals("1,{\"VAR=\":\"Gold\"},2,{\"VAR=\":\"gold\"}") + "}");
        Assert.Equal(DialogueCodes.ImportSyntax, Fixture.Refusal(() => InkImporter.Import(story, Options)));
    }

    [Fact]
    public void ConvertingAnInkStoryTwiceGivesTheSameBytes()
    {
        var story = Rounds(Seq("2,\"seq\"", "A", "B"));
        var one = InkImporter.Import(story, Options);
        var two = InkImporter.Import(story, Options);
        Assert.Equal(one.Document, two.Document);
        Assert.Equal(one.Strings, two.Strings);
    }
}
