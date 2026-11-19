using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataServicePlanner
{
    private sealed record CorridorCapacityContext(Dictionary<string,MetadataServiceSegmentCapacity> Limits,
        Dictionary<string,WorldCorridorPlan> ByConnection);

    private static CorridorCapacityContext PrepareCorridorCapacity(IReadOnlyList<MetadataServiceCorridorCapacity>? definitions,
        MetadataCorridorPlan? corridors,WorldSpatialWorkBudget budget)
    {
        var limits=new Dictionary<string,MetadataServiceSegmentCapacity>(StringComparer.Ordinal);
        var byConnection=new Dictionary<string,WorldCorridorPlan>(StringComparer.Ordinal);
        if(definitions is null)return new(limits,byConnection);
        if(definitions.Count is <1 or >16)throw new ArgumentException("METADATA_SERVICE_CORRIDOR_CAPACITY_INVALID");
        var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var definition in definitions.OrderBy(d=>d?.GroupId,StringComparer.Ordinal))
        {
            budget.Charge();
            if(definition is null||!Id(definition.GroupId)||!seen.Add(definition.GroupId)||!Quantity(definition.CapacityPerSegment)||
                !Text(definition.Unit)||!Text(definition.Purpose)||!Text(definition.SourceRuleId))throw new ArgumentException("METADATA_SERVICE_CORRIDOR_CAPACITY_INVALID");
            var group=corridors?.Groups.SingleOrDefault(g=>g.Definition.Id==definition.GroupId)
                ??throw new ArgumentException("METADATA_SERVICE_CORRIDOR_GROUP_REQUIRED");
            if(group.Segments.Count is <1 or >16384||group.Journeys.Count is <1 or >128)throw new ArgumentException("METADATA_SERVICE_CORRIDOR_PLAN_INVALID");
            foreach(var segment in group.Segments)
            {
                budget.Charge();
                if(!limits.TryAdd(segment.Id,new(definition.GroupId,segment.Id,definition.CapacityPerSegment,definition.Unit,definition.Purpose,definition.SourceRuleId)))
                    throw new ArgumentException("METADATA_SERVICE_CORRIDOR_PLAN_INVALID");
            }
            foreach(var id in group.Journeys.Select(j=>j.ConnectionId).Distinct(StringComparer.Ordinal))
                if(!byConnection.TryAdd(id,group))throw new ArgumentException("METADATA_SERVICE_CORRIDOR_PLAN_INVALID");
        }
        return new(limits,byConnection);
    }

    private static void CountCorridorUse(CorridorCapacityContext context,string connectionId,WorldRouteResult route,
        MetadataServiceRequirement requirement,Dictionary<string,int> counts,WorldSpatialWorkBudget budget)
    {
        if(!context.ByConnection.TryGetValue(connectionId,out var group))return;
        var journey=group.Journeys.SingleOrDefault(j=>j.Id==route.Id&&j.ConnectionId==connectionId)
            ??throw new ArgumentException("METADATA_SERVICE_CORRIDOR_JOURNEY_REQUIRED");
        if(journey.Steps.Count is <1 or >16384)throw new ArgumentException("METADATA_SERVICE_CORRIDOR_PLAN_INVALID");
        foreach(var step in journey.Steps)
        {
            budget.Charge();
            if(!context.Limits.TryGetValue(step.SegmentId,out var limit)||limit.GroupId!=group.Definition.Id||limit.Unit!=requirement.Unit)
                throw new ArgumentException("METADATA_SERVICE_CORRIDOR_UNIT_OR_SEGMENT_INVALID");
            counts[step.SegmentId]=counts.GetValueOrDefault(step.SegmentId)+1;
        }
    }
}
