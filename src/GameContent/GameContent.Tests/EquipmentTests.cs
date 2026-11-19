using System.Text.Json.Nodes;
using AlchemyEngine;
using GameContent.Catalog;
using Xunit;
using static GameContent.Tests.PackBuilder;

namespace GameContent.Tests;

/// <summary>Eras, availability, the stat vocabulary and items: the rules, and the refusals that keep a weapon from predating its materials.</summary>
public sealed class EquipmentTests
{
    // ---- builders ----------------------------------------------------------------------------------------------------------

    private static string Era(string id, int order, int tier, string label, string span = "null") =>
        $$"""{ "id": "{{id}}", "name": "{{id}}", "order": {{order}}, "tier": {{tier}}, "label": "{{label}}", "span": {{span}} }""";

    private static string Years(int from, int to) => $$"""{ "from": {{from}}, "to": {{to}} }""";

    private static string Avail(string id, string from, string until = "null") =>
        $$"""{ "id": "{{id}}", "from": "{{from}}", "until": {{(until == "null" ? "null" : "\"" + until + "\"")}} }""";

    private static string Stat(string id, string dimension = "mass", string unit = "kg", string kind = "number", string min = "null", string max = "null", string values = "", string classes = "\"weapon\"") =>
        $$"""{ "id": "{{id}}", "name": "{{id}}", "dimension": "{{dimension}}", "unit": "{{unit}}", "kind": "{{kind}}", "min": {{min}}, "max": {{max}}, "values": [{{values}}], "classes": [{{classes}}] }""";

    private static string Item(string resource, string cls = "weapon", string stats = "", string slots = "", string cargo = "", string crew = "", string fuel = "null", string ammo = "null", string upkeep = "", string replaces = "null") =>
        $$"""{ "resource": "{{resource}}", "class": "{{cls}}", "stats": { {{stats}} }, "slots": [{{slots}}], "cargo": [{{cargo}}], "crew": [{{crew}}], "fuel": {{fuel}}, "ammo": {{ammo}}, "upkeep": [{{upkeep}}], "replaces": {{(replaces == "null" ? "null" : "\"" + replaces + "\"")}} }""";

    private static string Gear(string id, string origin = "crafted", string cls = "equipment") => Res(id, cls: cls, unit: "{each}", origin: origin, stack: "1");

    private static string Make(string output, string input, string station = "null", string availabilityFree = "") =>
        Rec(output.Replace("t.", "t.make-"), inputs: Line(input, "1"), outputs: Line(output, "1"), station: station);

    /// <summary>One pack 't' of the given world with the given resources, recipes and equipment arrays.</summary>
    private static CatalogReport Check(string world, string resources, string recipes = "", string eras = "", string availability = "", string stats = "", string items = "", string skills = "", string stations = "")
    {
        var parts = new List<string> { "\"format\": 1", "\"pack\": \"t\"" };
        if (resources.Length > 0) parts.Add($"\"resources\": [{resources}]");
        if (stations.Length > 0) parts.Add($"\"stations\": [{stations}]");
        if (skills.Length > 0) parts.Add($"\"skills\": [{skills}]");
        if (recipes.Length > 0) parts.Add($"\"recipes\": [{recipes}]");
        if (eras.Length > 0) parts.Add($"\"eras\": [{eras}]");
        if (availability.Length > 0) parts.Add($"\"availability\": [{availability}]");
        if (stats.Length > 0) parts.Add($"\"stats\": [{stats}]");
        if (items.Length > 0) parts.Add($"\"items\": [{items}]");
        var files = new Dictionary<string, string> { ["t/pack.json"] = Manifest("t", world: world), ["t/doc.json"] = "{ " + string.Join(", ", parts) + " }" };
        return CatalogLoader.Load(new MemoryCatalogSource(files), "t").Report;
    }

    private static void Refused(string code, CatalogReport report) => Assert.True(report.Has(code), "expected " + code + " but got:\n" + report);

    private static readonly string Ages = Era("t.bronze", 2, 1, "bronze-age", Years(-3300, -1200)) + "," + Era("t.iron", 3, 2, "iron-age", Years(-1200, -500)) + "," + Era("t.modern", 8, 7, "modern", Years(1900, 2025));

    // ---- a complete valid example ----------------------------------------------------------------------------------------------

    private static readonly string Plain = Res("t.ore");
    private static readonly string Material = Res("t.ore") + "," + Res("t.ingot", origin: "crafted");
    private static readonly string Sword = Gear("t.sword");
    private static readonly string MakeIngot = Rec("t.smelt", kind: "smelt", inputs: Line("t.ore", "2"), outputs: Line("t.ingot", "1"));
    private static readonly string MakeSword = Rec("t.forge-sword", kind: "craft", inputs: Line("t.ingot", "2"), outputs: Line("t.sword", "1"));

    private static CatalogReport Valid(string world = "historical", string extraItems = "", string extraResources = "", string extraRecipes = "", string extraAvailability = "") =>
        Check(world, Material + "," + Sword + extraResources, MakeIngot + "," + MakeSword + extraRecipes, Ages,
            Avail("t.ingot", "t.bronze") + "," + Avail("t.sword", "t.bronze") + "," + Avail("t.forge-sword", "t.bronze") + extraAvailability,
            Stat("t.damage", "none", "1", "integer", "0", "100") + "," + Stat("t.weight", "mass", "kg", "number", "0", "50", classes: "\"weapon\",\"armor\""),
            Item("t.sword", stats: "\"t.damage\": 12, \"t.weight\": 1.5") + extraItems);

    [Fact]
    public void AnEraBoundWeaponWithItsMaterialsAndStatsIsValid() => Assert.True(Valid().IsValid, Valid().ToString());

    // ---- eras ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ErasMustAgreeOnOrderTierAndPeriodLabel()
    {
        Refused(CatalogCodes.EraOrder, Check("fictional", Plain, eras: Era("t.a", 1, 1, "classical") + "," + Era("t.b", 1, 2, "medieval")));
        Refused(CatalogCodes.EraOrder, Check("fictional", Plain, eras: Era("t.a", 1, 5, "classical") + "," + Era("t.b", 2, 3, "medieval")));
        Refused(CatalogCodes.EraOrder, Check("fictional", Plain, eras: Era("t.a", 1, 1, "medieval") + "," + Era("t.b", 2, 2, "classical")));
        Assert.True(Check("fictional", Plain, eras: Era("t.a", 1, 1, "classical") + "," + Era("t.b", 2, 1, "classical")).IsValid);
    }

    [Fact]
    public void HistoricalErasHaveYearsAndFictionalOnesDoNot()
    {
        Refused(CatalogCodes.EraSpan, Check("historical", Plain, eras: Era("t.a", 1, 1, "bronze-age")));
        Refused(CatalogCodes.EraSpan, Check("fictional", Plain, eras: Era("t.a", 1, 1, "bronze-age", Years(-3000, -1000))));
        Refused(CatalogCodes.EraSpan, Check("historical", Plain, eras: Era("t.a", 1, 1, "bronze-age", Years(-1000, -3000))));
        Refused(CatalogCodes.EraSpan, Check("historical", Plain, eras: Era("t.a", 1, 1, "bronze-age", Years(-3000, -1000)) + "," + Era("t.b", 2, 2, "iron-age", Years(-1500, -500))));
        Assert.True(Check("historical", Plain, eras: Era("t.a", 1, 1, "bronze-age", Years(-3000, -1000)) + "," + Era("t.b", 2, 2, "iron-age", Years(-1000, -500))).IsValid);
    }

    [Fact]
    public void ATimelessPackHasNoErasAndAHistoricalOneStopsAtThePresent()
    {
        Refused(CatalogCodes.EraWorld, Check("timeless", Plain, eras: Era("t.a", 1, 1, "bronze-age")));
        Refused(CatalogCodes.EraWorld, Check("historical", Plain, eras: Era("t.a", 1, 1, "near-future", Years(2030, 2100))));
        Refused(CatalogCodes.EraWorld, Check("timeless", Plain, availability: Avail("t.ore", "t.nowhere")));
        Assert.True(Check("fictional", Plain, eras: Era("t.a", 1, 1, "far-future")).IsValid);
    }

    [Fact]
    public void AvailabilityNamesRealThingsAndErasInOrderWithoutDuplicates()
    {
        Refused(CatalogCodes.UnknownRef, Check("fictional", Plain, eras: Ages.Replace("Years", ""), availability: Avail("t.ghost", "t.bronze")));
        Refused(CatalogCodes.UnknownRef, Check("fictional", Plain, eras: Era("t.a", 1, 1, "classical"), availability: Avail("t.ore", "t.nowhere")));
        Refused(CatalogCodes.EraWindow, Check("fictional", Plain, eras: Era("t.a", 1, 1, "classical") + "," + Era("t.b", 2, 2, "medieval"), availability: Avail("t.ore", "t.b", "t.a")));
        Refused(CatalogCodes.AvailabilityDuplicate, Check("fictional", Plain, eras: Era("t.a", 1, 1, "classical"), availability: Avail("t.ore", "t.a") + "," + Avail("t.ore", "t.a")));
    }

    // ---- anachronism --------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARecipeMayNotStartBeforeItsIngredientsOrMakeAThingBeforeItExists()
    {
        // The sword is forged in the bronze age from an ingot that only exists from the iron age.
        Refused(CatalogCodes.EraAnachronism, Check("historical", Material + "," + Sword, MakeIngot + "," + MakeSword, Ages,
            Avail("t.ingot", "t.iron") + "," + Avail("t.sword", "t.bronze") + "," + Avail("t.forge-sword", "t.bronze")));
        // The recipe starts earlier than the sword it makes.
        Refused(CatalogCodes.EraAnachronism, Check("historical", Material + "," + Sword, MakeIngot + "," + MakeSword, Ages,
            Avail("t.sword", "t.iron") + "," + Avail("t.forge-sword", "t.bronze")));
        // The ingredient is gone before the recipe starts.
        Refused(CatalogCodes.EraAnachronism, Check("historical", Material + "," + Sword, MakeIngot + "," + MakeSword, Ages,
            Avail("t.ingot", "t.bronze", "t.bronze") + "," + Avail("t.forge-sword", "t.iron") + "," + Avail("t.sword", "t.iron")));
        Assert.True(Valid().IsValid);
    }

    [Fact]
    public void AThingCannotExistBeforeAnyRecipeCouldMakeIt()
    {
        Refused(CatalogCodes.EraAnachronism, Check("historical", Material + "," + Sword, MakeIngot + "," + MakeSword, Ages,
            Avail("t.sword", "t.bronze") + "," + Avail("t.forge-sword", "t.iron") + "," + Avail("t.ingot", "t.bronze")));
        // A timeless producer makes it possible.
        Assert.True(Check("historical", Material + "," + Sword, MakeIngot + "," + MakeSword, Ages, Avail("t.sword", "t.bronze")).IsValid);
    }

    [Fact]
    public void ATimelessRecipeMayUseAnEraBoundThing()
    {
        Assert.True(Check("historical", Material + "," + Sword, MakeIngot + "," + MakeSword, Ages, Avail("t.ingot", "t.iron")).IsValid);
    }

    // ---- stats ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AStatsUnitMustBelongToItsDimensionAndItsShapeMustFitItsKind()
    {
        Refused(CatalogCodes.StatUnit, Check("fictional", Plain, stats: Stat("t.range", "length", "kg")));
        Refused(CatalogCodes.StatUnit, Check("fictional", Plain, stats: Stat("t.speed", "speed", "furlong")));
        Refused(CatalogCodes.Schema, Check("fictional", Plain, stats: Stat("t.x", "gravity", "kg")));
        Refused(CatalogCodes.StatShape, Check("fictional", Plain, stats: Stat("t.type", "none", "1", "enum")));
        Refused(CatalogCodes.StatShape, Check("fictional", Plain, stats: Stat("t.type", "mass", "kg", "enum", values: "\"a\"")));
        Refused(CatalogCodes.StatShape, Check("fictional", Plain, stats: Stat("t.weight", "mass", "kg", "number", values: "\"a\"")));
        Refused(CatalogCodes.StatShape, Check("fictional", Plain, stats: Stat("t.weight", "mass", "kg", "number", "5", "1")));
        Refused(CatalogCodes.StatShape, Check("fictional", Plain, stats: Stat("t.count", "count", "{each}", "integer", "0.5", "10")));
        Assert.True(Check("fictional", Plain, stats: Stat("t.range", "length", "m") + "," + Stat("t.kind", "none", "1", "enum", values: "\"slash\",\"pierce\"")).IsValid);
    }

    [Fact]
    public void AnItemsStatsMustBeInTheVocabularyAndFitTheirDefinition()
    {
        var stats = Stat("t.damage", "none", "1", "integer", "0", "100") + "," + Stat("t.kind", "none", "1", "enum", values: "\"slash\",\"pierce\"") + "," + Stat("t.armor-only", "mass", "kg", classes: "\"armor\"");
        CatalogReport Item1(string itemStats) => Check("fictional", Material + "," + Sword, MakeIngot + "," + MakeSword, stats: stats, items: Item("t.sword", stats: itemStats));
        Refused(CatalogCodes.UnknownRef, Item1("\"t.speed\": 3"));
        Refused(CatalogCodes.ItemStat, Item1("\"t.damage\": 150"));
        Refused(CatalogCodes.ItemStat, Item1("\"t.damage\": 1.5"));
        Refused(CatalogCodes.ItemStat, Item1("\"t.damage\": \"high\""));
        Refused(CatalogCodes.ItemStat, Item1("\"t.kind\": \"blunt\""));
        Refused(CatalogCodes.ItemStat, Item1("\"t.kind\": 3"));
        Refused(CatalogCodes.ItemStat, Item1("\"t.armor-only\": 3"));
        Assert.True(Item1("\"t.damage\": 12, \"t.kind\": \"slash\"").IsValid);
    }

    // ---- items ---------------------------------------------------------------------------------------------------------------

    private static CatalogReport WithItem(string item, string extraResources = "", string extraRecipes = "", string skills = "", string world = "fictional") =>
        Check(world, Material + "," + Sword + extraResources, MakeIngot + "," + MakeSword + extraRecipes, skills: skills, items: item);

    [Fact]
    public void AnItemIsAnEquipmentToolOrUniqueResourceOfItsOwnPack()
    {
        Refused(CatalogCodes.ItemResource, Check("fictional", Material, MakeIngot, items: Item("t.ingot")));            // an intermediate is not an item
        Refused(CatalogCodes.UnknownRef, WithItem(Item("t.ghost")));
        Refused(CatalogCodes.DuplicateId, WithItem(Item("t.sword") + "," + Item("t.sword")));
        var core = new Dictionary<string, string>
        {
            ["core/pack.json"] = Manifest("core"), ["core/doc.json"] = Document("core", Gear("core.club", origin: "natural")),
            ["t/pack.json"] = Manifest("t", dependencies: """{ "id": "core", "version": "1.0.0" }""", world: "fictional"),
            ["t/doc.json"] = """{ "format": 1, "pack": "t", "resources": [], "items": [ """ + Item("core.club") + " ] }",
        };
        Refused(CatalogCodes.ItemResource, CatalogLoader.Load(new MemoryCatalogSource(core), "t").Report);
    }

    [Fact]
    public void ALandVehicleLinksToFuelCargoCrewAndUpkeepThatExist()
    {
        var resources = "," + Res("t.diesel", cls: "fluid", unit: "L") + "," + Res("t.shell", cls: "consumable", unit: "{each}") + "," + Res("t.wheat") + "," + Gear("t.truck");
        var recipes = "," + Rec("t.make-truck", inputs: Line("t.ingot", "5"), outputs: Line("t.truck", "1"));
        const string skills = """{ "id": "t.driving", "name": "Driving", "max": 5 }""";
        CatalogReport Truck(string fuel = "null", string ammo = "null", string cargo = "", string crew = "", string upkeep = "", string cls = "vehicle-land", string slots = "") =>
            WithItem(Item("t.truck", cls, fuel: fuel, ammo: ammo, cargo: cargo, crew: crew, upkeep: upkeep, slots: slots) + "," + Item("t.shell", "ammunition"), resources + "," + Res("t.loose", cls: "consumable", unit: "{each}"), recipes, skills);
        const string fuelOk = """{ "resource": "t.diesel", "capacity": 200, "burn-per-hour": 12 }""";
        Assert.True(Truck(fuelOk, cargo: """{ "dimension": "mass", "amount": 3000 }""", crew: """{ "skill": "t.driving", "level": 2, "count": 1 }""", upkeep: """{ "resource": "t.ingot", "per-day": 0.5 }""",
            slots: """{ "slot": "turret", "count": 1 }""").IsValid, Truck(fuelOk).ToString());
        Refused(CatalogCodes.ItemLink, Truck("""{ "resource": "t.diesel", "capacity": 10, "burn-per-hour": 12 }"""));                    // burns more in an hour than it holds
        Refused(CatalogCodes.ItemLink, Truck("""{ "resource": "t.shell", "capacity": 10, "burn-per-hour": 1 }"""));                      // a count is not a fuel
        Refused(CatalogCodes.UnknownRef, Truck("""{ "resource": "t.ghost", "capacity": 10, "burn-per-hour": 1 }"""));
        Refused(CatalogCodes.ItemLink, Truck(ammo: """{ "resource": "t.wheat", "capacity": 10 }"""));                                    // ammunition is a counted consumable
        Refused(CatalogCodes.ItemLink, Truck(ammo: """{ "resource": "t.shell", "capacity": 2.5 }"""));
        Refused(CatalogCodes.ItemLink, Truck(ammo: """{ "resource": "t.loose", "capacity": 5 }"""));
        Assert.True(Truck(ammo: """{ "resource": "t.shell", "capacity": 30 }""").IsValid);
        Refused(CatalogCodes.ItemLink, Truck(cargo: """{ "dimension": "mass", "amount": 5 }, { "dimension": "mass", "amount": 6 }"""));
        Refused(CatalogCodes.ItemLink, Truck(cargo: """{ "dimension": "count", "amount": 2.5 }"""));
        Refused(CatalogCodes.SkillLevel, Truck(crew: """{ "skill": "t.driving", "level": 9, "count": 1 }"""));
        Refused(CatalogCodes.UnknownRef, Truck(crew: """{ "skill": "t.flying", "level": 1, "count": 1 }"""));
        Refused(CatalogCodes.ItemLink, Truck(upkeep: """{ "resource": "t.shell", "per-day": 0.5 }"""));
        Refused(CatalogCodes.ItemLink, Truck(cls: "armor", cargo: """{ "dimension": "mass", "amount": 5 }"""));                       // armour carries nothing
        Refused(CatalogCodes.ItemLink, Truck(cls: "tool", ammo: """{ "resource": "t.shell", "capacity": 3 }"""));
    }

    [Fact]
    public void ALineageReplacesAnItemOfTheSameClassWithoutLoopingOrGoingBackInTime()
    {
        var resources = "," + Gear("t.axe") + "," + Gear("t.axe2") + "," + Gear("t.shield");
        var recipes = "," + Rec("t.make-axe", inputs: Line("t.ingot", "1"), outputs: Line("t.axe", "1")) + "," + Rec("t.make-axe2", inputs: Line("t.ingot", "2"), outputs: Line("t.axe2", "1")) + "," + Rec("t.make-shield", inputs: Line("t.ingot", "3"), outputs: Line("t.shield", "1"));
        CatalogReport Lines(string items, string availability = "") => Check("historical", Material + "," + Sword + resources, MakeIngot + "," + MakeSword + recipes, Ages, availability, items: items);
        var axe = Item("t.axe");
        Assert.True(Lines(axe + "," + Item("t.axe2", replaces: "t.axe")).IsValid);
        Refused(CatalogCodes.ItemLineage, Lines(Item("t.axe", replaces: "t.axe")));
        Refused(CatalogCodes.ItemLineage, Lines(axe + "," + Item("t.shield", "armor", replaces: "t.axe")));                              // a shield does not replace an axe
        Refused(CatalogCodes.ItemLineage, Lines(Item("t.axe", replaces: "t.sword") + "," + Item("t.sword", replaces: "t.axe")));         // a loop
        Refused(CatalogCodes.ItemLineage, Lines(axe + "," + Item("t.axe2", replaces: "t.ore")));                                        // not an item
        // The newer axe appears in an earlier era than the one it replaces.
        Refused(CatalogCodes.EraLineage, Lines(axe + "," + Item("t.axe2", replaces: "t.axe"),
            Avail("t.axe", "t.iron") + "," + Avail("t.axe2", "t.bronze") + "," + Avail("t.make-axe", "t.iron") + "," + Avail("t.make-axe2", "t.bronze")));
        // An ungated item may not replace a gated one either (it would predate it).
        Refused(CatalogCodes.EraLineage, Lines(axe + "," + Item("t.axe2", replaces: "t.axe"), Avail("t.axe", "t.iron") + "," + Avail("t.make-axe", "t.iron")));
        Assert.True(Lines(axe + "," + Item("t.axe2", replaces: "t.axe"),
            Avail("t.axe", "t.bronze") + "," + Avail("t.axe2", "t.iron") + "," + Avail("t.make-axe", "t.bronze") + "," + Avail("t.make-axe2", "t.iron")).IsValid);
    }

    [Fact]
    public void AMadeItemNeedsAManufacturingRecipe()
    {
        var resources = "," + Gear("t.relic");
        // Only a trade makes the relic: it is bought, not built.
        var bought = "," + Rec("t.buy-relic", kind: "trade", inputs: Line("t.ore", "1"), outputs: Line("t.relic", "1"));
        Refused(CatalogCodes.ItemNoBuild, WithItem(Item("t.relic"), resources, bought));
        // A natural item is found; a mount may be bred or caught.
        Assert.True(WithItem(Item("t.horse", "mount"), "," + Gear("t.horse", origin: "natural", cls: "unique")).IsValid);
        Assert.True(WithItem(Item("t.relic"), resources, "," + Rec("t.cast-relic", inputs: Line("t.ingot", "1"), outputs: Line("t.relic", "1"))).IsValid);
    }

    [Fact]
    public void AHistoricalWorldHasNoSpacecraft()
    {
        var ship = "," + Gear("t.rocket");
        var recipe = "," + Rec("t.make-rocket", inputs: Line("t.ingot", "5"), outputs: Line("t.rocket", "1"));
        Refused(CatalogCodes.ItemWorld, Check("historical", Material + "," + Sword + ship, MakeIngot + "," + MakeSword + recipe, Ages, items: Item("t.rocket", "vehicle-space")));
        Assert.True(Check("fictional", Material + "," + Sword + ship, MakeIngot + "," + MakeSword + recipe, items: Item("t.rocket", "vehicle-space")).IsValid);
    }



    [Fact]
    public void AWeaponMayNotFireAmmunitionThatDoesNotExistYet()
    {
        var resources = Plain + "," + Res("t.ingot", origin: "crafted") + "," + Gear("t.gun") + "," + Res("t.slug", cls: "consumable", unit: "{each}", origin: "crafted");
        var recipes = MakeIngot + "," + Rec("t.make-gun", inputs: Line("t.ingot", "2"), outputs: Line("t.gun", "1")) + "," + Rec("t.make-slug", inputs: Line("t.ingot", "1"), outputs: Line("t.slug", "5"));
        string Doc(string gunEra, string slugEra) => "{ \"format\": 1, \"pack\": \"t\", \"resources\": [" + resources + "], \"recipes\": [" + recipes + "], \"eras\": [" + Ages + "], \"availability\": [" +
            Avail("t.gun", gunEra) + "," + Avail("t.make-gun", gunEra) + "," + Avail("t.slug", slugEra) + "," + Avail("t.make-slug", slugEra) + "], \"items\": [" +
            Item("t.gun", ammo: """{ "resource": "t.slug", "capacity": 10 }""") + "," + Item("t.slug", "ammunition") + "] }";
        CatalogReport Load(string gun, string slug) => CatalogLoader.Load(new MemoryCatalogSource(new Dictionary<string, string> { ["t/pack.json"] = Manifest("t", world: "historical"), ["t/doc.json"] = Doc(gun, slug) }), "t").Report;
        Refused(CatalogCodes.EraAnachronism, Load("t.bronze", "t.iron"));
        Assert.True(Load("t.iron", "t.bronze").IsValid, Load("t.iron", "t.bronze").ToString());
    }
    // ---- converters ------------------------------------------------------------------------------------------------------------

    private static CatalogSet ValidSet()
    {
        var files = new Dictionary<string, string>
        {
            ["t/pack.json"] = Manifest("t", world: "historical"),
            ["t/doc.json"] = "{ \"format\": 1, \"pack\": \"t\", \"resources\": [" + Material + "," + Sword + "], \"recipes\": [" + MakeIngot + "," + MakeSword + "], \"eras\": [" + Ages + "], \"availability\": [" +
                Avail("t.ingot", "t.bronze") + "," + Avail("t.sword", "t.iron") + "," + Avail("t.forge-sword", "t.iron", "t.modern") + "], \"stats\": [" +
                Stat("t.damage", "none", "1", "integer", "0", "100") + "], \"items\": [" + Item("t.sword", stats: "\"t.damage\": 12") + "] }",
        };
        return CatalogLoader.Load(new MemoryCatalogSource(files), "t").Required();
    }

    [Fact]
    public void AlchemyTurnsAnItemIntoAClassedElementAndAnEraIntoAnEnvironmentTag()
    {
        var conversion = CatalogToAlchemy.Convert(ValidSet());
        Assert.Contains("item-weapon", conversion.Content.Elements.Single(e => e.Id == "t.sword").Properties);
        Assert.DoesNotContain(conversion.Content.Elements.Single(e => e.Id == "t.ingot").Properties, p => p.StartsWith("item-", StringComparison.Ordinal));
        Assert.Contains("era-iron", conversion.Content.Recipes.Single(r => r.Id == "t.forge-sword").Environment);
        Assert.DoesNotContain(conversion.Content.Recipes.Single(r => r.Id == "t.smelt").Environment, tag => tag.StartsWith("era-", StringComparison.Ordinal));
        var notes = conversion.Report.Notes.Select(n => n.Code).ToHashSet();
        Assert.Contains(AlchemyConversionCodes.EraAsEnvironment, notes);
        Assert.Contains(AlchemyConversionCodes.ItemStats, notes);
        Assert.Contains(SimConversionCodes.EraNotModelled, CatalogToSim.Convert(ValidSet()).Report.Notes.Select(n => n.Code));
        var rules = new AlchemyRules(conversion.Content);
        Assert.Equal(CombineOutcome.NoReaction, rules.WhatDoes(["t.ingot"], []).Outcome);
        Assert.Equal(["t.sword"], rules.WhatDoes(["t.ingot"], ["era-iron"]).Produced);
    }

    [Fact]
    public void SimTurnsVehiclesIntoMoversAndKeepsWeaponsAsData()
    {
        var resources = Plain + "," + Res("t.ingot", origin: "crafted") + "," + Gear("t.truck") + "," + Gear("t.boat") + "," + Gear("t.wagon") + "," + Gear("t.sword");
        var recipes = MakeIngot + "," + string.Join(",", new[] { "truck", "boat", "wagon", "sword" }.Select(n => Rec("t.make-" + n, inputs: Line("t.ingot", "2"), outputs: Line("t." + n, "1"))));
        var stats = Stat("t.speed", "speed", "km/h", classes: "\"vehicle-land\",\"vehicle-sea\"") + "," + Stat("t.pace", "speed", "m/s", classes: "\"vehicle-land\"") + "," + Stat("t.damage", "none", "1", "integer", "0", "100");
        var items = Item("t.truck", "vehicle-land", "\"t.speed\": 72") + "," + Item("t.boat", "vehicle-sea", "\"t.speed\": 18") + "," + Item("t.wagon", "vehicle-land") + "," + Item("t.sword", stats: "\"t.damage\": 12");
        var files = new Dictionary<string, string>
        {
            ["t/pack.json"] = Manifest("t", world: "fictional"),
            ["t/doc.json"] = "{ \"format\": 1, \"pack\": \"t\", \"resources\": [" + resources + "], \"recipes\": [" + recipes + "], \"stats\": [" + stats + "], \"items\": [" + items + "] }",
        };
        var conversion = CatalogToSim.Convert(CatalogLoader.Load(new MemoryCatalogSource(files), "t").Required());
        Assert.Equal(["catalog.vehicle.t.boat", "catalog.vehicle.t.truck"], conversion.Vehicles.ToArray());
        Assert.Equal(SimConversionCodes.VehicleNoSpeed, Assert.Single(conversion.Report.Refusals, n => n.Subject == "t.wagon").Code);
        var entities = JsonNode.Parse(conversion.Files["world.json"])!["entities"]!.AsArray();
        var truck = entities.Single(e => (string)e!["id"]! == "catalog.vehicle.t.truck")!["components"]!["mover"]!;
        Assert.Equal(72m, (decimal)truck["speed"]!);
        Assert.Equal("land", (string)truck["class"]!);
        Assert.Equal("sea", (string)entities.Single(e => (string)e!["id"]! == "catalog.vehicle.t.boat")!["components"]!["mover"]!["class"]!);
        var notes = conversion.Report.Notes.Select(n => n.Code).ToHashSet();
        Assert.Contains(SimConversionCodes.VehicleStats, notes);
        Assert.Contains(SimConversionCodes.NoCombatEngine, notes);
        Assert.NotNull(SimWorld.Compose(conversion));
    }

    [Fact]
    public void ASpeedInMetresPerSecondBecomesKilometresPerHour()
    {
        var resources = Plain + "," + Res("t.ingot", origin: "crafted") + "," + Gear("t.cart");
        var files = new Dictionary<string, string>
        {
            ["t/pack.json"] = Manifest("t", world: "fictional"),
            ["t/doc.json"] = "{ \"format\": 1, \"pack\": \"t\", \"resources\": [" + resources + "], \"recipes\": [" + MakeIngot + "," + Rec("t.make-cart", inputs: Line("t.ingot", "2"), outputs: Line("t.cart", "1")) + "], \"stats\": [" +
                Stat("t.pace", "speed", "m/s", classes: "\"vehicle-land\"") + "], \"items\": [" + Item("t.cart", "vehicle-land", "\"t.pace\": 5") + "] }",
        };
        var conversion = CatalogToSim.Convert(CatalogLoader.Load(new MemoryCatalogSource(files), "t").Required());
        Assert.Equal(18m, (decimal)JsonNode.Parse(conversion.Files["world.json"])!["entities"]!.AsArray().Single(e => (string)e!["id"]! == "catalog.vehicle.t.cart")!["components"]!["mover"]!["speed"]!);
        Assert.NotNull(SimWorld.Compose(conversion));
    }
    // ---- round trip ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void EquipmentRoundTripsToTheSameBytes()
    {
        var files = new Dictionary<string, string>
        {
            ["t/pack.json"] = Manifest("t", world: "historical"),
            ["t/doc.json"] = "{ \"format\": 1, \"pack\": \"t\", \"resources\": [" + Material + "," + Sword + "], \"recipes\": [" + MakeIngot + "," + MakeSword + "], \"eras\": [" + Ages + "], \"availability\": [" +
                Avail("t.ingot", "t.bronze") + "," + Avail("t.sword", "t.bronze") + "," + Avail("t.forge-sword", "t.bronze") + "], \"stats\": [" +
                Stat("t.damage", "none", "1", "integer", "0", "100") + "], \"items\": [" + Item("t.sword", stats: "\"t.damage\": 12", slots: """{ "slot": "grip", "count": 1 }""") + "] }",
        };
        var first = CatalogLoader.Load(new MemoryCatalogSource(files), "t").Required();
        var written = CatalogWriter.Document(first.Root);
        var again = CatalogLoader.Load(new MemoryCatalogSource(new Dictionary<string, string> { ["t/pack.json"] = files["t/pack.json"], ["t/doc.json"] = written }), "t").Required();
        Assert.Equal(written, CatalogWriter.Document(again.Root));
        Assert.Equal(3, again.Root.Eras.Count);
        Assert.Single(again.Root.Items);
        Assert.Equal(12m, again.Root.Items[0].Stats["t.damage"]);
    }
}
