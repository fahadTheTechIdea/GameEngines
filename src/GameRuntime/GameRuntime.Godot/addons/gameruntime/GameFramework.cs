using Godot;

namespace GameRuntime.GodotHost;

/// <summary>
/// The singleton game layer in Godot: an Autoload. Its clock is Godot's physics loop; it adds no second timer, loop or bus. The tick is the
/// count of physics callbacks (never a sum of delta); the physics loop paces, pauses and scales it. It runs before every
/// component node, which then read the same tick.
/// </summary>
public partial class GameFramework : Node
{
    [Signal] public delegate void EventRaisedEventHandler(string kind, string payloadJson);

    public GodotEventPort Port { get; } = new();
    public long Tick { get; private set; }
    public bool Faulted { get; private set; }
    public bool Armed { get; private set; }

    /// <summary>Starts counting ticks from the given tick once every component is in the tree. Ticks before this belong to nobody.</summary>
    public void Arm(long startTick = 0) { Tick = startTick; Armed = true; }

    public override void _Ready() => ProcessPhysicsPriority = -1000;

    public override void _PhysicsProcess(double delta) { if (Armed && !Faulted) Tick++; }

    public UpdateContext Context() =>
        new(Tick, 1_000_000L / Engine.PhysicsTicksPerSecond, Engine.GetPhysicsInterpolationFraction(),
            Tick * 1_000_000L / Engine.PhysicsTicksPerSecond);

    internal void Flush() => Port.Drain((kind, json) => EmitSignal(SignalName.EventRaised, kind, json));

    /// <summary>Fail-stop: report once through Godot's logging and stop every component node.</summary>
    internal void Fault(string component, Exception exception)
    {
        if (Faulted) return;
        Faulted = true;
        GD.PushError($"GameRuntime fault in '{component}' at tick {Tick}: {exception}");
        EmitSignal(SignalName.EventRaised, nameof(SessionFaulted), $"{{\"component\":\"{component}\",\"tick\":{Tick}}}");
    }
}
