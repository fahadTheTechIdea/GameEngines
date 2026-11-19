using System.Text.Json;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataRegionPlanner
{
    public WorldRegionPlan Plan(WorldRegionRequest request,WorldEnvironmentDefinition environment,
        WorldSurfaceDefinition surface,MetadataSpace space,CancellationToken token=default)
        =>Plan(request,environment,surface,space,null,token);
    internal WorldRegionPlan Plan(WorldRegionRequest request,WorldEnvironmentDefinition environment,
        WorldSurfaceDefinition surface,MetadataSpace space,WorldSpatialWorkBudget? parent,CancellationToken token)
    {
        var query=new MetadataSurfaceConstraint(surface,space,token).Query;
        return new WorldRegionPlanner().Plan(new WorldEnvironmentQuery(query,environment,token),request,parent,token);
    }
    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        var demand=ValidateDemand(document,token);
        if(document.RegionPlan is not {} stored)return;
        if(stored.Request is null||document.EnvironmentPlan is null||document.Surface is null)throw new ArgumentException("METADATA_REGION_DEPENDENCIES_REQUIRED");
        if(stored.Request.ExcludedAssignments is {Count:>0}&&document.ProgramCompositionPlan is null&&!MetadataPopulationSources.IsInheritedRegion(document))throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_PLAN_REQUIRED");
        var request=demand is null?stored.Request:demand.DerivedRequest with{ExcludedAssignments=stored.Request.ExcludedAssignments,RequiredAssignment=stored.Request.RequiredAssignment};
        var expected=Plan(request,document.EnvironmentPlan.Definition,document.Surface,document.Space,token);
        if(JsonSerializer.Serialize(stored,WorldJson.CreateOptions())!=JsonSerializer.Serialize(expected,WorldJson.CreateOptions()))
            throw new ArgumentException("METADATA_REGION_PLAN_MISMATCH");
    }
}
