using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataGroundCoverPlanner
{
    private sealed record Shape(MetadataGroundSourceKind Kind,string Selector,string Id,string SourceRuleId,string? NodeId,string? ParentNodeId,WorldPolygon Boundary);
    private static IReadOnlyList<Shape> SourceShapes(WorldMetadataDocument world,WorldSurfaceQuery surface,IReadOnlyList<MetadataGroundRule> rules,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var selected=rules.Select(r=>(r.Source,r.Selector)).ToHashSet();var kinds=rules.Select(r=>r.Source).ToHashSet();
        budget.Charge(world.Nodes.Count+rules.Count);
        var result=new List<Shape>();var nodes=world.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        void Add(MetadataGroundSourceKind kind,string selector,string id,string sourceRule,WorldPolygon polygon,string? nodeId=null)
        {
            if(kind!=MetadataGroundSourceKind.Terrain&&!selected.Contains((kind,selector)))return;
            budget.Charge(1+(polygon?.Vertices.Count??0));
            if(result.Count>=16384||polygon?.Vertices is null)throw new ArgumentException("METADATA_GROUND_GEOMETRY_BUDGET");
            result.Add(new(kind,selector,id,sourceRule,nodeId,nodeId is null?null:nodes[nodeId].ParentId,polygon));
        }
        if(surface.HeightField is {} field)
        {
            var f=field.Geometry;
            Add(MetadataGroundSourceKind.Terrain,f.Kind,f.Id,f.SourceRuleId,new([f.Origin,new(f.MaxX,f.Origin.Y),new(f.MaxX,f.MaxY),new(f.Origin.X,f.MaxY)]));
        }
        foreach(var p in surface.Definition.Patches)Add(MetadataGroundSourceKind.Terrain,p.Kind,p.Id,p.SourceRuleId,p.Boundary);
        if(world.PlacementPlan is {} placement)
        {
            foreach(var allocation in kinds.Contains(MetadataGroundSourceKind.Footprint)||kinds.Contains(MetadataGroundSourceKind.Reservation)?MetadataPhysicalAccessPlanner.Allocations(placement):[])
            {
                token.ThrowIfCancellationRequested();var node=nodes[allocation.NodeId];
                Add(MetadataGroundSourceKind.Footprint,node.ArchetypeId,node.Id,allocation.SourceRuleId,allocation.Footprint,node.Id);
                if(allocation.Reservation is {} reservation)Add(MetadataGroundSourceKind.Reservation,node.ArchetypeId,node.Id,allocation.SourceRuleId,reservation,node.Id);
            }
            foreach(var feature in kinds.Contains(MetadataGroundSourceKind.SiteFeature)?placement.SiteFeatures??[]:[])
                Add(MetadataGroundSourceKind.SiteFeature,feature.Kind.ToString(),feature.Id,feature.SourceRuleId,feature.Boundary,feature.NodeId);
        }
        foreach(var region in kinds.Contains(MetadataGroundSourceKind.Region)?world.RegionPlan?.Regions??[]:[])
        foreach(var cell in region.Cells)
            Add(MetadataGroundSourceKind.Region,region.Id,region.Id+"/"+MetadataExecutionCache.Hash(cell),region.Seed.SourceRuleId,cell);
        foreach(var allocation in kinds.Contains(MetadataGroundSourceKind.Allocation)?world.LocalLayoutPlan?.Allocations??[]:[])
        foreach(var cell in allocation.Shape.Cells)
            Add(MetadataGroundSourceKind.Allocation,allocation.Id,allocation.Id+"/"+MetadataExecutionCache.Hash(cell),allocation.Program.SourceRuleId,cell);
        foreach(var corridor in kinds.Contains(MetadataGroundSourceKind.Corridor)?world.CorridorPlan?.Groups??[]:[])
        {
            foreach(var segment in corridor.Segments)
            {
                if(!double.IsFinite(segment.Width)||segment.Width<=0)throw new ArgumentException("METADATA_GROUND_CORRIDOR_WIDTH_REQUIRED");
                Add(MetadataGroundSourceKind.Corridor,corridor.Definition.Id,segment.Id,corridor.Definition.SourceRuleId,
                    WorldSurfaceTraversal.Envelope(segment.From,segment.To,segment.Width/2));
            }
            foreach(var junction in corridor.Junctions)
                Add(MetadataGroundSourceKind.Corridor,corridor.Definition.Id,junction.Id,corridor.Definition.SourceRuleId,junction.Envelope);
        }
        return result;
    }
}
