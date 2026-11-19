using CombatEngine;

namespace GameRuntime.Field.Headless;

/// <summary>
/// One run of the field game: the SimEngine world, the combat component and the commander as components of one RuntimeSession. The session
/// owns the one clock; each tick it runs every component's Update once, in order: the world, the effect dispatcher, the combat rules,
/// the commander and (when recording) the journal. Whoever hosts it feeds it elapsed time.
/// </summary>
public sealed class FieldSession
{
    private FieldSession(RuntimeSession runtime, FieldWorld world, CombatComponent combat, FieldCommander commander, CombatStockBridge stock, FieldQuartermaster quartermaster)
    {
        Runtime = runtime;
        World = world;
        Combat = combat;
        Commander = commander;
        Stock = stock;
        Quartermaster = quartermaster;
    }

    public RuntimeSession Runtime { get; }
    public FieldWorld World { get; }
    public CombatComponent Combat { get; }
    public FieldCommander Commander { get; }
    public CombatStockBridge Stock { get; }
    public FieldQuartermaster Quartermaster { get; }

    public string Fingerprint() => $"{World.Game.Hash():X16} {Combat.Fingerprint()} {Commander.Fingerprint()}";

    /// <summary>True when a checkpoint loses nothing: no combat request, no director ticket, no commander request and no stock flow is open.</summary>
    public bool IsSettled => Combat.IsQuiet && World.Tickets.OpenCount == 0 && Commander.Settled && Stock.Settled && Quartermaster.Settled;

    /// <param name="combat">Combat state from a verified checkpoint to continue from; null starts the fight fresh.</param>
    /// <param name="commander">Commander state from a verified checkpoint; null starts raising the field.</param>
    public static FieldSession Create(FieldOptions options, FieldForces forces, FieldWorld world, IRuntimeReporter reporter,
        CombatCheckpointParts? combat = null, FieldCommanderState? commander = null, SessionJournal? journal = null, Func<bool>? authorised = null)
    {
        FieldAi.Check(forces.Groups);
        var clock = new RuntimeClock(world.Game.AuthoredTicksPerSecond, startTick: combat?.Tick ?? 0);
        var runtime = new RuntimeSession(clock, reporter);
        CombatEventCatalog.Declare(runtime);
        StockEventCatalog.Declare(runtime);
        runtime.DeclareEvent<FieldProgress>("field.progress", 1);
        AiEventCatalog.Declare(runtime);

        var rules = new CombatComponent(new CombatResolver(forces.Rules), forces.Groups, options.Seed);
        if (combat is not null) rules.Restore(combat.Groups, combat.Attempt, combat.Engagements, combat.DistanceMetres);
        var state = commander ?? new FieldCommanderState();
        var roster = new FieldSquadRoster(forces, state, world.Depots);
        var stock = new CombatStockBridge(forces.Groups, roster, options.StockLinesPerTick, options.DepotPercent);
        var commanderAi = FieldAi.Component("commander-ai", options.Seed, "commander", FieldCommander.AgentCount);
        var supplyAi = FieldAi.Component("supply-ai", options.Seed, "quartermaster", 1);
        var quartermaster = new FieldQuartermaster(rules, state, roster, world.Held, () => stock.Settled, supplyAi);
        var boss = new FieldCommander(forces, options, state, () => stock.Settled, commanderAi, authorised);
        runtime.AddComponent(new SimComponent(world.Game, null, world.Outbox), 1);
        runtime.AddComponent(new SimEffectDispatcher(world.Game, world.Tickets), 2);
        runtime.AddComponent(rules, 100);
        runtime.AddComponent(boss, 250);
        runtime.AddComponent(commanderAi, 255);
        runtime.AddComponent(stock, 260);
        runtime.AddComponent(quartermaster, 270);
        runtime.AddComponent(supplyAi, 275);
        var session = new FieldSession(runtime, world, rules, boss, stock, quartermaster);
        if (journal is not null) runtime.AddComponent(new JournalComponent(journal, session.Fingerprint), 900);
        return session;
    }
}
