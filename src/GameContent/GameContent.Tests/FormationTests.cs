using GameContent.Catalog;
using Xunit;
using static GameContent.Tests.PackBuilder;

namespace GameContent.Tests;

/// <summary>Formations (unit rosters): people with items, crew, training and upkeep, and the ammunition item class.</summary>
public sealed class FormationTests
{
    private const string Drill = """{ "id": "t.drill", "name": "Drill", "max": 5 }""";

    private static string Squad(string id = "t.squad", string strength = "10", string role = "infantry", string training = "regular", string equipment = "", string crew = "", string upkeep = "", string replaces = "null") =>
        $$"""{ "resource": "{{id}}", "strength": {{strength}}, "role": "{{role}}", "training": "{{training}}", "equipment": [{{equipment}}], "crew": [{{crew}}], "upkeep": [{{upkeep}}], "replaces": {{(replaces == "null" ? "null" : "\"" + replaces + "\"")}} }""";

    private static string Eq(string item, int count) => $$"""{ "item": "{{item}}", "count": {{count}} }""";

    private static readonly string Base = Res("t.ore") + "," + Res("t.ingot", origin: "crafted") + "," + Res("t.rifle", cls: "equipment", unit: "{each}", origin: "crafted", stack: "1") +
        "," + Res("t.squad", cls: "unique", unit: "{each}", origin: "crafted", stack: "1") + "," + Res("t.platoon", cls: "unique", unit: "{each}", origin: "crafted", stack: "1") +
        "," + Res("t.cartridge", cls: "consumable", unit: "{each}", origin: "crafted");

    private static readonly string Recipes = Rec("t.smelt", kind: "smelt", inputs: Line("t.ore", "2"), outputs: Line("t.ingot", "1")) + "," +
        Rec("t.make-rifle", inputs: Line("t.ingot", "2"), outputs: Line("t.rifle", "1")) + "," + Rec("t.make-cartridge", inputs: Line("t.ingot", "1"), outputs: Line("t.cartridge", "5")) + "," +
        Rec("t.raise-squad", kind: "assemble", inputs: Line("t.ingot", "1"), outputs: Line("t.squad", "1")) + "," + Rec("t.raise-platoon", kind: "assemble", inputs: Line("t.ingot", "3"), outputs: Line("t.platoon", "1"));

    private static string ItemJson(string resource, string cls = "weapon") =>
        $$"""{ "resource": "{{resource}}", "class": "{{cls}}", "stats": { }, "slots": [], "cargo": [], "crew": [], "fuel": null, "ammo": null, "upkeep": [], "replaces": null }""";

    private static string Items => ItemJson("t.rifle") + "," + ItemJson("t.cartridge", "ammunition");

    private static MemoryCatalogSource Source(string document) =>
        new(new Dictionary<string, string> { ["t/pack.json"] = Manifest("t", world: "fictional"), ["t/doc.json"] = document });

    private static CatalogReport Check(string units, string extraResources = "", string extraRecipes = "") =>
        CatalogLoader.Load(Source($$"""{ "format": 1, "pack": "t", "resources": [{{Base}}{{extraResources}}], "recipes": [{{Recipes}}{{extraRecipes}}], "skills": [{{Drill}}], "items": [{{Items}}], "units": [{{units}}] }"""), "t").Report;

    private static void Refused(string code, CatalogReport report) => Assert.True(report.Has(code), "expected " + code + " but got:\n" + report);

    [Fact]
    public void AFormationWithItemsCrewAndUpkeepIsValid()
    {
        var report = Check(Squad(equipment: Eq("t.rifle", 10), crew: """{ "skill": "t.drill", "level": 2, "count": 2 }""", upkeep: """{ "resource": "t.ingot", "per-day": 0.5 }"""));
        Assert.True(report.IsValid, report.ToString());
    }

    [Fact]
    public void AFormationIsAUniqueResourceOfItsOwnPack()
    {
        Refused(CatalogCodes.UnitResource, Check(Squad("t.rifle")));
        Refused(CatalogCodes.UnknownRef, Check(Squad("t.ghost")));
        Refused(CatalogCodes.DuplicateId, Check(Squad() + "," + Squad()));
    }

    [Fact]
    public void EquipmentMustBeItemsAndNotAmmunition()
    {
        Refused(CatalogCodes.UnitEquipment, Check(Squad(equipment: Eq("t.ingot", 2))));
        Refused(CatalogCodes.UnitEquipment, Check(Squad(equipment: Eq("t.cartridge", 50))));
        Refused(CatalogCodes.UnitEquipment, Check(Squad(equipment: Eq("t.rifle", 5) + "," + Eq("t.rifle", 6))));
        Refused(CatalogCodes.UnknownRef, Check(Squad(equipment: Eq("t.ghost", 5))));
    }

    [Fact]
    public void CrewCannotExceedStrengthOrTheSkillLevel()
    {
        Refused(CatalogCodes.UnitCrew, Check(Squad(strength: "3", crew: """{ "skill": "t.drill", "level": 1, "count": 4 }""")));
        Refused(CatalogCodes.SkillLevel, Check(Squad(crew: """{ "skill": "t.drill", "level": 9, "count": 1 }""")));
        Refused(CatalogCodes.UnknownRef, Check(Squad(crew: """{ "skill": "t.ghost", "level": 1, "count": 1 }""")));
        Refused(CatalogCodes.UnitCrew, Check(Squad(upkeep: """{ "resource": "t.cartridge", "per-day": 0.5 }""")));
    }

    [Fact]
    public void ALineageKeepsItsRoleAndNeverLoops()
    {
        Assert.True(Check(Squad() + "," + Squad("t.platoon", "40", replaces: "t.squad")).IsValid);
        Refused(CatalogCodes.UnitLineage, Check(Squad(replaces: "t.squad")));
        Refused(CatalogCodes.UnitLineage, Check(Squad() + "," + Squad("t.platoon", "40", role: "armour", replaces: "t.squad")));
        Refused(CatalogCodes.UnitLineage, Check(Squad(replaces: "t.platoon") + "," + Squad("t.platoon", "40", replaces: "t.squad")));
        Refused(CatalogCodes.UnitLineage, Check(Squad(replaces: "t.rifle")));
    }

    [Fact]
    public void AFormationNeedsARecipeThatRaisesIt()
    {
        var extra = "," + Res("t.orphan", cls: "unique", unit: "{each}", origin: "crafted", stack: "1");
        Refused(CatalogCodes.UnitNoBuild, Check(Squad("t.orphan"), extra));
    }

    [Fact]
    public void AFormationIsNotRaisedBeforeItsEquipmentExists()
    {
        const string eras = """{ "id": "t.early", "name": "Early", "order": 1, "tier": 0, "label": "classical", "span": null }, { "id": "t.late", "name": "Late", "order": 2, "tier": 1, "label": "medieval", "span": null }""";
        CatalogReport Load(string rifleEra, string squadEra) => CatalogLoader.Load(Source($$"""
            { "format": 1, "pack": "t", "resources": [{{Base}}], "recipes": [{{Recipes}}], "eras": [{{eras}}], "availability": [
              { "id": "t.rifle", "from": "{{rifleEra}}", "until": null }, { "id": "t.make-rifle", "from": "{{rifleEra}}", "until": null },
              { "id": "t.squad", "from": "{{squadEra}}", "until": null }, { "id": "t.raise-squad", "from": "{{squadEra}}", "until": null }], "items": [{{Items}}],
              "units": [{{Squad(equipment: Eq("t.rifle", 10))}}] }
            """), "t").Report;
        Refused(CatalogCodes.EraAnachronism, Load("t.late", "t.early"));
        Assert.True(Load("t.early", "t.late").IsValid);
    }

    [Fact]
    public void AnAmmunitionItemIsAConsumableInEach()
    {
        string Doc(string resource) => $$"""
            { "format": 1, "pack": "t", "resources": [{{Base}}, {{Res("t.bad", cls: "equipment", unit: "{each}", origin: "crafted", stack: "1")}}],
              "recipes": [{{Recipes}}, {{Rec("t.make-bad", inputs: Line("t.ingot", "1"), outputs: Line("t.bad", "1"))}}], "items": [{{ItemJson(resource, "ammunition")}}] }
            """;
        Refused(CatalogCodes.ItemResource, CatalogLoader.Load(Source(Doc("t.bad")), "t").Report);
        Assert.True(CatalogLoader.Load(Source(Doc("t.cartridge")), "t").Report.IsValid);
    }

    [Fact]
    public void FormationsRoundTripToTheSameBytes()
    {
        var document = $$"""
            { "format": 1, "pack": "t", "resources": [{{Base}}], "recipes": [{{Recipes}}], "skills": [{{Drill}}], "items": [{{Items}}],
              "units": [{{Squad(equipment: Eq("t.rifle", 10), crew: """{ "skill": "t.drill", "level": 2, "count": 2 }""", upkeep: """{ "resource": "t.ingot", "per-day": 0.5 }""")}}] }
            """;
        var first = CatalogLoader.Load(Source(document), "t").Required();
        var written = CatalogWriter.Document(first.Root);
        var again = CatalogLoader.Load(Source(written), "t").Required();
        Assert.Equal(written, CatalogWriter.Document(again.Root));
        var unit = Assert.Single(again.Root.Units);
        Assert.Equal(TrainingLevel.Regular, unit.Training);
        Assert.Equal(10, unit.Equipment[0].Count);
    }
}
