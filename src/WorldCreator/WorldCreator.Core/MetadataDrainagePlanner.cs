using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Optional terrain-derived facts; no implied water footprint or climate.</summary>
public sealed class MetadataDrainagePlanner
{
    public WorldDrainagePlan Plan(WorldDrainageDefinition definition,WorldSurfaceDefinition surface,MetadataSpace space,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        // Use the same bounded two-coordinate/unit owner as all metadata terrain.
        var constraint=new MetadataSurfaceConstraint(surface,space,token);
        return new WorldDrainagePlanner().Plan(constraint.Query.Definition,definition,token);
    }

    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(document);
        if(document.WaterPlan is not null){new MetadataWaterPlanner().ValidateDocument(document,token);return;}
        if(document.DrainagePlan is not {} stored)return;
        if(document.Surface is null)throw new ArgumentException("METADATA_DRAINAGE_SURFACE_REQUIRED");
        var constraint=new MetadataSurfaceConstraint(document.Surface,document.Space,token);
        if(!MetadataPopulationSources.Equal(stored.Surface,constraint.Query.Definition))throw new ArgumentException("METADATA_DRAINAGE_SURFACE_MISMATCH");
        new WorldDrainagePlanner().Validate(stored,token);
    }
}
