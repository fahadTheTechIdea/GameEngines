using System.Text.Json.Nodes;
using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>
/// The Ink converter's pass-33 additions, on hand-written compiled JSON: temporaries per knot and stitch, threads that say text, shuffle once and shuffle stopping, and glue at the start of content a
/// divert reaches. No inklecate was available, so none of these stories is real compiler output; the shuffle once / stopping shape and the stitch naming rest on the author's memory of the compiler
/// (the format document describes neither) and are the least certain parts.
/// </summary>
public sealed partial class ImportFeatureTests
{
    private static List<string> InkSeeded(string story, ulong seed, params string[] answers)
    {
        var result = InkImporter.Import(story, Options);
        var set = DialogueLoader.Load(result.Document, new Speakers(result.Speakers.ToArray()));
        var table = LocaleTable.Load(result.Strings);
        var runtime = new DialogueRuntime(set, seed);
        var facts = new Dictionary<string, FactValue>();
        var seen = new List<string>();
        var next = 0;
        var step = runtime.Start(ImportOptions.Conversation, facts);
        for (var guard = 0; guard < 200; guard++)
        {
            switch (step.Kind)
            {
                case StepKind.Line:
                    seen.Add(step.Line!.Speaker + ": " + LocaleRenderer.Render(table, step.Line.Key, step.Line.Args));
                    step = runtime.Next(facts);
                    break;
                case StepKind.Choices:
                    seen.Add("choices");
                    step = runtime.Choose(answers[next++], facts);
                    break;
                case StepKind.Ended:
                    return seen;
                case StepKind.Queued:
                    throw new InvalidOperationException("a converted document does not queue");
            }
        }
        throw new InvalidOperationException("the conversation did not end");
    }

    // ---- temporaries per knot and stitch ------------------------------------------------------------------------------------------------

    [Fact]
    public void TheSameTemporaryNameInTwoKnotsIsTwoVariablesEvenWithDifferentTypes()
    {
        var story = Story("{\"->\":\"a\"}",
            "{\"a\":[\"ev\",5,{\"temp=\":\"x\"},\"/ev\",\"^A: \",\"ev\",{\"VAR?\":\"x\"},\"out\",\"/ev\",\"\\n\",{\"->\":\"b\"},null],"
            + "\"b\":[\"ev\",\"str\",\"^hi\",\"/str\",{\"temp=\":\"x\"},\"/ev\",\"^B: \",\"ev\",{\"VAR?\":\"x\"},\"out\",\"/ev\",\"\\n\",\"done\",null]}");
        Assert.Equal(["narrator: A: 5", "narrator: B: hi", "ended done"], Ink(story));
        var variables = JsonNode.Parse(InkImporter.Import(story, Options).Document)!["variables"]!.AsArray().Select(v => v!["id"]!.GetValue<string>()).ToList();
        Assert.Equal(["t-a-x", "t-b-x"], variables);
    }

    [Fact]
    public void ATemporaryIsSetAgainEachTimeItsDeclarationRunsAndAStitchSeesItsKnotsTemporary()
    {
        // knot k declares n = 0 and adds one, says it, offers to go round; every round says 1. Its stitch inner reads the knot's temporary x.
        var story = Story("{\"->\":\"k\"}",
            "{\"k\":[\"ev\",0,{\"temp=\":\"n\"},\"/ev\",\"ev\",{\"VAR?\":\"n\"},1,\"+\",{\"temp=\":\"n\",\"re\":true},\"/ev\",\"^N=\",\"ev\",{\"VAR?\":\"n\"},\"out\",\"/ev\",\"\\n\","
            + "\"ev\",\"str\",\"^Again\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":4},\"ev\",\"str\",\"^In\",\"/str\",\"/ev\",{\"*\":\".^.c-1\",\"flg\":4},"
            + "{\"c-0\":[\"\\n\",{\"->\":\"k\"},{\"#f\":5}],\"c-1\":[\"\\n\",\"ev\",9,{\"temp=\":\"x\"},\"/ev\",{\"->\":\"k.inner\"},{\"#f\":5}],"
            + "\"inner\":[\"^X=\",\"ev\",{\"VAR?\":\"x\"},\"out\",\"/ev\",\"\\n\",\"done\",null]}]}");
        Assert.Equal(["narrator: N=1", "choices Again | In", "narrator: N=1", "choices Again | In", "narrator: N=1", "choices Again | In", "narrator: X=9", "ended done"],
            Ink(story, null, null, "o1", "o1", "o2"));
    }

    [Fact]
    public void ATemporaryReadInAnotherKnotIsNotTheOneDeclaredElsewhere()
    {
        // knot b reads x, which only knot a declares: it is not visible there, as in Ink.
        var story = Story("{\"->\":\"a\"}",
            "{\"a\":[\"ev\",5,{\"temp=\":\"x\"},\"/ev\",{\"->\":\"b\"},null],\"b\":[\"^X: \",\"ev\",{\"VAR?\":\"x\"},\"out\",\"/ev\",\"\\n\",\"done\",null]}");
        Assert.Equal(DialogueCodes.ImportSyntax, Fixture.Refusal(() => InkImporter.Import(story, Options)));
    }

    // ---- threads that say text ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void AThreadSaysItsTextWhereItIsPulledInThenTheFlowGoesOnAndItsChoicesJoinTheOnesHere()
    {
        var story = Story("\"^Pick.\",\"\\n\",\"thread\",{\"->\":\"chat\"},\"^After.\",\"\\n\",\"ev\",\"str\",\"^Stay\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":4},{\"c-0\":[\"\\n\",\"^Stayed.\",\"\\n\",\"end\",{\"#f\":5}]}",
            "{\"chat\":[\"^Hello.\",\"\\n\",\"ev\",\"str\",\"^Leave\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":4},{\"c-0\":[\"\\n\",\"^Left.\",\"\\n\",\"end\",{\"#f\":5}]}]}");
        Assert.Equal(["narrator: Pick.", "narrator: Hello.", "narrator: After.", "choices Leave | Stay", "narrator: Left.", "ended done"], Ink(story, null, null, "o1"));   // the thread's choice was met first
    }

    [Fact]
    public void AThreadThatSetsAVariableDoesSoBeforeTheTextAfterIt()
    {
        var story = Story("\"^Start.\",\"\\n\",\"thread\",{\"->\":\"setter\"},\"^Gold: \",\"ev\",{\"VAR?\":\"gold\"},\"out\",\"/ev\",\"\\n\",\"done\"",
            "{\"setter\":[\"ev\",7,{\"VAR=\":\"gold\",\"re\":true},\"/ev\",\"done\",null]," + Globals("1,{\"VAR=\":\"gold\"}") + "}");
        Assert.Equal(["narrator: Start.", "narrator: Gold: 7", "ended done"], Ink(story));
    }

    [Fact]
    public void AThreadThatGoesOnWithADivertOrAConditionIsRefusedAndSaysWhich()
    {
        var diverting = Story("\"thread\",{\"->\":\"chat\"},\"done\"", "{\"chat\":[\"^Hi.\",\"\\n\",{\"->\":\"other\"},null],\"other\":[\"done\",null]}");
        var refusal = Assert.Throws<EngineRefusedException>(() => InkImporter.Import(diverting, Options));
        Assert.Equal(DialogueCodes.ImportUnsupported, refusal.Code);
        Assert.Contains("go", refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- glue at the start of content a divert reaches ----------------------------------------------------------------------------------

    [Fact]
    public void GlueAtTheStartOfContentGluesTheLineSaidJustBeforeTheDivert()
    {
        var story = Story("\"^Hello\",\"\\n\",{\"->\":\"k\"}", "{\"k\":[\"<>\",\"^there.\",\"\\n\",\"done\",null]}");
        Assert.Equal(["narrator: Hello <glue>", "narrator: there.", "ended done"], Ink(story));
    }

    [Fact]
    public void VariableChangesBetweenTheLineAndTheDivertDoNotHideTheLineTheGlueJoins()
    {
        var story = Story("\"^Hello\",\"\\n\",\"ev\",3,{\"VAR=\":\"n\",\"re\":true},\"/ev\",{\"->\":\"k\"}", "{\"k\":[\"<>\",\"^there.\",\"\\n\",\"done\",null]," + Globals("1,{\"VAR=\":\"n\"}") + "}");
        Assert.Equal(["narrator: Hello <glue>", "narrator: there.", "ended done"], Ink(story));
    }

    [Fact]
    public void GlueAfterAVariableChangeInTheSameRunStillGluesTheLineBeforeIt()
    {
        var story = Story("\"^Hello\",\"\\n\",\"ev\",3,{\"VAR=\":\"n\",\"re\":true},\"/ev\",\"<>\",\"^there.\",\"\\n\",\"done\"", "{" + Globals("1,{\"VAR=\":\"n\"}") + "}");
        Assert.Equal(["narrator: Hello <glue>", "narrator: there.", "ended done"], Ink(story));
    }

    [Fact]
    public void GlueAtTheStartOfContentReachedFromTwoPlacesIsRefusedWhenOneOfThemSaysNothingFirst()
    {
        // The start says Hello and diverts to k (fine); knot other diverts to k with nothing said before it in its run, so which line k's glue joins is not known.
        var story = Story("\"^Hello\",\"\\n\",{\"->\":\"k\"}", "{\"k\":[\"<>\",\"^there.\",\"\\n\",\"ev\",{\"VAR?\":\"n\"},0,\">\",\"/ev\",{\"->\":\"other\",\"c\":true},\"done\",null],"
            + "\"other\":[{\"->\":\"k\"},null]," + Globals("1,{\"VAR=\":\"n\"}") + "}");
        var refusal = Assert.Throws<EngineRefusedException>(() => InkImporter.Import(story, Options));
        Assert.Equal(DialogueCodes.ImportUnsupported, refusal.Code);
        Assert.Contains("glue", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GlueAtTheStartOfAChoiceBranchIsStillRefused()
    {
        var story = Story("\"ev\",\"str\",\"^Go\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":4},{\"c-0\":[\"\\n\",\"<>\",\"^glued.\",\"\\n\",\"end\",{\"#f\":5}]}");
        Assert.Equal(DialogueCodes.ImportUnsupported, Fixture.Refusal(() => InkImporter.Import(story, Options)));
    }

    // ---- shuffle once and shuffle stopping ----------------------------------------------------------------------------------------------

    private static string ShuffleRounds(int last, params string[] texts) => Rounds("[" + ImportCapabilities.InkShuffleLimited(last, texts) + "]");

    [Fact]
    public void AStoppingShuffleSaysTheEarlierBranchesOnceInAShuffledOrderThenTheLastForEver()
    {
        var orders = new HashSet<string>();
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var played = Said(InkSeeded(ShuffleRounds(3, "A", "B", "C", "D"), seed, Again(6)));
            Assert.Equal(["narrator: A", "narrator: B", "narrator: C"], played.Take(3).Order());
            Assert.Equal(["narrator: D", "narrator: D", "narrator: D", "narrator: D"], played.Skip(3).Take(4));
            Assert.Equal(7, played.Count);
            orders.Add(string.Join(",", played.Take(3)));
        }
        Assert.True(orders.Count >= 4, "thirty seeds gave only " + orders.Count + " orders of three branches");
    }

    [Fact]
    public void AShuffleOnceSaysEachBranchOnceInAShuffledOrderThenNothingAndTheStoryGoesOn()
    {
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var played = InkSeeded(ShuffleRounds(2, "A", "B", ""), seed, Again(4));
            Assert.Equal(["narrator: A", "narrator: B"], Said(played).Order());
            Assert.Equal(5, played.Count(l => l == "choices"));
        }
    }

    [Fact]
    public void TheSameSeedGivesTheSameShuffleOnceOrderAndTheDocumentUsesAShuffleNodeEnteredByAOnceNode()
    {
        var story = ShuffleRounds(2, "A", "B", "C");
        Assert.Equal(InkSeeded(story, 5, Again(4)), InkSeeded(story, 5, Again(4)));
        var nodes = JsonNode.Parse(InkImporter.Import(story, Options).Document)!["conversations"]![0]!["nodes"]!.AsArray();
        var variants = nodes.Where(n => n!["kind"]!.GetValue<string>() == "variant").ToList();
        var outer = Assert.Single(variants, v => v!["mode"]!.GetValue<string>() == "once");
        var inner = Assert.Single(variants, v => v!["mode"]!.GetValue<string>() == "shuffle");
        Assert.Equal([inner!["id"]!.GetValue<string>(), inner["id"]!.GetValue<string>()], outer!["options"]!.AsArray().Select(o => o!.GetValue<string>()));
        Assert.Equal(2, inner["options"]!.AsArray().Count);
    }

    [Fact]
    public void AShuffleOnceOrStoppingOfMoreThanSixteenShuffledBranchesIsRefused()
    {
        var texts = Enumerable.Range(0, 18).Select(i => "T" + i).ToArray();
        Assert.Equal(DialogueCodes.ImportUnsupported, Fixture.Refusal(() => InkImporter.Import(ShuffleRounds(17, texts), Options)));
    }

    [Fact]
    public void AShuffleOnceOrStoppingWithNothingBeforeItsLastBranchHasNothingToShuffleAndIsRefused() =>
        Assert.Equal(DialogueCodes.ImportUnsupported, Fixture.Refusal(() => InkImporter.Import(ShuffleRounds(0, "A"), Options)));

    [Theory]
    [InlineData(false)]   // the compare is there but the divert that acts on it is not
    [InlineData(true)]    // neither the compare nor the divert: MIN followed by seq
    public void AMinThenSeqWithoutTheSkipDivertIsNotAShuffleOnceOrStopping(bool withoutCompare)
    {
        // The skip compare-and-divert is what tells a shuffle once / stopping from a stray MIN and seq; without it the story is refused instead of guessed.
        var shape = ImportCapabilities.InkShuffleLimited(2, "A", "B", "C");
        var stray = withoutCompare
            ? shape.Replace("\"du\",2,\"==\",{\"->\":\".^.10\",\"c\":true},", "", StringComparison.Ordinal).Replace(".^.^.30", ".^.^.26", StringComparison.Ordinal)
            : shape.Replace("{\"->\":\".^.10\",\"c\":true},", "", StringComparison.Ordinal).Replace(".^.^.30", ".^.^.29", StringComparison.Ordinal);
        Assert.NotEqual(shape, stray);
        Assert.Equal(DialogueCodes.ImportUnsupported, Fixture.Refusal(() => InkImporter.Import(Rounds("[" + stray + "]"), Options)));
    }

    [Fact]
    public void GlueAfterAConditionalDivertIsRefusedAndTheMessageNamesIt()
    {
        var story = Story("\"ev\",{\"VAR?\":\"n\"},0,\">\",\"/ev\",{\"->\":\"other\",\"c\":true},\"<>\",\"^x\",\"\\n\",\"done\"", "{\"other\":[\"done\",null]," + Globals("1,{\"VAR=\":\"n\"}") + "}");
        var refusal = Assert.Throws<EngineRefusedException>(() => InkImporter.Import(story, Options));
        Assert.Equal(DialogueCodes.ImportUnsupported, refusal.Code);
        Assert.Contains("conditional divert", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AShuffleWhoseSkipDoesNotMatchItsLastBranchIsNotTakenForOnceOrStopping()
    {
        // The skip compares with 1 while the shuffle counts 2: not the shape the compiler writes, so it is refused instead of guessed.
        var odd = ImportCapabilities.InkShuffleLimited(2, "A", "B", "C").Replace("\"du\",2,\"==\",{\"->\":\".^.10\"", "\"du\",1,\"==\",{\"->\":\".^.10\"", StringComparison.Ordinal);
        Assert.Equal(DialogueCodes.ImportUnsupported, Fixture.Refusal(() => InkImporter.Import(Rounds("[" + odd + "]"), Options)));
    }
}
