using GameContent.Catalog;
using Xunit;
using static GameContent.Tests.PackBuilder;

namespace GameContent.Tests;

/// <summary>Names in other languages: locale documents, their rules, and the one place a name is chosen.</summary>
public sealed class LocaleTests
{
    private static readonly string Doc = Document("t", AB, Rec("t.make-b", inputs: Line("t.a", "2"), outputs: Line("t.b", "1")));

    private static string Locale(string language, string texts, string pack = "t") =>
        $$"""{ "format": 1, "pack": "{{pack}}", "language": "{{language}}", "texts": { {{texts}} } }""";

    private static CatalogLoadResult Load(string locales, params (string Path, string Text)[] files)
    {
        var map = new Dictionary<string, string> { ["t/pack.json"] = Manifest("t", locales: locales), ["t/doc.json"] = Doc };
        foreach (var (path, text) in files) map[path] = text;
        return CatalogLoader.Load(new MemoryCatalogSource(map), "t");
    }

    private const string French = """{ "language": "fr", "document": "fr.json" }""";

    [Fact]
    public void ALocaleNamesThePacksOwnThingsInAnotherLanguage()
    {
        var result = Load(French, ("t/fr.json", Locale("fr", "\"t.a\": \"Fer\", \"t.make-b\": \"Forger\"")));
        Assert.True(result.Report.IsValid, result.Report.ToString());
        var set = result.Required();
        Assert.Equal("Fer", CatalogText.NameOf(set, "t.a", "fr"));
        Assert.Equal("Forger", CatalogText.NameOf(set, "t.make-b", "fr"));
        Assert.Equal("t.a", CatalogText.NameOf(set, "t.a", "en"));          // the source language is the definitions' own names
        Assert.Null(CatalogText.NameOf(set, "t.b", "fr"));                  // untranslated is null, never a guess
        Assert.Null(CatalogText.NameOf(set, "t.a", "de"));                  // a language the pack does not offer
        Assert.Null(CatalogText.NameOf(set, "zz.a", "en"));                 // an id no loaded pack owns
        var coverage = CatalogText.Coverage(set.Root);
        Assert.Equal((3, 3), coverage["en"]);
        Assert.Equal((2, 3), coverage["fr"]);
    }

    [Fact]
    public void ALocaleMayNotNameWhatThePackDoesNotDefine()
    {
        var result = Load(French, ("t/fr.json", Locale("fr", "\"t.ghost\": \"Fantome\"")));
        Assert.True(result.Report.Has(CatalogCodes.LocaleUnknownId));
        // Nor something a dependency defines: each pack translates its own.
        var core = new Dictionary<string, string> { ["core/pack.json"] = Manifest("core"), ["core/doc.json"] = Document("core", Res("core.x")) };
        var map = new Dictionary<string, string>(core)
        {
            ["t/pack.json"] = Manifest("t", dependencies: """{ "id": "core", "version": "1.0.0" }""", locales: French), ["t/doc.json"] = Document("t", Res("t.a")),
            ["t/fr.json"] = Locale("fr", "\"core.x\": \"X\""),
        };
        Assert.True(CatalogLoader.Load(new MemoryCatalogSource(map), "t").Report.Has(CatalogCodes.LocaleUnknownId));
    }

    [Fact]
    public void TheLocaleDocumentMustMatchItsPackAndItsDeclaredLanguage()
    {
        Assert.True(Load(French, ("t/fr.json", Locale("fr", "\"t.a\": \"Fer\"", pack: "other"))).Report.Has(CatalogCodes.LocalePack));
        Assert.True(Load(French, ("t/fr.json", Locale("de", "\"t.a\": \"Eisen\""))).Report.Has(CatalogCodes.LocaleLanguage));
        var own = """{ "language": "en", "document": "en.json" }""";
        Assert.True(Load(own, ("t/en.json", Locale("en", "\"t.a\": \"Iron\""))).Report.Has(CatalogCodes.LocaleLanguage));
        var twice = French + ", " + """{ "language": "fr", "document": "fr2.json" }""";
        var result = Load(twice, ("t/fr.json", Locale("fr", "\"t.a\": \"Fer\"")), ("t/fr2.json", Locale("fr", "\"t.b\": \"Acier\"")));
        Assert.True(result.Report.Has(CatalogCodes.LocaleDuplicate));
    }

    [Fact]
    public void AMissingOrMalformedLocaleIsRefusedLikeAnyOtherFile()
    {
        Assert.True(Load(French).Report.Has(CatalogCodes.FileMissing));
        Assert.True(Load(French, ("t/fr.json", "{ nope")).Report.Has(CatalogCodes.NotJson));
        Assert.True(Load(French, ("t/fr.json", Locale("fr", "\"t.a\": \"\""))).Report.Has(CatalogCodes.Schema));                 // empty text
        Assert.True(Load(French, ("t/fr.json", Locale("fr", "\"T.A\": \"Fer\""))).Report.Has(CatalogCodes.Schema));              // not an id
        Assert.True(Load(French, ("t/fr.json", """{ "format": 1, "pack": "t", "language": "fr" }""")).Report.Has(CatalogCodes.Schema));
        Assert.True(Load(French, ("t/fr.json", Locale("French", "\"t.a\": \"Fer\""))).Report.Has(CatalogCodes.Schema));
    }

    [Fact]
    public void TheManifestMustDeclareItsLanguageAndItsLocales()
    {
        var withoutLanguage = Manifest("t").Replace("\"language\": \"en\", ", "");
        Assert.True(CatalogLoader.Load(new MemoryCatalogSource(new Dictionary<string, string> { ["t/pack.json"] = withoutLanguage, ["t/doc.json"] = Doc }), "t").Report.Has(CatalogCodes.Schema));
        var withoutLocales = Manifest("t").Replace("\"locales\": [], ", "");
        Assert.True(CatalogLoader.Load(new MemoryCatalogSource(new Dictionary<string, string> { ["t/pack.json"] = withoutLocales, ["t/doc.json"] = Doc }), "t").Report.Has(CatalogCodes.Schema));
    }

    [Fact]
    public void StatsReportTheLanguagesAPackOffers()
    {
        var result = Load(French, ("t/fr.json", Locale("fr", "\"t.a\": \"Fer\"")));
        var stats = CatalogStats.Of(result.Required(), result.Report);
        Assert.Contains("languages: en 3/3, fr 1/3", stats.Format());
    }
}
