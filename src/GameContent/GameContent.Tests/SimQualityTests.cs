using System.Text.Json.Nodes;
using GameContent.Catalog;
using Xunit;
using static GameContent.Tests.PackBuilder;

namespace GameContent.Tests;

/// <summary>Grades map onto SimEngine quality axes and inherit-input quality onto output_qualities; what SimEngine cannot do stays refused.</summary>
public sealed class SimQualityTests
{
    private const string Grade3 = """{ "scheme": "quality", "levels": ["low", "mid", "high"] }""";
    private const string Grade2 = """{ "scheme": "quality", "levels": ["low", "high"] }""";

    private static SimConversion Sim(string resources, string recipes) => CatalogToSim.Convert(Load(Single(resources, recipes)).Required());

    [Fact]
    public void AGradedResourceBecomesAQualityAxisAndAnInheritingRecipeIsWiredToIt()
    {
        var resources = Res("t.ore", grade: Grade3) + "," + Res("t.ingot", origin: "crafted", grade: Grade3);
        var conversion = Sim(resources, Rec("t.smelt", kind: "smelt", inputs: Line("t.ore", "2"), outputs: Line("t.ingot", "1"), quality: "\"inherit-input\""));
        Assert.Equal(["t.smelt"], conversion.Transformations);
        var document = JsonNode.Parse(conversion.Files["resources.json"])!;
        var axes = document["resource_qualities"]!.AsArray();
        Assert.Equal(2, axes.Count);
        Assert.Equal(2, (int)axes.Single(a => (string)a!["resource"]! == "t.ingot")!["maximum"]!);
        Assert.Equal(CatalogToSim.GradeUnit, (string)axes[0]!["unit"]!);
        var rule = JsonNode.Parse(conversion.Files["recipes.json"])!["transformations"]![0]!["output_qualities"]![CatalogToSim.QualityId("t.ingot")]!;
        Assert.Equal(CatalogToSim.QualityId("t.ore"), (string)rule["source"]!);
        Assert.Equal("floor", (string)rule["rounding"]!);
        Assert.NotNull(SimWorld.Compose(conversion));   // SimEngine's own readers accept the axes and the rule
        Assert.DoesNotContain(conversion.Report.Refusals, n => n.Code == SimConversionCodes.Quality);
    }

    [Fact]
    public void QualityBySkillAndInheritanceItCannotWireAreRefusedWithAReason()
    {
        var resources = Res("t.ore", grade: Grade3) + "," + Res("t.ingot", origin: "crafted", grade: Grade3) + "," + Res("t.small", origin: "crafted", grade: Grade2) + "," + Res("t.plain", origin: "crafted") + "," + Res("t.sk", origin: "crafted", grade: Grade3);
        var recipes = string.Join(",",
            Rec("t.by-skill", kind: "craft", inputs: Line("t.ore", "1"), outputs: Line("t.sk", "1"), quality: "\"skill\"", skill: """{ "id": "t.craft", "level": 1 }"""),
            Rec("t.size", kind: "craft", inputs: Line("t.ore", "1"), outputs: Line("t.small", "1"), quality: "\"inherit-input\""),
            Rec("t.ungraded-out", kind: "craft", inputs: Line("t.ore", "1"), outputs: Line("t.plain", "1"), quality: "\"inherit-input\""),
            Rec("t.fine", kind: "craft", inputs: Line("t.ore", "1"), outputs: Line("t.ingot", "1"), quality: "\"inherit-input\""));
        var set = Load(Single(resources, recipes, skills: """{ "id": "t.craft", "name": "Craft", "max": 5 }""")).Required();
        var conversion = CatalogToSim.Convert(set);
        var refused = conversion.Report.Refusals.ToDictionary(n => n.Subject, n => n.Detail);
        Assert.Contains("skill", refused["t.by-skill"]);
        Assert.Contains("same size", refused["t.size"]);
        Assert.Contains("graded output", refused["t.ungraded-out"]);
        Assert.DoesNotContain("t.fine", refused.Keys);
        Assert.Equal(["t.fine"], conversion.Transformations);
    }

    [Fact]
    public void AGradedResourceInNoInheritingRecipeStillGetsItsAxis()
    {
        var conversion = Sim(Res("t.a", grade: Grade3) + "," + Res("t.b", origin: "crafted"), Rec("t.make", inputs: Line("t.a", "1"), outputs: Line("t.b", "1")));
        Assert.NotNull(JsonNode.Parse(conversion.Files["resources.json"])!["resource_qualities"]);
        Assert.NotNull(SimWorld.Compose(conversion));
    }

    [Fact]
    public void APackWithoutGradesWritesNoQualitySection()
    {
        var conversion = Sim(AB, Rec("t.make", inputs: Line("t.a", "1"), outputs: Line("t.b", "1")));
        Assert.Null(JsonNode.Parse(conversion.Files["resources.json"])!["resource_qualities"]);
    }
}
