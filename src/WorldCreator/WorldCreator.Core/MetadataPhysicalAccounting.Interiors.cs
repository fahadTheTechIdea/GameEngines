using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataPhysicalAccountingPlanner
{
    private sealed record InteriorSource(string RuleId,string ScopeNodeId,string Purpose,string SourceRuleId,
        IReadOnlyList<Source> Rooms,IReadOnlyList<Source> Walls,IReadOnlyList<MetadataAccountingInteriorWall> WallIdentities,MetadataInteriorFloorRequirement? FloorRequirement);

    // This binds saved assembly facts for measurement. Assembly/replay acceptance
    // remains at MetadataInteriorPlanner; accounting neither invents nor repairs walls.
    private static IReadOnlyList<InteriorSource>? SnapshotInteriors(MetadataInteriorPlan? plan,IReadOnlyList<Source> sources,
        IReadOnlyDictionary<string,WorldMetadataNode> nodes,Func<WorldPolygon,int,WorldPolygon> snapshot,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(plan is null)return null;
        if(plan.Definition?.Rules is not {Count:>=1 and <=16}||plan.Results is null||plan.Results.Count!=plan.Definition.Rules.Count)
            throw new ArgumentException("METADATA_ACCOUNTING_INTERIOR_INPUT_INVALID");
        var sites=sources.ToDictionary(s=>s.Identity.NodeId,StringComparer.Ordinal);
        var rules=new Dictionary<string,MetadataInteriorRule>(StringComparer.Ordinal);
        foreach(var rule in plan.Definition.Rules)
        {
            token.ThrowIfCancellationRequested();budget.Charge();
            if(rule is null||!WorldEnvironmentQuery.Id(rule.Id)||!rules.TryAdd(rule.Id,rule)||
                !WorldEnvironmentQuery.Text(rule.Purpose)||!WorldEnvironmentQuery.Text(rule.SourceRuleId)||
                rule.ScopeNodeId is null||!nodes.ContainsKey(rule.ScopeNodeId)||rule.RoomKinds is not {Count:>=1 and <=16}||
                rule.RoomKinds.Any(k=>!WorldEnvironmentQuery.Id(k)))
                throw new ArgumentException("METADATA_ACCOUNTING_INTERIOR_INPUT_INVALID");
            if(rule.FloorRequirement is {} required)ValidateFloorRequirement(required);
        }
        var seenRules=new HashSet<string>(StringComparer.Ordinal);var ownedRooms=new HashSet<string>(StringComparer.Ordinal);
        var result=new List<InteriorSource>();
        foreach(var assembly in plan.Results.OrderBy(r=>r?.RuleId,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();budget.Charge(nodes.Count);
            if(assembly is null||assembly.RuleId is null||!seenRules.Add(assembly.RuleId)||!rules.TryGetValue(assembly.RuleId,out var rule)||
                assembly.RoomNodeIds is not {Count:>=2 and <=32}||assembly.Walls is null||assembly.Walls.Count>WorldSurfaceQuery.MaximumExclusions)
                throw new ArgumentException("METADATA_ACCOUNTING_INTERIOR_INPUT_INVALID");
            var expected=nodes.Values.Where(n=>n.ParentId==rule.ScopeNodeId&&rule.RoomKinds.Contains(n.Kind,StringComparer.Ordinal))
                .Select(n=>n.Id).Order(StringComparer.Ordinal).ToArray();
            if(!assembly.RoomNodeIds.Order(StringComparer.Ordinal).SequenceEqual(expected,StringComparer.Ordinal))
                throw new ArgumentException("METADATA_ACCOUNTING_INTERIOR_ROOM_SOURCE_MISMATCH:"+rule.Id);
            var rooms=new List<Source>();
            foreach(var id in expected)
            {
                if(!ownedRooms.Add(id)||!sites.TryGetValue(id,out var source)||source.Site.Occupancy!=MetadataOccupancyKind.Reservation)
                    throw new ArgumentException("METADATA_ACCOUNTING_INTERIOR_ROOM_SOURCE_MISMATCH:"+id);
                rooms.Add(source);
            }
            var roomIds=expected.ToHashSet(StringComparer.Ordinal);var wallIds=new HashSet<string>(StringComparer.Ordinal);
            var walls=new List<Source>();var identities=new List<MetadataAccountingInteriorWall>();
            foreach(var wall in assembly.Walls.OrderBy(w=>w?.Id,StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();budget.Charge();
                if(wall is null||!WorldEnvironmentQuery.Id(wall.Id)||!wallIds.Add(wall.Id)||wall.RoomNodeId is null||!roomIds.Contains(wall.RoomNodeId))
                    throw new ArgumentException("METADATA_ACCOUNTING_INTERIOR_WALL_SOURCE_MISMATCH:"+rule.Id);
                var polygon=snapshot(wall.Footprint,64);var room=sites[wall.RoomNodeId];
                budget.Charge((long)polygon.Vertices.Count*room.Site.Footprint.Vertices.Count*room.Site.Footprint.Vertices.Count*2);
                if(!room.Site.Footprint.ContainsPolygon(polygon))
                    throw new ArgumentException("METADATA_ACCOUNTING_INTERIOR_WALL_OUTSIDE_ROOM:"+wall.Id);
                string hash=MetadataExecutionCache.Hash(polygon);
                var identity=new MetadataAccountingSource(rule.Id+":"+wall.Id,MetadataOccupancyKind.Solid,rule.Purpose,rule.SourceRuleId,hash,null);
                walls.Add(MakeSource(new(identity.NodeId,MetadataOccupancyKind.Solid,rule.SourceRuleId,polygon,[]),identity));
                identities.Add(new(wall.Id,wall.RoomNodeId,hash));
            }
            result.Add(new(rule.Id,rule.ScopeNodeId,rule.Purpose,rule.SourceRuleId,rooms.AsReadOnly(),walls.AsReadOnly(),identities.AsReadOnly(),rule.FloorRequirement));
        }
        return result.AsReadOnly();
    }

    internal static void ValidateFloorRequirement(MetadataInteriorFloorRequirement required)
    {
        if(!double.IsFinite(required.MinimumClearArea)||required.MinimumClearArea<0||
            !double.IsFinite(required.MinimumClearFraction)||required.MinimumClearFraction is <0 or >1)
            throw new ArgumentException("METADATA_INTERIOR_FLOOR_REQUIREMENT_INVALID");
    }

    private static IReadOnlyList<MetadataInteriorCoverageObservation> ObserveInteriors(IReadOnlyList<InteriorSource> interiors,
        IReadOnlyList<Source> sources,string unit,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var blockers=interiors.SelectMany(i=>i.Walls).Concat(sources.Where(s=>s.Site.Occupancy==MetadataOccupancyKind.Solid)).ToArray();
        var observations=new List<MetadataInteriorCoverageObservation>();
        foreach(var interior in interiors)
        {
            double floor=MeasureUnion(interior.Rooms,null,unit,budget,token),wall=MeasureUnion(interior.Walls,null,unit,budget,token),
                clear=MeasureUnion(interior.Rooms,blockers,unit,budget,token);
            var required=interior.FloorRequirement;
            bool? satisfied=required is null?null:clear+1e-8>=required.MinimumClearArea&&Math.Clamp(clear/floor,0,1)+1e-9>=required.MinimumClearFraction;
            observations.Add(new(interior.RuleId,interior.ScopeNodeId,interior.Purpose,interior.SourceRuleId,
                Array.AsReadOnly(interior.Rooms.Select(r=>r.Identity.NodeId).ToArray()),interior.WallIdentities,floor,wall,clear,required,satisfied));
        }
        return observations.AsReadOnly();
    }

    // Called by the existing interior feasibility/replay owner after all walls
    // exist. Charge its current ledger, including the placement parent ledger.
    internal void ValidateRequiredInteriorFloors(MetadataInteriorPlan plan,IReadOnlyList<MetadataPhysicalSite> sites,
        IReadOnlyList<WorldMetadataNode> nodes,string unit,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(!plan.Definition.Rules.Any(r=>r.FloorRequirement is not null))return;
        budget.Charge(nodes.Count+sites.Count);
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);var sources=new List<Source>();
        foreach(var site in sites.OrderBy(s=>s.NodeId,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if(site.Occupancy==MetadataOccupancyKind.Unspecified)throw new ArgumentException("METADATA_ACCOUNTING_INTERIOR_OCCUPANCY_REQUIRED");
            var polygon=SnapshotPolygon(site.Footprint,64,budget);
            sources.Add(MakeSource(site with{Footprint=polygon},new(site.NodeId,site.Occupancy,lookup[site.NodeId].Purpose,
                site.SourceRuleId,MetadataExecutionCache.Hash(polygon),null)));
        }
        var interiors=SnapshotInteriors(plan,sources,lookup,(p,max)=>SnapshotPolygon(p,max,budget),budget,token)!;
        foreach(var observation in ObserveInteriors(interiors,sources,unit,budget,token))
            if(observation.MinimumSatisfied==false)
            {
                string Number(double value)=>value.ToString("G17",System.Globalization.CultureInfo.InvariantCulture);
                var error=new MetadataSpatialConstraintException("METADATA_INTERIOR_FLOOR_MINIMUM_UNSATISFIED:"+observation.RuleId+
                    ":clear="+Number(observation.ClearFloorArea)+":floor="+Number(observation.FloorUnionArea)+
                    ":minimumArea="+Number(observation.Requirement!.MinimumClearArea)+":minimumFraction="+Number(observation.Requirement.MinimumClearFraction));
                error.Data["InteriorFloorObservation"]=observation;throw error;
            }
    }
}
