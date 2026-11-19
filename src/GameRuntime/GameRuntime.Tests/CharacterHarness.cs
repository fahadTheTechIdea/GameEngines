using Beep.SimEngine;
using Beep.SimEngine.Content;
using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime.Tests;

/// <summary>
/// A real SimEngine game with the character module, and a way to run code inside the world's own tick (as a director effect does), so a test
/// can call the factory or look at entities without any back door into the world.
/// </summary>
internal sealed class CharacterHarness
{
    private sealed class RunEffect : IDirectorEffect
    {
        public Func<World, object?>? Pending;
        public object? Result;
        public string Name => "run";
        public Code Check(World world, string payloadJson) => Code.None;
        public void Apply(World world, string payloadJson) => Result = Pending!(world);
    }

    private readonly DirectorTickets _tickets = new();
    private readonly RunEffect _effect = new();
    private int _key;

    public SimGame Game { get; }
    public GameFactory Factory { get; }

    private CharacterHarness(SimGame game, GameFactory factory, DirectorTickets tickets, RunEffect effect)
    {
        Game = game;
        Factory = factory;
        _tickets = tickets;
        _effect = effect;
    }

    private static GameDefinition Definition(DirectorTickets tickets, RunEffect effect, bool spatial) => new("chars", spatial
        ? new GameModule[] { new ContentAccessModule(), new WorldSetupModule(), new CharacterModule(), new CharsModule(), new DirectorBridgeModule(tickets, new IDirectorEffect[] { effect }, null) }
        : new GameModule[] { new ContentAccessModule(), new WorldSetupModule(), new CharacterModule(), new CharsModule(), new DirectorBridgeModule(tickets, new IDirectorEffect[] { effect }, null) });

    private sealed class CharsModule : GameModule
    {
        public override string Id => "chars";
        public override ModuleVersion Version => new(1, 0, 0);
        public override IReadOnlyList<ModuleRequirement> Requires => new[] {
            new ModuleRequirement(ContentAccessModule.ModuleId, new ModuleVersion(1, 0, 0)),
            new ModuleRequirement(WorldSetupModule.ModuleId, new ModuleVersion(1, 0, 0)),
            new ModuleRequirement(CharacterModule.ModuleId, new ModuleVersion(1, 0, 0)) };
    }

    /// <summary>Content with edits applied: each edit names a file under the content folder and a function over its text.</summary>
    public static string EditedContent(string folder, params (string File, Func<string, string> Edit)[] edits)
    {
        var target = Directory.CreateTempSubdirectory("chars-content-").FullName;
        var source = Path.Combine(AppContext.BaseDirectory, folder);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, File.ReadAllText(file));
        }
        foreach (var (file, edit) in edits)
        {
            var path = Path.Combine(target, file);
            var text = File.ReadAllText(path);
            var edited = edit(text);
            if (edited == text) throw new InvalidOperationException("the edit changed nothing in " + file);
            File.WriteAllText(path, edited);
        }
        return target;
    }

    public static CharacterHarness Create(string folder = "CharacterContent", string scenario = "field", ulong seed = 11, string? contentPath = null)
    {
        var tickets = new DirectorTickets();
        var effect = new RunEffect();
        var definition = Definition(tickets, effect, true);
        var files = new DiskFileSource(contentPath ?? Path.Combine(AppContext.BaseDirectory, folder));
        var factory = new GameFactory(definition, ContentSet.Load(files, definition.ContentTypes));
        var game = factory.NewGame(new NewGameRequest { Scenario = scenario, Seed = seed });
        game.Initialize();
        return new CharacterHarness(game, factory, tickets, effect);
    }

    /// <summary>Loads a saved game of the same content into a new harness.</summary>
    public CharacterHarness Load(byte[] save, string folder = "CharacterContent")
    {
        var tickets = new DirectorTickets();
        var effect = new RunEffect();
        var definition = Definition(tickets, effect, true);
        var files = new DiskFileSource(Path.Combine(AppContext.BaseDirectory, folder));
        var factory = new GameFactory(definition, ContentSet.Load(files, definition.ContentTypes));
        var game = factory.Load(save);
        return new CharacterHarness(game, factory, tickets, effect);
    }

    /// <summary>Runs <paramref name="action"/> inside the next tick of the world and returns what it returned.</summary>
    public T Do<T>(Func<World, T> action)
    {
        _effect.Pending = world => action(world);
        var key = "run" + _key++;
        _tickets.Issue(key, "run", "{}");
        Game.Seat(0).Submit(new DirectorEffectCommand { Key = key, Effect = "run", Payload = "{}" });
        Game.Update(Game.Tick + 1);
        var result = _effect.Result;
        _effect.Result = null;
        _effect.Pending = null;
        return (T)result!;
    }

    public void Tick(int count = 1)
    {
        for (var i = 0; i < count; i++) Game.Update(Game.Tick + 1);
    }

    public ICharacterFactory FactoryOf(World world) => world.Services.Get<ICharacterFactory>();

    public ulong Hash => Game.Hash();

    /// <summary>The hash of the entities alone: unlike the whole-world hash it does not move with the clock, so a refused request can be shown to have changed nothing.</summary>
    public ulong EntitiesHash => Game.HashBreakdown().First(p => p.Key == "entities").Value;
}
