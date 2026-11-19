using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataPlacementPlanner
{
    // Expansion and footprint resolution use the same owners as actual placement.
    // Nested sources are contained by root programs and must not be counted twice.
    internal IReadOnlyList<(string? RegionId,MetadataResolvedFootprint Source)> RegionSources(MetadataPlacementDefinition definition,
        IReadOnlyList<WorldMetadataNode> nodes,string unit,IReadOnlyDictionary<string,MetadataCatalogFootprint> footprints,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures,IReadOnlyDictionary<string,MetadataCatalogAccess>? access,
        WorldSpatialWorkBudget budget,CancellationToken token,bool unplaced=false)
    {
        if(definition?.Request is null||definition.Bindings is null)throw new ArgumentException("METADATA_REGION_DEMAND_PLACEMENT_REQUIRED");
        budget.Charge(nodes.Count+definition.Bindings.Count);
        // Only the selected root stage contributes a regional capacity envelope.
        var expanded=ExpandPartBindings(definition with{NestedStages=null,Programs=null},nodes,budget,token,[],[],[],[],measureOnly:unplaced);
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);var bindings=expanded.Bindings.ToDictionary(b=>b.DemandId,StringComparer.Ordinal);
        var poses=PoseControls(definition.PoseControls,budget,token);
        var result=new List<(string?,MetadataResolvedFootprint)>();var used=new HashSet<string>(StringComparer.Ordinal);
        foreach(var demand in expanded.Request.Demands.OrderBy(d=>d.Id,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if(!bindings.TryGetValue(demand.Id,out var binding)||!lookup.TryGetValue(binding.NodeId,out var node)||!used.Add(node.Id))
                throw new ArgumentException("METADATA_REGION_DEMAND_SOURCE_BINDING_INVALID");
            if(poses?.TryGetValue(node.Id,out var pose)==true)binding=PoseBinding(binding,pose,definition.SourceWorld,budget);
            int vertices=demand.Footprint?.Vertices.Count??footprints.GetValueOrDefault(node.ArchetypeId)?.Polygon.Vertices.Count??64;
            budget.Charge(8L*vertices*vertices);
            var source=MetadataFootprintResolver.Resolve(node,demand,binding,unit,footprints,structures,access,budget);
            if(source.Reservation is {} reservation)budget.Charge(8L*reservation.Vertices.Count*reservation.Vertices.Count);
            budget.Charge((source.SiteFeatures?.Count??0)*128L);result.Add((demand.RegionId,source));
        }
        return result.AsReadOnly();
    }
}
