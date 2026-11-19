using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

internal sealed record MetadataGroundUsePlacementStage(string Id,string? ParentNodeId,IReadOnlyList<string> NodeIds,bool InParentFootprint=true);
public sealed record MetadataGroundUseCapacityWitness(string GroundNodeId,string GroundKind,string SourceRuleId,string StageId,
    IReadOnlyList<string> SolidNodeIds,IReadOnlyList<double> SolidSourceAreas,double GroundSourceArea,double RequiredSolidArea,
    double AreaUncertainty,double MaximumOpenFraction,double MinimumOpenFraction,string Unit,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? SourceStageIds=null);

public sealed partial class MetadataPhysicalAccountingPlanner
{
    /// <summary>Proves only pose-invariant shortfalls. A nested stage's physical
    /// members are contained in its parent and have disjoint interiors. Distinct
    /// branches inherit that separation; solid ancestors cover descendants once.
    /// An absent containment proof leaves exact accounting in force.</summary>
    internal void ValidateContainedCapacity(IReadOnlyList<MetadataGroundUsePlacementStage> stages,
        IReadOnlyDictionary<string,WorldMetadataNode> nodes,IReadOnlyDictionary<string,MetadataCatalogFootprint> definitions,
        IReadOnlyDictionary<string,MetadataResolvedFootprint> sources,IReadOnlyDictionary<string,MetadataCatalogAccess>? access,
        string unit,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(!definitions.Values.Any(f=>f.GroundUse is not null))return;
        budget.Charge(nodes.Count+stages.Sum(s=>(long)s.NodeIds.Count));
        var owners=stages.SelectMany(s=>s.NodeIds.Select(id=>(Id:id,Stage:s))).ToDictionary(p=>p.Id,p=>p.Stage,StringComparer.Ordinal);
        var grounds=new Dictionary<string,MetadataGroundUse>(StringComparer.Ordinal);
        foreach(var node in nodes.Values)
        {
            token.ThrowIfCancellationRequested();
            if(definitions.GetValueOrDefault(node.ArchetypeId)?.GroundUse is not {} ground)continue;
            ValidateGroundUse(ground);
            if(access?.GetValueOrDefault(node.ArchetypeId)?.Occupancy!=MetadataOccupancyKind.Reservation)
                throw new ArgumentException("METADATA_GROUND_USE_RESERVATION_REQUIRED:"+node.Id);
            if(!sources.ContainsKey(node.Id))throw new ArgumentException("METADATA_GROUND_USE_PHYSICAL_SOURCE_REQUIRED:"+node.Id);
            grounds.Add(node.Id,ground);
        }
        var children=stages.Where(s=>s.ParentNodeId is not null).ToDictionary(s=>s.ParentNodeId!,StringComparer.Ordinal);
        var selectedByNode=new Dictionary<string,string[]>(StringComparer.Ordinal);
        string[] SelectDisjoint(string nodeId,int depth)
        {
            budget.Charge();
            if(depth>32)throw new ArgumentException("METADATA_PLACEMENT_STAGE_DEPENDENCY_INVALID");
            if(selectedByNode.TryGetValue(nodeId,out var cached))return cached;
            // A solid source already occupies its contained descendants' ground;
            // counting both would manufacture a capacity shortfall.
            if(access?.GetValueOrDefault(nodes[nodeId].ArchetypeId)?.Occupancy==MetadataOccupancyKind.Solid)
                return selectedByNode[nodeId]=[nodeId];
            if(!children.TryGetValue(nodeId,out var childStage)||!childStage.InParentFootprint)
                return selectedByNode[nodeId]=[];
            var selected=new List<string>();
            foreach(string child in childStage.NodeIds.Order(StringComparer.Ordinal))
            {
                var branch=SelectDisjoint(child,depth+1);budget.Charge(branch.Length);selected.AddRange(branch);
            }
            return selectedByNode[nodeId]=selected.ToArray();
        }
        foreach(var (parent,ground) in grounds.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            var solidIds=SelectDisjoint(parent,0).Order(StringComparer.Ordinal).ToArray();
            if(solidIds.Length==0)continue;
            budget.Charge(solidIds.Length);
            var areas=solidIds.Select(id=>AreaBounds(sources[id].Polygon,budget)).ToArray();
            double required=areas.Sum(a=>a.Area);
            // The common boundary-contact policy tolerates <=1e-8 intersection
            // area per pair. Leave a conservative allowance even when disabled.
            double uncertainty=areas.Sum(a=>a.Error)+8e-8*solidIds.Length*solidIds.Length;
            double lower=Math.Max(0,required-uncertainty);
            var available=AreaBounds(sources[parent].Polygon,budget);
            double upper=Math.Clamp(1-lower/(available.Area+available.Error),0,1);
            if(ground.MinimumOpenFraction>upper+1e-9)
            {
                var sourceStages=solidIds.Select(id=>owners[id].Id).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                string stageId=sourceStages.Length==1?sourceStages[0]:children[parent].Id;
                var witness=new MetadataGroundUseCapacityWitness(parent,ground.Kind,ground.SourceRuleId,stageId,
                    Array.AsReadOnly(solidIds),Array.AsReadOnly(areas.Select(a=>a.Area).ToArray()),available.Area,required,
                    uncertainty+available.Error,upper,ground.MinimumOpenFraction,unit,sourceStages.Length>1?Array.AsReadOnly(sourceStages):null);
                var error=new MetadataPlacementInfeasibleException("METADATA_PLACEMENT_UNRESOLVED:GROUND_USE_CAPACITY:"+parent+":stage="+stageId+":rule="+ground.SourceRuleId);
                error.Data["PlacementPoseInvariantFailure"]=true;error.Data["PlacementWork"]=budget.Used;
                error.Data["GroundUseCapacityWitness"]=witness;throw error;
            }
        }
    }

    private static (double Area,double Error) AreaBounds(WorldPolygon polygon,WorldSpatialWorkBudget budget)
    {
        budget.Charge(8L*polygon.Vertices.Count);var origin=polygon.Vertices[0];double twice=0,perimeter=0;
        for(int i=0;i<polygon.Vertices.Count;i++)
        {
            var a=polygon.Vertices[i];var b=polygon.Vertices[(i+1)%polygon.Vertices.Count];
            twice+=(a.X-origin.X)*(b.Y-origin.Y)-(b.X-origin.X)*(a.Y-origin.Y);perimeter+=a.DistanceTo(b);
        }
        double area=Math.Abs(twice)/2;
        double dx=polygon.Vertices.Max(p=>p.X)-polygon.Vertices.Min(p=>p.X),dy=polygon.Vertices.Max(p=>p.Y)-polygon.Vertices.Min(p=>p.Y);
        // Include the maximum validated containment depth, not just one edge:
        // source descendants may sit through thirty-two nested coordinate frames.
        double coordinateError=33*(32*(Math.BitIncrement(1e7)-1e7)+4*WorldPolygon.BoundaryTolerance);
        double roundoff=32*(Math.BitIncrement(1d)-1)*polygon.Vertices.Count*(dx*dy+area+1);
        return(area,coordinateError*(perimeter+1)+roundoff);
    }
}
