using EngineCommon;
using System.Text.Json.Nodes;
using DialogueEngine;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>The dialogue authoring tool: every command, its output, and the exit code of every mistake.</summary>
public sealed class CliTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "dlg-cli-" + Guid.NewGuid().ToString("N"));

    public CliTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    private string File(string name, string text)
    {
        var path = Path.Combine(_folder, name);
        System.IO.File.WriteAllText(path, text);
        return path;
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = DialogueCli.Run(args, output, error);
        return (code, output.ToString().Replace("\r\n", "\n"), error.ToString().Replace("\r\n", "\n"));
    }

    private const string English = """
    { "format": 1, "locale": "en", "messages": {
      "brief.hello": { "select": "count", "one": "{count} squad is ready.", "other": "{count} squads are ready." },
      "brief.accept": "Understood.", "brief.refuse": "No.", "brief.secret": "I know a way.", "brief.lucky": "Good luck.", "brief.plain": "Hold the bridge.",
      "quest.hold.title": "Hold the bridge", "quest.hold.order": "Do not let them cross.", "quest.hold.done": "The bridge held." } }
    """;

    // ---- check --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ACheckOfAGoodDocumentSaysSoWithWhatIsInIt()
    {
        var (code, output, error) = Run("check", File("doc.json", Fixture.Document));
        Assert.Equal(0, code);
        Assert.Equal("", error);
        var lines = output.TrimEnd('\n').Split('\n');
        Assert.Equal($"ok demo.briefing digest {Fixture.Load().Digest} conversations 1 variables 3 external-facts 2 speakers 2 quests 1 text-keys 9", lines[0]);
        Assert.Equal("conversation briefing start hello nodes 8 (branch 1, choice 1, effects 1, end 1, line 3, random 1)", lines[1]);
    }

    [Fact]
    public void ACheckListsTheConversationsInIdOrder()
    {
        var (_, output, _) = Run("check", File("doc.json", FeatureFixture.Document));
        Assert.Equal(
            ["conversation alarm start a1 nodes 2 (line 1, return 1)", "conversation detour start d1 nodes 3 (effects 1, line 1, return 1)", "conversation main start hi nodes 8 (call 1, choice 1, end 1, line 4, variant 1)", "conversation quit start q1 nodes 2 (end 1, line 1)"],
            output.TrimEnd('\n').Split('\n').Skip(1));
    }

    [Theory]
    [InlineData("\"format\": 3", "\"format\": 1", DialogueCodes.Format)]                                                // the old format
    [InlineData("\"next\": \"ask\" }", "\"next\": \"nowhere\" }", DialogueCodes.Reference)]
    [InlineData("\"portrait\": \"calm\", \"line\": \"brief.hello\"", "\"portrait\": \"angry\", \"line\": \"brief.hello\"", DialogueCodes.UnknownPortrait)]
    [InlineData("\"id\": \"bye\", \"kind\": \"end\"", "\"id\": \"bye\", \"kind\": \"teleport\"", DialogueCodes.Format)]
    public void ACheckOfABadDocumentPrintsTheCodeAndExitsOne(string old, string replacement, string code)
    {
        var (exit, output, error) = Run("check", File("bad.json", Fixture.With(old, replacement)));
        Assert.Equal(1, exit);
        Assert.Equal("", error);
        Assert.StartsWith("refused " + code + ": ", output);
    }

    [Fact]
    public void ADocumentThatIsNotJsonIsRefusedAndASpeakerOfTheDocumentIsAcceptedWithoutAGameCatalog()
    {
        Assert.StartsWith("refused " + DialogueCodes.Format, Run("check", File("junk.json", "{ nope")).Output);
        Assert.Equal(0, Run("check", File("doc.json", Fixture.Document)).Code);                // cmdr and scout are not in any catalog the tool has: the document's own speakers stand in
    }

    // ---- explain ------------------------------------------------------------------------------------------------------------------

    private const string Session = """
    { "seed": 1, "facts": { "ext.alarm": false },
      "calls": [ { "call": "start", "conversation": "main" }, { "call": "next" }, { "call": "next" }, { "call": "interrupt", "conversation": "alarm" }, { "call": "next" },
                 { "call": "choose", "option": "stay" }, { "call": "next" }, { "call": "timeout" } ] }
    """;

    [Fact]
    public void ExplainPlaysASessionAndSaysWhyEachCallDidWhatItDid()
    {
        var (code, output, error) = Run("explain", File("doc.json", FeatureFixture.Document), File("session.json", Session));
        Assert.Equal(0, code);
        Assert.Equal("", error);
        Assert.Equal(
            "> start main\n"
            + "main/hi [line]\n"
            + "line main/hi cmdr/calm key main.hi tags greeting,loud voice vo-hi emotion happy auto-advance 1500ms\n"
            + "> next\n"
            + "main/sub [call] - calls detour, comes back to ask\n"
            + "detour/d1 [line]\n"
            + "line detour/d1 cmdr/calm key detour.d1\n"
            + "> next\n"
            + "detour/d2 [effects] - 1 effects applied\n"
            + "detour/back [return] - returns\n"
            + "main/ask [choice] - stay: Available (no condition); leave: Available (no condition); shout: ShownDisabled (var.mood gte 3 [1] = false); sneak: Hidden (ext.alarm eq true [false] = false)\n"
            + "choices main/ask: stay, leave, shout (disabled); after 5000ms takes stay\n"
            + "  stay: Available (no condition)\n"
            + "  leave: Available (no condition)\n"
            + "  shout: ShownDisabled (var.mood gte 3 [1] = false)\n"
            + "  sneak: Hidden (ext.alarm eq true [false] = false)\n"
            + "event variable mood = 1\n", output.Split("> interrupt")[0]);
        Assert.Contains("> interrupt alarm\nalarm/a1 [line]\nline alarm/a1 cmdr/calm key alarm.a1 emotion angry\n", output);
        Assert.Contains("> timeout\n", output);
        Assert.Contains("main/var [variant] - cycle, entered 1 times before -> vb\n", output);
    }

    [Fact]
    public void ExplainStopsAtTheFirstRefusedCallAndSaysWhy()
    {
        var session = Session.Replace("{ \"call\": \"choose\", \"option\": \"stay\" }", "{ \"call\": \"choose\", \"option\": \"shout\" }");
        var (code, output, _) = Run("explain", File("doc.json", FeatureFixture.Document), File("session.json", session));
        Assert.Equal(1, code);
        Assert.EndsWith("refused DLG_OPTION_UNAVAILABLE: answer 'shout' is not available now\n", output);
        Assert.DoesNotContain("> timeout", output);
    }

    [Theory]
    [InlineData("\"seed\": 1", "\"seed\": -1", DialogueCodes.Range)]
    [InlineData("\"ext.alarm\": false", "\"ext.alarm\": 3", DialogueCodes.ValueType)]
    [InlineData("\"ext.alarm\": false", "\"ext.ghost\": false", DialogueCodes.Reference)]
    [InlineData("\"ext.alarm\": false", "\"xxxxalarm\": false", DialogueCodes.Reference)]                     // the name must start with ext., not merely end like a fact
    [InlineData("\"call\": \"start\"", "\"call\": \"warp\"", DialogueCodes.Format)]
    [InlineData("\"conversation\": \"main\"", "\"conversation\": \"nowhere\"", DialogueCodes.NoConversation)]
    [InlineData("\"facts\": { \"ext.alarm\": false },", "", DialogueCodes.MissingField)]
    public void ABadSessionIsRefusedWithItsCode(string old, string replacement, string code)
    {
        Assert.Contains(old, Session, StringComparison.Ordinal);
        var (exit, output, _) = Run("explain", File("doc.json", FeatureFixture.Document), File("session.json", Session.Replace(old, replacement, StringComparison.Ordinal)));
        Assert.Equal(1, exit);
        Assert.Contains("refused " + code + ": ", output);
    }

    // ---- strings, coverage ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void ExportThenImportCarriesTextToATranslatorAndBack()
    {
        var doc = File("doc.json", Fixture.Document);
        var en = File("en.json", English);
        var (code, sheet, error) = Run("export-strings", doc, "en", "fr", "--source", en);
        Assert.Equal(0, code);
        Assert.Equal("", error);
        var node = JsonNode.Parse(sheet)!;
        Assert.Equal(9, node["entries"]!.AsArray().Count);
        foreach (var entry in node["entries"]!.AsArray())
            if ((string)entry!["key"]! is "brief.accept" or "brief.refuse") entry["target"] = "Traduit " + (string)entry["key"]!;
        var translated = File("sheet.json", node.ToJsonString());
        var table = Path.Combine(_folder, "fr.json");
        var (importCode, importOut, _) = Run("import-strings", doc, translated, "--out", table);
        Assert.Equal(0, importCode);
        Assert.StartsWith("wrote fr: 2 of 9 messages (22%)\nmissing ", importOut);
        Assert.Equal(7, importOut.Split('\n').Count(l => l.StartsWith("missing ", StringComparison.Ordinal)));
        Assert.Equal("Traduit brief.accept", LocaleTable.Load(System.IO.File.ReadAllText(table)).Messages["brief.accept"].Text);
    }

    [Fact]
    public void ExportCarriesWhatIsTranslatedSoFarAndImportExitsOneWhenSomethingWasRejected()
    {
        var doc = File("doc.json", Fixture.Document);
        var fr = File("fr.json", "{ \"format\": 1, \"locale\": \"fr\", \"messages\": { \"brief.accept\": \"Compris.\" } }");
        var (_, sheet, _) = Run("export-strings", doc, "en", "fr", "--target", fr);
        var node = JsonNode.Parse(sheet)!;
        Assert.Equal("Compris.", (string)node["entries"]!.AsArray().Single(e => (string)e!["key"]! == "brief.accept")!["target"]!);
        node["entries"]!.AsArray().Single(e => (string)e!["key"]! == "brief.refuse")!["target"] = "Non {who}.";
        var (code, output, _) = Run("import-strings", doc, File("sheet.json", node.ToJsonString()), "--out", Path.Combine(_folder, "out.json"));
        Assert.Equal(1, code);
        Assert.Contains("rejected brief.refuse: uses '{who}', which the line does not give\n", output);
    }

    [Fact]
    public void CoverageSaysHowMuchEachTableCoversAndExitsOneUnlessAllAreComplete()
    {
        var doc = File("doc.json", Fixture.Document);
        var en = File("en.json", English);
        var fr = File("fr.json", "{ \"format\": 1, \"locale\": \"fr\", \"messages\": { \"brief.accept\": \"Compris.\", \"brief.old\": \"Vieux.\", \"brief.plain\": \"Tenez {ici}.\" } }");
        Assert.Equal((0, "en: 9 of 9 (100%)\n"), Run("coverage", doc, en) is var a ? (a.Code, a.Output) : default);
        var (code, output, _) = Run("coverage", doc, en, fr);
        Assert.Equal(1, code);
        Assert.Contains("fr: 2 of 9 (22%)\n  missing brief.hello\n", output);
        Assert.Contains("  unknown brief.old\n", output);
        Assert.Contains("  problem brief.plain: uses '{ici}', which the line does not give\n", output);
        Assert.StartsWith("en: 9 of 9 (100%)\n", output);
    }

    // ---- graph --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void GraphDrawsEveryNodeAndEdgeInAFixedOrder()
    {
        var (code, output, _) = Run("graph", File("doc.json", FeatureFixture.Document));
        Assert.Equal(0, code);
        Assert.StartsWith("digraph \"demo.features\" {\n  rankdir=LR;\n", output);
        Assert.Contains("\"main/hi\" [label=\"hi\\n[line]\\ncmdr\", shape=box, style=bold];", output);
        Assert.Contains("\"main/ask\" [label=\"ask\\n[choice]\", shape=diamond];", output);
        Assert.Contains("\"main/sub\" -> \"main/ask\" [label=\"returns here\"];", output);
        Assert.Contains("\"main/ask\" -> \"main/var\" [label=\"stay\"];", output);
        Assert.Contains("\"main/ask\" -> \"main/bye\" [label=\"shout ?\"];", output);
        Assert.Contains("\"main/var\" -> \"main/vb\" [label=\"cycle 1\"];", output);
        Assert.Contains("\"detour/back\" [label=\"back\\n[return]\", shape=doublecircle];", output);
        Assert.EndsWith("}\n", output);
        Assert.Equal(output, Run("graph", File("doc2.json", FeatureFixture.Document)).Output);
        var one = Run("graph", File("doc3.json", FeatureFixture.Document), "detour").Output;
        Assert.Contains("detour/d1", one);
        Assert.DoesNotContain("main/hi", one);
        Assert.Equal(1, Run("graph", File("doc4.json", FeatureFixture.Document), "nowhere").Code);
    }

    // ---- usage and files ------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData]
    [InlineData("check")]
    [InlineData("check", "a", "b")]
    [InlineData("explain", "a")]
    [InlineData("import-strings", "a", "b")]
    [InlineData("import-strings", "a", "b", "--to", "c")]
    [InlineData("export-strings", "a", "b")]
    [InlineData("coverage", "a")]
    [InlineData("graph", "a", "b", "c")]
    [InlineData("build", "a")]
    public void WrongArgumentsPrintTheUsageAndExitTwo(params string[] args)
    {
        var (code, output, error) = Run(args);
        Assert.Equal(2, code);
        Assert.Equal("", output);
        Assert.StartsWith("usage: DialogueEngine.Cli check", error);
    }

    [Fact]
    public void ExportWithAnUnknownOptionIsAUsageMistakeAndAMissingFileIsAFileProblem()
    {
        var doc = File("doc.json", Fixture.Document);
        Assert.Equal(2, Run("export-strings", doc, "en", "fr", "--wrong", "x").Code);
        Assert.Equal(2, Run("export-strings", doc, "en", "fr", "--source").Code);
        var (code, output, error) = Run("check", Path.Combine(_folder, "missing.json"));
        Assert.Equal((2, ""), (code, output));
        Assert.StartsWith("file problem: ", error);
        Assert.StartsWith("file problem: ", Run("coverage", doc, Path.Combine(_folder, "missing.json")).Error);
    }
}
