using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataPlacementPlanner
{
    private static Dictionary<string,MetadataResolvedFootprint> ResolveStageSources(IReadOnlyList<Stage> stages,
        IReadOnlyDictionary<string,WorldMetadataNode> nodes,string unit,IReadOnlyDictionary<string,MetadataCatalogFootprint> footprints,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures,IReadOnlyDictionary<string,MetadataCatalogAccess>? access,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var result=new Dictionary<string,MetadataResolvedFootprint>(StringComparer.Ordinal);
        foreach(var stage in stages)
        {
            budget.Charge(stage.Bindings.Count);var bindings=stage.Bindings.ToDictionary(b=>b.DemandId,StringComparer.Ordinal);
            foreach(var demand in stage.Request.Demands)
            {
                token.ThrowIfCancellationRequested();var binding=bindings[demand.Id];var node=nodes[binding.NodeId];
                result.Add(node.Id,ResolveChargedSource(node,demand,binding,unit,footprints,structures,access,budget));
            }
        }
        foreach(var stage in stages.Where(s=>s.Parent is not null))
        {
            var parent=stage.ParentBoundary==MetadataParentBoundary.Reservation?
                result[stage.Parent!].Reservation??throw new ArgumentException("METADATA_PLACEMENT_PARENT_RESERVATION_REQUIRED:"+stage.Id):result[stage.Parent!].Polygon;
            var boundary=stage.Request.Boundary is {} declared?MetadataFootprintResolver.Polygon(declared):parent;
            if(stage.Request.Boundary is not null)
            {
                budget.Charge((long)boundary.Vertices.Count*parent.Vertices.Count*4);
                if(!parent.ContainsPolygon(boundary))throw new ArgumentException("METADATA_PLACEMENT_STAGE_REGION_OUTSIDE:"+stage.Id);
            }
            foreach(var binding in stage.Bindings)
            {
                CheckNestedEnvelope(stage,boundary,result[binding.NodeId],budget);
            }
        }
        return result;
    }
    private static MetadataResolvedFootprint ResolveChargedSource(WorldMetadataNode node,WorldSiteDemand demand,MetadataPlacementBinding binding,string unit,
        IReadOnlyDictionary<string,MetadataCatalogFootprint> footprints,IReadOnlyDictionary<string,MetadataStructureDefinition> structures,
        IReadOnlyDictionary<string,MetadataCatalogAccess>? access,WorldSpatialWorkBudget budget)
    {
        int vertices=demand.Footprint?.Vertices?.Count??(footprints.TryGetValue(node.ArchetypeId,out var shape)?shape.Polygon?.Vertices?.Count??64:4);
        budget.Charge(8L*vertices*vertices);
        var source=MetadataFootprintResolver.Resolve(node,demand,binding,unit,footprints,structures,access,budget);
        if(source.Reservation is {} reservation)budget.Charge(8L*reservation.Vertices.Count*reservation.Vertices.Count);
        budget.Charge((source.SiteFeatures?.Count??0)*128L);return source;
    }
    private static void CheckNestedEnvelope(Stage stage,WorldPolygon boundary,MetadataResolvedFootprint source,WorldSpatialWorkBudget budget)
    {
                var envelope=source.Reservation??source.Polygon;
                foreach(var axis in Axes(boundary,envelope))
                {
                    budget.Charge(boundary.Vertices.Count+envelope.Vertices.Count+8);
                    double Width(WorldPolygon polygon)
                    {
                        var values=polygon.Vertices.Select(p=>p.X*axis.X+p.Y*axis.Y).ToArray();return values.Max()-values.Min();
                    }
                    double required=Width(envelope),available=Width(boundary);
                    // Conservative at the validated +/-1e7 coordinate ceiling,
                    // including translation/projection roundoff and containment tolerance.
                    double direction=Math.Abs(axis.X)+Math.Abs(axis.Y);
                    double uncertainty=16*(Math.BitIncrement(1e7)-1e7)*(direction+1)+2*WorldPolygon.BoundaryTolerance*direction;
                    if(required<=available+uncertainty)continue;
                    var error=new MetadataPlacementInfeasibleException("METADATA_PLACEMENT_UNRESOLVED:NESTED_SOURCE_ENVELOPE_TOO_LARGE:"+
                        source.NodeId+":parent="+stage.Parent+":rule="+source.SourceRuleId);
                    error.Data["PlacementWork"]=budget.Used;error.Data["PlacementPoseInvariantFailure"]=true;
                    error.Data["NestedSourceEnvelope"]=new{stage.Id,ParentNodeId=stage.Parent,source.NodeId,source.SourceRuleId,Axis=axis,Required=required,Available=available,Uncertainty=uncertainty};
                    throw error;
                }
    }

    // Necessary source-only proof, shared with normal placement. It never
    // certifies packing or skips the later complete physical qualification.
    internal void ValidateProgramSourceEnvelopes(MetadataPlacementDefinition definition,MetadataProgramAnalysis analysis,string unit,
        IReadOnlyDictionary<string,MetadataCatalogAccess> access,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(definition.ConnectedLayouts is not null||definition.Programs?.Any(p=>p.Growth is not null)==true)return;
        definition=InstantiatePrograms(definition,analysis.Nodes,budget,token).Definition;
        var nodes=analysis.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        var stages=new[]{new Stage(definition.Request.Id,null,definition.Request,definition.Bindings)}.Concat(
            (definition.NestedStages??[]).Select(s=>new Stage(s.Id,s.ParentNodeId,s.Request,s.Bindings,s.ParentBoundary))).ToArray();
        var poses=PoseControls(definition.PoseControls,budget,token);
        bool Matches(MetadataPlacementBinding binding,WorldMetadataNode node)=>binding.PartId is null?binding.NodeId==node.Id:
            node.ParentId==binding.NodeId&&MetadataCapacityPlanner.IsPartChild(node,binding.NodeId,binding.PartId);
        MetadataResolvedFootprint Resolve(WorldMetadataNode node,WorldSiteDemand demand,MetadataPlacementBinding binding)
        {
            binding=binding with{NodeId=node.Id,PartId=null};
            if(poses?.TryGetValue(node.Id,out var pose)==true)binding=PoseBinding(binding,pose,definition.SourceWorld,budget);
            return ResolveChargedSource(node,demand,binding,unit,analysis.FootprintDefinitions,analysis.StructureDefinitions,access,budget);
        }
        foreach(var stage in stages.Where(s=>s.Parent is not null))
        {
            token.ThrowIfCancellationRequested();budget.Charge(stages.Sum(s=>(long)s.Bindings.Count)+analysis.Nodes.Count);
            var parentNode=nodes[stage.Parent!];var parents=stages.SelectMany(s=>s.Bindings.Select(b=>(Stage:s,Binding:b))).Where(p=>Matches(p.Binding,parentNode)).ToArray();
            if(parents.Length!=1||!parents[0].Binding.UseCatalogFootprint||parents[0].Binding.Population?.Facing is not null)continue;
            var parentBinding=parents[0];var parent=Resolve(parentNode,parentBinding.Stage.Request.Demands.Single(d=>d.Id==parentBinding.Binding.DemandId),parentBinding.Binding);
            var boundary=stage.ParentBoundary==MetadataParentBoundary.Reservation?parent.Reservation??
                throw new ArgumentException("METADATA_PLACEMENT_PARENT_RESERVATION_REQUIRED:"+stage.Id):parent.Polygon;
            if(stage.Request.Boundary is {} declared)
            {
                var polygon=MetadataFootprintResolver.Polygon(declared);budget.Charge((long)polygon.Vertices.Count*boundary.Vertices.Count*4);
                if(!boundary.ContainsPolygon(polygon))throw new ArgumentException("METADATA_PLACEMENT_STAGE_REGION_OUTSIDE:"+stage.Id);boundary=polygon;
            }
            foreach(var binding in stage.Bindings.Where(b=>b.UseCatalogFootprint&&b.Population?.Facing is null))foreach(var node in analysis.Nodes.Where(n=>Matches(binding,n)))
            {
                var demand=stage.Request.Demands.Single(d=>d.Id==binding.DemandId);var source=Resolve(node,demand,binding);
                try{CheckNestedEnvelope(stage,boundary,source,budget);}
                catch(MetadataPlacementInfeasibleException error){error.Data["PlacementSourceOnlyProof"]=true;throw;}
            }
        }
        if(!analysis.FootprintDefinitions.Values.Any(f=>f.GroundUse is not null))return;
        // Unplaced analysis may contain dynamic facings or non-catalog demands.
        // Keep only resolved fixed sources; removing an unknown branch weakens
        // the lower bound rather than inventing its dimensions or containment.
        var fixedSources=new Dictionary<string,MetadataResolvedFootprint>(StringComparer.Ordinal);
        var groundStages=new List<MetadataGroundUsePlacementStage>();var parentsUsed=new HashSet<string>(StringComparer.Ordinal);
        foreach(var stage in stages)
        {
            token.ThrowIfCancellationRequested();budget.Charge(stage.Bindings.Count+1);
            if(stage.Parent is {} parent&&!parentsUsed.Add(parent))return;
            var members=new List<string>();
            foreach(var binding in stage.Bindings.Where(b=>b.UseCatalogFootprint&&b.Population?.Facing is null))
            {
                budget.Charge(analysis.Nodes.Count);
                var demand=stage.Request.Demands.Single(d=>d.Id==binding.DemandId);
                foreach(var node in analysis.Nodes.Where(n=>Matches(binding,n)))
                {
                    if(fixedSources.ContainsKey(node.Id))return;
                    fixedSources.Add(node.Id,Resolve(node,demand,binding));members.Add(node.Id);
                }
            }
            groundStages.Add(new(stage.Id,stage.Parent,members.ToArray(),stage.ParentBoundary!=MetadataParentBoundary.Reservation));
        }
        budget.Charge(analysis.Nodes.Count);
        var fixedNodes=analysis.Nodes.Where(n=>fixedSources.ContainsKey(n.Id)).ToDictionary(n=>n.Id,StringComparer.Ordinal);
        try{new MetadataPhysicalAccountingPlanner().ValidateContainedCapacity(groundStages,fixedNodes,analysis.FootprintDefinitions,fixedSources,access,unit,budget,token);}
        catch(MetadataPlacementInfeasibleException error){error.Data["PlacementSourceOnlyProof"]=true;throw;}
    }

    private static IEnumerable<WorldPoint> Axes(WorldPolygon parent,WorldPolygon child)
    {
        yield return new(1,0);yield return new(0,1);
        foreach(var polygon in new[]{parent,child})for(int i=0;i<polygon.Vertices.Count;i++)
        {
            var a=polygon.Vertices[i];var b=polygon.Vertices[(i+1)%polygon.Vertices.Count];double dx=b.X-a.X,dy=b.Y-a.Y,length=Math.Sqrt(dx*dx+dy*dy);
            if(length>0)yield return new(-dy/length,dx/length);
        }
    }
}
