using System.Text;
using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>
/// Content warnings as a declared tag mechanism: the document names the warning tags and the true/false host fact for each, and what happens to blocked content (passed over, or replaced by a declared
/// alternative). Comparable: Ink and Yarn Spinner tags are free text the game acts on (the Ink documentation read this pass); declaring the warning tags, a host policy fact and a skip or replace rule per
/// tag is this engine's own design (docs/DIALOGUE_ENGINE.md section 9 says what was and was not reached).
/// </summary>
public sealed class ContentWarningTests
{
    internal const string Document = """
    {
     "format": 3, "id": "demo.warnings",
     "variables": [], "externalFacts": [ { "id": "cw-violence", "type": "bool" }, { "id": "cw-spiders", "type": "bool" } ],
     "contentWarnings": [ { "tag": "cw.violence", "fact": "cw-violence", "action": "replace" }, { "tag": "cw.spiders", "fact": "cw-spiders", "action": "skip" } ],
     "speakers": [ { "id": "cmdr", "portraits": ["calm"] } ], "quests": [],
     "conversations": [
      { "id": "talk", "start": "l1", "nodes": [
        { "id": "l1", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.l1", "next": "l2" },
        { "id": "l2", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.l2", "tags": ["cw.violence", "loud"], "alternative": "k.l2.safe", "next": "l3" },
        { "id": "l3", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.l3", "tags": ["cw.spiders"], "next": "l4" },
        { "id": "l4", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.l4", "next": "ask" },
        { "id": "ask", "kind": "choice", "pick": "player", "options": [
           { "id": "fight", "line": "k.fight", "tags": ["cw.violence"], "alternative": "k.fight.safe", "next": "bye" },
           { "id": "web", "line": "k.web", "tags": ["cw.spiders"], "next": "bye" },
           { "id": "walk", "line": "k.walk", "next": "bye" } ] },
        { "id": "bye", "kind": "end", "result": "done" } ] },
      { "id": "only-web", "start": "w", "nodes": [
        { "id": "w", "kind": "choice", "pick": "player", "fallback": "wend", "options": [ { "id": "web", "line": "k.web", "tags": ["cw.spiders"], "next": "wend" } ] },
        { "id": "wend", "kind": "end", "result": "done" } ] },
      { "id": "mixed", "start": "m", "nodes": [
        { "id": "m", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.m", "tags": ["cw.violence", "cw.spiders"], "alternative": "k.m.safe", "next": "pick" },
        { "id": "pick", "kind": "choice", "pick": "player", "options": [
           { "id": "both", "line": "k.both", "tags": ["cw.violence", "cw.spiders"], "alternative": "k.both.safe", "next": "mend" },
           { "id": "plain", "line": "k.plain", "next": "mend" } ] },
        { "id": "mend", "kind": "end", "result": "done" } ] },
      { "id": "no-fallback", "start": "n", "nodes": [
        { "id": "n", "kind": "choice", "pick": "player", "options": [ { "id": "web", "line": "k.web", "tags": ["cw.spiders"], "next": "nend" } ] },
        { "id": "nend", "kind": "end", "result": "done" } ] }
     ]
    }
    """;

    private static IReadOnlyDictionary<string, FactValue> Facts(bool violence = false, bool spiders = false) => new Dictionary<string, FactValue>
    {
        ["ext.cw-violence"] = FactValue.From(violence),
        ["ext.cw-spiders"] = FactValue.From(spiders),
    };

    private static DialogueRuntime Runtime(string? document = null) => new(DialogueLoader.Load(document ?? Document, Fixture.Catalog), 1);

    private static string Refusal(string old, string replacement) => Fixture.Refusal(() => DialogueLoader.Load(Fixture.With(old, replacement, Document), Fixture.Catalog));

    // ---- what the player sees ------------------------------------------------------------------------------------------------------

    [Fact]
    public void WithNothingBlockedEveryLineAndAnswerIsShownAsWritten()
    {
        var runtime = Runtime();
        var facts = Facts();
        Assert.Equal("k.l1", runtime.Start("talk", facts).Line!.Key);
        var l2 = runtime.Next(facts).Line!;
        Assert.Equal(("k.l2", false), (l2.Key, l2.Replaced));
        Assert.Equal("l3", runtime.Next(facts).Line!.Node);
        Assert.Equal("l4", runtime.Next(facts).Line!.Node);
        var ask = runtime.Next(facts).Choices!;
        Assert.Equal([("fight", "k.fight", false), ("web", "k.web", false), ("walk", "k.walk", false)], ask.Options.Select(o => (o.Id, o.Key, o.Replaced)));
        Assert.All(ask.Options, o => Assert.True(o.Enabled));
    }

    [Fact]
    public void ABlockedReplaceWarningShowsTheDeclaredAlternativeAndKeepsTheLinesTagsAndSpeaker()
    {
        var runtime = Runtime();
        var facts = Facts(violence: true);
        runtime.Start("talk", facts);
        var line = runtime.Next(facts).Line!;
        Assert.Equal(("k.l2.safe", true, "cmdr", "l2"), (line.Key, line.Replaced, line.Speaker, line.Node));
        Assert.Equal(["cw.violence", "loud"], line.Tags);
    }

    [Fact]
    public void ABlockedSkipWarningPassesTheLineWithoutShowingItAndItIsNotCountedAsSeen()
    {
        var runtime = Runtime();
        runtime.Start("talk", Facts(spiders: true));
        runtime.Next(Facts(spiders: true));                              // l2
        var next = runtime.Next(Facts(spiders: true));                   // l3 is skipped: l4 is what comes
        Assert.Equal("l4", next.Line!.Node);
        Assert.False(runtime.State.Visits.ContainsKey("talk/l3"));
        Assert.DoesNotContain(runtime.History(), e => e.Node == "l3");
    }

    [Fact]
    public void ALineSkippedOnceIsNewTheFirstTimeItIsShownLater()
    {
        // The player blocks spiders on the first pass and allows them on the second: the line was never shown, so it must not read as seen.
        var runtime = Runtime();
        var blocked = Facts(spiders: true);
        runtime.Start("talk", blocked);
        runtime.Next(blocked);
        runtime.Next(blocked);
        runtime.Next(blocked);
        Assert.Equal(StepKind.Ended, runtime.Choose("walk", blocked).Kind);
        runtime.Start("talk", Facts());
        Assert.True(runtime.Next(Facts()).Line!.Seen);   // l2 was shown before
        var l3 = runtime.Next(Facts()).Line!;
        Assert.Equal(("l3", false), (l3.Node, l3.Seen));
        Assert.Equal(1, runtime.State.Visits["talk/l3"]);
    }

    [Fact]
    public void AnAnswerThatIsBlockedToSkipIsHiddenAndOneThatReplacesShowsItsAlternative()
    {
        var runtime = Runtime();
        var facts = Facts(violence: true, spiders: true);
        runtime.Start("talk", facts);
        runtime.Next(facts);
        runtime.Next(facts);
        var ask = runtime.Next(facts).Choices!;
        Assert.Equal([("fight", "k.fight.safe", true), ("walk", "k.walk", false)], ask.Options.Select(o => (o.Id, o.Key, o.Replaced)));
    }

    [Fact]
    public void AHiddenAnswerCannotBeTakenAndTheRefusalLeavesTheStateAlone()
    {
        var runtime = Runtime();
        var facts = Facts(spiders: true);
        runtime.Start("talk", facts);
        runtime.Next(facts);
        runtime.Next(facts);
        runtime.Next(facts);   // l3 is passed, l4 shown, then the choice
        var before = DialogueSave.StateDigest(runtime.State);
        Assert.Equal(DialogueCodes.OptionUnavailable, Fixture.Refusal(() => runtime.Choose("web", facts)));
        Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
    }

    [Fact]
    public void AReplacedAnswerIsKeptInTheHistoryUnderItsAlternativeKey()
    {
        var runtime = Runtime();
        var facts = Facts(violence: true);
        runtime.Start("talk", facts);
        runtime.Next(facts);
        runtime.Next(facts);
        runtime.Next(facts);
        runtime.Next(facts);   // choices
        runtime.Choose("fight", facts);
        var kept = runtime.History();
        Assert.Equal(["k.l1", "k.l2.safe", "k.l3", "k.l4", "k.fight.safe"], kept.Select(e => e.Key));
        Assert.Equal([false, true, false, false, true], kept.Select(e => e.Replaced));
    }

    [Fact]
    public void WhenOneWarningReplacesAndAnotherHidesTheHidingOneWinsForALineAndForAnAnswer()
    {
        var facts = Facts(violence: true, spiders: true);
        var runtime = Runtime();
        Assert.Equal(StepKind.Choices, runtime.Start("mixed", facts).Kind);   // the line is passed (spiders skip), so the choice is next
        Assert.Equal(["plain"], runtime.State.Node == "pick" ? runtime.ExplainChoice(facts).Where(e => e.Availability == OptionAvailability.Available).Select(e => e.Id).ToList() : []);
        var onlyViolence = Runtime();
        var replaced = onlyViolence.Start("mixed", Facts(violence: true));
        Assert.Equal(("k.m.safe", true), (replaced.Line!.Key, replaced.Line.Replaced));
        var choices = onlyViolence.Next(Facts(violence: true)).Choices!;
        Assert.Equal([("both", "k.both.safe", true), ("plain", "k.plain", false)], choices.Options.Select(o => (o.Id, o.Key, o.Replaced)));
    }

    [Fact]
    public void WhenEveryAnswerIsBlockedTheChoiceTakesItsFallbackOrIsRefusedAsNoOptionAvailable()
    {
        var facts = Facts(spiders: true);
        var runtime = Runtime();
        Assert.Equal(StepKind.Ended, runtime.Start("only-web", facts).Kind);
        Assert.Equal(DialogueCodes.NoOption, Fixture.Refusal(() => runtime.Start("no-fallback", facts)));
        Assert.False(runtime.InConversation);
    }

    [Fact]
    public void TheHostMustSupplyEveryPolicyFactAWarnedLineNeedsAndAMissingOneRefusesTheCall()
    {
        var runtime = Runtime();
        runtime.Start("talk", Facts());
        var before = DialogueSave.StateDigest(runtime.State);
        var onlyViolence = new Dictionary<string, FactValue> { ["ext.cw-violence"] = FactValue.From(false) };
        Assert.Equal(DialogueCodes.FactMissing, Fixture.Refusal(() => runtime.Next(new Dictionary<string, FactValue>())));
        Assert.Equal(DialogueCodes.FactMissing, Fixture.Refusal(() => { runtime.Next(Facts()); runtime.Next(onlyViolence); }));
        Assert.NotEqual(before, DialogueSave.StateDigest(runtime.State));   // the first Next succeeded
        Assert.Equal("l2", runtime.State.Node);
    }

    [Fact]
    public void AMistypedPolicyFactIsRefusedAsInvalid()
    {
        var runtime = Runtime();
        runtime.Start("talk", Facts());
        var wrong = new Dictionary<string, FactValue> { ["ext.cw-violence"] = FactValue.From(1L), ["ext.cw-spiders"] = FactValue.From(false) };
        Assert.Equal(DialogueCodes.FactInvalid, Fixture.Refusal(() => runtime.Next(wrong)));
    }

    [Fact]
    public void TheExplanationOfAnAnswerNamesTheWarningThatHidesIt()
    {
        var runtime = Runtime();
        var facts = Facts(spiders: true);
        runtime.Start("talk", facts);
        runtime.Next(facts);
        runtime.Next(facts);
        runtime.Next(facts);
        var why = runtime.ExplainChoice(facts).Single(e => e.Id == "web");
        Assert.Equal(OptionAvailability.Hidden, why.Availability);
        Assert.Contains("blocked by content warning 'cw.spiders'", why.Why);
    }

    [Fact]
    public void ABlockedLineIsReportedInTheTraceAndTheTraceChangesNothing()
    {
        var plain = Runtime();
        var traced = Runtime();
        traced.Explaining = true;
        plain.Start("talk", Facts(spiders: true));
        traced.Start("talk", Facts(spiders: true));
        plain.Next(Facts(spiders: true));
        traced.Next(Facts(spiders: true));
        plain.Next(Facts(spiders: true));
        var step = traced.Next(Facts(spiders: true));
        Assert.Contains("content warning 'cw.spiders'", traced.LastTrace!.ToText());
        Assert.Equal("l4", step.Line!.Node);
        Assert.Equal(DialogueSave.StateDigest(plain.State), DialogueSave.StateDigest(traced.State));
    }

    [Fact]
    public void ASavedSessionKeepsGoingWithTheSamePolicyDecisions()
    {
        var runtime = Runtime();
        runtime.Start("talk", Facts(violence: true));
        runtime.Next(Facts(violence: true));
        var resumed = DialogueSave.Open(runtime.Set, DialogueSave.Write(runtime));
        var one = runtime.Next(Facts(violence: true, spiders: true));
        var two = resumed.Next(Facts(violence: true, spiders: true));
        Assert.Equal(one.Line!.Node, two.Line!.Node);
        Assert.Equal(DialogueSave.StateDigest(runtime.State), DialogueSave.StateDigest(resumed.State));
    }

    // ---- the document --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ACwTagThatIsNotDeclaredIsRefusedSoATypoCannotShowUnwarnedContent()
    {
        Assert.Equal(DialogueCodes.WarningUndeclared, Refusal("\"tags\": [\"cw.violence\", \"loud\"]", "\"tags\": [\"cw.violance\", \"loud\"]"));
        Assert.Equal(DialogueCodes.WarningUndeclared, Refusal("\"tags\": [\"cw.violence\"], \"alternative\": \"k.fight.safe\"", "\"tags\": [\"cw.gore\"], \"alternative\": \"k.fight.safe\""));
    }

    [Theory]
    [InlineData("gore")]    // no cw. prefix
    [InlineData("cw.")]     // no name after it
    [InlineData("Cw.gore")] // not an id
    public void ADeclaredWarningTagMustStartWithCwAndNameTheWarning(string tag)
    {
        // An extra declaration nothing uses, so the refusal can only come from the declaration itself.
        var code = Refusal("\"contentWarnings\": [", "\"contentWarnings\": [ { \"tag\": \"" + tag + "\", \"fact\": \"cw-violence\", \"action\": \"skip\" },");
        Assert.Equal(tag == "Cw.gore" ? DialogueCodes.Format : DialogueCodes.WarningUndeclared, code);
    }

    [Fact]
    public void TheFactOfAWarningMustBeADeclaredTrueFalseExternalFact()
    {
        Assert.Equal(DialogueCodes.WarningFact, Refusal("\"fact\": \"cw-violence\"", "\"fact\": \"not-declared\""));
        Assert.Equal(DialogueCodes.WarningFact, Refusal("{ \"id\": \"cw-violence\", \"type\": \"bool\" }", "{ \"id\": \"cw-violence\", \"type\": \"string\" }"));
    }

    [Fact]
    public void AWarningThatReplacesNeedsAnAlternativeAndAnAlternativeNeedsAReplacingWarning()
    {
        Assert.Equal(DialogueCodes.WarningAlternative, Refusal("\"tags\": [\"cw.violence\", \"loud\"], \"alternative\": \"k.l2.safe\", ", "\"tags\": [\"cw.violence\", \"loud\"], "));
        Assert.Equal(DialogueCodes.WarningAlternative, Refusal("\"tags\": [\"cw.spiders\"], \"next\": \"l4\"", "\"tags\": [\"cw.spiders\"], \"alternative\": \"k.l3.safe\", \"next\": \"l4\""));
        Assert.Equal(DialogueCodes.WarningAlternative, Refusal("\"tags\": [\"cw.violence\"], \"alternative\": \"k.fight.safe\", ", "\"tags\": [\"cw.violence\"], "));
        Assert.Equal(DialogueCodes.WarningAlternative, Refusal("{ \"id\": \"walk\", \"line\": \"k.walk\", \"next\": \"bye\" }", "{ \"id\": \"walk\", \"line\": \"k.walk\", \"alternative\": \"k.walk.safe\", \"next\": \"bye\" }"));
    }

    [Fact]
    public void ADeclarationTwiceOrWithAnUnknownActionIsRefused()
    {
        Assert.Equal(DialogueCodes.Duplicate, Refusal("{ \"tag\": \"cw.spiders\", \"fact\": \"cw-spiders\", \"action\": \"skip\" }", "{ \"tag\": \"cw.violence\", \"fact\": \"cw-spiders\", \"action\": \"skip\" }"));
        Assert.Equal(DialogueCodes.Format, Refusal("\"action\": \"skip\"", "\"action\": \"hide\""));
        Assert.Equal(DialogueCodes.UnknownField, Refusal("\"action\": \"skip\"", "\"action\": \"skip\", \"extra\": 1"));
    }

    [Fact]
    public void ALineThatCanBeSkippedDoesNotCountAsAWaitSoLoopsAndLongRunsOfThemAreRefused()
    {
        // A cycle of lines that can all be skipped could run for ever when they are blocked.
        var loop = Fixture.With("\"tags\": [\"cw.spiders\"], \"next\": \"l4\"", "\"tags\": [\"cw.spiders\"], \"next\": \"l3\"", Document);
        Assert.Equal(DialogueCodes.Unreachable, Fixture.Refusal(() => DialogueLoader.Load(loop, Fixture.Catalog)));   // l4 is no longer reached

        var chain = new StringBuilder();
        for (var i = 1; i <= 70; i++) chain.Append("{ \"id\": \"s" + i + "\", \"kind\": \"line\", \"speaker\": \"cmdr\", \"portrait\": \"calm\", \"line\": \"k.s" + i + "\", \"tags\": [\"cw.spiders\"], \"next\": \"" + (i == 70 ? "z" : "s" + (i + 1)) + "\" },");
        var text = Fixture.With("\"conversations\": [", "\"conversations\": [ { \"id\": \"chain\", \"start\": \"s1\", \"nodes\": [" + chain + " { \"id\": \"z\", \"kind\": \"line\", \"speaker\": \"cmdr\", \"portrait\": \"calm\", \"line\": \"k.z\", \"next\": \"e\" }, { \"id\": \"e\", \"kind\": \"end\", \"result\": \"done\" } ] },", Document);
        Assert.Equal(DialogueCodes.SilentChain, Fixture.Refusal(() => DialogueLoader.Load(text, Fixture.Catalog)));
    }

    [Fact]
    public void ASkippableLoopOfLinesIsRefusedAsALoopWithoutWait()
    {
        var text = Fixture.With("\"conversations\": [", """
            "conversations": [ { "id": "spin", "start": "a", "nodes": [
              { "id": "a", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.a", "tags": ["cw.spiders"], "next": "b" },
              { "id": "b", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.b", "tags": ["cw.spiders"], "next": "a" } ] },
            """, Document);
        Assert.Equal(DialogueCodes.Loop, Fixture.Refusal(() => DialogueLoader.Load(text, Fixture.Catalog)));
    }

    [Fact]
    public void ALoopOfReplacingLinesIsFineBecauseTheyStillWait()
    {
        var text = Fixture.With("\"conversations\": [", """
            "conversations": [ { "id": "spin", "start": "a", "nodes": [
              { "id": "a", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.a", "tags": ["cw.violence"], "alternative": "k.a.safe", "next": "b" },
              { "id": "b", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.b", "next": "a" } ] },
            """, Document);
        Assert.NotNull(DialogueLoader.Load(text, Fixture.Catalog));
    }

    [Fact]
    public void TheAlternativeKeysAreLocalisationKeysOfTheDocumentLikeAnyOther()
    {
        var set = DialogueLoader.Load(Document, Fixture.Catalog);
        Assert.Contains("k.l2.safe", set.Keys.Keys);
        Assert.Contains("k.fight.safe", set.Keys.Keys);
        Assert.Contains(set.KeyUses["k.l2.safe"], u => u.Contains("l2", StringComparison.Ordinal));
        Assert.Equal(2, set.ContentWarnings.Count);
        Assert.Equal((WarningAction.Skip, "cw-spiders"), (set.ContentWarnings["cw.spiders"].Action, set.ContentWarnings["cw.spiders"].Fact));
    }

    [Fact]
    public void AnAlternativeUsedWithOtherPlaceholdersThanItsLineIsRefused()
    {
        var text = Fixture.With("\"line\": \"k.l2\", \"tags\"", "\"line\": \"k.l2\", \"args\": { \"v\": \"ext.cw-violence\" }, \"tags\"", Document);
        // The line now takes a placeholder; its alternative takes the same one, so this is accepted; an alternative key reused by a line with other placeholders is not.
        Assert.NotNull(DialogueLoader.Load(text, Fixture.Catalog));
        var clash = Fixture.With("\"alternative\": \"k.l2.safe\"", "\"alternative\": \"k.l1\"", Document);
        Assert.Equal(DialogueCodes.Duplicate, Fixture.Refusal(() => DialogueLoader.Load(clash.Replace("\"line\": \"k.l2\", \"tags\"", "\"line\": \"k.l2\", \"args\": { \"v\": \"ext.cw-violence\" }, \"tags\""), Fixture.Catalog)));
    }

    [Fact]
    public void ADocumentWithoutWarningsStillLoadsAndNothingIsAskedOfTheHost()
    {
        var runtime = new DialogueRuntime(DialogueLoader.Load(Fixture.Document, Fixture.Catalog), 1);
        Assert.Empty(runtime.Set.ContentWarnings);
        Assert.Equal(StepKind.Line, runtime.Start("briefing", Fixture.Facts()).Kind);
    }
}
