using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>More cases of <see cref="ImportFeatureTests"/> that pin the order of tags, texts and conditions, subtraction, and the starts of a script.</summary>
public sealed partial class ImportFeatureTests
{
    [Fact]
    public void TagsBeforeAndAfterTheOnceOfALineAreAllKeptInTheOrderWritten()
    {
        const string script = "title: Start\n---\nAnn: Hi. #first #second <<once>> #third #fourth\n===\n";
        var set = DialogueLoader.Load(YarnImporter.Import(script, Options).Document, new Speakers("ann"));
        var step = new DialogueRuntime(set, 1).Start("talk", new Dictionary<string, FactValue>());
        Assert.Equal(["first", "second", "third", "fourth"], step.Line!.Tags);
    }

    [Fact]
    public void ANodeThatOnlyLeadsToItselfIsStillAStartWhenAllStartsAreWanted()
    {
        const string script = "title: Loop\n---\nAnn: x.\n-> Again\n    <<jump Loop>>\n-> Stop\n===\ntitle: Other\n---\nAnn: y.\n===\n";
        Assert.Equal(["loop", "other"], YarnImporter.Import(script, Options with { AllStarts = true }).Entries.Select(e => e.Name));
    }

    [Fact]
    public void AGlobalCanBeTakenFromAndTheResultIsPrinted()
    {
        var story = Story("\"ev\",{\"VAR?\":\"gold\"},3,\"-\",{\"VAR=\":\"gold\",\"re\":true},\"/ev\",\"^Gold \",\"ev\",{\"VAR?\":\"gold\"},\"out\",\"/ev\",\"\\n\",\"done\"", "{" + Globals("10,{\"VAR=\":\"gold\"}") + "}");
        Assert.Equal(["narrator: Gold 7", "ended done"], Ink(story));
    }

    [Fact]
    public void OnlyWholeNumbersCompareInOrder()
    {
        var story = Story("\"ev\",{\"VAR?\":\"mood\"},\"str\",\"^b\",\"/str\",\"<\",\"/ev\",{\"->\":\"x\",\"c\":true},\"done\"", "{\"x\":[\"done\",null]," + Globals("\"str\",\"^calm\",\"/str\",{\"VAR=\":\"mood\"}") + "}");
        Assert.Equal(DialogueCodes.ImportSyntax, Fixture.Refusal(() => InkImporter.Import(story, Options)));
    }

    [Theory]
    [InlineData("\"ev\",\"str\",\"^Hello\",\"/str\",\"str\",\"^ there\",\"/str\",\"/ev\"", 6)]
    [InlineData("\"ev\",{\"VAR?\":\"ok\"},\"str\",\"^Hello\",\"/str\",\"str\",\"^ there\",\"/str\",\"/ev\"", 7)]
    [InlineData("\"ev\",\"str\",\"^Hello\",\"/str\",\"str\",\"^ there\",\"/str\",{\"VAR?\":\"ok\"},\"/ev\"", 7)]
    [InlineData("\"ev\",\"str\",\"^Hello\",\"/str\",{\"VAR?\":\"ok\"},\"str\",\"^ there\",\"/str\",\"/ev\"", 7)]
    public void AChoiceWithStartAndChoiceOnlyTextShowsThemInThatOrderWhereverTheConditionWasPushed(string pushes, int flags)
    {
        var story = Story(pushes + ",{\"*\":\".^.c-0\",\"flg\":" + flags + "},{\"c-0\":[\"\\n\",\"^Done.\",\"\\n\",\"end\",{\"#f\":5}]}", "{" + Globals("true,{\"VAR=\":\"ok\"}") + "}");
        Assert.Equal(["choices Hello there", "narrator: Done.", "ended done"], Ink(story, null, null, "o1"));
    }

    [Fact]
    public void AShorthandCaseNeedsAVariableThatHoldsAnEnum()
    {
        Assert.Equal(DialogueCodes.ImportSyntax, Fixture.Refusal(() => YarnImporter.Import(Enums + "<<declare $s = \"x\">>\n<<if $s == .Calm>>\nAnn: x.\n<<endif>>\nAnn: Hi.\n===\n", Options)));
    }

    [Fact]
    public void AShuffleIsAVariantInShuffleModeAndNotACycle()
    {
        var result = InkImporter.Import(Rounds(Seq("2,\"seq\"", "A", "B")), Options);
        var variant = System.Text.Json.Nodes.JsonNode.Parse(result.Document)!["conversations"]![0]!["nodes"]!.AsArray().Single(n => n!["kind"]!.GetValue<string>() == "variant")!;
        Assert.Equal("shuffle", variant["mode"]!.GetValue<string>());
    }

    [Fact]
    public void AnExternalFunctionWithAWholeNumberAnswerIsTrueWhenItIsNotZero()
    {
        var story = Story("\"ev\",{\"x()\":\"bank\",\"exArgs\":0},\"/ev\",{\"->\":\"rich\",\"c\":true},\"^Empty.\",\"\\n\",\"done\"", "{\"rich\":[\"^Some.\",\"\\n\",\"done\",null]}");
        var options = Options with { Functions = [ImportFunction.Parse("bank()=bank:int:0..100")] };
        Assert.Equal(["narrator: Some.", "ended done"], Ink(story, new Dictionary<string, FactValue> { ["ext.bank"] = FactValue.From(5L) }, options));
        Assert.Equal(["narrator: Empty.", "ended done"], Ink(story, new Dictionary<string, FactValue> { ["ext.bank"] = FactValue.From(0L) }, options));
    }

    [Fact]
    public void TextWrittenBeforeAConditionalIsGluedToWhatTheConditionSays()
    {
        var story = Story("\"^Gold: \",\"ev\",{\"VAR?\":\"gold\"},3,\">\",\"/ev\",{\"->\":\"rich\",\"c\":true},\"^Poor.\",\"\\n\",\"done\"", "{\"rich\":[\"^Rich.\",\"\\n\",\"done\",null]," + Globals("1,{\"VAR=\":\"gold\"}") + "}");
        Assert.Equal(["narrator: Gold: <glue>", "narrator: Poor.", "ended done"], Ink(story));
    }
}
