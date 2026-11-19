using GameContent.Catalog;
using Xunit;

namespace GameContent.Tests;

/// <summary>The shipped equipment, vehicle and era packs: coverage per era, materials not older than what is made of them, lineages, and what the converters make of them.</summary>
public sealed class ArmsPackTests
{
    private static readonly string[] ArmsPacks = ["arms-ancient", "arms-medieval", "arms-modern", "arms-future", "arms-fantasy"];

    private static CatalogSet Load(string id) => CatalogLoader.Load(new DirectoryCatalogSource(ShippedPackTests.PacksRoot), id).Required();

    private static IEnumerable<(CatalogItem Item, CatalogEra Era)> ItemsByEra()
    {
        foreach (var id in ArmsPacks)
        {
            var set = Load(id);
            foreach (var item in set.Root.Items)
                yield return (item, set.TryAvailability(item.Resource, out var window) && set.TryEra(window.From, out var era) ? era : throw new InvalidOperationException(item.Resource + " has no era"));
        }
    }

    [Fact]
    public void EveryMajorEraHasDozensOfItems()
    {
        var perEra = ItemsByEra().GroupBy(x => x.Era.Id).ToDictionary(g => g.Key, g => g.Count());
        var minimums = new Dictionary<string, int>
        {
            // major eras: a dozen or so; intermediate ages added later: fewer, but present
            ["ages.stone"] = 12, ["ages.copper"] = 5, ["ages.bronze"] = 12, ["ages.iron"] = 12, ["ages.classical"] = 10, ["ages.medieval"] = 14, ["ages.renaissance"] = 10, ["ages.gunpowder"] = 12,
            ["ages.industrial"] = 12, ["ages.modern"] = 10, ["ages.atomic"] = 7, ["ages.information"] = 5, ["horizons.space-age"] = 5, ["horizons.near-future"] = 10, ["horizons.far-future"] = 12,
        };
        foreach (var (era, minimum) in minimums) Assert.True(perEra.GetValueOrDefault(era) >= minimum, era + " has " + perEra.GetValueOrDefault(era) + " items, expected at least " + minimum);
        Assert.True(ItemsByEra().Count() >= 170, "the packs hold " + ItemsByEra().Count() + " items");
    }

    [Fact]
    public void TheItemsCoverEveryClassInEveryKindOfWorld()
    {
        var all = ItemsByEra().ToList();
        foreach (var itemClass in Enum.GetValues<ItemClass>().Where(c => c != ItemClass.Ammunition)) Assert.Contains(all, x => x.Item.Class == itemClass);
        Assert.True(Load("matter").Root.Items.Count(i => i.Class == ItemClass.Ammunition) >= 15, "ammunition items live in the matter pack");
        Assert.Contains(all, x => x.Item.Class == ItemClass.VehicleSpace && x.Era.Label == PeriodLabel.FarFuture);
        Assert.Contains(all, x => x.Item.Class == ItemClass.VehicleAir && x.Era.Label == PeriodLabel.Modern);
        Assert.Contains(all, x => x.Item.Class == ItemClass.VehicleSea && x.Era.Label == PeriodLabel.BronzeAge);
        Assert.DoesNotContain(all.Where(x => x.Era.Span is not null), x => x.Item.Class == ItemClass.VehicleSpace);
    }

    [Fact]
    public void NoRecipeStartsBeforeAnyMaterialItNeedsExists()
    {
        // Checked here independently of the validator: read the availability table and compare orders.
        foreach (var id in ArmsPacks)
        {
            var set = Load(id);
            int Start(string thing) => set.TryAvailability(thing, out var window) && set.TryEra(window.From, out var era) ? era.Order : int.MinValue;
            foreach (var recipe in set.Root.Recipes)
            {
                var start = Start(recipe.Id);
                Assert.NotEqual(int.MinValue, start);
                foreach (var need in recipe.Inputs.Select(i => i.Resource).Concat(recipe.Catalysts.Select(c => c.Resource)).Concat(recipe.Tools.Select(t => t.Resource)))
                    Assert.True(Start(need) <= start, recipe.Id + " needs " + need + " before it exists");
            }
        }
    }

    [Fact]
    public void ABronzeAgeAxeNeedsBronzeAndTheRuleCatchesAnEarlierRecipeOnTheShippedData()
    {
        var set = Load("arms-ancient");
        var recipe = set.Root.Recipes.Single(r => r.Id == "arms-ancient.make-bronze-axe");
        Assert.Contains(recipe.Inputs, i => i.Resource == "core.bronze-ingot");
        Assert.True(set.TryAvailability("core.bronze-ingot", out var bronze) && bronze.From == "ages.bronze");
        // Move the recipe (and the axe) back to the Stone Age in memory: the library must refuse it.
        var files = new Dictionary<string, string>();
        foreach (var path in Directory.GetFiles(ShippedPackTests.PacksRoot, "*.json", SearchOption.AllDirectories))
            files[Path.GetRelativePath(ShippedPackTests.PacksRoot, path).Replace('\\', '/')] = File.ReadAllText(path);
        var document = files["arms-ancient/resources.json"];
        var stoneAxe = "{ \"id\": \"arms-ancient.make-bronze-axe\", \"from\": \"ages.bronze\"";
        Assert.Contains(stoneAxe, document);
        files["arms-ancient/resources.json"] = document.Replace(stoneAxe, "{ \"id\": \"arms-ancient.make-bronze-axe\", \"from\": \"ages.stone\"");
        var report = CatalogLoader.Load(new MemoryCatalogSource(files), "arms-ancient").Report;
        Assert.True(report.Has(CatalogCodes.EraAnachronism), report.ToString());
    }

    [Fact]
    public void EveryReplacementLineMovesForwardInTimeAndStaysInItsClass()
    {
        var items = ArmsPacks.SelectMany(id => Load(id).Root.Items).ToDictionary(i => i.Resource);
        var order = ItemsByEra().ToDictionary(x => x.Item.Resource, x => x.Era.Order);
        var chains = 0;
        foreach (var item in items.Values.Where(i => i.Replaces is not null))
        {
            var old = item.Replaces!;
            if (!items.TryGetValue(old, out var parent)) continue;   // an older pack's item the loop above did not collect
            Assert.Equal(parent.Class, item.Class);
            Assert.True(order[item.Resource] >= order[old], item.Resource + " replaces " + old + " from a later era");
            chains++;
        }
        Assert.True(chains >= 60, "expected long lineages, found " + chains + " links");
    }

    [Fact]
    public void ThereAreLongLineagesThroughManyEras()
    {
        var items = ArmsPacks.SelectMany(id => Load(id).Root.Items).ToDictionary(i => i.Resource);
        int Depth(string id) => items[id].Replaces is { } old && items.ContainsKey(old) ? 1 + Depth(old) : 0;
        Assert.True(items.Keys.Max(Depth) >= 5, "the deepest line is " + items.Keys.Max(Depth) + " items long");
    }

    [Fact]
    public void EveryVehicleHasASpeedAndBecomesAMoverInSimEngine()
    {
        foreach (var id in ArmsPacks)
        {
            var set = Load(id);
            var vehicles = set.Root.Items.Where(i => i.Class is ItemClass.VehicleLand or ItemClass.VehicleSea or ItemClass.VehicleAir or ItemClass.VehicleSpace).ToList();
            var conversion = CatalogToSim.Convert(set);
            Assert.DoesNotContain(conversion.Report.Refusals, n => n.Code == SimConversionCodes.VehicleNoSpeed);
            Assert.True(conversion.Vehicles.Count >= vehicles.Count, id + ": " + conversion.Vehicles.Count + " movers for " + vehicles.Count + " vehicles");
            if (vehicles.Count > 0) Assert.NotNull(SimWorld.Compose(conversion));
        }
    }

    [Fact]
    public void CombatStatsStayDataAndTheConverterSaysSo()
    {
        var notes = CatalogToSim.Convert(Load("arms-modern")).Report.Notes;
        Assert.Contains(notes, n => n.Code == SimConversionCodes.NoCombatEngine);
        Assert.Contains(notes, n => n.Code == SimConversionCodes.EraNotModelled);
    }


    [Fact]
    public void TheIntermediateAgesAreInOrderWithContiguousYears()
    {
        var set = Load("arms-modern");
        var ages = set.Eras.Where(e => e.Id.StartsWith("ages.", StringComparison.Ordinal)).OrderBy(e => e.Order).ToList();
        Assert.Equal(["stone", "copper", "bronze", "iron", "classical", "medieval", "renaissance", "gunpowder", "industrial", "modern", "atomic", "information"], ages.Select(e => e.Id[5..]).ToArray());
        for (var i = 1; i < ages.Count; i++) Assert.Equal(ages[i - 1].Span!.To, ages[i].Span!.From);
        Assert.Equal(1945, ages.Single(e => e.Id == "ages.atomic").Span!.From);
        var future = Load("arms-future");
        Assert.Equal(PeriodLabel.SpaceAge, future.Eras.Single(e => e.Id == "horizons.space-age").Label);
        Assert.All(future.Eras.Where(e => e.Id.StartsWith("horizons.", StringComparison.Ordinal)), e => Assert.Null(e.Span));
    }

    [Fact]
    public void EveryWeaponAmmunitionIsAnAmmunitionItemWithItsOwnStatsAndNoLaterThanTheWeapon()
    {
        foreach (var id in ArmsPacks)
        {
            var set = Load(id);
            foreach (var weapon in set.Root.Items.Where(i => i.Ammo is not null))
            {
                Assert.True(set.TryItem(weapon.Ammo!.Resource, out var ammo), weapon.Resource + " fires " + weapon.Ammo.Resource + ", which is not an item");
                Assert.Equal(ItemClass.Ammunition, ammo.Class);
                Assert.Contains("gear.damage", ammo.Stats.Keys);
                Assert.Contains("gear.penetration", ammo.Stats.Keys);
            }
        }
    }

    [Fact]
    public void FormationsCoverEveryTrainingLevelAndManyRolesWithLongLines()
    {
        var units = ArmsPacks.SelectMany(id => Load(id).Root.Units).ToList();
        Assert.True(units.Count >= 20, units.Count + " formations");
        foreach (var level in Enum.GetValues<TrainingLevel>().Where(l => l != TrainingLevel.Raw)) Assert.Contains(units, u => u.Training == level);
        Assert.Contains(units, u => u.Training == TrainingLevel.Raw);
        Assert.True(units.Select(u => u.Role).Distinct().Count() >= 6);
        var byId = units.ToDictionary(u => u.Resource);
        int Depth(string id) => byId[id].Replaces is { } old && byId.ContainsKey(old) ? 1 + Depth(old) : 0;
        Assert.True(byId.Keys.Max(Depth) >= 8, "the longest infantry line is " + byId.Keys.Max(Depth) + " formations long");
    }

    [Fact]
    public void EveryFormationIsRaisedByARecipeThatUsesItsEquipmentAndPeople()
    {
        foreach (var id in ArmsPacks)
        {
            var set = Load(id);
            foreach (var unit in set.Root.Units)
            {
                var raise = Assert.Single(set.Root.Recipes, r => r.Outputs.Any(o => o.Resource == unit.Resource));
                Assert.Contains(raise.Inputs, i => i.Resource == "core.people" && i.Amount == unit.Strength);
                foreach (var equipment in unit.Equipment) Assert.Contains(raise.Inputs, i => i.Resource == equipment.Item && i.Amount == equipment.Count);
            }
        }
    }
    [Fact]
    public void AlchemyOffersTheEraAsAnEnvironmentTagAndLosesNothingElse()
    {
        var conversion = CatalogToAlchemy.Convert(Load("arms-ancient"));
        Assert.Empty(conversion.Report.Refusals);
        Assert.Contains(conversion.Content.Recipes, r => r.Environment.Contains("era-bronze"));
        Assert.Contains(conversion.Content.Elements, e => e.Properties.Contains("item-vehicle-sea"));
    }
}
