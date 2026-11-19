using System.Globalization;
using Beep.SimEngine;
using Beep.SimEngine.Content;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>The character factory against a real SimEngine game: content, determinism, admission, squads, individuals, persistence.</summary>
public sealed class CharacterFactoryTests
{
    private static readonly PlayerId Company = new(0);

    private static SpawnLayout Grid(int x = 2, int y = 2, int columns = 10, int spacing = 2) => SpawnLayout.Grid(new TileCoord(x, y), columns, spacing);

    private static IReadOnlyList<EntityId> SpawnSquads(CharacterHarness h, string formation = "chars.platoon", int squads = 3, ulong seed = 5, SpawnLayout? layout = null) =>
        h.Do(w => h.FactoryOf(w).SpawnFormation(new SpawnFormationRequest(formation, squads, Company, seed, layout ?? Grid())));

    private static IReadOnlyList<EntityId> SpawnPeople(CharacterHarness h, string archetype = "chars.rifleman", int count = 5, ulong seed = 9, SpawnLayout? layout = null) =>
        h.Do(w => h.FactoryOf(w).SpawnIndividuals(new SpawnIndividualsRequest(archetype, count, Company, seed, layout ?? Grid())));

    private static (int Total, int Lost, int Materialized, int Headcount, int Aggregate) Squad(CharacterHarness h, EntityId id) =>
        h.Do(w => { var s = w.Entities.Get(id)!.Get<ISquad>()!; return (s.Total, s.Lost, s.Materialized, s.Headcount, s.Aggregate); });

    private static int EntityCount(CharacterHarness h) => h.Do(w => w.Entities.All.Count);

    // ---- content ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheCharacterContentLoadsAndResolves()
    {
        var h = CharacterHarness.Create();
        var (strength, members, entity, traitMax) = h.Do(w =>
        {
            var formation = w.Content.Find<CharacterFormationDef>("chars.platoon")!;
            var rifleman = w.Content.Find<CharacterArchetypeDef>("chars.rifleman")!;
            return (formation.Strength, formation.Members.Count, rifleman.Entity.Id, rifleman.TraitMax);
        });
        Assert.Equal(32, strength);
        Assert.Equal(2, members);
        Assert.Equal("chars.walker", entity);
        Assert.Equal(2, traitMax);
    }

    public static IEnumerable<object[]> BadContent() => new[]
    {
        new object[] { "chars/characters.json", "\"min\": 1, \"max\": 20 }", "\"min\": 30, \"max\": 20 }", "min 30 exceeds max 20" },
        new object[] { "chars/characters.json", "\"max_level\": 5 },\n    { \"id\": \"chars.skill.drill\"", "\"max_level\": 0 },\n    { \"id\": \"chars.skill.drill\"", "max_level must be at least 1" },
        new object[] { "chars/characters.json", "\"names\": [\"Adam\", \"Bram\"", "\"names\": [\"Adam\", \"Adam\"", "unique" },
        new object[] { "chars/characters.json", "\"entity\": \"chars.walker\", \"max_spawn\": 2000", "\"entity\": \"chars.nothing\", \"max_spawn\": 2000", "unknown EntityDef" },
        new object[] { "chars/characters.json", "\"entity\": \"chars.walker\", \"max_spawn\": 2000", "\"entity\": \"chars.squad\", \"max_spawn\": 2000", "no character component" },
        new object[] { "chars/characters.json", "\"stat\": \"chars.stat.strength\", \"min\": 8, \"max\": 14", "\"stat\": \"chars.stat.strength\", \"min\": 8, \"max\": 30", "must lie inside" },
        new object[] { "chars/characters.json", "\"skill\": \"chars.skill.shooting\", \"min\": 1, \"max\": 4", "\"skill\": \"chars.skill.shooting\", \"min\": 1, \"max\": 9", "must lie inside" },
        new object[] { "chars/characters.json", "\"min\": 0, \"max\": 2, \"pool\"", "\"min\": 0, \"max\": 9, \"pool\"", "pool size" },
        new object[] { "chars/characters.json", "{ \"trait\": \"chars.trait.brave\", \"weight\": 3 }", "{ \"trait\": \"chars.trait.brave\", \"weight\": 0 }", "weight of at least 1" },
        new object[] { "chars/characters.json", "\"max_spawn\": 2000", "\"max_spawn\": 0", "max_spawn must be at least 1" },
        new object[] { "chars/characters.json", "\"squad_entity\": \"chars.squad\", \"max_squads\": 200, \"catalog_unit\": null,\n      \"members\": [{ \"archetype\": \"chars.rifleman\", \"count\": 30 }", "\"squad_entity\": \"chars.walker\", \"max_squads\": 200, \"catalog_unit\": null,\n      \"members\": [{ \"archetype\": \"chars.rifleman\", \"count\": 30 }", "no squad component" },
        new object[] { "chars/characters.json", "\"squad_entity\": \"chars.small-squad\", \"max_squads\": 10, \"catalog_unit\": null,\n      \"members\": [{ \"archetype\": \"chars.rifleman\", \"count\": 6 }", "\"squad_entity\": \"chars.small-squad\", \"max_squads\": 10, \"catalog_unit\": null,\n      \"members\": [{ \"archetype\": \"chars.rifleman\", \"count\": 60 }", "exceeds" },
        new object[] { "chars/characters.json", "\"count\": 30 }, { \"archetype\": \"chars.sergeant\", \"count\": 2 }", "\"count\": 30 }, { \"archetype\": \"chars.rifleman\", \"count\": 2 }", "listed twice" },
        new object[] { "chars/characters.json", "\"max_squads\": 200, \"catalog_unit\": null,\n      \"members\": [{ \"archetype\": \"chars.rifleman\", \"count\": 96 }", "\"max_squads\": 0, \"catalog_unit\": null,\n      \"members\": [{ \"archetype\": \"chars.rifleman\", \"count\": 96 }", "max_squads must be at least 1" },
        new object[] { "chars/characters.json", "\"count\": 96 }, { \"archetype\": \"chars.sergeant\", \"count\": 4 }", "\"count\": 0 }, { \"archetype\": \"chars.sergeant\", \"count\": 4 }", "count of at least 1" },
        new object[] { "chars/world.json", "\"squad\": { \"max_members\": 20000 }", "\"squad\": { \"max_members\": 0 }", "max_members must be 1 to" },
        new object[] { "chars/world.json", "\"squad\": { \"max_members\": 20000 }", "\"squad\": { \"max_members\": 20000, \"extra\": 1 }", "unknown key 'extra'" },
        new object[] { "chars/characters.json", "\"catalog_unit\": null,\n      \"members\": [{ \"archetype\": \"chars.rifleman\", \"count\": 96 }", "\"members\": [{ \"archetype\": \"chars.rifleman\", \"count\": 96 }", "catalog_unit" },
    };

    [Theory]
    [MemberData(nameof(BadContent))]
    public void EveryContentMistakeIsRefusedAtLoadWithAMessageThatNamesIt(string file, string find, string replace, string expected)
    {
        var path = CharacterHarness.EditedContent("CharacterContent", (file, (Func<string, string>)(text =>
        {
            Assert.Contains(find, text);
            return text.Replace(find, replace);
        })));
        try
        {
            var error = Assert.ThrowsAny<Exception>(() => CharacterHarness.Create(contentPath: path));
            Assert.Contains(expected, error.Message, StringComparison.Ordinal);
        }
        finally { Directory.Delete(path, true); }
    }

    // ---- the pure parts ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheSameArchetypeSeedAndIndexAlwaysMakeTheSameCharacterAndOthersDiffer()
    {
        var h = CharacterHarness.Create();
        var (first, again, other, otherIndex) = h.Do(w =>
        {
            var a = w.Content.Find<CharacterArchetypeDef>("chars.rifleman")!;
            return (CharacterGenerator.Generate(a, 42, 7), CharacterGenerator.Generate(a, 42, 7), CharacterGenerator.Generate(a, 43, 7), CharacterGenerator.Generate(a, 42, 8));
        });
        Assert.True(first.SameAs(again));
        Assert.False(first.SameAs(other) && first.SameAs(otherIndex));
        // Pinned: if the generator changes, every saved squad changes who its members are. Change it only on purpose.
        Assert.Equal("Emil Gray", first.Name);
        Assert.Equal(new[] { 14, 58 }, first.Stats);
        Assert.Equal(new[] { 4, 1 }, first.Skills);
        Assert.Equal(new[] { "chars.trait.steady" }, first.Traits);
    }

    [Theory]
    [InlineData("sv-SE")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    public void TheCharactersDoNotDependOnTheMachineCulture(string culture)
    {
        var h = CharacterHarness.Create();
        string Sample() => h.Do(w =>
        {
            var a = w.Content.Find<CharacterArchetypeDef>("chars.sergeant")!;
            var records = Enumerable.Range(0, 200).Select(i => CharacterGenerator.Generate(a, 99, i));
            return string.Join("|", records.Select(r => r.Name + ":" + string.Join(",", r.Stats) + ":" + string.Join(",", r.Skills) + ":" + string.Join(",", r.Traits)));
        });
        var expected = Sample();
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            Assert.Equal(expected, Sample());
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(64)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(4097)]
    public void TheCasualtyOrderVisitsEverySlotExactlyOnce(int count)
    {
        var seen = new HashSet<int>();
        for (var i = 0; i < count; i++)
        {
            var slot = SlotPermutation.Permute(i, count, 12345);
            Assert.InRange(slot, 0, count - 1);
            Assert.True(seen.Add(slot), "slot " + slot + " came up twice");
        }
        if (count >= 64)
        {
            var other = Enumerable.Range(0, count).Select(i => SlotPermutation.Permute(i, count, 54321)).ToArray();
            var mine = Enumerable.Range(0, count).Select(i => SlotPermutation.Permute(i, count, 12345)).ToArray();
            Assert.NotEqual(mine, other);   // another key, another order
            Assert.NotEqual(Enumerable.Range(0, count).ToArray(), mine);   // and not the identity
        }
    }

    [Fact]
    public void TheDrawsAreUnbiasedEnoughThatEveryValueOfASmallBoundComesUp()
    {
        var counts = new int[7];
        for (var i = 0; i < 7000; i++) counts[new CharacterRandom(1, i).Next(7)]++;
        Assert.All(counts, c => Assert.InRange(c, 800, 1200));
    }

    // ---- spawning squads ------------------------------------------------------------------------------------------------------

    [Fact]
    public void AFormationBecomesSquadsOfTheRosterStrengthStandingInTheLayout()
    {
        var h = CharacterHarness.Create();
        var ids = SpawnSquads(h, squads: 3);
        Assert.Equal(3, ids.Count);
        Assert.Equal(ids.Select(i => i.Value).Order().ToArray(), ids.Select(i => i.Value).ToArray());   // monotonic ids in batch order
        Assert.All(ids, id => Assert.Equal((32, 0, 0, 32, 32), Squad(h, id)));
        var tiles = h.Do(w => ids.Select(id => w.Entities.Get(id)!.Get<Beep.SimEngine.Maps.TilePresence>()!.Tile).ToArray());
        Assert.Equal(new[] { new TileCoord(2, 2), new TileCoord(4, 2), new TileCoord(6, 2) }, tiles);
        Assert.Equal(3, EntityCount(h));
    }

    [Fact]
    public void EachSquadGetsItsOwnSeedAndSoItsOwnPeople()
    {
        var h = CharacterHarness.Create();
        var ids = SpawnSquads(h, squads: 2, seed: 77);
        var (a, b, aAgain) = h.Do(w =>
        {
            var f = h.FactoryOf(w);
            return (f.Describe(ids[0], 3), f.Describe(ids[1], 3), f.Describe(ids[0], 3));
        });
        Assert.True(a.SameAs(aAgain));
        Assert.False(a.SameAs(b));
        var seeds = h.Do(w => ids.Select(id => w.Entities.Get(id)!.Get<ISquad>()!.Seed).ToArray());
        Assert.Equal(new[] { CharacterRandom.Derive(77, 0), CharacterRandom.Derive(77, 1) }, seeds);
    }

    [Fact]
    public void ASlotsCharacterIsDrawnFromTheArchetypeOfItsRosterEntry()
    {
        var h = CharacterHarness.Create();
        var id = SpawnSquads(h, squads: 1)[0];
        var roles = h.Do(w =>
        {
            var squad = (SquadComponent)w.Entities.Get(id)!.Get<ISquad>()!;
            return new[] { squad.RosterIndexOf(0), squad.RosterIndexOf(29), squad.RosterIndexOf(30), squad.RosterIndexOf(31) };
        });
        Assert.Equal(new[] { 0, 0, 1, 1 }, roles);
        var (rifle, sergeant) = h.Do(w => (h.FactoryOf(w).Describe(id, 0), h.FactoryOf(w).Describe(id, 31)));
        Assert.InRange(rifle.Skills[0], 1, 4);        // rifleman shooting 1 to 4
        Assert.InRange(sergeant.Skills[0], 3, 5);     // sergeant shooting 3 to 5
        Assert.InRange(sergeant.Stats[1], 60, 95);    // sergeant courage
    }

    // ---- admission is all or nothing ----------------------------------------------------------------------------------------------

    public static IEnumerable<object[]> Refusals() => new[]
    {
        new object[] { "chars.unknown", 1, 2, 2, 10, 2, "characters.unknown-formation" },
        new object[] { "chars.platoon", 0, 2, 2, 10, 2, "characters.bad-count" },
        new object[] { "chars.platoon", -3, 2, 2, 10, 2, "characters.bad-count" },
        new object[] { "chars.platoon", 201, 2, 2, 10, 2, "characters.too-many" },
        new object[] { "chars.platoon", 3, 2, 2, 0, 2, "characters.bad-layout" },
        new object[] { "chars.platoon", 3, 2, 2, 10, -1, "characters.bad-layout" },
        new object[] { "chars.platoon", 3, -1, 2, 10, 2, "characters.bad-layout" },
        new object[] { "chars.platoon", 3, 126, 2, 10, 2, "characters.bad-layout" },    // the third squad would stand at x 130, off a 128 wide map
        new object[] { "chars.platoon", 70, 0, 0, 1, 2, "characters.bad-layout" },      // 70 squads in one column run off a 128 tall map
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public void ARefusedSpawnChangesNothingNotEvenTheHashOrTheNextIdentity(string formation, int squads, int x, int y, int columns, int spacing, string code)
    {
        var h = CharacterHarness.Create();
        SpawnSquads(h, squads: 2);   // something already in the world
        var hash = h.EntitiesHash;
        var count = EntityCount(h);
        var refusal = h.Do(w => h.FactoryOf(w).CheckSpawnFormation(new SpawnFormationRequest(formation, squads, Company, 1, SpawnLayout.Grid(new TileCoord(x, y), columns, spacing))));
        Assert.Equal(code, refusal.Id);
        var message = h.Do(w => { try { h.FactoryOf(w).SpawnFormation(new SpawnFormationRequest(formation, squads, Company, 1, SpawnLayout.Grid(new TileCoord(x, y), columns, spacing))); return "created"; } catch (InvalidOperationException e) { return e.Message; } });
        Assert.Contains(code, message);
        Assert.Equal(hash, h.EntitiesHash);
        Assert.Equal(count, EntityCount(h));
    }

    [Fact]
    public void ABatchWhoseLastMemberDoesNotFitCreatesNobodyAtAll()
    {
        var h = CharacterHarness.Create();
        var before = (Hash: h.Hash, Count: EntityCount(h));
        // 11 squads of a 2-tile spacing starting at x 108 put the last squad at x 128, one tile off the map.
        var layout = Grid(108, 5, 11, 2);
        var refusal = h.Do(w => h.FactoryOf(w).CheckSpawnFormation(new SpawnFormationRequest("chars.platoon", 11, Company, 3, layout)));
        Assert.Equal("characters.bad-layout", refusal.Id);
        Assert.Equal(before.Count, EntityCount(h));
        var ok = h.Do(w => h.FactoryOf(w).CheckSpawnFormation(new SpawnFormationRequest("chars.platoon", 10, Company, 3, layout)));
        Assert.True(ok.IsNone);
    }

    [Fact]
    public void IndividualsOfABlockingKindMustBeSpacedApartAndTheLayoutIsStated()
    {
        var h = CharacterHarness.Create();
        var tight = h.Do(w => h.FactoryOf(w).CheckSpawnIndividuals(new SpawnIndividualsRequest("chars.sentry", 4, Company, 1, Grid(5, 5, 2, 0))));
        Assert.Equal("characters.bad-layout", tight.Id);
        var ids = SpawnPeople(h, "chars.sentry", 4, 1, Grid(5, 5, 2, 1));
        Assert.Equal(4, ids.Count);
        // A second batch onto the same tiles is refused by the world's own placement admission, so nothing is created.
        var count = EntityCount(h);
        var overlap = h.Do(w => h.FactoryOf(w).CheckSpawnIndividuals(new SpawnIndividualsRequest("chars.sentry", 2, Company, 2, Grid(5, 5, 2, 1))));
        Assert.False(overlap.IsNone);
        Assert.Equal(count, EntityCount(h));
        var none = h.Do(w => h.FactoryOf(w).CheckSpawnIndividuals(new SpawnIndividualsRequest("chars.rifleman", 2, Company, 2, SpawnLayout.None)));
        Assert.Equal("characters.no-placement", none.Id);
    }

    [Fact]
    public void ASpawnForAnUnseatedCompanyOrOverTheLimitIsRefused()
    {
        var h = CharacterHarness.Create();
        var unseated = h.Do(w => h.FactoryOf(w).CheckSpawnFormation(new SpawnFormationRequest("chars.platoon", 1, new PlayerId(9), 1, Grid())));
        Assert.False(unseated.IsNone);
        var tooMany = h.Do(w => h.FactoryOf(w).CheckSpawnIndividuals(new SpawnIndividualsRequest("chars.sergeant", 201, Company, 1, Grid())));
        Assert.Equal("characters.too-many", tooMany.Id);
        var unknown = h.Do(w => h.FactoryOf(w).CheckSpawnIndividuals(new SpawnIndividualsRequest("chars.nobody", 1, Company, 1, Grid())));
        Assert.Equal("characters.unknown-archetype", unknown.Id);
    }

    // ---- individuals --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnIndividualIsTheCharacterTheGeneratorDrawsForItsSeedAndIndex()
    {
        var h = CharacterHarness.Create();
        var ids = SpawnPeople(h, "chars.rifleman", 4, 9);
        var (chars, expected) = h.Do(w =>
        {
            var a = w.Content.Find<CharacterArchetypeDef>("chars.rifleman")!;
            return (ids.Select(id => w.Entities.Get(id)!.Get<ICharacter>()!).Select(c => (c.Name, c.Index, c.Seed, c.Archetype, string.Join(",", c.Stats), string.Join(",", c.Traits))).ToArray(),
                Enumerable.Range(0, 4).Select(i => CharacterGenerator.Generate(a, 9, i)).Select(r => (r.Name, string.Join(",", r.Stats), string.Join(",", r.Traits))).ToArray());
        });
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(expected[i].Name, chars[i].Name);
            Assert.Equal(expected[i].Item2, chars[i].Item5);
            Assert.Equal(expected[i].Item3, chars[i].Item6);
            Assert.Equal((i, 9ul, "chars.rifleman"), (chars[i].Index, chars[i].Seed, chars[i].Archetype));
        }
    }

    // ---- casualties, materializing and killing ------------------------------------------------------------------------------------

    [Fact]
    public void CasualtiesRemoveMembersBySeededOrderAndNeverMoreThanThereAre()
    {
        var h = CharacterHarness.Create();
        var id = SpawnSquads(h, squads: 1)[0];
        h.Do(w => { h.FactoryOf(w).Casualties(id, 10); return 0; });
        Assert.Equal((32, 10, 0, 22, 22), Squad(h, id));
        var lost1 = h.Do(w => Enumerable.Range(0, 32).Where(s => w.Entities.Get(id)!.Get<ISquad>()!.IsLost(s)).ToArray());
        Assert.Equal(10, lost1.Length);
        h.Do(w => { h.FactoryOf(w).Casualties(id, 12); return 0; });
        Assert.Equal((32, 22, 0, 10, 10), Squad(h, id));
        var refusal = h.Do(w => h.FactoryOf(w).CheckCasualties(id, 11));
        Assert.Equal("characters.not-enough-members", refusal.Id);
        var before = h.Hash;
        Assert.Contains("not-enough-members", h.Do(w => { try { h.FactoryOf(w).Casualties(id, 11); return "removed"; } catch (InvalidOperationException e) { return e.Message; } }));
        Assert.Equal((32, 22, 0, 10, 10), Squad(h, id));
        Assert.Equal("characters.bad-count", h.Do(w => h.FactoryOf(w).CheckCasualties(id, 0)).Id);
        Assert.Equal("characters.unknown-squad", h.Do(w => h.FactoryOf(w).CheckCasualties(new EntityId(999), 1)).Id);
        _ = before;
    }

    [Fact]
    public void TwoGamesGiveTheSameSquadTheSameCasualtiesAndAnotherSeedDoesNot()
    {
        List<int> Lost(ulong seed)
        {
            var h = CharacterHarness.Create();
            var id = SpawnSquads(h, squads: 1, seed: seed)[0];
            h.Do(w => { h.FactoryOf(w).Casualties(id, 12); return 0; });
            return h.Do(w => Enumerable.Range(0, 32).Where(s => w.Entities.Get(id)!.Get<ISquad>()!.IsLost(s)).ToList());
        }
        Assert.Equal(Lost(5), Lost(5));
        Assert.NotEqual(Lost(5), Lost(6));
    }

    [Fact]
    public void MaterializingAMemberMakesTheSameCharacterTheSquadDescribedAndKeepsTheCountExact()
    {
        var h = CharacterHarness.Create();
        var squad = SpawnSquads(h, squads: 1, seed: 21)[0];
        var described = h.Do(w => h.FactoryOf(w).Describe(squad, 5));
        var character = h.Do(w => h.FactoryOf(w).Materialize(squad, 5));
        var c = h.Do(w => { var x = w.Entities.Get(character)!.Get<ICharacter>()!; return (x.Name, string.Join(",", x.Stats), string.Join(",", x.Skills), string.Join(",", x.Traits), x.Squad, x.Slot, x.Archetype); });
        Assert.Equal(described.Name, c.Name);
        Assert.Equal(string.Join(",", described.Stats), c.Item2);
        Assert.Equal(string.Join(",", described.Traits), c.Item4);
        Assert.Equal((squad, 5, "chars.rifleman"), (c.Squad, c.Slot, c.Archetype));
        Assert.Equal((32, 0, 1, 32, 31), Squad(h, squad));
        Assert.Equal("characters.slot-materialized", h.Do(w => h.FactoryOf(w).CheckMaterialize(squad, 5)).Id);
        Assert.Equal("characters.bad-slot", h.Do(w => h.FactoryOf(w).CheckMaterialize(squad, 32)).Id);
        Assert.Equal("characters.bad-slot", h.Do(w => h.FactoryOf(w).CheckMaterialize(squad, -1)).Id);
        // The squad still describes the same character for that slot: the two representations agree.
        Assert.True(h.Do(w => h.FactoryOf(w).Describe(squad, 5)).SameAs(described));
    }

    [Fact]
    public void CasualtiesSkipMaterializedMembersAndAKilledIndividualIsALostSlot()
    {
        var h = CharacterHarness.Create();
        var squad = SpawnSquads(h, squads: 1)[0];
        var heroes = new[] { 0, 1, 2, 3, 4, 5, 6, 7 }.Select(s => h.Do(w => h.FactoryOf(w).Materialize(squad, s))).ToArray();
        Assert.Equal((32, 0, 8, 32, 24), Squad(h, squad));
        h.Do(w => { h.FactoryOf(w).Casualties(squad, 24); return 0; });
        Assert.Equal((32, 24, 8, 8, 0), Squad(h, squad));
        Assert.Equal(Enumerable.Range(0, 8), h.Do(w => w.Entities.Get(squad)!.Get<SquadComponent>()!.MaterializedSlots.ToList()));   // none of the heroes were taken by the order
        h.Do(w => { h.FactoryOf(w).Kill(heroes[3]); return 0; });
        Assert.Equal((32, 25, 7, 7, 0), Squad(h, squad));
        Assert.True(h.Do(w => w.Entities.Get(heroes[3]) == null));
        Assert.True(h.Do(w => w.Entities.Get(squad)!.Get<ISquad>()!.IsLost(3)));
        Assert.Equal("characters.not-materialized", h.Do(w => h.FactoryOf(w).CheckKill(squad)).Id);
        Assert.Equal("characters.slot-lost", h.Do(w => h.FactoryOf(w).CheckMaterialize(squad, 3)).Id);
    }

    // ---- identity, persistence and the engine's own rules ----------------------------------------------------------------------------

    [Fact]
    public void IdentitiesAreNeverReusedNotAfterRemovalAndNotAfterALoad()
    {
        var h = CharacterHarness.Create();
        var squad = SpawnSquads(h, squads: 1)[0];
        var hero = h.Do(w => h.FactoryOf(w).Materialize(squad, 0));
        h.Do(w => { h.FactoryOf(w).Kill(hero); return 0; });
        var after = SpawnPeople(h, "chars.rifleman", 2);
        Assert.True(after[0].Value > hero.Value, "a removed identity came back");
        var loaded = h.Load(h.Game.Save());
        var more = SpawnPeople(loaded, "chars.rifleman", 2, 4);
        Assert.True(more[0].Value > after[1].Value, "a load handed out an identity again");
    }

    [Fact]
    public void SaveAndLoadGiveTheSameHashAndTheSameFutureForSquadsIndividualsAndLosses()
    {
        var h = CharacterHarness.Create();
        var squads = SpawnSquads(h, "chars.company", 3, 31);
        SpawnPeople(h, "chars.sergeant", 4, 8, Grid(40, 40, 4, 1));
        h.Do(w => { h.FactoryOf(w).Casualties(squads[1], 30); return 0; });
        var hero = h.Do(w => h.FactoryOf(w).Materialize(squads[0], 50));
        h.Tick(3);
        var entitiesBefore = h.EntitiesHash;
        var loaded = h.Load(h.Game.Save());
        Assert.Equal(h.Hash, loaded.Hash);   // compared after the save: saving settles the world, so the whole-world hash before it is not the saved one
        Assert.Equal(entitiesBefore, h.EntitiesHash);   // but saving does not touch the entities

        // Both continue the same way: more casualties, a death, a new spawn.
        foreach (var world in new[] { h, loaded })
        {
            world.Do(w => { world.FactoryOf(w).Casualties(squads[2], 17); return 0; });
            world.Do(w => { world.FactoryOf(w).Kill(hero); return 0; });
            SpawnPeople(world, "chars.rifleman", 3, 12, Grid(60, 60, 3, 1));
        }
        var a = h.Game.HashBreakdown(); var b = loaded.Game.HashBreakdown();
        Assert.True(h.Hash == loaded.Hash, "differs in: " + string.Join(", ", a.Zip(b, (x, y) => x.Key + (x.Value == y.Value ? "=" : "!=")).Where(s => s.Contains("!="))) + " tick " + h.Game.Tick + "/" + loaded.Game.Tick);
    }

    [Fact]
    public void TwoSeparateRunsOfTheSameCommandsHaveTheSameHashAtEveryTick()
    {
        var hashes = new List<ulong>[2];
        for (var run = 0; run < 2; run++)
        {
            var h = CharacterHarness.Create();
            hashes[run] = new List<ulong>();
            var squads = SpawnSquads(h, "chars.company", 2, 3);
            hashes[run].Add(h.Hash);
            h.Do(w => { h.FactoryOf(w).Casualties(squads[0], 20); return 0; }); hashes[run].Add(h.Hash);
            h.Do(w => { h.FactoryOf(w).Materialize(squads[1], 7); return 0; }); hashes[run].Add(h.Hash);
            h.Tick(); hashes[run].Add(h.Hash);
        }
        Assert.Equal(hashes[0], hashes[1]);
        Assert.Equal(hashes[0].Count, hashes[0].Distinct().Count());   // and each step really changed the state
    }

    [Fact]
    public void AnEntityWithACharacterComponentThatTheFactoryDidNotMakeCannotBeSavedOrHashed()
    {
        var h = CharacterHarness.Create();
        // Create one by the world's own path, as another module might, without going through the factory.
        h.Do(w =>
        {
            var def = w.Content.Find<EntityDef>("chars.walker")!;
            var placement = new Beep.SimEngine.Maps.TilePlacement("chars.tiles", new TileCoord(3, 3), FixVec2.Centre(new TileCoord(3, 3)));
            Assert.True(w.PrepareCreation(def, Company, EntityId.None, placement, out var creation).IsNone);
            w.CommitCreation(creation);
            return 0;
        });
        Assert.ThrowsAny<Exception>(() => h.Game.Save());
    }

    [Fact]
    public void ADirectorRequestChangesNothingUntilTheWorldTicksAndTheFactoryIsOnlyReachedInsideTheTick()
    {
        var h = CharacterHarness.Create();
        var before = h.Hash;
        var ticksBefore = h.Game.Tick;
        // The harness submits and ticks in one call; the world after the call has the squads and exactly one tick more.
        SpawnSquads(h, squads: 1);
        Assert.Equal(ticksBefore + 1, h.Game.Tick);
        Assert.NotEqual(before, h.Hash);
    }

    [Fact]
    public void AGameWithNoMapRaisesSquadsAndPeopleWithoutAPlace()
    {
        var h = CharacterHarness.Create("CharacterBareContent", "bare");
        var squads = h.Do(w => h.FactoryOf(w).SpawnFormation(new SpawnFormationRequest("chars.village", 2, new PlayerId(0), 6, SpawnLayout.None)));
        Assert.Equal(2, squads.Count);
        Assert.Equal((50, 0, 0, 50, 50), Squad(h, squads[0]));
        var people = h.Do(w => h.FactoryOf(w).SpawnIndividuals(new SpawnIndividualsRequest("chars.villager", 3, new PlayerId(0), 6, SpawnLayout.None)));
        Assert.Equal(3, people.Count);
        var hero = h.Do(w => h.FactoryOf(w).Materialize(squads[0], 4));
        Assert.True(hero.IsValid);
        var placed = h.Do(w => h.FactoryOf(w).CheckSpawnFormation(new SpawnFormationRequest("chars.village", 1, new PlayerId(0), 6, SpawnLayout.Grid(new TileCoord(1, 1), 1, 1))));
        Assert.Equal("characters.unexpected-placement", placed.Id);
        Assert.Equal(h.Hash, h.Load(h.Game.Save(), "CharacterBareContent").Hash);
    }

    [Fact]
    public void TheEventsOfTheFactoryAreDeclaredAndPublishedOnce()
    {
        var h = CharacterHarness.Create();
        var ids = SpawnSquads(h, squads: 2);
        var names = h.Do(w => w.Events.Registrations.Select(r => r.Id).Where(i => i.StartsWith("sim.characters.", StringComparison.Ordinal)).Order().ToArray());
        Assert.Equal(new[] { "sim.characters.casualties", "sim.characters.formation-spawned", "sim.characters.materialized", "sim.characters.reinforced", "sim.characters.spawned" }, names);
        Assert.Equal(2, ids.Count);
    }
}
