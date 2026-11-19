using System.Collections.Immutable;
using Beep.SimEngine.Hosting;
using ScenarioEngine.Contracts;

namespace GameRuntime.Headless;

/// <summary>
/// One run of the tycoon game: the sim, the mission, the player and the effect dispatcher as components of one RuntimeSession.
/// The session owns the one clock (<see cref="Clock"/>); each tick it runs every component's Update once, in order. Whoever
/// hosts the session (the headless main loop, a test, a Godot node) feeds it elapsed time and writes <see cref="Input"/>.
/// </summary>
public sealed class HeadlessSession
{
    public const string Controller = "player";

    private HeadlessSession(RuntimeSession runtime, RuntimeClock clock, SimGame game, IScenarioSimulation mission, DirectorTickets tickets, InputState input)
    {
        Runtime = runtime;
        Clock = clock;
        Game = game;
        Mission = mission;
        Tickets = tickets;
        Input = input;
    }

    public RuntimeSession Runtime { get; }
    public RuntimeClock Clock { get; }
    public SimGame Game { get; }
    public IScenarioSimulation Mission { get; }
    public DirectorTickets Tickets { get; }
    /// <summary>The input the outside world writes between Updates; the player polls it inside its Update.</summary>
    public InputState Input { get; }

    /// <summary>A short description of the whole state: tick, sim hash and mission hash. Equal fingerprints mean equal worlds.</summary>
    public string Fingerprint() =>
        $"tick={Game.Tick} sim={Game.Hash():X16} mission={ScenarioEngine.Core.ScenarioEngine.StateHash(Mission.Snapshot)[..16]}";

    /// <param name="resume">A verified checkpoint to continue from; null starts a new game.</param>
    /// <param name="journal">When given, a component records the state hash after every tick into it.</param>
    /// <param name="recordInputWith">When given together with <paramref name="journal"/>, every write to <see cref="Input"/> is journaled with its delivery tick.</param>
    /// <param name="bot">The bot that plays through <see cref="Input"/>; null leaves the player to whoever writes the input.</param>
    /// <param name="extra">Further components of the host, updated after the dispatcher and before the journal.</param>
    public static HeadlessSession Create(HeadlessEnvironment environment, ulong seed, CheckpointParts? resume = null,
        SessionJournal? journal = null, IReadOnlyList<IGameComponent>? extra = null, TycoonBot? bot = null, JournalCodec? recordInputWith = null)
    {
        var tickets = new DirectorTickets();
        var outbox = new SimEventOutbox();
        var factory = TycoonWorld.Factory(TycoonWorld.Definition(tickets, outbox, [new CreditEffect()]));
        var game = resume is null
            ? factory.NewGame(new NewGameRequest { Scenario = TycoonWorld.Scenario, Seed = seed })
            : factory.Load(resume.Sim);
        var mission = resume is null
            ? environment.Sessions.Create(environment.Scenario, new ScenarioStartOptions("headless", seed, ImmutableHashSet.Create(Controller)), new WorldFrame(0, 0, []))
            : environment.Sessions.Restore(environment.Scenario, resume.Mission, new WorldFrame(resume.Mission.WorldGeneration, resume.Tick, []));

        var clock = new RuntimeClock(game.AuthoredTicksPerSecond, startTick: resume?.Tick ?? 0);
        var runtime = new RuntimeSession(clock, new EngineReporterAdapter(environment.Reporter));
        runtime.DeclareEvent<PlayerDecided>("headless.player-decided", 1);
        runtime.DeclareEvent<ClockRequested>("headless.clock-requested", 1);
        AiEventCatalog.Declare(runtime);

        Action<long, InputEdgeRaised>? record = journal is not null && recordInputWith is not null
            ? (tick, edge) => recordInputWith.Record(journal, tick, edge)
            : null;
        var input = new InputState(() => clock.Tick + 1, record);

        runtime.AddComponent(new InputStateBridge(input), 10);
        runtime.AddComponent(new SimComponent(game, null, outbox), 100);
        runtime.AddComponent(new TycoonObservations(), 150);
        runtime.AddComponent(new MissionComponent(mission, new HashSet<string> { TycoonMissionLayer.CashChanged, TycoonMissionLayer.ObjectiveChanged }), 200);
        runtime.AddComponent(new PlayerComponent(input, bot, environment.GoalProfit), 250);
        if (bot is not null) runtime.AddComponent(bot.Component, 255);
        runtime.AddComponent(new SimEffectDispatcher(game, tickets), 300);
        for (var i = 0; i < (extra?.Count ?? 0); i++) runtime.AddComponent(extra![i], 400 + i);
        var session = new HeadlessSession(runtime, clock, game, mission, tickets, input);
        if (journal is not null) runtime.AddComponent(new JournalComponent(journal, session.Fingerprint), 900);
        return session;
    }
}
