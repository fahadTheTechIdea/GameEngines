using CombatEngine;
using GameContent.Catalog;
using Xunit;

namespace GameContent.Tests;

/// <summary>The catalog is the single owner of every combat number: the adapter reads them, the engine never stores them.</summary>
public sealed class CombatAdapterTests
{
    private static readonly string[] ArmsPacks = ["arms-ancient", "arms-medieval", "arms-modern", "arms-future", "arms-fantasy"];

    private static readonly CombatRules Rules = CombatRulesLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Content", "combat-rules.json")));

    private static CatalogSet Load(string id) => CatalogLoader.Load(new DirectoryCatalogSource(ShippedPackTests.PacksRoot), id).Required();

    private static IEnumerable<(CatalogSet Set, CatalogItem Item)> FightingItems() =>
        ArmsPacks.SelectMany(id => { var set = Load(id); return set.Root.Items.Where(i => i.Stats.ContainsKey(GearStats.Damage)).Select(i => (set, i)); });

    [Fact]
    public void EveryFightingItemIsCompleteEnoughForCombat()
    {
        var incomplete = new List<string>();
        foreach (var (_, item) in FightingItems())
            try { CatalogCombat.Weapon(item); }
            catch (CombatAdaptRefusedException exception) { incomplete.Add(exception.Message); }
        Assert.True(incomplete.Count == 0, string.Join("\n", incomplete));
    }

    [Fact]
    public void EveryWeaponSpecEqualsTheCatalogStatsInTheEnginesUnits()
    {
        var count = 0;
        foreach (var (_, item) in FightingItems())
        {
            var weapon = CatalogCombat.Weapon(item);
            decimal Stat(string id) => (decimal)item.Stats[id];
            Assert.Equal(Stat(GearStats.Damage), weapon.Damage);
            Assert.Equal(Stat(GearStats.Penetration), weapon.Penetration);
            Assert.Equal(Stat(GearStats.Range) * 1000m, weapon.RangeMilli);
            Assert.Equal(Stat(GearStats.Accuracy) * 10m, weapon.AccuracyPermille);
            Assert.Equal(Stat(GearStats.RateOfFire) * 1000m, weapon.RatePerMinuteMilli);
            Assert.Equal((string)item.Stats[GearStats.DamageType], weapon.DamageType);
            Assert.Equal(item.Resource, weapon.Id);
            count++;
        }
        Assert.True(count >= 60, "checked " + count + " fighting items");
    }

    [Fact]
    public void EveryAmmunitionItemBuildsASpecFromItsOwnStats()
    {
        var matter = Load("matter");
        var ammunition = matter.Root.Items.Where(i => i.Class == ItemClass.Ammunition).ToList();
        Assert.True(ammunition.Count >= 15);
        foreach (var item in ammunition)
        {
            var spec = CatalogCombat.Ammo(item);
            Assert.Equal((decimal)item.Stats[GearStats.Damage], spec.Damage);
            Assert.Equal((decimal)item.Stats[GearStats.Penetration], spec.Penetration);
            Assert.Equal(item.Stats.TryGetValue(GearStats.SplashRadius, out var splash) ? (decimal)splash * 1000m : 0m, spec.SplashMilli);
        }
        Assert.Throws<CombatAdaptRefusedException>(() => CatalogCombat.Ammo(Load("arms-ancient").Root.Items.First(i => i.Class == ItemClass.Weapon)));
    }

    [Fact]
    public void EveryFormationBecomesAGroupAndEveryPairCanFightUnderTheRules()
    {
        var sets = ArmsPacks.Select(Load).ToList();
        var groups = new List<(string Id, Group Group)>();
        foreach (var set in sets)
            foreach (var unit in set.Root.Units)
                groups.Add((unit.Resource, CatalogCombat.Group(set, Rules, unit.Resource, unit.Resource, 1)));
        Assert.True(groups.Count >= 20);
        var resolver = new CombatResolver(Rules);
        foreach (var (id, group) in groups)
        {
            Assert.True(group.Health > 0 && group.InitialMembers > 0, id);
            Assert.True(group.Batteries.Count > 0 || id.EndsWith("drone-team", StringComparison.Ordinal), id + " fights with nothing");   // a reconnaissance drone team carries no weapon
            var target = groups[(groups.FindIndex(g => g.Id == id) + 1) % groups.Count].Group with { Id = "target", Side = group.Side + 1 };
            var result = resolver.Fire(new EngagementRequest(group, target, 100_000, 10, "open", "open", 1, 1, 0));
            Assert.Equal(result.Batteries.Sum(b => b.Earned), result.Batteries.Sum(b => b.Fired + b.OutOfRange + b.NoAmmo + b.Wasted));
        }
    }

    [Fact]
    public void AGroupTakesItsTrainingItsArmorAndItsAmmunitionFromTheCatalog()
    {
        var modern = Load("arms-modern");
        var platoon = modern.Root.Units.Single(u => u.Resource == "arms-modern.rifle-platoon");
        var group = CatalogCombat.Group(modern, Rules, platoon.Resource, "g", 2);
        Assert.Equal("regular", group.Training);
        Assert.Equal(platoon.Strength * (long)Rules.PersonnelHealth, group.Health);
        Assert.Equal(0, group.ArmorRating);                                    // nothing worn
        var rifle = group.Batteries.Single(b => b.Id == "arms-modern.bolt-rifle");
        Assert.Equal(30, rifle.Count);
        Assert.Equal(5 * 30, rifle.Rounds);                                    // a full load: the rifle's ammunition capacity x the rifles held
        Assert.Equal("matter.rifle-round", rifle.Ammo!.Id);

        var future = Load("arms-future");
        var squad = CatalogCombat.Group(future, Rules, "arms-future.exo-squad", "e", 1);
        Assert.Equal("elite", squad.Training);
        Assert.True(squad.ArmorRating > 100, "exo-frames add armor: " + squad.ArmorRating);
    }

    [Fact]
    public void LaterEraFormationsBeatEarlierOnesUnderTheSameRules()
    {
        var resolver = new CombatResolver(Rules);
        var ancient = CatalogCombat.Group(Load("arms-ancient"), Rules, "arms-ancient.iron-warband", "old", 1);
        var modern = CatalogCombat.Group(Load("arms-modern"), Rules, "arms-modern.rifle-platoon", "new", 2);
        var exchange = resolver.Exchange(ancient, modern, 150_000, 30, "open", "open", 5, 1, 0);
        Assert.True(exchange.First.Health * 100 / ancient.Health < exchange.Second.Health * 100 / modern.Health, "the rifle platoon should come off better at 150 m");
    }


    [Fact]
    public void AGroupMarchesAtTheSlowestPlatformOrOnFootAtTheRulesPaceAndAStructureNeverMoves()
    {
        var modern = Load("arms-modern");
        Assert.Equal(Rules.FootSpeedMilliPerSecond, CatalogCombat.Group(modern, Rules, "arms-modern.rifle-platoon", "g", 1).SpeedMilli);
        var platoon = modern.Root.Units.Single(u => u.Resource == "arms-modern.tank-platoon");
        var platforms = platoon.Equipment.Select(e => CatalogCombat.Item(modern, e.Item)).Where(i => i.Stats.ContainsKey(GearStats.Speed)).ToList();
        Assert.NotEmpty(platforms);
        var slowest = platforms.Min(i => (decimal)i.Stats[GearStats.Speed]);
        Assert.Equal((long)Math.Round(slowest * 2500m / 9m, MidpointRounding.ToEven), CatalogCombat.Group(modern, Rules, platoon.Resource, "g", 1).SpeedMilli);
        foreach (var set in ArmsPacks.Select(Load))
            foreach (var unit in set.Root.Units)
                Assert.True(CatalogCombat.Group(set, Rules, unit.Resource, "g", 1).SpeedMilli >= 0, unit.Resource);
    }

    [Fact]
    public void APlatformFormationHasItsPeopleAndItsPlatformsAsTwoPoolsWithTheCrewTheItemsName()
    {
        var modern = Load("arms-modern");
        var tanks = CatalogCombat.Group(modern, Rules, "arms-modern.tank-platoon", "t", 1);
        var tank = CatalogCombat.Item(modern, "arms-modern.medium-tank");
        Assert.Equal(16, tanks.InitialMembers);                                          // the people of the formation, not people plus steel
        Assert.Equal(16 * Rules.PersonnelHealth, tanks.Health);
        Assert.Equal(4, tanks.InitialPlatforms);
        Assert.Equal(4 * (long)(decimal)tank.Stats[GearStats.Structure], tanks.PlatformHealth);
        Assert.Equal((long)(decimal)tank.Stats[GearStats.Structure], tanks.PlatformUnitHealth);
        Assert.Equal(tank.Crew.Sum(c => c.Count), tanks.CrewPerPlatform);
        Assert.Equal(4, tanks.OperablePlatforms);
        Assert.True(tanks.Batteries.Single(b => b.Id == tank.Resource).Platform);
        // A formation with no platform items has none.
        var riflemen = CatalogCombat.Group(modern, Rules, "arms-modern.rifle-company", "r", 1);
        Assert.Equal((0, 0, 0, 0), (riflemen.InitialPlatforms, riflemen.PlatformHealth, riflemen.PlatformUnitHealth, riflemen.CrewPerPlatform));
        Assert.All(riflemen.Batteries, b => Assert.False(b.Platform));
    }

    [Fact]
    public void EveryFormationOfEveryPackBuildsAGroupWhosePoolsAddUpToItsItems()
    {
        var checkedCount = 0;
        foreach (var set in ArmsPacks.Select(Load))
            foreach (var unit in set.Root.Units)
            {
                var group = CatalogCombat.Group(set, Rules, unit.Resource, "g", 1);
                var platformItems = unit.Equipment.Select(e => (Item: CatalogCombat.Item(set, e.Item), e.Count))
                    .Where(x => x.Item.Class is ItemClass.VehicleLand or ItemClass.VehicleSea or ItemClass.VehicleAir or ItemClass.VehicleSpace or ItemClass.StructureUnit or ItemClass.Mount).ToList();
                Assert.Equal(unit.Strength, group.InitialMembers);
                Assert.Equal(unit.Strength * Rules.PersonnelHealth, group.Health);
                Assert.Equal(platformItems.Sum(x => (long)x.Count), group.InitialPlatforms);
                Assert.Equal(platformItems.Sum(x => (decimal)x.Item.Stats[GearStats.Structure] * x.Count), group.PlatformHealth);
                Assert.True(group.PlatformHealth <= group.InitialPlatforms * group.PlatformUnitHealth, unit.Resource);
                Assert.Equal(group.InitialPlatforms, group.Platforms);
                // The engine accepts what the adapter built.
                new CombatResolver(Rules).Fire(new EngagementRequest(group, CatalogCombat.Group(Load("arms-modern"), Rules, "arms-modern.rifle-company", "foe", 2), 100_000, 5, "open", "open", 3, 1, 0));
                checkedCount++;
            }
        Assert.True(checkedCount >= 20, "checked " + checkedCount);
    }

    [Fact]
    public void FuelTanksComeFromTheFuelOfThePlatformItemsAndFullTanksAreTheStart()
    {
        var modern = Load("arms-modern");
        var tank = CatalogCombat.Item(modern, "arms-modern.medium-tank");
        var tanks = CatalogCombat.Group(modern, Rules, "arms-modern.tank-platoon", "t", 1);
        Assert.Equal((long)(tank.Fuel!.Capacity * 4000m), tanks.FuelCapacity);       // four tanks, in thousandths of a unit
        Assert.Equal(tanks.FuelCapacity, tanks.Fuel);
        Assert.Equal((long)(tank.Fuel.BurnPerHour * 4000m), tanks.BurnPerHourMilli);
        Assert.Equal(0, CatalogCombat.Group(modern, Rules, "arms-modern.rifle-company", "r", 1).FuelCapacity);
        // A platform with no fuel (a catapult, a horse) rides on nothing: no tank at all.
        var catapult = CatalogCombat.Group(Load("arms-ancient"), Rules, "arms-ancient.catapult-battery", "c", 1);
        Assert.Equal((0, 0), (catapult.FuelCapacity, catapult.BurnPerHourMilli));
    }

    [Fact]
    public void AFormationWhosePlatformsBurnTwoFuelsIsRefusedBecauseOneTankCannotHoldBoth()
    {
        var copy = Path.Combine(Directory.CreateTempSubdirectory("packs-fuel-").FullName, "Packs");
        try
        {
            foreach (var file in Directory.GetFiles(ShippedPackTests.PacksRoot, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(copy, Path.GetRelativePath(ShippedPackTests.PacksRoot, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            var resources = Path.Combine(copy, "arms-modern", "resources.json");
            var original = "\"resource\": \"arms-modern.tank-platoon\", \"strength\": 16, \"role\": \"armour\", \"training\": \"regular\", \"equipment\": [{ \"item\": \"arms-modern.medium-tank\", \"count\": 4 }]";
            var text = File.ReadAllText(resources);
            Assert.Contains(original, text);
            File.WriteAllText(resources, text.Replace(original, original.Replace("\"count\": 4 }]", "\"count\": 4 }, { \"item\": \"arms-modern.steam-tractor\", \"count\": 1 }]")));
            var set = CatalogLoader.Load(new DirectoryCatalogSource(copy), "arms-modern").Required();
            Assert.Equal(CombatAdaptCodes.MixedFuel, Assert.Throws<CombatAdaptRefusedException>(() => CatalogCombat.Group(set, Rules, "arms-modern.tank-platoon", "g", 1)).Code);
        }
        finally { Directory.Delete(Path.GetDirectoryName(copy)!, true); }
    }

    [Fact]
    public void AWeaponThatIsNotAPlatformCarriesTheCrewItsItemNamesAndNoShippedFormationHasMoreCrewThanPeople()
    {
        var modern = Load("arms-modern");
        var platoon = CatalogCombat.Group(modern, Rules, "arms-modern.rifle-platoon", "p", 1);
        var gun = CatalogCombat.Item(modern, "arms-modern.light-machine-gun");
        Assert.Equal(gun.Crew.Sum(c => c.Count), platoon.Batteries.Single(b => b.Id == gun.Resource).CrewPerUnit);
        Assert.True(gun.Crew.Sum(c => c.Count) > 0);
        Assert.All(CatalogCombat.Group(modern, Rules, "arms-modern.tank-platoon", "t", 1).Batteries, b => Assert.Equal(0, b.CrewPerUnit));   // a platform has its crew in the platform
        // The crew cap only bites on a formation with more crew than people; the shipped packs have none, so the rule is for what players build.
        foreach (var set in ArmsPacks.Select(Load))
            foreach (var unit in set.Root.Units)
            {
                var group = CatalogCombat.Group(set, Rules, unit.Resource, "g", 1);
                Assert.True(group.Batteries.Where(b => !b.Platform).Sum(b => b.Count * b.CrewPerUnit) <= group.InitialMembers, unit.Resource);
            }
    }

    [Fact]
    public void MoraleStartsFromTheTrainingLevelAndAFullLoadIsTheCapacity()
    {
        foreach (var set in ArmsPacks.Select(Load))
            foreach (var unit in set.Root.Units)
            {
                var group = CatalogCombat.Group(set, Rules, unit.Resource, "g", 1);
                Assert.Equal(Rules.TrainingLevel(group.Training).MoralePermille, group.Morale);
                Assert.All(group.Batteries, b => Assert.Equal(b.Rounds, b.Capacity));
            }
    }

    [Fact]
    public void AVocabularyThatDeclaresACombatStatInAnotherUnitIsRefused()
    {
        var copy = Path.Combine(Directory.CreateTempSubdirectory("packs-unit-").FullName, "Packs");
        try
        {
            foreach (var file in Directory.GetFiles(ShippedPackTests.PacksRoot, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(copy, Path.GetRelativePath(ShippedPackTests.PacksRoot, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            var gear = Path.Combine(copy, "gear", "resources.json");
            File.WriteAllText(gear, File.ReadAllText(gear).Replace("\"id\": \"gear.range\", \"name\": \"Effective range\", \"dimension\": \"length\", \"unit\": \"m\"", "\"id\": \"gear.range\", \"name\": \"Effective range\", \"dimension\": \"length\", \"unit\": \"km\""));
            var set = CatalogLoader.Load(new DirectoryCatalogSource(copy), "arms-ancient").Required();
            var refused = Assert.Throws<CombatAdaptRefusedException>(() => CatalogCombat.Group(set, Rules, "arms-ancient.iron-warband", "g", 1));
            Assert.Equal(CombatAdaptCodes.UnitMismatch, refused.Code);
            Assert.Contains("gear.range", refused.Message);
        }
        finally { Directory.Delete(Path.GetDirectoryName(copy)!, true); }
    }
    [Fact]
    public void AMissingStatIsRefusedNotInvented()
    {
        var ancient = Load("arms-ancient");
        var club = ancient.Root.Items.Single(i => i.Resource == "arms-ancient.wooden-club");
        var withoutRange = club with { Stats = club.Stats.Where(s => s.Key != GearStats.Range).ToDictionary(s => s.Key, s => s.Value) };
        Assert.Equal(CombatAdaptCodes.MissingStat, Assert.Throws<CombatAdaptRefusedException>(() => CatalogCombat.Weapon(withoutRange)).Code);
        Assert.Equal(CombatAdaptCodes.NotAFormation, Assert.Throws<CombatAdaptRefusedException>(() => CatalogCombat.Group(ancient, Rules, "arms-ancient.wooden-club", "g", 1)).Code);
        Assert.Equal(CombatAdaptCodes.NotAnItem, Assert.Throws<CombatAdaptRefusedException>(() => CatalogCombat.Item(ancient, "core.wood")).Code);
    }

    [Fact]
    public void EveryDamageTypeTheCatalogUsesIsKnownToTheRules()
    {
        var catalogTypes = Load("gear").Root.Stats.Single(s => s.Id == GearStats.DamageType).Values;
        foreach (var type in catalogTypes) Assert.Contains(Rules.DamageTypes, t => t.Id == type);
    }
}
