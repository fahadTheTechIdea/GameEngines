using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>
/// What the Yarn Spinner and Ink converters added in pass 32 do when the converted document is played: once, detour and return, enums, commands with arguments, allowed functions, visit counts and several
/// start nodes for Yarn; variables, conditions, sequences, tunnels, threads, glue, lists, visit counts and allowed external functions for Ink. The Ink stories are hand-written to the published runtime
/// format: no compiler output was available, so none of this proves the converter reads what inklecate really writes.
/// </summary>
public sealed partial class ImportFeatureTests
{
    private static readonly ImportOptions Options = new("imported.feature", "en");

    internal static List<string> Drive(ImportResult result, string conversation, IReadOnlyDictionary<string, FactValue>? facts, params string[] answers)
    {
        var set = DialogueLoader.Load(result.Document, new Speakers(result.Speakers.ToArray()));
        var table = LocaleTable.Load(result.Strings);
        var runtime = new DialogueRuntime(set, 1);
        facts ??= new Dictionary<string, FactValue>();
        var seen = new List<string>();
        var step = runtime.Start(conversation, facts);
        var next = 0;
        for (var guard = 0; guard < 200; guard++)
        {
            foreach (var e in step.Events)
                if (e is not VariableEvent) seen.Add("event " + e);
            switch (step.Kind)
            {
                case StepKind.Line:
                    seen.Add(step.Line!.Speaker + ": " + LocaleRenderer.Render(table, step.Line.Key, step.Line.Args) + (step.Line.Glue ? " <glue>" : ""));
                    step = runtime.Next(facts);
                    break;
                case StepKind.Choices:
                    seen.Add("choices " + string.Join(" | ", step.Choices!.Options.Select(o => LocaleRenderer.Render(table, o.Key, []) + (o.Enabled ? "" : "!"))));
                    step = runtime.Choose(answers[next++], facts);
                    break;
                case StepKind.Ended:
                    seen.Add("ended " + step.Result);
                    return seen;
                case StepKind.Queued:
                    throw new InvalidOperationException("a converted document does not queue");
            }
        }
        throw new InvalidOperationException("the conversation did not end");
    }

    private static List<string> Yarn(string script, ImportOptions? options = null, params string[] answers) => Drive(YarnImporter.Import(script, options ?? Options), ImportOptions.Conversation, null, answers);

    // ---- Yarn: once ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AOnceBlockRunsItsBodyTheFirstTimeAndItsElseAfter()
    {
        const string script = "title: Start\n---\n<<once>>\nAnn: First.\n<<else>>\nAnn: Again.\n<<endonce>>\n-> More\n    <<jump Start>>\n-> Stop\n===\n";
        Assert.Equal(["ann: First.", "choices More | Stop", "ann: Again.", "choices More | Stop", "ann: Again.", "choices More | Stop", "ended done"], Yarn(script, null, "o1", "o1", "o2"));
    }

    [Fact]
    public void AOnceWithoutAnElseSkipsItsBodyAfterTheFirstTime()
    {
        const string script = "title: Start\n---\n<<once>>\nAnn: Welcome.\n<<endonce>>\nAnn: Always.\n-> Again\n    <<jump Start>>\n-> Stop\n===\n";
        Assert.Equal(["ann: Welcome.", "ann: Always.", "choices Again | Stop", "ann: Always.", "choices Again | Stop", "ended done"], Yarn(script, null, "o1", "o2"));
    }

    [Fact]
    public void AOnceIfIsNotUsedUpWhileItsConditionIsFalse()
    {
        // The condition is false the first time round (the variable is set after), so the body has not run and runs the second time, once.
        const string script = "title: Start\n---\n<<declare $ready = false>>\n<<once if $ready>>\nAnn: Ready.\n<<else>>\nAnn: Not yet.\n<<endonce>>\n<<set $ready to true>>\n-> Again\n    <<jump Start>>\n-> Stop\n===\n";
        Assert.Equal(["ann: Not yet.", "choices Again | Stop", "ann: Ready.", "choices Again | Stop", "ann: Not yet.", "choices Again | Stop", "ended done"], Yarn(script, null, "o1", "o1", "o2"));
    }

    [Fact]
    public void ALineEndingInOnceIsShownOnlyTheFirstTime()
    {
        const string script = "title: Start\n---\nAnn: Who are you? <<once>>\nAnn: Bye.\n-> Again\n    <<jump Start>>\n-> Stop\n===\n";
        Assert.Equal(["ann: Who are you?", "ann: Bye.", "choices Again | Stop", "ann: Bye.", "choices Again | Stop", "ended done"], Yarn(script, null, "o1", "o2"));
    }

    [Fact]
    public void ATagMayComeBeforeOrAfterTheOnceOnALine()
    {
        const string script = "title: Start\n---\nAnn: One. <<once>> #a\nAnn: Two. #b <<once>>\n===\n";
        Assert.Equal(["ann: One.", "ann: Two.", "ended done"], Yarn(script).Where(l => !l.StartsWith("event", StringComparison.Ordinal)));
    }

    [Fact]
    public void AnOptionEndingInOnceIsHiddenAfterItWasTaken()
    {
        const string script = "title: Start\n---\n-> Ask <<once>>\n    Ann: Asked.\n    <<jump Start>>\n-> Leave\n===\n";
        Assert.Equal(["choices Ask | Leave", "ann: Asked.", "choices Leave", "ended done"], Yarn(script, null, "o1", "o2"));
    }

    [Fact]
    public void AnOptionOnceIfIsAvailableOnlyWhileItsConditionHoldsAndItWasNotTaken()
    {
        const string script = "title: Start\n---\n<<declare $k = false>>\n-> Use <<once if $k>>\n    Ann: Used.\n-> Wait\n    <<set $k to true>>\n    <<jump Start>>\n-> Stop\n    <<stop>>\n===\n";
        Assert.Equal(["choices Wait | Stop", "choices Use | Wait | Stop", "ann: Used.", "ended done"], Yarn(script, null, "o2", "o1"));
    }

    // ---- Yarn: detour and return --------------------------------------------------------------------------------------------------------

    [Fact]
    public void ADetourRunsTheOtherNodeAndComesBackToTheNextStatement()
    {
        const string script = "title: Start\n---\nAnn: Before.\n<<detour Side>>\nAnn: After.\n===\ntitle: Side\n---\nBob: In the side.\n===\n";
        var result = YarnImporter.Import(script, Options);
        Assert.Equal(["ann: Before.", "bob: In the side.", "ann: After.", "ended done"], Drive(result, "talk", null));
        Assert.Contains(result.Entries, e => e.Conversation == "talk");
        Assert.Contains("detour-side", DialogueLoader.Load(result.Document, new Speakers("ann", "bob")).Conversations.Keys);
    }

    [Fact]
    public void ReturnEndsADetourEarlyAndStopsTheDialogueOutsideOne()
    {
        const string script = "title: Start\n---\nAnn: Before.\n<<detour Side>>\nAnn: After.\n===\ntitle: Side\n---\nBob: One.\n<<return>>\nBob: Never.\n===\n";
        var result = YarnImporter.Import(script, Options);
        Assert.Contains(result.Report.Notes, n => n is { Status: ImportStatus.Dropped, Feature: "statements after a jump or stop" });
        Assert.Equal(["ann: Before.", "bob: One.", "ann: After.", "ended done"], Yarn(script));
        Assert.Equal(["ann: Hi.", "ended done"], Yarn("title: Start\n---\nAnn: Hi.\n<<return>>\nAnn: Never.\n===\n"));
    }

    [Fact]
    public void AJumpInsideADetourLeavesForGoodSoNothingAfterTheDetourRuns()
    {
        const string script = "title: Start\n---\nAnn: Before.\n<<detour Side>>\nAnn: After.\n===\ntitle: Side\n---\nBob: In the side.\n<<jump Last>>\n===\ntitle: Last\n---\nBob: Last.\n===\n";
        Assert.Equal(["ann: Before.", "bob: In the side.", "bob: Last.", "ended done"], Yarn(script));
    }

    [Fact]
    public void DetoursNestAndTheSameNodeCanBeBothJumpedToAndDetouredTo()
    {
        const string script = "title: Start\n---\nAnn: One.\n<<detour A>>\nAnn: Two.\n<<jump A>>\n===\ntitle: A\n---\nBob: In A.\n<<detour B>>\nBob: A again.\n===\ntitle: B\n---\nCat: In B.\n===\n";
        Assert.Equal(["ann: One.", "bob: In A.", "cat: In B.", "bob: A again.", "ann: Two.", "bob: In A.", "cat: In B.", "bob: A again.", "ended done"], Yarn(script));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(YarnImporter.Import(script, Options).Strings, "In A[.]"));   // the text of a node used both ways is stored once
    }

    [Fact]
    public void ADetouredNodeCanOfferOptionsAndTheCallerGoesOnAfterTheAnswer()
    {
        const string script = "title: Start\n---\n<<detour Ask>>\nAnn: Thanks.\n===\ntitle: Ask\n---\n-> Yes\n    Bob: Good.\n-> No\n    Bob: Pity.\n===\n";
        Assert.Equal(["choices Yes | No", "bob: Pity.", "ann: Thanks.", "ended done"], Yarn(script, null, "o2"));
    }

    [Fact]
    public void ADetourToANodeThatDetoursBackIsRefusedAsACircle()
    {
        Assert.Equal(DialogueCodes.ImportInvalid, Fixture.Refusal(() => YarnImporter.Import("title: Start\n---\n<<detour Other>>\n===\ntitle: Other\n---\n<<detour Start>>\nAnn: Hi.\n===\n", Options)));
    }

    [Fact]
    public void ADetourToAMissingNodeIsRefusedAsASyntaxProblem()
    {
        Assert.Equal(DialogueCodes.ImportSyntax, Fixture.Refusal(() => YarnImporter.Import("title: Start\n---\n<<detour Nowhere>>\n===\n", Options)));
        Assert.Equal(DialogueCodes.ImportUnsupported, Fixture.Refusal(() => YarnImporter.Import("title: Start\n---\n<<detour {$x}>>\n===\n", Options)));
    }

    // ---- Yarn: enums --------------------------------------------------------------------------------------------------------------------

    private const string Enums = "title: Start\n---\n<<enum Mood>>\n<<case Calm>>\n<<case Angry>>\n<<case Sad>>\n<<endenum>>\n<<declare $m = Mood.Calm>>\n";

    [Fact]
    public void AnEnumVariableHoldsOnlyItsCasesAndComparesAndSetsByCaseWithOrWithoutTheEnumName()
    {
        var script = Enums + "<<if $m == Mood.Calm>>\nAnn: Calm.\n<<endif>>\n<<set $m to Mood.Angry>>\n<<if .Angry == $m>>\nAnn: Angry.\n<<endif>>\n<<set $m = .Sad>>\n<<if $m != Mood.Calm and $m == .Sad>>\nAnn: Sad {$m}.\n<<endif>>\n===\n";
        var result = YarnImporter.Import(script, Options);
        Assert.Equal(["ann: Calm.", "ann: Angry.", "ann: Sad Sad.", "ended done"], Drive(result, "talk", null));
        var variable = System.Text.Json.Nodes.JsonNode.Parse(result.Document)!["variables"]![0]!;
        Assert.Equal(["Calm", "Angry", "Sad"], variable["allowed"]!.AsArray().Select(a => a!.GetValue<string>()));
        Assert.Equal("Calm", variable["initial"]!.GetValue<string>());
        Assert.Contains(result.Report.Notes, n => n is { Status: ImportStatus.Converted, Feature: "enum" });
    }

    [Theory]
    [InlineData("<<set $m to Mood.Happy>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<set $m to Food.Apple>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<set $m to .Happy>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<set $m to \"Calm\">>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<if $m == Mood.Happy>>\nAnn: x.\n<<endif>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<if $m == \"Happy\">>\nAnn: x.\n<<endif>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<if $m == .Happy>>\nAnn: x.\n<<endif>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<declare $other = Mood.Happy>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<enum Mood>>\n<<case Calm>>\n<<endenum>>", DialogueCodes.ImportSyntax)]       // the same enum twice
    [InlineData("<<enum Empty>>\n<<endenum>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<enum Open>>\n<<case A>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<case Stray>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<endenum>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<enum Dup>>\n<<case A>>\n<<case A>>\n<<endenum>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<enum Valued>>\n<<case A = 1>>\n<<endenum>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<declare $n = 3>>\n<<if $n == Mood.Calm>>\nAnn: x.\n<<endif>>", DialogueCodes.ImportSyntax)]
    public void AnEnumMistakeIsRefusedWithTheRightCode(string extra, string code) =>
        Assert.Equal(code, Fixture.Refusal(() => YarnImporter.Import(Enums + extra + "\nAnn: Hi.\n===\n", Options)));

    // ---- Yarn: commands with arguments --------------------------------------------------------------------------------------------------

    [Fact]
    public void ACommandWithArgumentsIsASignalThatCarriesThemInTheirTypes()
    {
        const string script = "title: Start\n---\n<<declare $gold = 5>>\n<<declare $ok = true>>\n<<give_item sword 3 \"to you\" true {$gold} {$ok}>>\nAnn: Hi.\n===\n";
        var result = YarnImporter.Import(script, Options);
        var set = DialogueLoader.Load(result.Document, new Speakers("ann"));
        var step = new DialogueRuntime(set, 1).Start("talk", new Dictionary<string, FactValue>());
        var signal = step.Events.OfType<SignalEvent>().Single();
        Assert.Equal("give-item", signal.Name);
        Assert.Equal([FactValue.From("sword"), FactValue.From(3L), FactValue.From("to you"), FactValue.From(true), FactValue.From(5L), FactValue.From(true)], signal.Args);
    }

    [Fact]
    public void ACommandArgumentThatIsAVariableIsReadWhenTheSignalIsRaised()
    {
        const string script = "title: Start\n---\n<<declare $gold = 5>>\n<<set $gold to $gold + 4>>\n<<pay {$gold}>>\n<<set $gold to 0>>\nAnn: Hi.\n===\n";
        var step = new DialogueRuntime(DialogueLoader.Load(YarnImporter.Import(script, Options).Document, new Speakers("ann")), 1).Start("talk", new Dictionary<string, FactValue>());
        Assert.Equal(FactValue.From(9L), step.Events.OfType<SignalEvent>().Single().Args.Single());
    }

    [Theory]
    [InlineData("<<give {$a + 1}>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<give 1.5>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<give $a>>", DialogueCodes.ImportUnsupported)]
    [InlineData("<<give {$nope}>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<give \"open>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<give {$a>>", DialogueCodes.ImportSyntax)]
    [InlineData("<<give 1 2 3 4 5 6 7 8 9>>", DialogueCodes.ImportUnsupported)]
    public void ACommandArgumentThatCannotBeKeptIsRefused(string command, string code) =>
        Assert.Equal(code, Fixture.Refusal(() => YarnImporter.Import("title: Start\n---\n<<declare $a = 1>>\n" + command + "\nAnn: Hi.\n===\n", Options)));

    [Fact]
    public void ACommandWithoutArgumentsStaysAPlainSignalAndWaitIsOneToo()
    {
        var step = new DialogueRuntime(DialogueLoader.Load(YarnImporter.Import("title: Start\n---\n<<shake>>\n<<wait 2>>\nAnn: Hi.\n===\n", Options).Document, new Speakers("ann")), 1).Start("talk", new Dictionary<string, FactValue>());
        Assert.Equal(["shake", "wait"], step.Events.OfType<SignalEvent>().Select(e => e.Name));
        Assert.Equal([FactValue.From(2L)], step.Events.OfType<SignalEvent>().Last().Args);
    }

    // ---- Yarn: functions ----------------------------------------------------------------------------------------------------------------

    private static ImportOptions WithFunctions(params string[] functions) => Options with { Functions = functions.Select(ImportFunction.Parse).ToList() };

    [Fact]
    public void AnAllowedFunctionIsReadFromTheFactTheHostSuppliesInConditionsComparisonsAndText()
    {
        const string script = "title: Start\n---\n<<if has_item(\"sword\")>>\nAnn: Armed.\n<<endif>>\n<<if not has_item(\"shield\") and bank() >= 100>>\nAnn: Rich and bare.\n<<endif>>\nAnn: The bank holds {bank()} and the mood is {mood()}.\n===\n";
        var options = WithFunctions("has_item(\"sword\")=has-sword:bool", "has_item(\"shield\")=has-shield:bool", "bank()=bank:int:0..100000", "mood()=mood:string");
        var result = YarnImporter.Import(script, options);
        Assert.Equal(new[] { "has-shield", "has-sword", "bank", "mood" }.Order(), result.Functions.Select(f => f.Fact).Order());
        Facts F(bool sword, bool shield, long bank) => new()
        {
            ["ext.has-sword"] = FactValue.From(sword), ["ext.has-shield"] = FactValue.From(shield), ["ext.bank"] = FactValue.From(bank), ["ext.mood"] = FactValue.From("calm"),
        };
        Assert.Equal(["ann: Armed.", "ann: Rich and bare.", "ann: The bank holds 250 and the mood is calm.", "ended done"], Drive(result, "talk", F(true, false, 250)));
        Assert.Equal(["ann: The bank holds 50 and the mood is calm.", "ended done"], Drive(result, "talk", F(false, true, 50)));
    }

    private sealed class Facts : Dictionary<string, FactValue>;

    [Fact]
    public void AFunctionNotOnTheListIsRefusedWithTheFunctionCodeAndNamesTheCall()
    {
        var thrown = Assert.Throws<EngineRefusedException>(() => YarnImporter.Import("title: Start\n---\n<<if has_item(\"axe\")>>\nAnn: x.\n<<endif>>\n===\n", WithFunctions("has_item(\"sword\")=has-sword:bool")));
        Assert.Equal(DialogueCodes.ImportFunction, thrown.Code);
        Assert.Contains("has_item(\"axe\")", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("roll_it()")]
    [InlineData("coin(2) == 1")]
    [InlineData("min(1, 10) > 5")]
    [InlineData("round(1) == 1")]
    [InlineData("bank(1) > 5")]
    [InlineData("Bank() > 5")]
    public void EveryOtherFunctionIsRefusedBecauseTheHostAnswersOnlyWhatTheListSays(string condition) =>
        Assert.Equal(DialogueCodes.ImportFunction, Fixture.Refusal(() => YarnImporter.Import("title: Start\n---\n<<if " + condition + ">>\nAnn: x.\n<<endif>>\n===\n", WithFunctions("bank()=bank:int:0..10"))));

    [Fact]
    public void AFunctionAnswerOfTheWrongTypeIsRefusedLikeAnyOtherMismatch()
    {
        var options = WithFunctions("bank()=bank:int:0..10");
        Assert.Equal(DialogueCodes.ImportSyntax, Fixture.Refusal(() => YarnImporter.Import("title: Start\n---\n<<if bank() == \"x\">>\nAnn: x.\n<<endif>>\n===\n", options)));
        Assert.Equal(DialogueCodes.ImportSyntax, Fixture.Refusal(() => YarnImporter.Import("title: Start\n---\n<<if bank()>>\nAnn: x.\n<<endif>>\n===\n", options)));
    }

    [Theory]
    [InlineData("nothing")]
    [InlineData("f=x:bool")]
    [InlineData("f(=x:bool")]
    [InlineData("f()=:bool")]
    [InlineData("f()=X:bool")]
    [InlineData("f()=x:float")]
    [InlineData("f()=x:int")]
    [InlineData("f()=x:int:5..1")]
    [InlineData("f()=x:int:a..b")]
    [InlineData("f()=x:int:0..2000000000")]
    [InlineData("f(1.5)=x:bool")]
    [InlineData("f(\"open)=x:bool")]
    [InlineData("f()=x:bool:extra")]
    public void AMalformedFunctionListEntryIsRefusedWithTheFunctionCode(string entry) =>
        Assert.Equal(DialogueCodes.ImportFunction, Fixture.Refusal(() => ImportFunction.Parse(entry)));

    [Fact]
    public void AFunctionListEntryCanFixTextNumbersAndTrueFalseArgumentsAndSpacesDoNotMatter()
    {
        var f = ImportFunction.Parse("check( \"a b\" , 5 , true )=chk:string");
        Assert.Equal("check(\"a b\",5,true)", f.Signature);
        var script = "title: Start\n---\n<<if check(\"a b\", 5, true) == \"yes\">>\nAnn: ok.\n<<endif>>\n===\n";
        var result = YarnImporter.Import(script, Options with { Functions = [f] });
        Assert.Equal(["ann: ok.", "ended done"], Drive(result, "talk", new Dictionary<string, FactValue> { ["ext.chk"] = FactValue.From("yes") }));
    }

    [Fact]
    public void TwoFunctionsOnOneFactMustAgreeOnItsTypeAndRange()
    {
        const string script = "title: Start\n---\n<<if a() > 1 and b() > 1>>\nAnn: x.\n<<endif>>\n===\n";
        Assert.NotNull(YarnImporter.Import(script, WithFunctions("a()=n:int:0..9", "b()=n:int:0..9")));
        Assert.Equal(DialogueCodes.ImportFunction, Fixture.Refusal(() => YarnImporter.Import(script, WithFunctions("a()=n:int:0..9", "b()=n:int:0..99"))));
    }

    [Fact]
    public void CallingAFunctionForItsEffectIsRefusedEvenWhenAllowed()
    {
        Assert.Equal(DialogueCodes.ImportFunction, Fixture.Refusal(() => YarnImporter.Import("title: Start\n---\n<<call has_key()>>\nAnn: Hi.\n===\n", WithFunctions("has_key()=has-key:bool"))));
    }

    // ---- Yarn: visited -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void VisitedAndVisitedCountReadHowOftenANodeWasEntered()
    {
        const string script = "title: Start\n---\n<<if visited(\"Shop\")>>\nAnn: You have been.\n<<else>>\nAnn: New here.\n<<endif>>\n<<if visited_count(\"Shop\") >= 2>>\nAnn: Regular.\n<<endif>>\n-> Go\n    <<jump Shop>>\n-> Leave\n===\ntitle: Shop\n---\nBob: Welcome.\n<<jump Start>>\n===\n";
        var result = YarnImporter.Import(script, Options);
        Assert.Equal(["ann: New here.", "choices Go | Leave", "bob: Welcome.", "ann: You have been.", "choices Go | Leave", "bob: Welcome.", "ann: You have been.", "ann: Regular.", "choices Go | Leave", "ended done"],
            Drive(result, "talk", null, "o1", "o1", "o2"));
        Assert.Contains(result.Report.Notes, n => n is { Status: ImportStatus.Approximated, Feature: "visited / visited_count" });
    }

    [Fact]
    public void VisitedCountsEntriesAnywhereEvenThroughADetour()
    {
        const string script = "title: Start\n---\n<<detour Shop>>\n<<detour Shop>>\n<<if visited_count(\"Shop\") == 2>>\nAnn: Twice.\n<<endif>>\n===\ntitle: Shop\n---\nBob: Hi.\n===\n";
        Assert.Equal(["bob: Hi.", "bob: Hi.", "ann: Twice.", "ended done"], Yarn(script));
    }

    [Theory]
    [InlineData("visited(\"Nowhere\")", DialogueCodes.ImportSyntax)]
    [InlineData("visited(5)", DialogueCodes.ImportUnsupported)]
    [InlineData("visited()", DialogueCodes.ImportUnsupported)]
    [InlineData("visited(\"Start\") == true", DialogueCodes.ImportUnsupported)]
    public void AMistakeInVisitedIsRefused(string condition, string code) =>
        Assert.Equal(code, Fixture.Refusal(() => YarnImporter.Import("title: Start\n---\n<<if " + condition + ">>\nAnn: x.\n<<endif>>\n===\n", Options)));

    // ---- Yarn: several starts -----------------------------------------------------------------------------------------------------------

    private const string TwoEntries = "title: Greeting\n---\nAnn: Hello.\n<<jump Common>>\n===\ntitle: Farewell\n---\nAnn: Goodbye.\n<<jump Common>>\n===\ntitle: Common\n---\nBob: See you.\n===\n";

    [Fact]
    public void EachStartNodeIsItsOwnConversationAndTheEntryTableSaysWhichIsWhich()
    {
        var result = YarnImporter.Import(TwoEntries, Options with { Starts = ["Greeting", "Farewell"] });
        Assert.Equal([("greeting", "talk-greeting"), ("farewell", "talk-farewell")], result.Entries.Select(e => (e.Name, e.Conversation)));
        Assert.Equal(["ann: Hello.", "bob: See you.", "ended done"], Drive(result, "talk-greeting", null));
        Assert.Equal(["ann: Goodbye.", "bob: See you.", "ended done"], Drive(result, "talk-farewell", null));
        Assert.Contains(result.Report.Notes, n => n is { Status: ImportStatus.Converted, Feature: "start nodes" });
    }

    [Fact]
    public void AllStartsMakesAStartOfEveryNodeNothingElseLeadsTo()
    {
        var result = YarnImporter.Import(TwoEntries, Options with { AllStarts = true });
        Assert.Equal(["greeting", "farewell"], result.Entries.Select(e => e.Name));
        Assert.Equal(2, DialogueLoader.Load(result.Document, new Speakers("ann", "bob")).Conversations.Count);
    }

    [Fact]
    public void AllStartsOfACircleOfNodesIsEveryNode()
    {
        var result = YarnImporter.Import("title: A\n---\nAnn: a.\n<<jump B>>\n===\ntitle: B\n---\nAnn: b.\n-> Again\n    <<jump A>>\n-> Stop\n===\n", Options with { AllStarts = true });
        Assert.Equal(["a", "b"], result.Entries.Select(e => e.Name));
    }

    [Fact]
    public void ASingleNamedStartStillMakesTheOneConversationCalledTalk()
    {
        var result = YarnImporter.Import(TwoEntries, Options with { Starts = ["Farewell"] });
        Assert.Equal([("farewell", "talk")], result.Entries.Select(e => (e.Name, e.Conversation)));
        Assert.Equal(["ann: Goodbye.", "bob: See you.", "ended done"], Drive(result, "talk", null));
    }

    [Fact]
    public void AStartThatDoesNotExistOrIsNamedTwiceIsRefused()
    {
        Assert.Equal(DialogueCodes.ImportSyntax, Fixture.Refusal(() => YarnImporter.Import(TwoEntries, Options with { Starts = ["Greeting", "Nope"] })));
        Assert.Equal(DialogueCodes.ImportSyntax, Fixture.Refusal(() => YarnImporter.Import(TwoEntries, Options with { Starts = ["Greeting", "greeting"] })));
    }

    [Fact]
    public void ConvertingWithSeveralStartsIsRepeatableAndSharedTextIsStoredOnce()
    {
        var one = YarnImporter.Import(TwoEntries, Options with { Starts = ["Greeting", "Farewell"] });
        var two = YarnImporter.Import(TwoEntries, Options with { Starts = ["Greeting", "Farewell"] });
        Assert.Equal(one.Document, two.Document);
        Assert.Equal(one.Strings, two.Strings);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(one.Strings, "See you"));
    }

    // ---- Ink -------------------------------------------------------------------------------------------------------------------------------

    internal static string Story(string flow, string? knots = null, string? lists = null) =>
        "{\"inkVersion\":21,\"root\":[[" + flow + (flow.EndsWith('}') ? "" : ",null") + "],\"done\"," + (knots ?? "null") + "],\"listDefs\":" + (lists ?? "{}") + "}";

    internal static string Globals(string declarations) => "\"global decl\":[\"ev\"," + declarations + ",\"/ev\",\"end\",null]";

    internal static List<string> Ink(string story, IReadOnlyDictionary<string, FactValue>? facts = null, ImportOptions? options = null, params string[] answers) =>
        Drive(InkImporter.Import(story, options ?? Options), ImportOptions.Conversation, facts, answers);

    private static string Loop(string sequence) => Story("{\"->\":\"top\"}",
        "{\"top\":[" + sequence + ",\"ev\",\"str\",\"^Again\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":4},{\"c-0\":[\"\\n\",{\"->\":\"top\"},{\"#f\":5}]}]}");

    [Fact]
    public void AGlobalVariableIsPrintedAssignedAndAddedTo()
    {
        var story = Story("\"^Gold: \",\"ev\",{\"VAR?\":\"gold\"},\"out\",\"/ev\",\"\\n\",\"ev\",{\"VAR?\":\"gold\"},3,\"+\",{\"VAR=\":\"gold\",\"re\":true},\"/ev\",\"^Now \",\"ev\",{\"VAR?\":\"gold\"},\"out\",\"/ev\",\"\\n\",\"ev\",{\"VAR?\":\"gold\"},2,\"-\",{\"VAR=\":\"gold\",\"re\":true},\"/ev\",\"ev\",10,{\"VAR=\":\"gold\",\"re\":true},\"/ev\",\"^Last \",\"ev\",{\"VAR?\":\"gold\"},\"out\",\"/ev\",\"\\n\",\"done\"",
            "{" + Globals("5,{\"VAR=\":\"gold\"}") + "}");
        Assert.Equal(["narrator: Gold: 5", "narrator: Now 8", "narrator: Last 10", "ended done"], Ink(story));
    }

    [Fact]
    public void TextAndTrueFalseVariablesAreDeclaredWithTheirTypesAndTemporariesToo()
    {
        var story = Story("\"ev\",\"str\",\"^calm\",\"/str\",{\"VAR=\":\"mood\",\"re\":true},\"/ev\",\"ev\",true,{\"VAR=\":\"seen\",\"re\":true},\"/ev\",\"ev\",7,{\"temp=\":\"n\"},\"/ev\",\"^Mood \",\"ev\",{\"VAR?\":\"mood\"},\"out\",\"/ev\",\"^ and \",\"ev\",{\"VAR?\":\"n\"},\"out\",\"/ev\",\"\\n\",\"ev\",{\"VAR?\":\"seen\"},\"/ev\",{\"->\":\"yes\",\"c\":true},\"^No.\",\"\\n\",\"done\"",
            "{\"yes\":[\"^Yes.\",\"\\n\",\"done\",null]," + Globals("\"str\",\"^bored\",\"/str\",{\"VAR=\":\"mood\"},false,{\"VAR=\":\"seen\"}") + "}");
        var result = InkImporter.Import(story, Options);
        Assert.Equal(["narrator: Mood calm and 7", "narrator: Yes.", "ended done"], Drive(result, "talk", null));
        var variables = System.Text.Json.Nodes.JsonNode.Parse(result.Document)!["variables"]!.AsArray().Select(v => v!["id"] + ":" + v["type"]).Order().ToList();
        Assert.Equal(new[] { "mood:string", "seen:bool", "t-n:int" }, variables.Select(s => s!.Replace("\"", "")));
    }

    [Fact]
    public void ConditionalDivertsAndConditionalTextFollowTheVariable()
    {
        string Rich(int gold) => Story("\"ev\",{\"VAR?\":\"gold\"},3,\">\",\"/ev\",[{\"->\":\".^.b\",\"c\":true},{\"b\":[\"\\n\",\"^Rich.\",\"\\n\",{\"->\":\"0.7\"},null]}],[{\"->\":\".^.b\"},{\"b\":[\"\\n\",\"^Poor.\",\"\\n\",{\"->\":\"0.7\"},null]}],\"nop\",\"\\n\",\"^After.\",\"\\n\",\"done\"",
            "{" + Globals(gold + ",{\"VAR=\":\"gold\"}") + "}");
        Assert.Equal(["narrator: Rich.", "narrator: After.", "ended done"], Ink(Rich(5)));
        Assert.Equal(["narrator: Poor.", "narrator: After.", "ended done"], Ink(Rich(1)));
    }

    [Fact]
    public void EveryComparisonAndLogicalOperatorMeansWhatItSays()
    {
        var checks = new (string Expression, bool Holds)[]
        {
            ("{\"VAR?\":\"a\"},5,\"==\"", true), ("{\"VAR?\":\"a\"},5,\"!=\"", false), ("{\"VAR?\":\"a\"},4,\">\"", true), ("{\"VAR?\":\"a\"},5,\">=\"", true), ("{\"VAR?\":\"a\"},5,\"<\"", false), ("{\"VAR?\":\"a\"},5,\"<=\"", true),
            ("7,{\"VAR?\":\"a\"},\">\"", true), ("7,{\"VAR?\":\"a\"},\"<\"", false), ("{\"VAR?\":\"b\"}", true), ("{\"VAR?\":\"b\"},\"!\"", false), ("{\"VAR?\":\"a\"}", true),
            ("{\"VAR?\":\"a\"},5,\"==\",{\"VAR?\":\"b\"},\"&&\"", true), ("{\"VAR?\":\"a\"},9,\"==\",{\"VAR?\":\"b\"},\"||\"", true), ("{\"VAR?\":\"a\"},9,\"==\",{\"VAR?\":\"b\"},\"!\",\"||\"", false),
            ("{\"VAR?\":\"s\"},\"str\",\"^x\",\"/str\",\"==\"", true),
        };
        // Each test diverts away when it holds, so run the story once per check with only that check in place.
        for (var i = 0; i < checks.Length; i++)
        {
            var single = Story("\"ev\"," + checks[i].Expression + ",\"/ev\",{\"->\":\"yes\",\"c\":true},\"^No.\",\"\\n\",\"done\"", "{\"yes\":[\"^Yes.\",\"\\n\",\"done\",null]," + Globals("5,{\"VAR=\":\"a\"},true,{\"VAR=\":\"b\"},\"str\",\"^x\",\"/str\",{\"VAR=\":\"s\"}") + "}");
            Assert.True(Ink(single).First().EndsWith(checks[i].Holds ? "Yes." : "No.", StringComparison.Ordinal), "check " + i + " " + checks[i].Expression);
        }
    }

    [Fact]
    public void AChoiceWithAConditionIsHiddenWhileItIsFalseAndTheStoryEndsWhenNoneIsLeft()
    {
        string Shop(int gold) => Story("\"ev\",{\"VAR?\":\"gold\"},0,\">\",\"/ev\",\"ev\",\"str\",\"^Buy\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":5},{\"c-0\":[\"\\n\",\"^Bought.\",\"\\n\",\"end\",{\"#f\":5}]}",
            "{" + Globals(gold + ",{\"VAR=\":\"gold\"}") + "}");
        Assert.Equal(["choices Buy", "narrator: Bought.", "ended done"], Ink(Shop(3), null, null, "o1"));
        Assert.Equal(["ended done"], Ink(Shop(0)));
    }

}
