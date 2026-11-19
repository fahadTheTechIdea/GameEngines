using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime.AlchemySim.Headless;

/// <summary>The workshop's authored world as a module: it adds content and dependencies but no code of its own.</summary>
public sealed class WorkshopModule : GameModule
{
    public const string ModuleId = "workshop";

    public override string Id => ModuleId;
    public override ModuleVersion Version => new(1, 0, 0);
    public override IReadOnlyList<ModuleRequirement> Requires =>
    [
        new ModuleRequirement(ContentAccessModule.ModuleId, Version),
        new ModuleRequirement(WorldSetupModule.ModuleId, Version),
        new ModuleRequirement(TransformationModule.ModuleId, Version),
        new ModuleRequirement(LogisticsModule.ModuleId, Version),
    ];
}
