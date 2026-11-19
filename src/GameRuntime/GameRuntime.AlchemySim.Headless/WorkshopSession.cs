using AlchemyEngine;
using Beep.SimEngine.Hosting;

namespace GameRuntime.AlchemySim.Headless;

/// <summary>
/// One run of the workshop: the sim, the alchemy rules, the unlock and order components, the goal and the player as components of one
/// RuntimeSession. The session owns the one clock; each tick it runs every component's Update once, in order. Whoever hosts the session
/// (the headless main loop, a test, a Godot node) feeds it elapsed time and writes <see cref="Input"/>.
/// </summary>
public sealed class WorkshopSession
{
    public const string GoalResource = "space.finished";

    private WorkshopSession(RuntimeSession runtime, RuntimeClock clock, SimGame game, SimRecipeView view, DirectorTickets tickets, InputState input,
        AlchemyComponent alchemy, RecipeUnlockComponent unlock, SimOrderComponent orders, WorkshopPlayerComponent player)
    {
        Runtime = runtime;
        Clock = clock;
        Game = game;
        View = view;
        Tickets = tickets;
        Input = input;
        Alchemy = alchemy;
        Unlock = unlock;
        Orders = orders;
        Player = player;
    }

    public RuntimeSession Runtime { get; }
    public RuntimeClock Clock { get; }
    public SimGame Game { get; }
    /// <summary>The alchemy view of the sim's own recipes, read when the world was composed.</summary>
    public SimRecipeView View { get; }
    public DirectorTickets Tickets { get; }
    /// <summary>The input the outside world writes between Updates; the player polls it inside its Update.</summary>
    public InputState Input { get; }
    public AlchemyComponent Alchemy { get; }
    public RecipeUnlockComponent Unlock { get; }
    public SimOrderComponent Orders { get; }
    public WorkshopPlayerComponent Player { get; }

    /// <summary>A short description of the whole state: tick, sim hash and book hash. Equal fingerprints mean equal worlds and books.</summary>
    public string Fingerprint() => $"tick={Clock.Tick} sim={Game.Hash():X16} book={Alchemy.Book.Hash()[..16]}";

    /// <summary>True when nothing is in flight between components, so a checkpoint holds everything.</summary>
    public bool IsSettled => Alchemy.IsQuiet && Unlock.IsQuiet && Orders.IsQuiet && Tickets.OpenCount == 0 && Input.IsQuiet && Player.View.Awaiting == 0;

    /// <summary>Every recipe the alchemy book and the sim's knowledge disagree about; empty means they agree.</summary>
    public IReadOnlyList<RecipeDrift> Drift() => AlchemySimConsistency.Check(View, Game, Alchemy.Book);

    /// <param name="resume">A verified checkpoint to continue from; null starts a new game.</param>
    /// <param name="journal">When given, a component records the state fingerprint after every tick into it.</param>
    /// <param name="recordInputWith">When given together with <paramref name="journal"/>, every write to <see cref="Input"/> is journaled with its delivery tick.</param>
    /// <param name="policy">The bot that plays through <see cref="Input"/>; null leaves the player to whoever writes the input.</param>
    /// <param name="extra">Further components of the host, updated after the dispatcher and before the journal.</param>
    public static WorkshopSession Create(ulong seed, IRuntimeReporter reporter, WorkshopCheckpointParts? resume = null, SessionJournal? journal = null,
        IReadOnlyList<IGameComponent>? extra = null, AlchemistPolicy? policy = null, JournalCodec? recordInputWith = null, string? contentDirectory = null)
    {
        var holder = new SimRecipeViewHolder();
        var tickets = new DirectorTickets();
        var outbox = new SimEventOutbox();
        var factory = WorkshopWorld.Factory(WorkshopWorld.Definition(holder, tickets, outbox), contentDirectory);
        var game = resume is null ? WorkshopWorld.NewGame(factory, seed) : factory.Load(resume.Sim);
        var view = holder.View;
        var book = resume is null ? new DiscoveryBook(view.Alchemy) : AlchemyCheckpoint.Open(view.Alchemy, resume.Alchemy).Book;

        var clock = new RuntimeClock(game.AuthoredTicksPerSecond, startTick: resume?.Tick ?? 0);
        var runtime = new RuntimeSession(clock, reporter);
        AlchemyEventCatalog.Declare(runtime);
        AlchemySimEventCatalog.Declare(runtime);
        runtime.DeclareEvent<WorkshopPlayerDecided>("alchemy-workshop.player-decided", 1);

        Action<long, InputEdgeRaised>? record = journal is not null && recordInputWith is not null
            ? (tick, edge) => recordInputWith.Record(journal, tick, edge)
            : null;
        var input = new InputState(() => clock.Tick + 1, record);

        var alchemy = new AlchemyComponent(new AlchemyRules(view.Alchemy), book, seed);
        var unlock = new RecipeUnlockComponent(view);
        var orders = new SimOrderComponent(game);
        var dispatcher = new SimEffectDispatcher(game, tickets);
        var player = new WorkshopPlayerComponent(input, policy, view, resume?.Ordered ?? []);
        runtime.AddComponent(new InputStateBridge(input), 10);
        runtime.AddComponent(new SimComponent(game, null, outbox), 100);
        runtime.AddComponent(alchemy, 120);
        runtime.AddComponent(unlock, 130);
        runtime.AddComponent(orders, 140);
        runtime.AddComponent(new WorkshopGoal(GoalResource), 150);
        runtime.AddComponent(player, 250);
        runtime.AddComponent(dispatcher, 300);
        for (var i = 0; i < (extra?.Count ?? 0); i++) runtime.AddComponent(extra![i], 400 + i);
        var session = new WorkshopSession(runtime, clock, game, view, tickets, input, alchemy, unlock, orders, player);
        if (journal is not null) runtime.AddComponent(new JournalComponent(journal, session.Fingerprint), 900);
        return session;
    }
}
