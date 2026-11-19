using Beep.SimEngine.Kernel;

namespace GameRuntime.Headless;

/// <summary>A module that adds authored content and dependencies but no code of its own.</summary>
public sealed class ContentOnlyModule(string id, params ModuleRequirement[] requires) : GameModule
{
    public override string Id => id;
    public override ModuleVersion Version => new(1, 0, 0);
    public override IReadOnlyList<ModuleRequirement> Requires => requires;
}
