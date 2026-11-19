using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataInteriorFloorCapacityRoom(string NodeId,string FootprintSha256,double SourceArea,double ClosedWallArea,
    double OpeningAllowance,string? SolidStageId,IReadOnlyList<string> SolidNodeIds,IReadOnlyList<double> SolidSourceAreas,double Uncertainty);
public sealed record MetadataInteriorFloorCapacityWitness(string RuleId,string ScopeNodeId,string SourceRuleId,
    IReadOnlyList<MetadataInteriorFloorCapacityRoom> Rooms,double FloorAreaUpper,double FloorAreaLower,double MinimumWallArea,double MinimumSolidArea,
    double AreaUncertainty,double MaximumClearArea,double MaximumClearFraction,MetadataInteriorFloorRequirement Requirement,string Unit);

public sealed partial class MetadataPhysicalAccountingPlanner
{
    /// <summary>Source-only necessary condition. Closed native wall bands may lose
    /// at most width*thickness per incident doorway. Select at most one disjoint
    /// descendant solid stage per room; never sum potentially overlapping stages.
    /// A valid interior has nonoverlapping rooms and no solid/wall intersections.</summary>
    internal void ValidateInteriorFloorCapacity(IReadOnlyList<MetadataGroundUsePlacementStage> stages,
        IReadOnlyDictionary<string,WorldMetadataNode> nodes,IReadOnlyDictionary<string,MetadataResolvedFootprint> sources,
        IReadOnlyDictionary<string,MetadataCatalogAccess>? access,MetadataInteriorDefinition? definition,
        IReadOnlyList<WorldMetadataConnection> connections,string unit,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(definition?.Rules is not {Count:>=1 and <=16}||!definition.Rules.Any(r=>r?.FloorRequirement is not null))return;
        budget.Charge(nodes.Count+stages.Sum(s=>(long)s.NodeIds.Count));
        var owners=stages.SelectMany(s=>s.NodeIds.Select(id=>(Id:id,Stage:s))).ToDictionary(p=>p.Id,p=>p.Stage,StringComparer.Ordinal);
        foreach(var rule in definition.Rules)
        {
            token.ThrowIfCancellationRequested();if(rule?.FloorRequirement is not {} required)continue;ValidateFloorRequirement(required);
            // Unsupported/malformed source shapes retain the interior owner's
            // ordinary validation; absence of a bound is never acceptance.
            if(rule.RoomKinds is null||rule.ConnectionKinds is null||!double.IsFinite(rule.WallThickness)||rule.WallThickness<=0||
                rule.WallThickness>1e5||!double.IsFinite(rule.OpeningWidth)||rule.OpeningWidth is <=0 or >1e6)continue;
            var rooms=nodes.Values.Where(n=>n.ParentId==rule.ScopeNodeId&&rule.RoomKinds.Contains(n.Kind,StringComparer.Ordinal)).OrderBy(n=>n.Id,StringComparer.Ordinal).ToArray();
            if(rooms.Length is <2 or >32||rooms.Any(r=>!sources.ContainsKey(r.Id)||access?.GetValueOrDefault(r.ArchetypeId)?.Occupancy!=MetadataOccupancyKind.Reservation||
                sources[r.Id].Polygon.Vertices.Count>16||!ConvexParcelGeometry.IsConvex(sources[r.Id].Polygon)))continue;
            var roomIds=rooms.Select(r=>r.Id).ToHashSet(StringComparer.Ordinal);
            var best=new Dictionary<string,(string Stage,string[] Ids,(double Area,double Error)[] Areas,double Lower)>(StringComparer.Ordinal);
            foreach(var stage in stages)
            {
                budget.Charge(stage.NodeIds.Count);var ids=stage.NodeIds.Where(id=>access?.GetValueOrDefault(nodes[id].ArchetypeId)?.Occupancy==MetadataOccupancyKind.Solid).Order(StringComparer.Ordinal).ToArray();
                if(ids.Length==0)continue;
                var areas=ids.Select(id=>AreaBounds(sources[id].Polygon,budget)).ToArray();
                double lower=Math.Max(0,areas.Sum(a=>a.Area-a.Error)-8e-8*ids.Length*ids.Length);
                string? parent=stage.ParentNodeId;int depth=0;bool contained=stage.InParentFootprint;
                while(parent is not null)
                {
                    if(!contained)break;
                    budget.Charge();
                    if(++depth>32||!owners.TryGetValue(parent,out var owner))throw new ArgumentException("METADATA_PLACEMENT_STAGE_DEPENDENCY_INVALID");
                    if(roomIds.Contains(parent)&&(!best.TryGetValue(parent,out var previous)||lower>previous.Lower))best[parent]=(stage.Id,ids,areas,lower);
                    contained=owner.InParentFootprint;parent=owner.ParentNodeId;
                }
            }
            var rows=new List<MetadataInteriorFloorCapacityRoom>();double floorUpper=0,floorLower=0,wallLower=0,solidLower=0,uncertainty=0;long vertices=1;
            foreach(var room in rooms)
            {
                var source=sources[room.Id];var area=AreaBounds(source.Polygon,budget);floorUpper+=area.Area+area.Error;floorLower+=Math.Max(0,area.Area-area.Error);
                var walls=MetadataInteriorPlanner.ClosedWallFootprints(source.Polygon,rule.WallThickness,budget);
                var wallSources=walls.Select((p,i)=>MakeSource(new("closed-"+i,MetadataOccupancyKind.Solid,rule.SourceRuleId,p,[]),
                    new("closed-"+i,MetadataOccupancyKind.Solid,rule.Purpose,rule.SourceRuleId,MetadataExecutionCache.Hash(p),null))).ToArray();
                double closed=MeasureUnion(wallSources,null,unit,budget,token);
                budget.Charge(connections.Count);
                int openings=connections.Count(c=>(c.FromId==room.Id||c.ToId==room.Id)&&rule.ConnectionKinds.Contains(c.Kind,StringComparer.Ordinal))+(rule.Entrances?.Count??0);
                double cuts=openings*rule.OpeningWidth*rule.WallThickness;
                double error=area.Error+walls.Sum(p=>AreaBounds(p,budget).Error)+Math.Abs(cuts)*1e-12;
                wallLower+=Math.Max(0,closed-cuts-error);uncertainty+=area.Error+error;
                vertices+=source.Polygon.Vertices.Count+walls.Sum(p=>p.Vertices.Count);
                if(best.TryGetValue(room.Id,out var solids))
                {
                    solidLower+=solids.Lower;vertices+=solids.Ids.Sum(id=>sources[id].Polygon.Vertices.Count);
                    uncertainty+=solids.Areas.Sum(a=>a.Error)+8e-8*solids.Ids.Length*solids.Ids.Length;
                    rows.Add(new(room.Id,MetadataExecutionCache.Hash(source.Polygon),area.Area,closed,cuts,solids.Stage,
                        Array.AsReadOnly(solids.Ids),Array.AsReadOnly(solids.Areas.Select(a=>a.Area).ToArray()),error+solids.Areas.Sum(a=>a.Error)));
                }
                else rows.Add(new(room.Id,MetadataExecutionCache.Hash(source.Polygon),area.Area,closed,cuts,null,[],[],error));
            }
            // Covers tolerated room/solid/wall boundary contacts and clipping
            // roundoff. The source bounds already include all32 containment levels.
            double contact=8e-8*vertices*vertices;uncertainty+=contact;
            double upper=Math.Max(0,floorUpper-wallLower-solidLower+contact);
            floorLower=Math.Max(0,floorLower-contact);
            double fraction=floorLower<=0?1:Math.Clamp(upper/floorLower,0,1);
            if(required.MinimumClearArea<=upper+1e-8&&required.MinimumClearFraction<=fraction+1e-9)continue;
            var witness=new MetadataInteriorFloorCapacityWitness(rule.Id,rule.ScopeNodeId,rule.SourceRuleId,rows.AsReadOnly(),floorUpper,floorLower,
                wallLower,solidLower,uncertainty,upper,fraction,required,unit);
            string N(double x)=>x.ToString("G17",System.Globalization.CultureInfo.InvariantCulture);
            var failure=new MetadataPlacementInfeasibleException("METADATA_PLACEMENT_UNRESOLVED:INTERIOR_FLOOR_CAPACITY:"+rule.Id+
                ":maximumClear="+N(upper)+":maximumFraction="+N(fraction)+":minimumArea="+N(required.MinimumClearArea)+":minimumFraction="+N(required.MinimumClearFraction));
            failure.Data["PlacementPoseInvariantFailure"]=true;failure.Data["PlacementWork"]=budget.Used;failure.Data["InteriorFloorCapacityWitness"]=witness;throw failure;
        }
    }
}
