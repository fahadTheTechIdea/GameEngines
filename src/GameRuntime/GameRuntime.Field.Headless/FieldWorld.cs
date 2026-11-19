using Beep.SimEngine;
using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime.Field.Headless;

/// <summary>
/// The SimEngine game of a run: the character module, the director bridge with the character effects, and the world content read from the
/// content folder plus the character definitions generated from the catalog. It can start new or load a saved world of the same forces.
/// </summary>
public sealed class FieldWorld
{
    private FieldWorld(SimGame game, SimEventOutbox outbox, DirectorTickets tickets, GameFactory factory)
    {
        Game = game;
        Outbox = outbox;
        Tickets = tickets;
        Factory = factory;
    }

    public SimGame Game { get; }
    public SimEventOutbox Outbox { get; }
    public DirectorTickets Tickets { get; }
    public GameFactory Factory { get; }

    /// <summary>What a holder (a depot or a squad) has of a stock resource, read as its owner sees it. The world owns the balance; this only reads.</summary>
    public long Held(int entity, string resource)
    {
        var facets = new List<Facet>();
        if (!Game.Seat(0).View.Inspect(new EntityId(entity), facets)) throw new InvalidOperationException("no such entity " + entity);
        foreach (var facet in facets)
            if (facet.Key == Storage.FacetStock && facet.Subject == resource) return facet.Value.Raw >> Fix.FractionalBits;
        return 0;
    }

    /// <summary>The depot entity of side 1 and of side 2, in that order: the world content places them first.</summary>
    public IReadOnlyList<int> Depots { get; private init; } = [];

    private sealed class FieldModule : GameModule
    {
        public override string Id => "field";
        public override ModuleVersion Version => new(1, 0, 0);
        public override IReadOnlyList<ModuleRequirement> Requires => new[] {
            new ModuleRequirement(ContentAccessModule.ModuleId, new ModuleVersion(1, 0, 0)),
            new ModuleRequirement(WorldSetupModule.ModuleId, new ModuleVersion(1, 0, 0)),
            new ModuleRequirement(InventoryModule.ModuleId, new ModuleVersion(1, 0, 0)),
            new ModuleRequirement(CharacterModule.ModuleId, new ModuleVersion(1, 0, 0)) };
    }

    public static string DefaultContentPath => Path.Combine(AppContext.BaseDirectory, "FieldContent");

    /// <param name="save">A saved world to continue; null starts a new game with the given seed.</param>
    public static FieldWorld Create(FieldOptions options, FieldForces forces, byte[]? save)
    {
        var outbox = new SimEventOutbox();
        var tickets = new DirectorTickets();
        var definition = new GameDefinition("field", new GameModule[] {
            new ContentAccessModule(), new WorldSetupModule(), new InventoryModule(), new CharacterModule(), new FieldModule(),
            new DirectorBridgeModule(tickets, [.. CharacterEffects.All(), .. StockEffects.All()], outbox) });
        var root = options.ContentPath ?? DefaultContentPath;
        var files = new PreloadedFileSource();
        foreach (var path in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            files.Add(relative, File.ReadAllText(path));
        }
        files.Add("field/characters.json", forces.CharactersDocument);
        files.Add("field/stock.json", StockContent.Document(forces.Groups));
        var factory = new GameFactory(definition, ContentSet.Load(files, definition.ContentTypes));
        var game = save is null ? factory.NewGame(new NewGameRequest { Scenario = "field", Seed = options.Seed }) : factory.Load(save);
        game.Initialize();
        var seen = new List<EntityView>();
        game.Seat(0).View.CopyEntities(seen);
        var depots = seen.Where(e => e.Def == "field.depot").Select(e => e.Id.Value).Order().ToList();
        if (depots.Count != 2) throw new InvalidOperationException("the field content must place exactly two depots, found " + depots.Count);
        return new FieldWorld(game, outbox, tickets, factory) { Depots = depots };
    }
}
