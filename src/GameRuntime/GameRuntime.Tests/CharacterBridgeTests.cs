using System.Text.Json;
using System.Text.Json.Nodes;
using Beep.SimEngine;
using Beep.SimEngine.Content;
using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;
using GameContent.Catalog;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// The game layer asks the character factory for things through the director bridge: the request waits for the world's next tick, the
/// answer is events, a malformed request is refused with a code, and the formation a request names can come from the catalog.
/// </summary>
public sealed class CharacterBridgeTests
{
    private sealed class NullReporter : IRuntimeReporter { public void Report(string code, string message, Exception? exception) { } }

    private sealed class Rig
    {
        public required SimGame Game { get; init; }
        public required RuntimeSession Session { get; init; }
        public List<ObservationPosted> Heard { get; } = new();
        public List<CommandResolved> Resolved { get; } = new();
        public long Step => 2 * 1_000_000L / Game.AuthoredTicksPerSecond + 10;
        public long OneTick => (1_000_000L + Game.AuthoredTicksPerSecond - 1) / Game.AuthoredTicksPerSecond;
        public int Entities => Game.Seat(0).View.EntityCount;
        public int Requests;

        public void Request(string kind, string payload) => Session.Submit(new EffectRequested("req" + Requests++, kind, payload, 0));
    }

    private static Rig Build(string folder = "CharacterContent")
    {
        var outbox = new SimEventOutbox();
        var tickets = new DirectorTickets();
        var definition = new GameDefinition("chars", new GameModule[] {
            new ContentAccessModule(), new WorldSetupModule(), new CharacterModule(), new CharsModule(),
            new DirectorBridgeModule(tickets, CharacterEffects.All(), outbox) });
        var game = new GameFactory(definition, ContentSet.Load(new DiskFileSource(Path.Combine(AppContext.BaseDirectory, folder)), definition.ContentTypes))
            .NewGame(new NewGameRequest { Scenario = "field", Seed = 11 });
        var session = new RuntimeSession(new RuntimeClock(game.AuthoredTicksPerSecond), new NullReporter());
        var rig = new Rig { Game = game, Session = session };
        session.AddComponent(new SimComponent(game, null, outbox), 1);
        session.AddComponent(new SimEffectDispatcher(game, tickets), 2);
        session.Subscribe<ObservationPosted>(rig.Heard.Add);
        session.Subscribe<CommandResolved>(rig.Resolved.Add);
        session.Init();
        session.Update(rig.Step);
        return rig;
    }

    private sealed class CharsModule : GameModule
    {
        public override string Id => "chars";
        public override ModuleVersion Version => new(1, 0, 0);
        public override IReadOnlyList<ModuleRequirement> Requires => new[] {
            new ModuleRequirement(ContentAccessModule.ModuleId, new ModuleVersion(1, 0, 0)),
            new ModuleRequirement(WorldSetupModule.ModuleId, new ModuleVersion(1, 0, 0)),
            new ModuleRequirement(CharacterModule.ModuleId, new ModuleVersion(1, 0, 0)) };
    }

    private const string Spawn = "{\"formation\":\"chars.platoon\",\"squads\":4,\"company\":0,\"seed\":77,\"layout\":{\"x\":3,\"y\":3,\"columns\":2,\"spacing\":2}}";

    // ---- the deferred rule -----------------------------------------------------------------------------------------------------

    [Fact]
    public void ARequestForSquadsChangesNothingUntilTheWorldTicksAndTheAnswerIsEvents()
    {
        var rig = Build();
        rig.Request("sim.characters.spawn-formation", Spawn);
        rig.Session.Update(rig.OneTick);   // the request is delivered at the start of this tick, the world applies it in the tick, the answer is events
        Assert.Equal(4, rig.Entities);
        Assert.Contains(rig.Resolved, r => r.Code == "QUEUED");
        Assert.Contains(rig.Resolved, r => r.Code == "APPLIED");
        var spawned = Assert.Single(rig.Heard, e => e.Kind == "sim.event.formation-spawned");
        using var payload = JsonDocument.Parse(spawned.PayloadJson);
        Assert.Equal(("chars.platoon", 4, 128), (payload.RootElement.GetProperty("formation").GetString(), payload.RootElement.GetProperty("squads").GetInt32(), payload.RootElement.GetProperty("members").GetInt32()));
    }

    // ---- refusals -----------------------------------------------------------------------------------------------------------------

    public static IEnumerable<object[]> BadRequests() => new[]
    {
        new object[] { "sim.characters.spawn-formation", "{}", "core.invalid-argument" },
        new object[] { "sim.characters.spawn-formation", "[]", "core.invalid-argument" },
        new object[] { "sim.characters.spawn-formation", "not json", "core.invalid-argument" },
        new object[] { "sim.characters.spawn-formation", Spawn.Replace("\"squads\":4", "\"squads\":4,\"extra\":1"), "core.invalid-argument" },
        new object[] { "sim.characters.spawn-formation", Spawn.Replace("\"squads\":4", "\"squads\":\"four\""), "core.invalid-argument" },
        new object[] { "sim.characters.spawn-formation", Spawn.Replace("\"squads\":4", "\"squads\":4.5"), "core.invalid-argument" },
        new object[] { "sim.characters.spawn-formation", Spawn.Replace("\"seed\":77", "\"seed\":-1"), "core.invalid-argument" },
        new object[] { "sim.characters.spawn-formation", Spawn.Replace("\"squads\":4,", "\"squads\":4,\"squads\":5,"), "core.invalid-argument" },
        new object[] { "sim.characters.spawn-formation", Spawn.Replace("\"columns\":2,", ""), "core.invalid-argument" },
        new object[] { "sim.characters.spawn-formation", Spawn.Replace("\"formation\":\"chars.platoon\"", "\"formation\":\"\""), "core.invalid-argument" },
        new object[] { "sim.characters.spawn-formation", Spawn.Replace("chars.platoon", "chars.nothing"), "characters.unknown-formation" },
        new object[] { "sim.characters.spawn-formation", Spawn.Replace("\"squads\":4", "\"squads\":0"), "characters.bad-count" },
        new object[] { "sim.characters.spawn-formation", Spawn.Replace("\"squads\":4", "\"squads\":1000"), "characters.too-many" },
        new object[] { "sim.characters.spawn-formation", Spawn.Replace("\"layout\":{\"x\":3,\"y\":3,\"columns\":2,\"spacing\":2}", "\"layout\":null"), "characters.no-placement" },
        new object[] { "sim.characters.spawn-formation", Spawn.Replace("\"x\":3", "\"x\":500"), "characters.bad-layout" },
        new object[] { "sim.characters.spawn-individuals", "{\"archetype\":\"chars.rifleman\",\"count\":3,\"company\":0,\"seed\":1}", "core.invalid-argument" },
        new object[] { "sim.characters.spawn-individuals", "{\"archetype\":\"chars.ghost\",\"count\":3,\"company\":0,\"seed\":1,\"layout\":null}", "characters.unknown-archetype" },
        new object[] { "sim.characters.materialize", "{\"squad\":99,\"slot\":0}", "characters.unknown-squad" },
        new object[] { "sim.characters.casualties", "{\"squad\":99,\"count\":1}", "characters.unknown-squad" },
        new object[] { "sim.characters.kill", "{\"character\":99}", "characters.not-materialized" },
        new object[] { "sim.characters.kill", "{\"character\":\"x\"}", "core.invalid-argument" },
    };

    [Theory]
    [MemberData(nameof(BadRequests))]
    public void ABadRequestIsRefusedWithACodeAndCreatesNothing(string kind, string payload, string code)
    {
        var rig = Build();
        rig.Request(kind, payload);
        rig.Session.Update(rig.Step);
        rig.Session.Update(rig.Step);
        Assert.Equal(0, rig.Entities);
        var answer = rig.Resolved.Last(r => r.Code != "QUEUED");
        Assert.False(answer.Applied);
        Assert.Equal(code, answer.Code);
        Assert.DoesNotContain(rig.Heard, e => e.Kind.StartsWith("sim.event.formation", StringComparison.Ordinal) || e.Kind == "sim.event.characters-spawned");
    }

    // ---- a request chain and its events -----------------------------------------------------------------------------------------

    [Fact]
    public void SquadsMaterializedMembersAndCasualtiesAllArriveAsEvents()
    {
        var rig = Build();
        rig.Request("sim.characters.spawn-formation", Spawn);
        rig.Session.Update(rig.Step); rig.Session.Update(rig.Step);
        var first = JsonDocument.Parse(Assert.Single(rig.Heard, e => e.Kind == "sim.event.formation-spawned").PayloadJson).RootElement.GetProperty("first").GetInt32();
        rig.Request("sim.characters.casualties", "{\"squad\":" + first + ",\"count\":5}");
        rig.Request("sim.characters.materialize", "{\"squad\":" + first + ",\"slot\":31}");
        rig.Session.Update(rig.Step); rig.Session.Update(rig.Step);
        var losses = JsonDocument.Parse(Assert.Single(rig.Heard, e => e.Kind == "sim.event.squad-casualties").PayloadJson).RootElement;
        Assert.Equal((5, 27), (losses.GetProperty("lost").GetInt32(), losses.GetProperty("remaining").GetInt32()));
        var made = JsonDocument.Parse(Assert.Single(rig.Heard, e => e.Kind == "sim.event.character-materialized").PayloadJson).RootElement;
        Assert.Equal((first, 31), (made.GetProperty("squad").GetInt32(), made.GetProperty("slot").GetInt32()));
        Assert.Equal(5, rig.Entities);   // four squads and one individual
        rig.Request("sim.characters.kill", "{\"character\":" + made.GetProperty("character").GetInt32() + "}");
        rig.Session.Update(rig.Step); rig.Session.Update(rig.Step);
        Assert.Equal(4, rig.Entities);
        Assert.Equal(2, rig.Heard.Count(e => e.Kind == "sim.event.squad-casualties"));
    }

    [Fact]
    public void TwoGamesGivenTheSameRequestsEndInTheSameStateAndASavedOneContinuesIdentically()
    {
        ulong Run(bool saveHalfway)
        {
            var rig = Build();
            rig.Request("sim.characters.spawn-formation", Spawn);
            rig.Session.Update(rig.Step); rig.Session.Update(rig.Step);
            rig.Request("sim.characters.casualties", "{\"squad\":1,\"count\":9}");
            rig.Session.Update(rig.Step); rig.Session.Update(rig.Step);
            if (saveHalfway) _ = rig.Game.Save();
            rig.Request("sim.characters.materialize", "{\"squad\":2,\"slot\":3}");
            rig.Session.Update(rig.Step); rig.Session.Update(rig.Step);
            return rig.Game.Hash();
        }
        Assert.Equal(Run(false), Run(false));
        Assert.Equal(Run(false), Run(true));   // saving does not change the future
    }

    // ---- the catalog is the one owner of roster facts -------------------------------------------------------------------------------

    internal static CharacterTemplate Template() => new(
        IndividualEntity: "chars.walker", SquadEntity: "chars.squad", GivenNames: "chars.names.given", FamilyNames: "chars.names.family",
        Stats:
        [
            new StatPlan("chars.stat.strength", new TrainingRanges(new LevelRange(5, 10), new LevelRange(8, 14), new LevelRange(10, 16), new LevelRange(12, 18))),
            new StatPlan("chars.stat.courage", new TrainingRanges(new LevelRange(10, 50), new LevelRange(30, 80), new LevelRange(50, 90), new LevelRange(70, 100))),
        ],
        Skills:
        [
            new StatPlan("chars.skill.shooting", new TrainingRanges(new LevelRange(0, 2), new LevelRange(1, 4), new LevelRange(2, 5), new LevelRange(3, 5))),
        ],
        Traits: [new TraitWeight("chars.trait.brave", 3), new TraitWeight("chars.trait.careful", 2), new TraitWeight("chars.trait.steady", 2)],
        TraitMin: 0, TraitMax: 2, MaxSpawn: 500, MaxSquads: 50);

    internal static CatalogSet Load(string id) => CatalogLoader.Load(new DirectoryCatalogSource(Path.Combine(AppContext.BaseDirectory, "Packs")), id).Required();

    /// <summary>The character test content with its archetypes and formations replaced by the ones generated from catalog formations.</summary>
    private static string ContentFor(CatalogSet set, string[] formations)
    {
        var generated = JsonNode.Parse(CatalogCharacters.Document(set, formations, Template()))!.AsObject();
        return CharacterHarness.EditedContent("CharacterContent", ("chars/characters.json", text =>
        {
            var document = JsonNode.Parse(text)!.AsObject();
            document["character_archetypes"] = generated["character_archetypes"]!.DeepClone();
            document["character_formations"] = generated["character_formations"]!.DeepClone();
            return document.ToJsonString();
        }));
    }

    [Fact]
    public void ASquadRaisedFromACatalogFormationHasTheCatalogsHeadcountAndTheCombatGroupAgreesWithIt()
    {
        var set = Load("arms-modern");
        var formations = set.Root.Units.Select(u => u.Resource).ToArray();
        var path = ContentFor(set, formations);
        try
        {
            var h = CharacterHarness.Create(contentPath: path);
            var rules = CombatEngine.CombatRulesLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Content", "combat-rules.json")));
            var x = 2;
            foreach (var unit in set.Root.Units)
            {
                var ids = h.Do(w => h.FactoryOf(w).SpawnFormation(new SpawnFormationRequest(unit.Resource, 1, new PlayerId(0), 5, SpawnLayout.Grid(new TileCoord(x, 2), 1, 1))));
                x += 3;
                var squad = h.Do(w => w.Entities.Get(ids[0])!.Get<ISquad>()!);
                var group = CatalogCombat.Group(set, rules, unit.Resource, "g", 1);
                Assert.Equal(unit.Strength, squad.Total);                       // the people of the catalog formation
                Assert.Equal(unit.Strength, squad.Headcount);
                // The combat group is built from the same formation: its personnel health is strength times the rules' personnel health,
                // and platforms (vehicles, structures) only add to it.
                Assert.True(group.Health >= unit.Strength * (long)rules.PersonnelHealth, unit.Resource);
                if (unit.Equipment.All(e => !set.TryItem(e.Item, out var item) || item.Class is ItemClass.Weapon or ItemClass.Armor or ItemClass.Tool or ItemClass.Ammunition))
                    Assert.Equal(unit.Strength, group.InitialMembers);          // people only: the same headcount on both sides
                var definition = h.Do(w => w.Content.Find<CharacterFormationDef>(unit.Resource)!);
                Assert.Equal(unit.Resource, definition.CatalogUnit);
                Assert.Equal(unit.Equipment.Select(e => (e.Item, e.Count)).ToArray(), definition.Equipment.Select(e => (e.Item, e.Count)).ToArray());
                foreach (var link in definition.Equipment) Assert.True(set.TryItem(link.Item, out _), "equipment id " + link.Item + " is not a catalog item");
            }
        }
        finally { Directory.Delete(path, true); }
    }

    [Fact]
    public void AnythingTheCatalogDoesNotKnowOrTheTemplateGetsWrongIsRefusedAtTheBridge()
    {
        var set = Load("arms-modern");
        Assert.Equal(CharacterAdaptCodes.NotAFormation, Assert.Throws<CharacterAdaptRefusedException>(() => CatalogCharacters.Document(set, ["arms-modern.bolt-rifle"], Template())).Code);
        Assert.Equal(CharacterAdaptCodes.BadTemplate, Assert.Throws<CharacterAdaptRefusedException>(() => CatalogCharacters.Document(set, ["arms-modern.rifle-platoon"], Template() with { MaxSquads = 0 })).Code);
        Assert.Equal(CharacterAdaptCodes.BadTemplate, Assert.Throws<CharacterAdaptRefusedException>(() => CatalogCharacters.Document(set, ["arms-modern.rifle-platoon"], Template() with { TraitMax = 9 })).Code);
        Assert.Equal(CharacterAdaptCodes.BadTemplate, Assert.Throws<CharacterAdaptRefusedException>(() => CatalogCharacters.Document(set, ["arms-modern.rifle-platoon"], Template() with { GivenNames = " " })).Code);
    }

    [Fact]
    public void TheGeneratedDocumentIsTheSameEveryTimeAndFollowsTheCatalog()
    {
        var set = Load("arms-ancient");
        var formations = set.Root.Units.Select(u => u.Resource).ToArray();
        var one = CatalogCharacters.Document(set, formations, Template());
        Assert.Equal(one, CatalogCharacters.Document(set, formations, Template()));
        var node = JsonNode.Parse(one)!.AsObject();
        Assert.Equal(formations.Length, node["character_formations"]!.AsArray().Count);
        var hunting = node["character_formations"]!.AsArray().Single(f => f!["id"]!.GetValue<string>() == "arms-ancient.hunting-band")!;
        Assert.Equal(set.Root.Units.Single(u => u.Resource == "arms-ancient.hunting-band").Strength, hunting["members"]![0]!["count"]!.GetValue<int>());
    }
}
