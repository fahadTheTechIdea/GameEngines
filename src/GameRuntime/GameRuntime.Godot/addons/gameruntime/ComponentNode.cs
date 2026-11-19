using Godot;

namespace GameRuntime.GodotHost;

/// <summary>A Node that hosts one game component: _Ready runs Init, _PhysicsProcess runs Update, in scene-tree order.</summary>
[GlobalClass]
public partial class ComponentNode : Node
{
    private GameFramework? _framework;

    public IGameComponent? Component { get; set; }

    public override void _Ready()
    {
        _framework = GetNode<GameFramework>("/root/GameFramework");
        RunGuarded(() => Component!.Init(new ComponentContext(_framework.Port)));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_framework is null || !_framework.Armed || _framework.Faulted || Component is null) return;
        RunGuarded(() =>
        {
            Component.Update(_framework.Context());
            _framework.Flush();
        });
    }

    private void RunGuarded(Action action)
    {
        try { action(); }
        catch (Exception exception) // Reported through Godot's logging by the framework, which then stops every component.
        {
            _framework!.Fault(Component?.Id ?? Name, exception);
        }
    }
}
