using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using AlchemyEngine;
using GameContent.Catalog;
using Xunit;
using static GameContent.Tests.PackBuilder;

namespace GameContent.Tests;

/// <summary>Findings of the second-pass audit, kept from coming back: dead codes, drift between schema and code, fields nothing reads.</summary>
public sealed class AuditGuardTests
{
    private static string TestSources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GameContent.Tests.csproj"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return string.Concat(Directory.GetFiles(directory!.FullName, "*.cs").Select(File.ReadAllText));
    }

    private static IEnumerable<string> Constants(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).Select(f => f.Name);

    [Theory]
    [InlineData(typeof(CatalogCodes))]
    [InlineData(typeof(AlchemyConversionCodes))]
    [InlineData(typeof(SimConversionCodes))]
    [InlineData(typeof(EconomyConversionCodes))]
    [InlineData(typeof(OwnershipCodes))]
    public void EveryCodeIsExercisedByATest(Type codes)
    {
        var sources = TestSources();
        var untested = Constants(codes).Where(name => !sources.Contains(codes.Name + "." + name, StringComparison.Ordinal)).ToList();
        Assert.True(untested.Count == 0, "no test names these codes: " + string.Join(", ", untested));
    }

    [Theory]
    [InlineData(typeof(CatalogCodes))]
    [InlineData(typeof(AlchemyConversionCodes))]
    [InlineData(typeof(SimConversionCodes))]
    [InlineData(typeof(EconomyConversionCodes))]
    [InlineData(typeof(OwnershipCodes))]
    public void EveryCodeIsRaisedBySomeCode(Type codes)
    {
        // A declared code that nothing raises is a rule that was meant and never built.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "GameContent.Catalog"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var library = string.Concat(Directory.GetFiles(directory!.FullName, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("Tests", StringComparison.Ordinal) && !f.Contains("/obj/", StringComparison.Ordinal) && !f.Contains("\\obj\\", StringComparison.Ordinal) && !f.EndsWith("Codes.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText));
        var unused = Constants(codes).Where(name => !library.Contains(codes.Name + "." + name, StringComparison.Ordinal)).ToList();
        Assert.True(unused.Count == 0, "nothing raises these codes: " + string.Join(", ", unused));
    }

    private static JsonObject Schema(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "schemas", "catalog"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return JsonNode.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "schemas", "catalog", name)))!.AsObject();
    }

    private static string[] Names(JsonNode? properties) => properties!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public void TheWritersFieldsAreExactlyTheSchemasFields()
    {
        var set = CatalogLoader.Load(new DirectoryCatalogSource(ShippedPackTests.PacksRoot), "survival").Required();
        var written = JsonNode.Parse(CatalogWriter.Document(set.Root))!.AsObject();
        var document = Schema("gameframework-catalog-document.schema.json");
        Assert.Equal(Names(document["properties"]).Where(n => n != "format"), written.Select(p => p.Key).Where(n => n != "format").Order(StringComparer.Ordinal).Where(n => document["properties"]![n] is not null));
        var resource = Names(document["$defs"]!["resource"]!["properties"]);
        Assert.Equal(resource, written["resources"]![0]!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal).ToArray());
        var recipe = Names(document["$defs"]!["recipe"]!["properties"]);
        Assert.Equal(recipe, written["recipes"]![0]!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal).ToArray());
        // Every field the schema requires is one it declares, and the other way round.
        foreach (var def in new[] { "resource", "recipe" })
            Assert.Equal(Names(document["$defs"]![def]!["properties"]), document["$defs"]![def]!["required"]!.AsArray().Select(n => n!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray());
        var pack = Schema("gameframework-catalog-pack.schema.json");
        Assert.Equal(Names(pack["properties"]), pack["required"]!.AsArray().Select(n => n!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray());
        var locale = Schema("gameframework-catalog-locale.schema.json");
        Assert.Equal(Names(locale["properties"]), locale["required"]!.AsArray().Select(n => n!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray());
    }

    // ---- the origin rule --------------------------------------------------------------------------------------------------

    [Fact]
    public void ANaturalResourceMayNotBeMadeByManufacturing()
    {
        var resources = Res("t.a") + "," + Res("t.b");   // both natural
        var report = Check(resources, Rec("t.craft-b", inputs: Line("t.a", "2"), outputs: Line("t.b", "1")));
        Assert.True(report.Has(CatalogCodes.Origin), report.ToString());
        Assert.True(Check(resources, Rec("t.dig-b", kind: "extract", outputs: Line("t.b", "1"))).IsValid);
        Assert.True(Check(resources, Rec("t.trade-b", kind: "trade", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"))).IsValid);
        Assert.True(Check(resources + "," + Res("t.c", origin: "crafted"), Rec("t.split", inputs: Line("t.a", "2"), outputs: Line("t.c", "1"), byproducts: Line("t.b", "1"))).Has(CatalogCodes.Origin));
        // Energy is a flow generators convert, not an item that exists only once made.
        var power = Res("t.fuel") + "," + Res("t.power", cls: "energy", unit: "kW.h");
        Assert.True(Check(power, Rec("t.burn", kind: "transform", inputs: Line("t.fuel", "1"), outputs: Line("t.power", "5"))).IsValid);
    }

    // ---- fields the standard has that a converter must read ------------------------------------------------------------------

    [Fact]
    public void TheCpcSectionIsCarriedByBothConverters()
    {
        var set = Load(Single(Res("t.a", cpc: "\"2\"") + "," + Res("t.b", origin: "crafted"), Rec("t.make", inputs: Line("t.a", "1"), outputs: Line("t.b", "1")))).Required();
        Assert.Contains("cpc-2", CatalogToAlchemy.Convert(set).Content.Elements.Single(e => e.Id == "t.a").Properties);
        Assert.DoesNotContain(CatalogToAlchemy.Convert(set).Content.Elements.Single(e => e.Id == "t.b").Properties, p => p.StartsWith("cpc-", StringComparison.Ordinal));
        var traits = JsonNode.Parse(CatalogToSim.Convert(set).Files["resources.json"])!["resources"]!.AsArray().Single(r => (string)r!["id"]! == "t.a")!["traits"]!.AsArray();
        Assert.Contains(traits, t => (string)t! == "cpc-2");
    }

    [Fact]
    public void AlchemyAndSimSayWhichRecipeFieldsTheyDoNotModel()
    {
        var resources = Res("t.a") + "," + Res("t.b", origin: "crafted", grade: """{ "scheme": "quality", "levels": ["lo", "hi"] }""", stack: "50") + "," + Res("t.c", origin: "crafted", grade: """{ "scheme": "quality", "levels": ["lo", "hi"] }""") + "," + Res("t.d", origin: "crafted");
        var recipes = Rec("t.make-b", inputs: Line("t.a", "2"), outputs: Line("t.b", "1")) + "," +
            Rec("t.make-c", inputs: Line("t.b", "1"), outputs: Line("t.c", "1"), quality: "\"inherit-input\"") + "," +
            Rec("t.make-d", inputs: Line("t.c", "1"), outputs: Line("t.d", "1"), reverse: "\"t.take-apart\"") + "," +
            Rec("t.take-apart", kind: "disassemble", inputs: Line("t.d", "1"), outputs: Line("t.c", "1"));
        var set = Load(Single(resources, recipes)).Required();
        var alchemy = CatalogToAlchemy.Convert(set).Report.Notes.Select(n => n.Code).ToHashSet();
        Assert.Contains(AlchemyConversionCodes.QualityNotModelled, alchemy);
        Assert.Contains(AlchemyConversionCodes.ReverseNotModelled, alchemy);
        Assert.Contains(AlchemyConversionCodes.ResourceFields, alchemy);
        Assert.Contains(SimConversionCodes.ReverseLink, CatalogToSim.Convert(set).Report.Notes.Select(n => n.Code));
    }
}
