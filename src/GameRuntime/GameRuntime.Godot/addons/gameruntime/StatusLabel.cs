using Godot;

namespace GameRuntime.GodotHost;

/// <summary>A UI node that shows the latest runtime event. It only connects to the Autoload's signal; it holds no game rule.</summary>
[GlobalClass]
public partial class StatusLabel : Label
{
    public override void _Ready()
    {
        var framework = GetNode<GameFramework>("/root/GameFramework");
        framework.EventRaised += (kind, json) => Text = kind + " " + json;
    }
}
