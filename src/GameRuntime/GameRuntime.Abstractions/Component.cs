using System;

namespace GameRuntime;

/// <summary>The clock reading shared by every component for one tick. Components never own a counter of their own.</summary>
public readonly struct UpdateContext
{
    public UpdateContext(long tick, long tickMicroseconds, double alpha, long totalMicroseconds)
    {
        Tick = tick;
        TickMicroseconds = tickMicroseconds;
        Alpha = alpha;
        TotalMicroseconds = totalMicroseconds;
    }
    /// <summary>The one time coordinate. The first tick is 1.</summary>
    public long Tick { get; }
    /// <summary>Real duration of one tick at scale 1, in microseconds (informational).</summary>
    public long TickMicroseconds { get; }
    /// <summary>Fraction of the next tick, for host-side interpolation only.</summary>
    public double Alpha { get; }
    /// <summary>Game time elapsed through this tick, exactly Tick / TicksPerSecond seconds (the equivalent of XNA's GameTime.TotalGameTime). Tick itself is the count of elapsed ticks.</summary>
    public long TotalMicroseconds { get; }
}

/// <summary>What a host hands a component at Init.</summary>
public readonly struct ComponentContext
{
    public ComponentContext(IEventPort events) { Events = events; }
    public IEventPort Events { get; }
}

/// <summary>The only way components talk to each other and to the host.</summary>
public interface IEventPort
{
    /// <summary>Queued; delivered after the emitting Update (or handler) returns, in publication order.</summary>
    void Emit<T>(in T evt) where T : struct;
    /// <summary>Connect during Init only; handlers run in connection order and change only their own component.</summary>
    void Connect<T>(Action<T> handler) where T : struct;
}

/// <summary>The Node of this layer: Init like _ready, Update like _physics_process.</summary>
public interface IGameComponent
{
    string Id { get; }
    void Init(in ComponentContext context);
    void Update(in UpdateContext context);
}
