using Godot;

namespace GameRuntime.GodotHost;

/// <summary>The session's configuration as a Godot Resource, edited in the inspector.</summary>
[GlobalClass]
public partial class GameProfile : Resource
{
    [Export] public string MissionPack { get; set; } = "";
    [Export] public string SimContent { get; set; } = "";
    [Export] public string SimScenario { get; set; } = "";
    [Export] public int Seed { get; set; }
}
