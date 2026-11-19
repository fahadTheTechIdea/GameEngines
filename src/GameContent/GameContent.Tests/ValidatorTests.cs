using GameContent.Catalog;
using Xunit;
using static GameContent.Tests.PackBuilder;

namespace GameContent.Tests;

/// <summary>Every refusal code of the catalog, each exercised by the smallest pack that breaks the rule.</summary>
public sealed class ValidatorTests
{
    private static readonly string MakeB = Rec("t.make-b", inputs: Line("t.a", "2"), outputs: Line("t.b", "1"));

    private static void Refused(string code, CatalogReport report) => Assert.True(report.Has(code), "expected " + code + " but got:\n" + report);

    [Fact]
    public void AMinimalPackIsValid() => Assert.True(Check(AB, MakeB).IsValid);

    // ---- loading: files, schema, manifest, dependencies --------------------------------------------------------------------

    [Fact]
    public void AMissingFileAndNonJsonAreRefused()
    {
        Refused(CatalogCodes.FileMissing, Load(new MemoryCatalogSource(new Dictionary<string, string>()), "t").Report);
        var files = new Dictionary<string, string> { ["t/pack.json"] = Manifest("t"), ["t/doc.json"] = "{ not json" };
        Refused(CatalogCodes.NotJson, Load(new MemoryCatalogSource(files)).Report);
        var missing = new Dictionary<string, string> { ["t/pack.json"] = Manifest("t") };
        Refused(CatalogCodes.FileMissing, Load(new MemoryCatalogSource(missing)).Report);
    }

    [Fact]
    public void UnknownFieldsMissingFieldsAndBadValuesAreSchemaRefusals()
    {
        var withExtra = Document("t", AB.Replace("\"properties\": {}", "\"properties\": {}, \"extra\": 1"), MakeB);
        Refused(CatalogCodes.Schema, Load(new MemoryCatalogSource(new Dictionary<string, string> { ["t/pack.json"] = Manifest("t"), ["t/doc.json"] = withExtra })).Report);
        var withoutField = Document("t", Res("t.a").Replace("\"cpc\": null, ", ""));
        Refused(CatalogCodes.Schema, Load(new MemoryCatalogSource(new Dictionary<string, string> { ["t/pack.json"] = Manifest("t"), ["t/doc.json"] = withoutField })).Report);
        Refused(CatalogCodes.Schema, Check(Res("t.a", cls: "gizmo")));
        Refused(CatalogCodes.Schema, Check(AB, Rec("t.make-b", kind: "teleport", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"))));
        Refused(CatalogCodes.Schema, Check(AB, Rec("t.make-b", inputs: Line("t.a", "0"), outputs: Line("t.b", "1"))));
        Refused(CatalogCodes.Schema, Check(AB, Rec("t.make-b", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), duration: """{ "value": 5, "unit": "fortnight" }""")));
        Refused(CatalogCodes.Schema, Check(Res("T.a")));
        Refused(CatalogCodes.Schema, Check(Res("t.a", stack: "0")));
    }

    [Fact]
    public void ProvenanceMustBeOriginal()
    {
        var files = new Dictionary<string, string> { ["t/pack.json"] = Manifest("t", provenance: "copied"), ["t/doc.json"] = Document("t", AB, MakeB) };
        Refused(CatalogCodes.Schema, Load(new MemoryCatalogSource(files)).Report);
    }

    [Fact]
    public void ThePackIdAndDocumentPackMustMatchTheirFolder()
    {
        var wrongFolder = new Dictionary<string, string> { ["t/pack.json"] = Manifest("other"), ["t/doc.json"] = Document("other", Res("other.a")) };
        Refused(CatalogCodes.PackId, Load(new MemoryCatalogSource(wrongFolder)).Report);
        var wrongDocument = new Dictionary<string, string> { ["t/pack.json"] = Manifest("t"), ["t/doc.json"] = Document("other", Res("other.a")) };
        Refused(CatalogCodes.DocumentPack, Load(new MemoryCatalogSource(wrongDocument)).Report);
    }

    [Fact]
    public void DependenciesMustExistMatchInVersionAndNotCycle()
    {
        var needsCore = Manifest("t", dependencies: """{ "id": "core", "version": "1.0.0" }""");
        Refused(CatalogCodes.DependencyMissing, Load(new MemoryCatalogSource(new Dictionary<string, string> { ["t/pack.json"] = needsCore, ["t/doc.json"] = Document("t", Res("t.a")) })).Report);

        var core = new Dictionary<string, string> { ["core/pack.json"] = Manifest("core", version: "1.2.0"), ["core/doc.json"] = Document("core", Res("core.x")) };
        var tooNew = new Dictionary<string, string>(core) { ["t/pack.json"] = Manifest("t", dependencies: """{ "id": "core", "version": "1.3.0" }"""), ["t/doc.json"] = Document("t", Res("t.a")) };
        Refused(CatalogCodes.DependencyVersion, Load(new MemoryCatalogSource(tooNew)).Report);
        var otherMajor = new Dictionary<string, string>(core) { ["t/pack.json"] = Manifest("t", dependencies: """{ "id": "core", "version": "2.0.0" }"""), ["t/doc.json"] = Document("t", Res("t.a")) };
        Refused(CatalogCodes.DependencyVersion, Load(new MemoryCatalogSource(otherMajor)).Report);
        var fine = new Dictionary<string, string>(core) { ["t/pack.json"] = Manifest("t", dependencies: """{ "id": "core", "version": "1.1.0" }"""), ["t/doc.json"] = Document("t", Res("t.a")) };
        Assert.True(Load(new MemoryCatalogSource(fine)).Report.IsValid);

        var cycle = new Dictionary<string, string>
        {
            ["a/pack.json"] = Manifest("a", dependencies: """{ "id": "b", "version": "1.0.0" }"""), ["a/doc.json"] = Document("a", Res("a.x")),
            ["b/pack.json"] = Manifest("b", dependencies: """{ "id": "a", "version": "1.0.0" }"""), ["b/doc.json"] = Document("b", Res("b.x")),
        };
        Refused(CatalogCodes.DependencyCycle, Load(new MemoryCatalogSource(cycle), "a").Report);
    }

    // ---- identity and references ------------------------------------------------------------------------------------------

    [Fact]
    public void DuplicateIdsAcrossKindsAndNamespaceViolationsAreRefused()
    {
        Refused(CatalogCodes.DuplicateId, Check(Res("t.a") + "," + Res("t.a")));
        Refused(CatalogCodes.DuplicateId, Check(Res("t.a"), stations: """{ "id": "t.a", "name": "x", "levels": 1 }"""));
        Refused(CatalogCodes.IdNamespace, Check(Res("zz.a")));
    }

    [Fact]
    public void UnknownAndInvisibleReferencesAreRefused()
    {
        Refused(CatalogCodes.UnknownRef, Check(AB, Rec("t.make-b", inputs: Line("t.ghost", "1"), outputs: Line("t.b", "1"))));
        Refused(CatalogCodes.UnknownRef, Check(AB, Rec("t.make-b", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), station: """{ "id": "t.ghost", "level": 1 }""")));
        Refused(CatalogCodes.UnknownRef, Check(AB, Rec("t.make-b", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), skill: """{ "id": "t.ghost", "level": 1 }""")));
        Refused(CatalogCodes.UnknownRef, Check(AB, Rec("t.make-b", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), reverse: "\"t.ghost\"")));
        Refused(CatalogCodes.UnknownRef, Check(Res("t.a", perishable: """{ "ticks": 5, "result": "t.ghost" }""")));

        // 'u' exists but 't' does not depend on it.
        var files = new Dictionary<string, string>
        {
            ["t/pack.json"] = Manifest("t"), ["t/doc.json"] = Document("t", AB, Rec("t.make-b", inputs: Line("u.x", "1"), outputs: Line("t.b", "1"))),
            ["u/pack.json"] = Manifest("u"), ["u/doc.json"] = Document("u", Res("u.x")),
        };
        // Not loaded at all (no dependency) so it is unknown; declared as a dependency of a third pack it is loaded but still not visible.
        Refused(CatalogCodes.UnknownRef, Load(new MemoryCatalogSource(files)).Report);
        var viaThird = new Dictionary<string, string>
        {
            ["t/pack.json"] = Manifest("t", dependencies: """{ "id": "m", "version": "1.0.0" }"""), ["t/doc.json"] = Document("t", AB, Rec("t.make-b", inputs: Line("u.x", "1"), outputs: Line("t.b", "1"))),
            ["m/pack.json"] = Manifest("m", dependencies: """{ "id": "u", "version": "1.0.0" }"""), ["m/doc.json"] = Document("m", Res("m.y")),
            ["u/pack.json"] = Manifest("u"), ["u/doc.json"] = Document("u", Res("u.x")),
        };
        Refused(CatalogCodes.NotVisible, Load(new MemoryCatalogSource(viaThird)).Report);
    }

    // ---- resources: units, classes, currency, stacks, durability, spoilage ------------------------------------------------

    [Fact]
    public void UnitsMustBeSupportedAndFitTheClass()
    {
        Refused(CatalogCodes.UnknownUnit, Check(Res("t.a", unit: "furlong")));
        Refused(CatalogCodes.UnitClass, Check(Res("t.a", cls: "energy", unit: "kg")));
        Refused(CatalogCodes.UnitClass, Check(Res("t.a", cls: "labor", unit: "{each}")));
        Refused(CatalogCodes.UnitClass, Check(Res("t.a", cls: "fluid", unit: "{each}")));
        Assert.True(Check(Res("t.a", cls: "energy", unit: "kW.h")).IsValid);
        Assert.True(Check(Res("t.a", cls: "labor", unit: "h")).IsValid);
    }

    [Fact]
    public void WholeUnitResourcesRefuseFractionalAmounts()
    {
        var resources = Res("t.a", unit: "{each}") + "," + Res("t.b", origin: "crafted", unit: "{each}");
        Refused(CatalogCodes.UnitDiscrete, Check(resources, Rec("t.make-b", inputs: Line("t.a", "1.5"), outputs: Line("t.b", "1"))));
        Refused(CatalogCodes.UnitDiscrete, Check(resources, Rec("t.make-b", inputs: Line("t.a", "2"), outputs: Line("t.b", """{ "min": 1, "max": 2.5 }"""))));
        Assert.True(Check(resources, Rec("t.make-b", inputs: Line("t.a", "2"), outputs: Line("t.b", "1"))).IsValid);
    }

    [Fact]
    public void CurrencyStackDurabilityAndSpoilageRulesHold()
    {
        Refused(CatalogCodes.Currency, Check(Res("t.coin", cls: "currency", unit: "{coin}")));
        Refused(CatalogCodes.Currency, Check(Res("t.a", currency: """{ "kind": "fictional", "code": "ABC" }""")));
        Assert.True(Check(Res("t.coin", cls: "currency", unit: "{coin}", currency: """{ "kind": "iso4217", "code": "EUR" }""")).IsValid);
        Refused(CatalogCodes.Stack, Check(Res("t.u", cls: "unique", unit: "{each}", stack: "5")));
        Refused(CatalogCodes.Durability, Check(Res("t.a", durability: """{ "uses": 10 }""")));
        Refused(CatalogCodes.Perishable, Check(Res("t.a", perishable: """{ "ticks": 5, "result": "t.a" }""")));
    }

    // ---- recipes ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AmountsChancesAndIngredientListsAreChecked()
    {
        Refused(CatalogCodes.BadAmount, Check(AB, Rec("t.make-b", inputs: Line("t.a", "1"), outputs: Line("t.b", """{ "min": 3, "max": 2 }"""))));
        Refused(CatalogCodes.BadChance, Check(AB, Rec("t.make-b", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), drops: """{ "resource": "t.b", "amount": 1, "chance": "3/2" }""")));
        Refused(CatalogCodes.DuplicateIngredient, Check(AB, Rec("t.make-b", inputs: Line("t.a", "1") + "," + Line("t.a", "2"), outputs: Line("t.b", "1"))));
    }

    [Fact]
    public void RecipesMustConsumeAndProduceUnlessTheirKindMayNot()
    {
        Refused(CatalogCodes.NoInputs, Check(AB, Rec("t.make-b", kind: "craft", outputs: Line("t.b", "1"))));
        Assert.True(Check(AB, Rec("t.dig-b", kind: "extract", outputs: Line("t.b", "1"))).IsValid);
        Refused(CatalogCodes.NoOutputs, Check(AB, Rec("t.make-b", kind: "craft", inputs: Line("t.a", "1"))));
        Assert.True(Check(AB, Rec("t.eat-a", kind: "consume", inputs: Line("t.a", "1")) + "," + MakeB).IsValid);
    }

    [Fact]
    public void ToolsCatalystsAndInputsMayNotOverlap()
    {
        Refused(CatalogCodes.ToolConflict, Check(AB, Rec("t.make-b", inputs: Line("t.a", "1"), catalysts: Line("t.a", "1"), outputs: Line("t.b", "1"))));
        Refused(CatalogCodes.ToolConflict, Check(AB, Rec("t.make-b", inputs: Line("t.a", "1"), tools: """{ "resource": "t.a", "uses": 10 }""", outputs: Line("t.b", "1"))));
        Refused(CatalogCodes.ToolConflict, Check(Res("t.a") + "," + Res("t.c") + "," + Res("t.b", origin: "crafted"),
            Rec("t.make-b", inputs: Line("t.a", "1"), catalysts: Line("t.c", "1"), tools: """{ "resource": "t.c", "uses": 10 }""", outputs: Line("t.b", "1"))));
    }

    [Fact]
    public void StationSkillQualityAndReverseRulesHold()
    {
        var station = """{ "id": "t.bench", "name": "Bench", "levels": 2 }""";
        var skill = """{ "id": "t.craft", "name": "Craft", "max": 5 }""";
        Refused(CatalogCodes.StationLevel, Check(AB, Rec("t.make-b", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), station: """{ "id": "t.bench", "level": 3 }"""), station));
        Refused(CatalogCodes.SkillLevel, Check(AB, Rec("t.make-b", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), skill: """{ "id": "t.craft", "level": 9 }"""), skills: skill));
        Refused(CatalogCodes.Quality, Check(AB, Rec("t.make-b", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), quality: "\"skill\"")));
        Refused(CatalogCodes.Quality, Check(AB, Rec("t.make-b", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), quality: "\"inherit-input\"")));
        Refused(CatalogCodes.Reverse, Check(AB, Rec("t.make-b", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), reverse: "\"t.make-a\"") + "," + Rec("t.make-a", inputs: Line("t.b", "1"), outputs: Line("t.a", "1"))));
        Refused(CatalogCodes.Reverse, Check(AB, Rec("t.take-apart", kind: "disassemble", inputs: Line("t.b", "1"), outputs: Line("t.a", "1"), reverse: "\"t.take-apart\"")));
    }

    // ---- whole-pack rules -------------------------------------------------------------------------------------------------

    [Fact]
    public void ARecipeMayNotGainWhatItConsumes()
    {
        var resources = Res("t.a") + "," + Res("t.b", origin: "crafted");
        Refused(CatalogCodes.NotConserved, Check(resources, Rec("t.multiply", kind: "transform", inputs: Line("t.a", "2") + "," + Line("t.b", "1"), outputs: Line("t.b", "3")) + "," + MakeB));
        Refused(CatalogCodes.NotConserved, Check(resources, Rec("t.lucky", kind: "refine", inputs: Line("t.b", "1") + "," + Line("t.a", "1"), outputs: Line("t.b", "1"), drops: """{ "resource": "t.b", "amount": 1, "chance": "1/2" }""") + "," + MakeB));
        Assert.True(Check(resources, Rec("t.repair", kind: "repair", inputs: Line("t.b", "1") + "," + Line("t.a", "1"), outputs: Line("t.b", "1")) + "," + MakeB).IsValid);
        // Generative kinds may make something from nothing.
        Assert.True(Check(resources, Rec("t.grow", kind: "grow", inputs: Line("t.b", "1"), outputs: Line("t.b", "5")) + "," + MakeB).IsValid);
    }

    [Fact]
    public void ALoopOfSingleInputRecipesThatGainsIsAFreeResourceMachine()
    {
        var resources = Res("t.a") + "," + Res("t.b") + "," + Res("t.c");
        var profitable = Rec("t.a-to-b", kind: "trade", inputs: Line("t.a", "1"), outputs: Line("t.b", "2")) + "," + Rec("t.b-to-c", kind: "trade", inputs: Line("t.b", "1"), outputs: Line("t.c", "2")) + "," + Rec("t.c-to-a", kind: "trade", inputs: Line("t.c", "1"), outputs: Line("t.a", "1"));
        var report = Check(resources, profitable);
        Refused(CatalogCodes.FreeCycle, report);
        Assert.Contains("t.a-to-b", report.Items.Single(i => i.Code == CatalogCodes.FreeCycle).Message);
        // The same loop at an exchange rate that loses a little every lap is fine.
        var lossy = Rec("t.a-to-b", kind: "trade", inputs: Line("t.a", "1"), outputs: Line("t.b", "2")) + "," + Rec("t.b-to-c", kind: "trade", inputs: Line("t.b", "1"), outputs: Line("t.c", "1")) + "," + Rec("t.c-to-a", kind: "trade", inputs: Line("t.c", "2"), outputs: Line("t.a", "1"));
        Assert.True(Check(resources, lossy).IsValid);
        // An exactly even loop is not a gain.
        var even = Rec("t.a-to-b", kind: "trade", inputs: Line("t.a", "1"), outputs: Line("t.b", "2")) + "," + Rec("t.b-to-a", kind: "trade", inputs: Line("t.b", "2"), outputs: Line("t.a", "1"));
        Assert.True(Check(Res("t.a") + "," + Res("t.b"), even).IsValid);
    }

    [Fact]
    public void ACraftedResourceNothingCanMakeIsUnreachable()
    {
        var resources = Res("t.a") + "," + Res("t.b", origin: "crafted") + "," + Res("t.island", origin: "crafted");
        Refused(CatalogCodes.Unreachable, Check(resources, MakeB));
        // A resource that needs itself, or a crafted resource no natural chain reaches, is unreachable too.
        Refused(CatalogCodes.Unreachable, Check(resources, MakeB + "," + Rec("t.loop", kind: "transform", inputs: Line("t.island", "1"), outputs: Line("t.island", "1"))));
        Assert.True(Check(resources, MakeB + "," + Rec("t.make-island", inputs: Line("t.b", "1"), outputs: Line("t.island", "1"))).IsValid);
        // Spoilage reaches its result.
        Assert.True(Check(Res("t.a", perishable: """{ "ticks": 5, "result": "t.rot" }""") + "," + Res("t.rot", origin: "crafted")).IsValid);
    }

    [Fact]
    public void TwoCombineRecipesForOneCombinationAreAmbiguousButAlternateCraftsAreNot()
    {
        var resources = Res("t.a") + "," + Res("t.b") + "," + Res("t.c", origin: "crafted") + "," + Res("t.d", origin: "crafted");
        var combine = Rec("t.one", kind: "combine", inputs: Line("t.a", "1") + "," + Line("t.b", "1"), outputs: Line("t.c", "1")) + "," + Rec("t.two", kind: "combine", inputs: Line("t.b", "1") + "," + Line("t.a", "1"), outputs: Line("t.d", "1"));
        Refused(CatalogCodes.AmbiguousCombine, Check(resources, combine));
        var withEnvironment = Rec("t.one", kind: "combine", inputs: Line("t.a", "1") + "," + Line("t.b", "1"), outputs: Line("t.c", "1"), environment: "\"heat\"") + "," + Rec("t.two", kind: "combine", inputs: Line("t.a", "1") + "," + Line("t.b", "1"), outputs: Line("t.d", "1"), environment: "\"cold\"");
        Assert.True(Check(resources, withEnvironment).IsValid);
        var crafts = Rec("t.one", inputs: Line("t.a", "1") + "," + Line("t.b", "1"), outputs: Line("t.c", "1")) + "," + Rec("t.two", inputs: Line("t.a", "1") + "," + Line("t.b", "1"), outputs: Line("t.d", "1"));
        Assert.True(Check(resources, crafts).IsValid);
    }

    [Fact]
    public void ARuleThatChecksBothEndsOfARangeReportsOnce()
    {
        var resources = Res("t.a", unit: "{each}") + "," + Res("t.b", origin: "crafted", unit: "{each}");
        var report = Check(resources, Rec("t.make-b", inputs: Line("t.a", "2"), outputs: Line("t.b", "1.5")));
        Assert.Single(report.Items, i => i.Code == CatalogCodes.UnitDiscrete);
    }

    [Fact]
    public void EveryFindingIsReportedNotJustTheFirst()
    {
        var report = Check(Res("zz.a", unit: "furlong") + "," + Res("zz.a"));
        Assert.True(report.Items.Count >= 3, report.ToString());
        Assert.True(report.Has(CatalogCodes.IdNamespace));
        Assert.True(report.Has(CatalogCodes.DuplicateId));
        Assert.True(report.Has(CatalogCodes.UnknownUnit));
    }
}
