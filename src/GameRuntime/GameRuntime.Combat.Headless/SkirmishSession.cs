using CombatEngine;

namespace GameRuntime.Combat.Headless;

/// <summary>
/// One run of the skirmish game: the combat component, the goal and the commander as components of one RuntimeSession. The session owns
/// the one clock; each tick it runs every component's Update once, in order. Whoever hosts the session (the headless main loop, a test,
/// a Godot node) feeds it elapsed time and writes <see cref="Input"/>.
/// </summary>
public sealed class SkirmishSession
{
    public const int TicksPerSecond = 30;

    private SkirmishSession(RuntimeSession runtime, RuntimeClock clock, InputState input, CombatComponent combat, SkirmishPlayerComponent player)
    { Runtime = runtime; Clock = clock; Input = input; Combat = combat; Player = player; }

    public RuntimeSession Runtime { get; }
    public RuntimeClock Clock { get; }
    public InputState Input { get; }
    public CombatComponent Combat { get; }
    public SkirmishPlayerComponent Player { get; }

    public string Fingerprint() => $"tick={Clock.Tick} {Combat.Fingerprint()}";

    /// <summary>True when a checkpoint is consistent: no request waits, no input is pending and the commander waits for no answer.</summary>
    public bool IsSettled => Combat.IsQuiet && Input.IsQuiet && Player.View.Awaiting == 0 && Player.View.Phase == SkirmishPhase.Approach;

    public static SkirmishSession Create(SkirmishForces forces, ulong seed, IRuntimeReporter reporter, CombatCheckpointParts? resume = null,
        SessionJournal? journal = null, IReadOnlyList<IGameComponent>? extra = null, SkirmishBot? bot = null, JournalCodec? recordInputWith = null)
    {
        var clock = new RuntimeClock(TicksPerSecond, startTick: resume?.Tick ?? 0);
        var runtime = new RuntimeSession(clock, reporter);
        CombatEventCatalog.Declare(runtime);
        runtime.DeclareEvent<SkirmishPlayerDecided>("combat-headless.player-decided", 1);
        AiEventCatalog.Declare(runtime);

        Action<long, InputEdgeRaised>? record = journal is not null && recordInputWith is not null
            ? (tick, edge) => recordInputWith.Record(journal, tick, edge)
            : null;
        var input = new InputState(() => clock.Tick + 1, record);
        var combat = new CombatComponent(new CombatResolver(forces.Rules), forces.Groups, seed);
        if (resume is not null) combat.Restore(resume.Groups, resume.Attempt, resume.Engagements, resume.DistanceMetres);
        var player = new SkirmishPlayerComponent(input, bot);
        runtime.AddComponent(new InputStateBridge(input), 10);
        runtime.AddComponent(combat, 100);
        runtime.AddComponent(new SkirmishGoal(), 150);
        runtime.AddComponent(player, 250);
        if (bot is not null) runtime.AddComponent(bot.Component, 255);
        for (var i = 0; i < (extra?.Count ?? 0); i++) runtime.AddComponent(extra![i], 400 + i);
        if (journal is not null) runtime.AddComponent(new JournalComponent(journal, combat.Fingerprint), 900);
        return new SkirmishSession(runtime, clock, input, combat, player);
    }
}
