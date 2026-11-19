using System.Collections.Immutable;
using Counter.GameLayer;
using Godot;
using GameRuntime.GodotHost;
using ScenarioEngine.Content;
using ScenarioEngine.Contracts;
using ScenarioEngine.Core;
using ScenarioEngine.Diagnostics;

namespace GameRuntime.GodotHost;

/// <summary>
/// Demo scene: the Counter mission driven entirely by Godot's physics loop. A source component posts one observation
/// per tick; the mission component turns observations into mission events; Godot signals carry every event out.
/// </summary>
public partial class Main : Node
{
    private int _exit;
    private ApplicationDiagnostics? _diagnostics;

    public override void _ExitTree() => _diagnostics?.Dispose();

    private const string IncrementAction = "mission_increment";
    private int _inputs;

    /// <summary>An InputMap action becomes a mission command event; the mission decides what it means.</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed(IncrementAction)) return;
        GetNode<GameFramework>("/root/GameFramework").Port.Emit(new MissionCommandRequested(
            "input-" + ++_inputs, "player", "test_counter.increment", "{\"amount\":1}"));
        GetViewport().SetInputAsHandled();
    }

    public override async void _Ready()
    {
        var framework = GetNode<GameFramework>("/root/GameFramework");
        var profile = GD.Load<GameProfile>("res://default_profile.tres");
        var log = new GodotLogger();
        var audit = ProjectSettings.GlobalizePath("user://audit.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(audit)!);
        _diagnostics = new ApplicationDiagnostics(log, audit, log);
        var diagnostics = _diagnostics;
        var registry = new GameLayerRegistry([new CounterLayer()]);
        var validator = new JsonSchemaContractValidator();
        var catalog = new ScenarioContentService(registry, validator, diagnostics);
        var load = await catalog.LoadAsync(new JsonFilePackSource(ProjectSettings.GlobalizePath(profile.MissionPack)), CancellationToken.None);
        if (!load.IsReady) { GD.PushError("pack did not load"); _exit = 2; GetTree().Quit(2); return; }
        var mission = new ScenarioSessionFactory(registry, validator, diagnostics)
            .Create(load.Scenarios[0], new ScenarioStartOptions("godot-demo", (ulong)profile.Seed, ImmutableHashSet.Create("player")), new WorldFrame(0, 0, []));

        framework.Port.Connect<MissionEvent>(e => GD.Print($"t{e.Tick} {e.Kind} {e.PayloadJson}"));
        framework.Port.Connect<SessionEnded>(e =>
        {
            GD.Print($"status label shows: {GetNode<Label>("Status").Text}");
            GD.Print($"t{e.Tick} ended {e.EndingId}; physics rate {Engine.PhysicsTicksPerSecond}; framework tick {framework.Tick}");
            GetTree().Quit(0);
        });
        framework.EventRaised += (kind, json) => GD.Print($"signal EventRaised {kind}");

        // Scene-tree order is update order: the source first, then the mission.
        var (game, tickets) = SimDemo.Create(profile);
        var probe = new SimProbe("test_counter.adjustment", g => g.Tick, (before, now) => "{\"delta\":" + (now - before) + "}");
        Engine.PhysicsTicksPerSecond = game.AuthoredTicksPerSecond;
        framework.Port.Connect<CommandResolved>(e => GD.Print($"command {e.CommandId} {e.Engine} applied={e.Applied} {e.Code}"));
        framework.Port.Connect<ObservationPosted>(e => { if (e.Kind == "sim.notification") GD.Print($"t{e.Tick} sim.notification {e.PayloadJson}"); });
        AddChild(new ComponentNode { Name = "Sim", Component = new SimComponent(game, [probe], new SimEventOutbox()) });
        AddChild(new ComponentNode { Name = "Mission", Component = new MissionComponent(mission, new HashSet<string> { "test_counter.adjustment" }) });
        AddChild(new ComponentNode { Name = "EffectSource", Component = new EffectSource(atTick: 2) });
        AddChild(new ComponentNode { Name = "Effects", Component = new SimEffectDispatcher(game, tickets) });
        if (!InputMap.HasAction(IncrementAction)) InputMap.AddAction(IncrementAction);
        framework.Port.Connect<CommandResolved>(e => { if (e.CommandId.StartsWith("input-")) GD.Print($"input command {e.CommandId} applied={e.Applied} {e.Code}"); });
        if (OS.GetEnvironment("GAMERUNTIME_PRESS_AT_START") == "1")
            Input.ParseInputEvent(new InputEventAction { Action = IncrementAction, Pressed = true });
        framework.Arm();
    }
}

