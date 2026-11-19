using System.Collections.Concurrent;
using Beep.SimEngine;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>One kind of change the mission may request of the world. One class per effect, as with every variant.</summary>
public interface IDirectorEffect
{
    /// <summary>The name after the "sim." prefix of an <see cref="EffectRequested"/> kind.</summary>
    string Name { get; }
    Code Check(World world, string payloadJson);
    void Apply(World world, string payloadJson);
}

/// <summary>One-shot permissions for director commands: a command with no matching ticket is refused, so a client cannot forge one.</summary>
public sealed class DirectorTickets
{
    private readonly ConcurrentDictionary<string, (string Effect, string Payload)> _open = new(StringComparer.Ordinal);

    public void Issue(string key, string effect, string payloadJson)
    {
        if (!_open.TryAdd(key, (effect, payloadJson))) throw new InvalidOperationException("director ticket '" + key + "' is already issued");
    }

    /// <summary>Tickets issued and not yet consumed by the sim; a checkpoint is only consistent while this is zero.</summary>
    public int OpenCount => _open.Count;

    internal bool Matches(string key, string effect, string payloadJson) =>
        _open.TryGetValue(key, out var ticket) && ticket.Effect == effect && ticket.Payload == payloadJson;

    internal void Consume(string key) => _open.TryRemove(key, out _);
}

public sealed class DirectorEffectCommand : Command
{
    public string Key = "";
    public string Effect = "";
    public string Payload = "";

    public override void Sync(IStateSync s)
    {
        s.String(ref Key);
        s.String(ref Effect);
        s.String(ref Payload);
    }
}

/// <summary>
/// SimEngine module that lets the mission act on the world. It adds one command; the command only runs with a ticket
/// issued by the host-side dispatcher. Compiled against SimEngine's public contracts only.
/// </summary>
public sealed class DirectorBridgeModule : GameModule
{
    public const string ModuleId = "gf.director";
    private readonly DirectorTickets _tickets;
    private readonly SimEventOutbox? _outbox;
    private readonly Dictionary<string, IDirectorEffect> _effects = new(StringComparer.Ordinal);

    public DirectorBridgeModule(DirectorTickets tickets, IEnumerable<IDirectorEffect> effects, SimEventOutbox? outbox = null)
    {
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _outbox = outbox;
        foreach (var effect in effects)
            if (!_effects.TryAdd(effect.Name, effect)) throw new InvalidOperationException("director effect '" + effect.Name + "' is registered twice");
    }

    public override string Id => ModuleId;
    public override ModuleVersion Version => new(1, 0, 0);
    public override void Compose(WorldComposer composer)
    {
        composer.Commands.Register("gf.director-effect", new Handler(_tickets, _effects));
        if (_outbox is null) return;
        // Forward the engine's declared in-world events, only for the providers this game actually selected.
        var selected = composer.Content.Modules;
        Forward<StructureBuilt>(composer, selected, "sim.construction", "sim.structure-built", 1, "built");
        Forward<StructureRemoved>(composer, selected, "sim.construction", "sim.structure-removed", 1, "removed");
        Forward<EntityTransformed>(composer, selected, "sim.transformations", "sim.entity-transformed", 1, "transformed");
        Forward<EntityReplaced>(composer, selected, "sim.work", "sim.entity-replaced", 1, "replaced");
        if (selected.Contains("sim.objectives"))
        {
            composer.Events.Require<ObjectiveStateChanged>(ObjectiveStateChanged.EventId, 1);
            composer.Events.Subscribe<ObjectiveStateChanged>("gf.director.forward.objective-changed",
                e => _outbox!.Add("sim.event.objective-changed", System.Text.Json.JsonSerializer.Serialize(new { company = e.Company.Value, objective = e.Objective, state = e.State.ToString() })));
        }
        if (selected.Contains("sim.technology"))
        {
            composer.Events.Require<TechnologyLearned>(TechnologyLearned.EventId, 1);
            composer.Events.Subscribe<TechnologyLearned>("gf.director.forward.technology-learned",
                e => _outbox!.Add("sim.event.technology-learned", System.Text.Json.JsonSerializer.Serialize(new { company = e.Company.Value, technology = e.Technology })));
        }
        if (selected.Contains("sim.inventory"))
        {
            composer.Events.Require<ResourceFlowFact>(ResourceFlowFact.EventId, 7);
            composer.Events.Subscribe<ResourceFlowFact>("gf.director.forward.stock-changed",
                e => _outbox!.Add("sim.event.stock-changed", System.Text.Json.JsonSerializer.Serialize(new
                { tick = e.Tick, company = e.Company.Value, resource = composer.Content.ResourceName(e.Resource), amount = e.Amount.Raw, kind = e.Kind.ToString(), isTransfer = e.IsTransfer })));
        }
        if (selected.Contains(CharacterModule.ModuleId))
        {
            composer.Events.Require<FormationSpawned>(FormationSpawned.EventId, 1);
            composer.Events.Subscribe<FormationSpawned>("gf.director.forward.formation-spawned",
                e => _outbox!.Add("sim.event.formation-spawned", System.Text.Json.JsonSerializer.Serialize(new { company = e.Company.Value, formation = e.Formation, squads = e.Squads, members = e.Members, first = e.First.Value })));
            composer.Events.Require<CharactersSpawned>(CharactersSpawned.EventId, 1);
            composer.Events.Subscribe<CharactersSpawned>("gf.director.forward.characters-spawned",
                e => _outbox!.Add("sim.event.characters-spawned", System.Text.Json.JsonSerializer.Serialize(new { company = e.Company.Value, archetype = e.Archetype, count = e.Count, first = e.First.Value })));
            composer.Events.Require<CharacterMaterialized>(CharacterMaterialized.EventId, 1);
            composer.Events.Subscribe<CharacterMaterialized>("gf.director.forward.character-materialized",
                e => _outbox!.Add("sim.event.character-materialized", System.Text.Json.JsonSerializer.Serialize(new { squad = e.Squad.Value, slot = e.Slot, character = e.Character.Value })));
            composer.Events.Require<SquadCasualties>(SquadCasualties.EventId, 1);
            composer.Events.Subscribe<SquadCasualties>("gf.director.forward.squad-casualties",
                e => _outbox!.Add("sim.event.squad-casualties", System.Text.Json.JsonSerializer.Serialize(new { squad = e.Squad.Value, lost = e.Lost, remaining = e.Remaining })));
            composer.Events.Require<SquadReinforced>(SquadReinforced.EventId, 1);
            composer.Events.Subscribe<SquadReinforced>("gf.director.forward.squad-reinforced",
                e => _outbox!.Add("sim.event.squad-reinforced", System.Text.Json.JsonSerializer.Serialize(new { squad = e.Squad.Value, added = e.Added, remaining = e.Remaining })));
        }
        if (selected.Contains("sim.tycoon"))
        {
            composer.Events.Require<BusinessCashChanged>(BusinessCashChanged.EventId, 1);
            composer.Events.Subscribe<BusinessCashChanged>("gf.director.forward.cash-changed",
                e => _outbox!.Add("sim.event.cash-changed", "{\"company\":" + e.Company.Value + ",\"before\":" + e.Before.Raw + ",\"after\":" + e.After.Raw + "}"));
        }
    }

    private void Forward<T>(WorldComposer composer, IReadOnlyList<string> selected, string provider, string eventId, int schema, string name) where T : struct
    {
        if (!selected.Contains(provider)) return;
        composer.Events.Require<T>(eventId, schema);
        composer.Events.Subscribe<T>("gf.director.forward." + name,
            e => _outbox!.Add("sim.event." + name, System.Text.Json.JsonSerializer.Serialize(e, new System.Text.Json.JsonSerializerOptions { IncludeFields = true })));
    }


    public static readonly Code NoTicket = new("gf.director.no-ticket");
    public static readonly Code UnknownEffect = new("gf.director.unknown-effect");

    private sealed class Handler(DirectorTickets tickets, IReadOnlyDictionary<string, IDirectorEffect> effects) : CommandHandler<DirectorEffectCommand>
    {
        protected override Code Check(World world, DirectorEffectCommand command)
        {
            if (!tickets.Matches(command.Key, command.Effect, command.Payload)) return NoTicket;
            if (!effects.TryGetValue(command.Effect, out var effect)) return UnknownEffect;
            return effect.Check(world, command.Payload);
        }

        protected override void Apply(World world, DirectorEffectCommand command, CommandContext context)
        {
            tickets.Consume(command.Key);
            effects[command.Effect].Apply(world, command.Payload);
        }
    }
}
