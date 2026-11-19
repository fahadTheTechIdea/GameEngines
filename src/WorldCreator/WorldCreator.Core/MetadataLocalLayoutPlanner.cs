using System.Text.Json;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataLocalLayoutPlanner(WorldCreator.Core.Planning.ISettlementCatalogRegistry? catalogs=null)
{
    private readonly WorldPlacementEvaluationMemo? evaluationMemo;
    private readonly string? memoWorldId;
    internal MetadataLocalLayoutPlanner(WorldCreator.Core.Planning.ISettlementCatalogRegistry? catalogs,WorldPlacementEvaluationMemo? memo,string? worldId):this(catalogs)
    {evaluationMemo=memo;memoWorldId=worldId;}
    public WorldLocalLayoutPlan Plan(WorldLocalLayoutDefinition definition,WorldRegionPlan regions,WorldEnvironmentDefinition environment,
        WorldSurfaceDefinition surface,MetadataSpace space,CancellationToken token=default)
    {
        try{return new WorldLocalLayoutPlanner().Plan(new WorldEnvironmentQuery(new MetadataSurfaceConstraint(surface,space,token).Query,environment,token),regions,definition,token);}
        catch(WorldLayoutRegionConstraintException error){throw new MetadataSpatialConstraintException(error.Message,error);}
        catch(ArgumentException error)when(SpatialPartitionFailure(error,includeSlot:false)){throw new MetadataSpatialConstraintException(error.Message,error);}
    }

    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
        =>ValidateDocumentCore(document,token,false);

    internal MetadataPlacementReplay? ValidateWithPlacementReplay(WorldMetadataDocument document,CancellationToken token)
        =>ValidateDocumentCore(document,token,true);

    private MetadataPlacementReplay? ValidateDocumentCore(WorldMetadataDocument document,CancellationToken token,bool capturePlacement)
    {
        if(document.LayoutAdaptationPlan is not null)return ValidateAdaptation(document,token,capturePlacement);
        if(document.LocalLayoutPlan is not {} stored)return null;
        if(document.RegionPlan is null||document.EnvironmentPlan is null||document.Surface is null)throw new ArgumentException("METADATA_LOCAL_LAYOUT_DEPENDENCIES_REQUIRED");
        var expected=Plan(stored.Definition,document.RegionPlan,document.EnvironmentPlan.Definition,document.Surface,document.Space,token);
        if(JsonSerializer.Serialize(stored,WorldJson.CreateOptions())!=JsonSerializer.Serialize(expected,WorldJson.CreateOptions()))
            throw new ArgumentException("METADATA_LOCAL_LAYOUT_PLAN_MISMATCH");
        return null;
    }
}
