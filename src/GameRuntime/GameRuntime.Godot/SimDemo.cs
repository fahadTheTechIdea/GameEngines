using Beep.SimEngine.Hosting;
using Godot;

namespace GameRuntime.GodotHost;

/// <summary>Builds the quickstart SimEngine game with the director bridge, from content copied beside the project.</summary>
internal static class SimDemo
{
    public static (SimGame Game, DirectorTickets Tickets) Create(GameProfile profile)
    {
        var tickets = new DirectorTickets();
        var definition = new Beep.SimEngine.Kernel.GameDefinition("quick-game", new Beep.SimEngine.Kernel.GameModule[] {
            new Beep.SimEngine.Standard.ContentAccessModule(), new Beep.SimEngine.Standard.WorldSetupModule(), new QuickGame.Engine.QuickModule(),
            new DirectorBridgeModule(tickets, new IDirectorEffect[] { new NotifyEffect() }, new SimEventOutbox()) });
        var files = new DiskFileSource(ProjectSettings.GlobalizePath(profile.SimContent));
        var factory = new GameFactory(definition, ContentSet.Load(files, definition.ContentTypes));
        return (factory.NewGame(new NewGameRequest { Scenario = profile.SimScenario, Seed = (ulong)profile.Seed }), tickets);
    }
}

internal sealed class NotifyEffect : IDirectorEffect
{
    public string Name => "notify";
    public Beep.SimEngine.Code Check(Beep.SimEngine.Kernel.World world, string payloadJson) => Beep.SimEngine.Code.None;
    public void Apply(Beep.SimEngine.Kernel.World world, string payloadJson) =>
        world.Notify(new Beep.SimEngine.Code("gf.director.notice"), Beep.SimEngine.PlayerId.None, subject: payloadJson);
}

/// <summary>Stands in for a mission request so the mission-to-world path shows with the Counter pack.</summary>
internal sealed class EffectSource(long atTick) : IGameComponent
{
    private IEventPort? _events;
    public string Id => "effect-source";
    public void Init(in ComponentContext context) { _events = context.Events; }
    public void Update(in UpdateContext context)
    {
        if (context.Tick == atTick) _events!.Emit(new EffectRequested("r1", "sim.notify", "\"hello from the mission\"", context.Tick));
    }
}
