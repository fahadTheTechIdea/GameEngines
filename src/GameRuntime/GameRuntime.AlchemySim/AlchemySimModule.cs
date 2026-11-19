using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>
/// SimEngine module that reads the world's own recipes into the alchemy view as the world is composed. It adds no commands and no
/// content of its own; it needs transformations and the technology content that gates them.
/// </summary>
public sealed class AlchemySimModule(SimRecipeViewHolder holder) : GameModule
{
    public const string ModuleId = "gf.alchemy-sim";

    public override string Id => ModuleId;
    public override ModuleVersion Version => new(1, 0, 0);
    public override IReadOnlyList<ModuleRequirement> Requires =>
    [
        new ModuleRequirement(TransformationModule.ModuleId, Version),
        new ModuleRequirement(TechnologyModule.ModuleId, Version),
    ];

    public override void Compose(WorldComposer composer) => holder.Capture(SimRecipeSource.Read(composer.Content));
}
