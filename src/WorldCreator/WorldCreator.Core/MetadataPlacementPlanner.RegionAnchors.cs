using WorldCreator.Core.Spatial;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataPlacementPlanner
{
    private static MetadataPlacementDefinition ResolveRegionAnchors(MetadataPlacementDefinition definition,WorldRegionPlan? regions,WorldLocalLayoutPlan? layouts,
        IReadOnlyList<WorldMetadataNode> nodes,string unit,IReadOnlyDictionary<string,MetadataCatalogFootprint>? footprints,
        IReadOnlyDictionary<string,MetadataStructureDefinition>? structures,MetadataPlacementFeedback? feedback,WorldPolygon? boundary,
        WorldSpatialWorkBudget budget,CancellationToken token,out IReadOnlyList<MetadataAllocationAnchorResult>? anchorResults,
        out IReadOnlyList<MetadataAllocationFootprintResult>? footprintAnchorResults)
    {
        anchorResults=null;footprintAnchorResults=null;var results=new List<MetadataAllocationAnchorResult>();var footprintResults=new List<MetadataAllocationFootprintResult>();
        if(definition.RegionAnchors is null&&definition.AllocationAnchors is null)return definition;
        var bindings=definition.RegionAnchors??[];var allocationBindings=definition.AllocationAnchors??[];
        if((definition.RegionAnchors is not null&&(bindings.Count is <1 or >32||regions is null))||definition.Request.Anchors is null)
            throw new ArgumentException("METADATA_REGION_ANCHOR_DEPENDENCIES_INVALID");
        if(definition.AllocationAnchors is not null&&(allocationBindings.Count is <1 or >32||layouts is null))
            throw new ArgumentException("METADATA_ALLOCATION_ANCHOR_DEPENDENCIES_INVALID");
        foreach(var binding in allocationBindings)if(binding?.Interior is {} search)WorldRegionPlanner.ValidateInteriorSearch(search);
        var anchors=definition.Request.Anchors.ToList();
        if(anchors.Any(a=>a is null||!WorldEnvironmentQuery.Id(a.Id)))throw new ArgumentException("METADATA_REGION_ANCHOR_BINDING_INVALID");
        var ids=new HashSet<string>(anchors.Select(a=>a.Id),StringComparer.Ordinal);
        if(ids.Count!=anchors.Count)throw new ArgumentException("METADATA_REGION_ANCHOR_BINDING_INVALID");
        foreach(var binding in bindings.OrderBy(b=>b?.AnchorId,StringComparer.Ordinal))
        {
            budget.Charge(regions!.Regions.Count+1);
            if(binding is null||!WorldEnvironmentQuery.Id(binding.AnchorId)||!ids.Add(binding.AnchorId)||!WorldEnvironmentQuery.Id(binding.RegionId)||
                !WorldEnvironmentQuery.Text(binding.Purpose)||!WorldEnvironmentQuery.Text(binding.SourceRuleId))throw new ArgumentException("METADATA_REGION_ANCHOR_BINDING_INVALID");
            var region=regions.Regions.SingleOrDefault(r=>r.Id==binding.RegionId)??throw new ArgumentException("METADATA_REGION_ANCHOR_REQUIRED:"+binding.RegionId);
            anchors.Add(new(binding.AnchorId,region.Seed.Position));
        }
        foreach(var binding in allocationBindings.OrderBy(b=>b?.AnchorId,StringComparer.Ordinal))
        {
            budget.Charge(layouts!.Allocations.Count+1);
            if(binding is null||!WorldEnvironmentQuery.Id(binding.AnchorId)||!ids.Add(binding.AnchorId)||!WorldEnvironmentQuery.Id(binding.AllocationId)||
                !WorldEnvironmentQuery.Text(binding.Purpose)||!WorldEnvironmentQuery.Text(binding.SourceRuleId))throw new ArgumentException("METADATA_ALLOCATION_ANCHOR_BINDING_INVALID");
            var allocation=layouts.Allocations.SingleOrDefault(a=>a.Id==binding.AllocationId)??throw new ArgumentException("METADATA_ALLOCATION_ANCHOR_REQUIRED:"+binding.AllocationId);
            if(binding.Footprint is {} fit)
            {
                if(binding.Interior is not null)throw new ArgumentException("METADATA_ALLOCATION_ANCHOR_MODE_CONFLICT");
                if(!WorldEnvironmentQuery.Id(fit.DemandId))throw new ArgumentException("METADATA_ALLOCATION_FOOTPRINT_DEMAND_INVALID");
                var demands=definition.Request.Demands.Where(d=>d.Id==fit.DemandId).ToArray();
                var targets=definition.Bindings.Where(b=>b.DemandId==fit.DemandId).ToArray();
                if(demands.Length!=1||targets.Length!=1||targets[0].PartId is not null||targets[0].Population is not null||
                    demands[0].AllocationId!=binding.AllocationId||demands[0].RegionId!=allocation.RegionId)
                    throw new ArgumentException("METADATA_ALLOCATION_FOOTPRINT_BINDING_INVALID");
                var node=nodes.SingleOrDefault(n=>n.Id==targets[0].NodeId)??throw new ArgumentException("METADATA_ALLOCATION_FOOTPRINT_NODE_REQUIRED");
                budget.Charge(128);
                var source=MetadataFootprintResolver.Resolve(node,demands[0],targets[0],unit,
                    footprints??new Dictionary<string,MetadataCatalogFootprint>(),structures??new Dictionary<string,MetadataStructureDefinition>(),feedback?.AccessDefinitions,budget);
                WorldFootprintAnchorPlan plan;
                try{plan=new WorldFootprintAnchorPlanner().Plan(allocation.Shape,source.Reservation??source.Polygon,allocation.Program.Anchor,fit.Search,boundary,token,budget);}
                catch(ArgumentException error)when(error.Message=="WORLD_FOOTPRINT_ANCHOR_UNSATISFIED")
                    {throw new MetadataPlacementInfeasibleException("METADATA_ALLOCATION_FOOTPRINT_UNSATISFIED:"+binding.AnchorId);}
                footprintResults.Add(new(binding.AnchorId,binding.AllocationId,fit.DemandId,plan));
                anchors.Add(new(binding.AnchorId,plan.Position));
            }
            else if(binding.Interior is {} search)
            {
                var interior=WorldRegionPlanner.FindInterior(allocation.Shape,search,budget);
                if(interior.Clearance<search.MinimumClearance)
                    throw new MetadataPlacementInfeasibleException("METADATA_ALLOCATION_ANCHOR_CLEARANCE_UNSATISFIED:"+binding.AnchorId);
                results.Add(new(binding.AnchorId,binding.AllocationId,interior));
                anchors.Add(new(binding.AnchorId,interior.Position));
            }
            else anchors.Add(new(binding.AnchorId,allocation.Program.Anchor));
        }
        if(results.Count>0)anchorResults=results.AsReadOnly();
        if(footprintResults.Count>0)footprintAnchorResults=footprintResults.AsReadOnly();
        return definition with{RegionAnchors=null,AllocationAnchors=null,Request=definition.Request with{Anchors=anchors.OrderBy(a=>a.Id,StringComparer.Ordinal).ToArray()}};
    }
}
