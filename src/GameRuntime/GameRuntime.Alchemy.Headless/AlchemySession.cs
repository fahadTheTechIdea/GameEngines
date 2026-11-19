using AlchemyEngine;

namespace GameRuntime.Alchemy.Headless;

/// <summary>
/// One run of the alchemy game: the rules component, the goal and the player as components of one RuntimeSession. The session owns
/// the one clock (<see cref="Clock"/>); each tick it runs every component's Update once, in order. Whoever hosts the session (the
/// headless main loop, a test, a Godot node) feeds it elapsed time and writes <see cref="Input"/>.
/// </summary>
public sealed class AlchemySession
{
    public const int TicksPerSecond = 30;

    private AlchemySession(RuntimeSession runtime, RuntimeClock clock, InputState input, AlchemyComponent alchemy, AlchemyPlayerComponent player, AlchemyContent content)
    {
        Runtime = runtime;
        Clock = clock;
        Input = input;
        Alchemy = alchemy;
        Player = player;
        Content = content;
    }

    public RuntimeSession Runtime { get; }
    public RuntimeClock Clock { get; }
    /// <summary>The input the outside world writes between Updates; the player polls it inside its Update.</summary>
    public InputState Input { get; }
    public AlchemyComponent Alchemy { get; }
    public AlchemyPlayerComponent Player { get; }
    public AlchemyContent Content { get; }

    /// <summary>A short description of the whole state. Equal fingerprints mean equal books at equal ticks.</summary>
    public string Fingerprint() => $"tick={Clock.Tick} book={Alchemy.Book.Hash()[..16]}";

    /// <summary>True when a checkpoint is consistent: no request waits, no input is pending and the player waits for no answer.</summary>
    public bool IsSettled => Alchemy.IsQuiet && Input.IsQuiet && Player.View.Awaiting == 0;

    /// <param name="resume">A verified checkpoint to continue from; null starts a new game.</param>
    /// <param name="journal">When given, a component records the state fingerprint after every tick into it.</param>
    /// <param name="recordInputWith">When given together with <paramref name="journal"/>, every write to <see cref="Input"/> is journaled with its delivery tick.</param>
    /// <param name="policy">The bot that plays through <see cref="Input"/>; null leaves the player to whoever writes the input.</param>
    /// <param name="extra">Further components of the host, updated after the player and before the journal.</param>
    public static AlchemySession Create(AlchemyContent content, ulong seed, IRuntimeReporter reporter, AlchemyCheckpointParts? resume = null,
        SessionJournal? journal = null, IReadOnlyList<IGameComponent>? extra = null, IAlchemyPolicy? policy = null, JournalCodec? recordInputWith = null)
    {
        var rules = new AlchemyRules(content);
        var book = resume?.Book ?? new DiscoveryBook(content);
        var clock = new RuntimeClock(TicksPerSecond, startTick: resume?.Tick ?? 0);
        var runtime = new RuntimeSession(clock, reporter);
        AlchemyEventCatalog.Declare(runtime);
        runtime.DeclareEvent<AlchemyPlayerDecided>("alchemy-headless.player-decided", 1);

        Action<long, InputEdgeRaised>? record = journal is not null && recordInputWith is not null
            ? (tick, edge) => recordInputWith.Record(journal, tick, edge)
            : null;
        var input = new InputState(() => clock.Tick + 1, record);
        // The environments the content can ask for, each as the whole set of tags a recipe needs together (a bot offers a set at once).
        var environmentOptions = content.Recipes.Where(r => r.Environment.Count > 0).Select(r => string.Join(',', r.Environment.Order(StringComparer.Ordinal))).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        var alchemy = new AlchemyComponent(rules, book, seed);
        var player = new AlchemyPlayerComponent(input, policy, environmentOptions);
        runtime.AddComponent(new InputStateBridge(input), 10);
        runtime.AddComponent(alchemy, 100);
        runtime.AddComponent(new AlchemyGoal(content.Elements.Count), 150);
        runtime.AddComponent(player, 250);
        for (var i = 0; i < (extra?.Count ?? 0); i++) runtime.AddComponent(extra![i], 400 + i);
        if (journal is not null) runtime.AddComponent(new JournalComponent(journal, alchemy.Fingerprint), 900);
        return new AlchemySession(runtime, clock, input, alchemy, player, content);
    }
}
