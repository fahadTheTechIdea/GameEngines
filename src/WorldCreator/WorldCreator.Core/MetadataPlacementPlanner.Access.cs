using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataPlacementPlanner
{
    private sealed class AccessSet
    {
        private readonly IReadOnlyDictionary<string,MetadataCatalogAccess> definitions;
        private readonly IReadOnlyDictionary<string,WorldMetadataNode> nodes;
        private readonly Dictionary<string,MetadataPlacementBinding> bindings;
        private readonly WorldSpatialWorkBudget budget;
        private readonly WorldSurfaceQuery surface;
        private readonly CancellationToken token;
        private readonly long baseEdges;
        internal readonly Dictionary<string,MetadataPhysicalSite> Sites;
        private readonly MetadataPhysicalSite[] solids,ports,interacting;
        internal AccessSet(IReadOnlyList<Stage> stages,IReadOnlyDictionary<string,WorldSitePlacement> known,IReadOnlyDictionary<string,WorldMetadataNode> nodes,
            IReadOnlyDictionary<string,MetadataCatalogAccess> definitions,WorldSurfaceQuery surface,WorldSpatialWorkBudget budget,CancellationToken token)
        {
            this.definitions=definitions;this.nodes=nodes;this.surface=surface;this.budget=budget;this.token=token;
            budget.Charge(stages.Sum(s=>s.Bindings.Count)+4L*known.Count);
            bindings=stages.SelectMany(s=>s.Bindings).ToDictionary(b=>b.NodeId,StringComparer.Ordinal);
            var placed=known.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>Positioned(p.Key,p.Value)).ToArray();
            Sites=placed.ToDictionary(s=>s.NodeId,StringComparer.Ordinal);
            solids=placed.Where(s=>s.Occupancy==MetadataOccupancyKind.Solid).ToArray();ports=placed.Where(s=>s.Ports.Count>0).ToArray();
            interacting=placed.Where(s=>s.Occupancy==MetadataOccupancyKind.Solid||s.Ports.Count>0).ToArray();
            baseEdges=(surface.Definition.Boundary?.Vertices.Count??0)+surface.Exclusions.Sum(e=>e.Boundary.Vertices.Count);
        }
        internal MetadataPhysicalSite Positioned(string id,WorldSitePlacement site)
        {
            token.ThrowIfCancellationRequested();
            if(!definitions.TryGetValue(nodes[id].ArchetypeId,out var source))throw new ArgumentException("METADATA_ACCESS_CLASSIFICATION_REQUIRED:"+id);
            budget.Charge((long)site.Footprint.Footprint.Vertices.Count*site.Footprint.Footprint.Vertices.Count*(source.Ports.Count+1));
            return MetadataPhysicalAccessPlanner.PositionSite(new(id,bindings[id].RotationDegrees,site.Position,site.Footprint.Footprint,"placement:candidate"),source);
        }
        internal string? Check(MetadataPhysicalSite current)
        {
            token.ThrowIfCancellationRequested();string id=current.NodeId;
            foreach(var port in current.Ports)
            {
                budget.Charge(4*(baseEdges+1)*(baseEdges+1));
                if(!surface.ContainsCorridor(new([port.BoundaryPosition,port.ApproachPosition]),port.Width/2,token))
                    return "METADATA_ACCESS_APPROACH_BLOCKED:"+id+":"+port.Id;
            }
            var others=current.Ports.Count==0?(current.Occupancy==MetadataOccupancyKind.Solid?ports:[]):
                current.Occupancy==MetadataOccupancyKind.Solid?interacting:solids;
            foreach(var other in others)
            {
                if(other.NodeId==id)continue;
                budget.Charge();
                if(current.Occupancy==MetadataOccupancyKind.Solid)foreach(var port in other.Ports)
                    if(WorldSurfaceQuery.IntersectsExcludedPolygon(current.Footprint,port.BoundaryPosition,port.ApproachPosition,port.Width/2,budget,token))
                        return "METADATA_ACCESS_APPROACH_BLOCKED:"+other.NodeId+":"+port.Id;
                if(other.Occupancy==MetadataOccupancyKind.Solid)foreach(var port in current.Ports)
                    if(WorldSurfaceQuery.IntersectsExcludedPolygon(other.Footprint,port.BoundaryPosition,port.ApproachPosition,port.Width/2,budget,token))
                        return "METADATA_ACCESS_APPROACH_BLOCKED:"+id+":"+port.Id;
            }
            return null;
        }
    }
    private static Func<WorldSitePlacement,string?>? AccessCandidates(Stage stage,IReadOnlyList<Stage> stages,
        IReadOnlyDictionary<string,WorldSitePlacement> known,IReadOnlyDictionary<string,WorldMetadataNode> nodes,
        MetadataPlacementFeedback? feedback,WorldSurfaceQuery surface,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(feedback?.AccessDefinitions is not {Count:>0} definitions)return null;
        var set=new AccessSet(stages,known,nodes,definitions,surface,budget,token);
        var bindings=stage.Bindings.ToDictionary(b=>b.DemandId,StringComparer.Ordinal);
        return candidate=>set.Check(set.Positioned(bindings[candidate.DemandId].NodeId,candidate));
    }
    private static string? AssignmentAccess(Stage stage,IReadOnlyList<Stage> stages,IReadOnlyList<WorldSitePlacement> candidates,
        IReadOnlyDictionary<string,WorldSitePlacement> all,IReadOnlyDictionary<string,WorldMetadataNode> nodes,
        MetadataPlacementFeedback? feedback,WorldSurfaceQuery surface,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(feedback?.AccessDefinitions is not {Count:>0} definitions)return null;
        var set=new AccessSet(stages,all,nodes,definitions,surface,budget,token);
        var bindings=stage.Bindings.ToDictionary(b=>b.DemandId,StringComparer.Ordinal);
        foreach(var candidate in candidates)
            if(set.Check(set.Sites[bindings[candidate.DemandId].NodeId]) is {} reason)return reason;
        return null;
    }
}
