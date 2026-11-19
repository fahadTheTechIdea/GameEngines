using System.Text.Json.Nodes;
using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>Locale fallback chains with a record of what fell back, the coverage report, and the translators' sheet (export, import, stale and rejected entries).</summary>
public sealed class LocaleToolingTests
{
    private const string English = """
    { "format": 1, "locale": "en", "messages": {
      "brief.hello": { "select": "count", "one": "{count} squad is ready.", "other": "{count} squads are ready." },
      "brief.accept": "Understood.", "brief.refuse": "No.", "brief.secret": "I know a way.", "brief.lucky": "Good luck.", "brief.plain": "Hold the bridge.",
      "quest.hold.title": "Hold the bridge", "quest.hold.order": "Do not let them cross.", "quest.hold.done": "The bridge held." } }
    """;

    private const string French = """
    { "format": 1, "locale": "fr", "messages": {
      "brief.hello": { "select": "count", "one": "{count} escouade est prête.", "other": "{count} escouades sont prêtes." },
      "brief.accept": "Compris.", "brief.refuse": "Non." } }
    """;

    private const string CanadianFrench = """
    { "format": 1, "locale": "fr-CA", "messages": { "brief.accept": "Bien reçu." } }
    """;

    private static MessageArg Count(long n) => new("count", FactValue.From(n));

    private static LocaleChain Chain(string requested) => LocaleChain.Create(requested, [LocaleTable.Load(English), LocaleTable.Load(French), LocaleTable.Load(CanadianFrench)], "en");

    // ---- chain ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheChainIsTheExactLocaleThenItsLanguageThenTheDefaultAndLeavesOutWhatHasNoTable()
    {
        Assert.Equal(["fr-CA", "fr", "en"], Chain("fr-CA").Locales);
        Assert.Equal(["fr", "en"], Chain("fr").Locales);
        Assert.Equal(["fr", "en"], Chain("fr-BE").Locales);                       // no fr-BE table: the language
        Assert.Equal(["en"], Chain("ja").Locales);                                // nothing for Japanese: the default alone
        Assert.Equal(["en"], Chain("en").Locales);
        Assert.Equal(["fr-CA", "fr", "en"], Chain("FR_ca").Locales);               // case and the underscore do not matter
    }

    [Fact]
    public void AKeyThatALocaleLacksFallsThroughToTheNextAndIsRemembered()
    {
        var chain = Chain("fr-CA");
        var accept = chain.Render("brief.accept", []);
        Assert.Equal(("Bien reçu.", "fr-CA"), (accept.Text, accept.Locale));
        Assert.Empty(accept.Skipped);
        var refuse = chain.Render("brief.refuse", []);
        Assert.Equal(("Non.", "fr"), (refuse.Text, refuse.Locale));
        Assert.Equal(["fr-CA"], refuse.Skipped);
        var secret = chain.Render("brief.secret", []);
        Assert.Equal(("I know a way.", "en"), (secret.Text, secret.Locale));
        Assert.Equal(["fr-CA", "fr"], secret.Skipped);
        chain.Render("brief.secret", []);
        chain.Render("brief.hello", [Count(2)]);
        Assert.Equal(new Dictionary<string, int> { ["fr-CA:brief.hello"] = 1, ["fr-CA:brief.refuse"] = 1, ["fr-CA:brief.secret"] = 2, ["fr:brief.secret"] = 2 }, chain.Fallbacks.ToDictionary(p => p.Key, p => p.Value));
        Assert.Equal(["fr-CA:brief.hello", "fr-CA:brief.refuse", "fr-CA:brief.secret", "fr:brief.secret"], chain.Fallbacks.Keys.ToArray());   // in order
    }

    [Fact]
    public void AFormsMessageIsRenderedWithTheRulesOfTheLocaleItCameFrom()
    {
        var chain = Chain("fr-CA");
        Assert.Equal("1 escouade est prête.", chain.Render("brief.hello", [Count(1)]).Text);
        Assert.Equal("0 escouade est prête.", chain.Render("brief.hello", [Count(0)]).Text);          // French: 0 is one
        Assert.Equal("0 squads are ready.", Chain("en").Render("brief.hello", [Count(0)]).Text);
    }

    [Fact]
    public void AKeyNoLocaleOfTheChainHasIsRefusedNamingEveryLocaleTriedAndABadChainIsRefused()
    {
        var refusal = Assert.Throws<EngineRefusedException>(() => Chain("fr-CA").Render("brief.nothing", []));
        Assert.Equal(LocaleCodes.MissingKey, refusal.Code);
        Assert.Contains("fr-CA, fr, en", refusal.Message);
        Assert.Equal(LocaleCodes.MissingKey, Fixture.Refusal(() => LocaleChain.Create("ja", [LocaleTable.Load(French)], "en")));            // not even the default
        Assert.Equal(LocaleCodes.Format, Fixture.Refusal(() => LocaleChain.Create("en", [LocaleTable.Load(English), LocaleTable.Load(English)], "en")));
    }

    // ---- coverage ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheCoverageReportListsEverythingAtOnceWhereCheckRefusesAtTheFirst()
    {
        var set = Fixture.Load();
        var report = LocaleCoverage.Report(set, LocaleTable.Load(French));
        Assert.Equal("fr", report.Locale);
        Assert.Equal(9, report.Total);
        Assert.Equal(3, report.Translated);
        Assert.Equal(33, report.Percent);
        Assert.Equal(["brief.lucky", "brief.plain", "brief.secret", "quest.hold.done", "quest.hold.order", "quest.hold.title"], report.Missing.Order(StringComparer.Ordinal).ToArray());
        Assert.False(report.IsComplete);
        Assert.Equal(LocaleCodes.MissingKey, Fixture.Refusal(() => LocaleCoverage.Check(set, LocaleTable.Load(French))));
        var full = LocaleCoverage.Report(set, LocaleTable.Load(English));
        Assert.True(full.IsComplete);
        Assert.Equal(100, full.Percent);
    }

    [Fact]
    public void TheReportSeparatesMissingUnknownAndMisplacedMessages()
    {
        var set = Fixture.Load();
        var edited = JsonNode.Parse(English)!;
        edited["messages"]!.AsObject().Remove("brief.lucky");
        edited["messages"]!["brief.old"] = "Gone.";
        edited["messages"]!["brief.plain"] = "Hold {where}.";
        var report = LocaleCoverage.Report(set, LocaleTable.Load(edited.ToJsonString()));
        Assert.Equal(["brief.lucky"], report.Missing);
        Assert.Equal(["brief.old"], report.Unknown);
        Assert.Equal([new MessageProblem("brief.plain", "uses '{where}', which the line does not give")], report.Problems);
        Assert.Equal(LocaleCodes.MissingKey, Fixture.Refusal(() => LocaleCoverage.Check(set, LocaleTable.Load(edited.ToJsonString()))));
        edited["messages"]!["brief.lucky"] = "Luck.";
        Assert.Equal(LocaleCodes.Placeholder, Fixture.Refusal(() => LocaleCoverage.Check(set, LocaleTable.Load(edited.ToJsonString()))));
        edited["messages"]!["brief.plain"] = "Hold.";
        Assert.Equal(LocaleCodes.UnknownKey, Fixture.Refusal(() => LocaleCoverage.Check(set, LocaleTable.Load(edited.ToJsonString()))));
    }

    // ---- translators' sheet ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheSheetHasOneEntryForEveryKeyWithItsContextPlaceholdersAndWhatIsTranslatedSoFar()
    {
        var set = Fixture.Load();
        var sheet = JsonNode.Parse(TranslationSheet.Export(set, "en", "fr", LocaleTable.Load(English), LocaleTable.Load(French)))!;
        Assert.Equal(1, (int)sheet["format"]!);
        Assert.Equal(set.Id, (string)sheet["document"]!);
        Assert.Equal(set.Digest, (string)sheet["digest"]!);
        var entries = sheet["entries"]!.AsArray();
        Assert.Equal(set.Keys.Count, entries.Count);
        var hello = entries.Single(e => (string)e!["key"]! == "brief.hello")!;
        Assert.Equal(["count"], hello["placeholders"]!.AsArray().Select(p => (string)p!).ToArray());
        Assert.Equal("conversation 'briefing' node 'hello'", (string)hello["uses"]![0]!);
        Assert.Equal("{count} squads are ready.", (string)hello["source"]!["other"]!);
        Assert.Equal("{count} escouades sont prêtes.", (string)hello["target"]!["other"]!);
        var secret = entries.Single(e => (string)e!["key"]! == "brief.secret")!;
        Assert.Equal("conversation 'briefing' node 'ask' answer 'secret'", (string)secret["uses"]![0]!);
        Assert.Null(secret["target"]);
        Assert.Equal("I know a way.", (string)secret["source"]!);
        var title = entries.Single(e => (string)e!["key"]! == "quest.hold.title")!;
        Assert.StartsWith("quest 'hold'", (string)title["uses"]![0]!);
        Assert.Equal(TranslationSheet.Export(set, "en", "fr", LocaleTable.Load(English), LocaleTable.Load(French)), TranslationSheet.Export(set, "en", "fr", LocaleTable.Load(English), LocaleTable.Load(French)));
    }

    [Fact]
    public void ATranslatedSheetImportsToATableAndTheReportSaysWhatIsStillOwed()
    {
        var set = Fixture.Load();
        var sheet = JsonNode.Parse(TranslationSheet.Export(set, "en", "fr", LocaleTable.Load(English), null))!;
        foreach (var entry in sheet["entries"]!.AsArray())
        {
            var key = (string)entry!["key"]!;
            if (key == "brief.hello") entry["target"] = JsonNode.Parse("""{ "select": "count", "one": "{count} escouade", "other": "{count} escouades" }""");
            else if (key is "brief.accept" or "quest.hold.title") entry["target"] = "Fait " + key;
        }
        var import = TranslationSheet.Import(set, sheet.ToJsonString());
        Assert.False(import.Stale);
        Assert.Empty(import.Rejected);
        Assert.Equal("fr", import.Table.Locale);
        Assert.Equal(3, import.Report.Translated);
        Assert.Equal(6, import.Report.Missing.Count);
        Assert.Equal("2 escouades", LocaleRenderer.Render(import.Table, "brief.hello", [Count(2)]));
        Assert.Equal("Fait brief.accept", LocaleRenderer.Render(import.Table, "brief.accept", []));
        // the table written out is a table that loads again: the round trip loses nothing
        Assert.Equal(import.Table.Digest.Length, LocaleTable.Load(import.Table.ToText()).Digest.Length);
        Assert.Equal(import.Table.Messages.Keys.ToArray(), LocaleTable.Load(import.Table.ToText()).Messages.Keys.ToArray());
    }

    [Fact]
    public void AnImportKeepsWhatIsUsableAndListsWhatIsNotAndSaysWhenTheSheetIsOlderThanTheDocument()
    {
        var set = Fixture.Load();
        var sheet = JsonNode.Parse(TranslationSheet.Export(set, "en", "fr", null, null))!;
        var entries = sheet["entries"]!.AsArray();
        void Put(string key, JsonNode? target) => entries.Single(e => (string)e!["key"]! == key)!["target"] = target;
        Put("brief.accept", "Compris.");
        Put("brief.refuse", "Non {nobody}.");                                                              // a placeholder the line does not give
        Put("brief.lucky", JsonNode.Parse("""{ "select": "n", "few": "x", "other": "y" }"""));             // a form French does not have
        Put("brief.plain", JsonNode.Parse("""{ "select": "n", "one": "x" }"""));                          // no 'other'
        entries.Add(JsonNode.Parse("""{ "key": "brief.removed", "uses": [], "placeholders": [], "source": null, "target": "Parti." }"""));
        sheet["digest"] = new string('0', 64);
        var import = TranslationSheet.Import(set, sheet.ToJsonString());
        Assert.True(import.Stale);
        Assert.Equal(["brief.accept"], import.Table.Messages.Keys.ToArray());
        Assert.Equal(["brief.lucky", "brief.plain", "brief.refuse", "brief.removed"], import.Rejected.Select(r => r.Key).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("the document no longer uses this key", import.Rejected.Single(r => r.Key == "brief.removed").Why);
        Assert.Contains("nobody", import.Rejected.Single(r => r.Key == "brief.refuse").Why);
    }

    [Fact]
    public void ASheetThatIsNotForThisDocumentOrIsMalformedIsRefused()
    {
        var set = Fixture.Load();
        var good = JsonNode.Parse(TranslationSheet.Export(set, "en", "fr", null, null))!;
        string Import(Action<JsonNode> change) { var copy = JsonNode.Parse(good.ToJsonString())!; change(copy); return Fixture.Refusal(() => TranslationSheet.Import(set, copy.ToJsonString())); }
        Assert.Equal(LocaleCodes.Format, Import(s => s["document"] = "another"));
        Assert.Equal(LocaleCodes.Format, Import(s => s["format"] = 2));
        Assert.Equal(LocaleCodes.Format, Import(s => s["extra"] = 1));
        Assert.Equal(LocaleCodes.Format, Import(s => s.AsObject().Remove("entries")));
        Assert.Equal(LocaleCodes.Format, Import(s => s["entries"]!.AsArray().Add(s["entries"]![0]!.DeepClone())));               // a key twice
        Assert.Equal(LocaleCodes.Unsupported, Import(s => s["target"] = "xx"));
        Assert.Equal(LocaleCodes.Format, Fixture.Refusal(() => TranslationSheet.Import(set, "not json")));
        Assert.Equal(LocaleCodes.Unsupported, Fixture.Refusal(() => TranslationSheet.Export(set, "en", "xx", null, null)));
    }

    [Fact]
    public void ATableWithOrdinalAndSelectMessagesWritesAndLoadsBackUnchanged()
    {
        var table = LocaleTable.Load("""
        { "format": 1, "locale": "en", "messages": {
          "k.place": { "type": "ordinal", "select": "n", "one": "{n}st", "two": "{n}nd", "few": "{n}rd", "other": "{n}th" },
          "k.who": { "type": "select", "select": "g", "feminine": "She", "other": "They" },
          "k.count": { "select": "n", "one": "one", "other": "many" } } }
        """);
        var again = LocaleTable.Load(table.ToText());
        Assert.Equal(table.Messages.Keys.ToArray(), again.Messages.Keys.ToArray());
        Assert.Equal(["2nd", "They", "She", "many"], new[]
        {
            LocaleRenderer.Render(again, "k.place", [new MessageArg("n", FactValue.From(2L))]),
            LocaleRenderer.Render(again, "k.who", [new MessageArg("g", FactValue.From("x"))]),
            LocaleRenderer.Render(again, "k.who", [new MessageArg("g", FactValue.From("feminine"))]),
            LocaleRenderer.Render(again, "k.count", [new MessageArg("n", FactValue.From(2L))]),
        });
        Assert.Equal(MessageSelect.Ordinal, again.Messages["k.place"].Mode);
        Assert.Equal(MessageSelect.Value, again.Messages["k.who"].Mode);
    }
}