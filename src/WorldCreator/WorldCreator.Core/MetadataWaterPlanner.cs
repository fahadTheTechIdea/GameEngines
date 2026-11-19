using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Binds original drainage and occupied wet terrain to one metadata frame.</summary>
public sealed class MetadataWaterPlanner
{
    public WorldWaterPlan Plan(WorldDrainagePlan drainage,WorldWaterDefinition definition,MetadataSpace space,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(drainage);
        _=new MetadataSurfaceConstraint(drainage.Surface,space,token);
        return new WorldWaterPlanner().Plan(drainage,definition,token);
    }
    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(document);
        if(document.WaterPlan is not {} water)return;
        if(document.DrainagePlan is not {} drainage||document.Surface is null)throw new ArgumentException("METADATA_WATER_DEPENDENCIES_REQUIRED");
        _=new MetadataSurfaceConstraint(drainage.Surface,document.Space,token);
        var effective=new WorldWaterPlanner().Apply(drainage,water,token);
        var actual=new MetadataSurfaceConstraint(document.Surface,document.Space,token).Query.Definition;
        if(!MetadataPopulationSources.Equal(effective,actual))throw new ArgumentException("METADATA_WATER_SURFACE_MISMATCH");
    }
}
