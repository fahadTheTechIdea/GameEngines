using System.Globalization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataPlacementPlanner
{
    private static bool HasPartBindings(MetadataPlacementDefinition d)=>d.Programs is not null||d.SourceWorld is not null||d.Bindings.Any(b=>b?.PartId is not null||b?.Population is not null)||
        d.NestedStages?.Any(s=>s?.Bindings?.Any(b=>b?.PartId is not null||b?.Population is not null)==true)==true;

    private static MetadataPlacementDefinition ExpandPartBindings(MetadataPlacementDefinition definition,IReadOnlyList<WorldMetadataNode> nodes,WorldSpatialWorkBudget budget,CancellationToken token,List<MetadataPopulationContourTrace> contourTraces,List<MetadataPopulationAreaTrace> areaTraces,List<MetadataPopulationBoundaryTrace> boundaryTraces,List<MetadataPopulationOrientationTrace> orientationTraces,List<MetadataPopulationDomainTrace>? domainTraces=null,bool measureOnly=false)
    {
        var children=nodes.Where(n=>n.ParentId is not null).GroupBy(n=>n.ParentId!).ToDictionary(g=>g.Key,g=>g.OrderBy(n=>n.Id,StringComparer.Ordinal).ToArray());
        HashSet<string>? orientationSourceIds=null;
        (WorldPlacementRequest Request,IReadOnlyList<MetadataPlacementBinding> Bindings) Expand(WorldPlacementRequest request,IReadOnlyList<MetadataPlacementBinding> bindings,string stageId,string? parent=null)
        {
            // A declared layout provisionally admits expansion. ConnectedStages
            // resolves it to actual graph links before final stage validation;
            // the spatial solver validates the complete growth graph.
            bool connected=request?.Growth is not null||definition.ConnectedLayouts?.Any(l=>l?.StageId==stageId)==true;
            var limits=request is null?default:WorldSitePlacementPlanner.Limits(request.Execution,connected);
            if(request?.Demands is null||request.Relations is null||bindings is null||bindings.Count<1||bindings.Count>limits.Demands||request.Demands.Count!=bindings.Count||
                request.Demands.Any(d=>d is null)||bindings.Any(b=>b is null)||request.Relations.Any(r=>r is null)||request.Relations.Count>limits.Relations)
                throw new ArgumentException("METADATA_PLACEMENT_PART_BINDING_INVALID");
            ArgumentException CountFailure(int expanded,int? selected=null)=>new("METADATA_PLACEMENT_PART_BINDING_COUNT:expanded="+
                expanded.ToString(CultureInfo.InvariantCulture)+":maximum="+limits.Demands.ToString(CultureInfo.InvariantCulture)+
                ":execution="+request.Execution+(selected is {} count?":selected="+count.ToString(CultureInfo.InvariantCulture):""));
            var demands=new List<WorldSiteDemand>();var concrete=new List<MetadataPlacementBinding>();var mapped=new Dictionary<string,string[]>(StringComparer.Ordinal);
            if(request.Anchors is null)throw new ArgumentException("METADATA_PLACEMENT_PART_BINDING_INVALID");
            var anchors=request.Anchors.ToList();var patternRelations=new List<WorldPlacementRelation>();
            void Population(MetadataPlacementBinding binding,IReadOnlyList<string> demandIds,IReadOnlyList<string> nodeIds)
            {
                if(binding.Population is not {} grammar)return;
                if(measureOnly)
                {
                    ValidatePopulationGrammar(grammar);
                    if(grammar.Facing is not null)throw new ArgumentException("METADATA_REGION_DEMAND_POPULATION_FACING_UNRESOLVED");
                    return;
                }
                var positions=PopulationPositions(grammar,nodeIds,budget,token,definition.SourceWorld,parent,contours=>
                    contourTraces.Add(new(stageId,binding.DemandId,grammar.Region!.RegionId,grammar.Region.AllocationId,grammar.Region.Contour!.FieldId,
                        grammar.Purpose,grammar.SourceRuleId,Array.AsReadOnly(nodeIds.ToArray()),contours)),
                    area=>areaTraces.Add(new(stageId,binding.DemandId,grammar.Region!.RegionId,grammar.Region.AllocationId,
                        grammar.Purpose,grammar.SourceRuleId,Array.AsReadOnly(nodeIds.ToArray()),area)),
                    boundary=>boundaryTraces.Add(new(stageId,binding.DemandId,grammar.Purpose,grammar.SourceRuleId,Array.AsReadOnly(nodeIds.ToArray()),boundary)),
                    (poses,inside)=>
                    {
                        budget.Charge(nodeIds.Count);
                        if(orientationSourceIds is null)
                        {
                            budget.Charge(definition.SourceWorld?.Nodes.Count??0);
                            orientationSourceIds=(definition.SourceWorld?.Nodes??[]).Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
                        }
                        if(!double.IsFinite(binding.RotationDegrees)||Math.Abs(binding.RotationDegrees)>360)throw new ArgumentException("METADATA_PLACEMENT_ROTATION_INVALID");
                        double offset=grammar.Facing switch{
                            MetadataPopulationFacing.Along=>0,MetadataPopulationFacing.Against=>180,MetadataPopulationFacing.Left=>90,
                            MetadataPopulationFacing.Right=>-90,MetadataPopulationFacing.Inside=>inside??throw new ArgumentException("METADATA_POPULATION_FACING_INVALID"),
                            MetadataPopulationFacing.Outside=>(inside??throw new ArgumentException("METADATA_POPULATION_FACING_INVALID"))+180,
                            _=>throw new ArgumentException("METADATA_POPULATION_FACING_INVALID")};
                        var rotations=poses.Select(p=>Normalize(p.TangentDegrees+offset+binding.RotationDegrees)).ToArray();
                        for(int i=0;i<nodeIds.Count;i++)
                        {
                            token.ThrowIfCancellationRequested();
                            if(orientationSourceIds.Contains(nodeIds[i]))throw new ArgumentException("METADATA_POPULATION_SOURCE_FACING_CONFLICT");
                            int index=concrete.Count-nodeIds.Count+i;
                            if(concrete[index].NodeId!=nodeIds[i]||concrete[index].DemandId!=demandIds[i])throw new ArgumentException("METADATA_PLACEMENT_PART_BINDING_INVALID");
                            concrete[index]=concrete[index] with{RotationDegrees=rotations[i]};
                        }
                        orientationTraces.Add(new(stageId,binding.DemandId,grammar.Purpose,grammar.SourceRuleId,Array.AsReadOnly(nodeIds.ToArray()),
                            grammar.Facing!.Value,binding.RotationDegrees,Array.AsReadOnly(poses.Select(p=>p.TangentDegrees).ToArray()),inside,Array.AsReadOnly(rotations)));
                    },domain=>domainTraces?.Add(new(stageId,binding.DemandId,grammar.Purpose,grammar.SourceRuleId,Array.AsReadOnly(nodeIds.ToArray()),domain)));
                for(int i=0;i<demandIds.Count;i++)
                {
                    string id="population-"+demandIds[i];
                    if(anchors.Any(a=>a.Id==id)||request.Demands.Any(d=>d.Id==id))throw new ArgumentException("METADATA_POPULATION_ID_COLLISION");
                    anchors.Add(new(id,positions[i]));
                    patternRelations.Add(new(id,demandIds[i],id,grammar.Purpose,grammar.SourceRuleId,0,0,0,1));
                }
            }
            foreach(var binding in bindings)
            {
                token.ThrowIfCancellationRequested();
                var source=request.Demands.Where(d=>d.Id==binding.DemandId).ToArray();
                if(source.Length!=1||mapped.ContainsKey(binding.DemandId))throw new ArgumentException("METADATA_PLACEMENT_DEMAND_MISMATCH");
                if(binding.Population?.Region is {} region)
                {
                    if(source[0].RegionId is {} oldRegion&&oldRegion!=region.RegionId||source[0].AllocationId is {} oldAllocation&&oldAllocation!=region.AllocationId)
                        throw new ArgumentException("METADATA_POPULATION_REGION_SCOPE_CONFLICT");
                    source[0]=source[0] with{RegionId=region.RegionId,AllocationId=region.AllocationId};
                }
                if(binding.Population?.GeneratedBoundary is {Side:WorldBoundarySide.Inside,Source:MetadataBoundarySource.Region or MetadataBoundarySource.Allocation} boundary)
                {
                    if(source[0].RegionId is {} oldRegion&&oldRegion!=boundary.RegionId||source[0].AllocationId is {} oldAllocation&&oldAllocation!=boundary.AllocationId)
                        throw new ArgumentException("METADATA_POPULATION_BOUNDARY_SCOPE_CONFLICT");
                    source[0]=source[0] with{RegionId=boundary.RegionId,AllocationId=boundary.AllocationId};
                }
                if(binding.PartId is null){demands.Add(source[0]);concrete.Add(binding);mapped.Add(binding.DemandId,[binding.DemandId]);Population(binding,[binding.DemandId],[binding.NodeId]);continue;}
                if(!WorldEnvironmentQuery.Id(binding.PartId)||binding.NodeId is null)throw new ArgumentException("METADATA_PLACEMENT_PART_BINDING_INVALID");
                var members=children.GetValueOrDefault(binding.NodeId,[]).Where(n=>MetadataCapacityPlanner.IsPartChild(n,binding.NodeId,binding.PartId))
                    .OrderBy(n=>int.Parse(n.Id[(n.Id.LastIndexOf('-')+1)..],CultureInfo.InvariantCulture)).ToArray();
                if(members.Length==0||concrete.Count+members.Length>limits.Demands)throw CountFailure(concrete.Count+members.Length,members.Length);
                var ids=new List<string>();
                for(int i=0;i<members.Length;i++)
                {
                    string id=binding.DemandId+"-"+i.ToString(CultureInfo.InvariantCulture);ids.Add(id);
                    demands.Add(source[0] with{Id=id});concrete.Add(binding with{DemandId=id,NodeId=members[i].Id,PartId=null});
                }
                mapped.Add(binding.DemandId,ids.ToArray());
                Population(binding,ids,members.Select(n=>n.Id).ToArray());
            }
            if(concrete.Count>limits.Demands)throw CountFailure(concrete.Count);
            if(!measureOnly&&definition.SourceWorld is {} sourceWorld)
            foreach(var binding in concrete)
            {
                var original=sourceWorld.Nodes.SingleOrDefault(n=>n.Id==binding.NodeId);
                if(original is null)continue;
                if(original.Position is not {Count:2} position)throw new ArgumentException("METADATA_POPULATION_BASE_POSITION_REQUIRED");
                var origin=parent is null?new double[]{0,0}:sourceWorld.Nodes.SingleOrDefault(n=>n.Id==parent)?.Position
                    ??throw new ArgumentException("METADATA_POPULATION_SOURCE_PARENT_MISSING");
                string id="source-"+binding.DemandId;
                if(anchors.Any(a=>a.Id==id)||request.Demands.Any(d=>d.Id==id))throw new ArgumentException("METADATA_POPULATION_ID_COLLISION");
                anchors.Add(new(id,new(position[0]-origin[0],position[1]-origin[1])));
                patternRelations.Add(new(id,binding.DemandId,id,"Preserve source-world member","population:source-world",0,0,0,1));
            }
            var relations=new List<WorldPlacementRelation>();
            foreach(var relation in request.Relations)
            {
                var from=mapped.GetValueOrDefault(relation.FromId,[relation.FromId]);var to=mapped.GetValueOrDefault(relation.ToId,[relation.ToId]);
                bool expanded=from.Length!=1||to.Length!=1||from[0]!=relation.FromId||to[0]!=relation.ToId;
                for(int i=0;i<from.Length;i++)for(int j=0;j<to.Length;j++)
                {
                    if(relations.Count>=limits.Relations)throw new ArgumentException("METADATA_PLACEMENT_PART_RELATION_BUDGET");
                    relations.Add(relation with{Id=expanded?relation.Id+"-"+i.ToString(CultureInfo.InvariantCulture)+"-"+j.ToString(CultureInfo.InvariantCulture):relation.Id,FromId=from[i],ToId=to[j]});
                }
            }
            if(relations.Count+patternRelations.Count>limits.Relations)throw new ArgumentException("METADATA_PLACEMENT_PART_RELATION_BUDGET");
            relations.AddRange(patternRelations);
            return(request with{Demands=demands.AsReadOnly(),Relations=relations.AsReadOnly(),Anchors=anchors.AsReadOnly()},concrete.AsReadOnly());
        }
        var root=Expand(definition.Request,definition.Bindings,definition.Request.Id);var nested=new List<MetadataNestedPlacement>();
        foreach(var stage in definition.NestedStages??[])
        {
            token.ThrowIfCancellationRequested();if(stage is null)throw new ArgumentException("METADATA_PLACEMENT_STAGE_INVALID");
            var result=Expand(stage.Request,stage.Bindings,stage.Id,stage.ParentNodeId);nested.Add(stage with{Request=result.Request,Bindings=result.Bindings});
        }
        return definition with{Request=root.Request,Bindings=root.Bindings,NestedStages=definition.NestedStages is null?null:nested.AsReadOnly()};
    }
    private static double Normalize(double degrees){double result=(degrees+180)%360;if(result<0)result+=360;return result-180;}
}
