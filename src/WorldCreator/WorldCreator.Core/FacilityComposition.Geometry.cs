using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;

namespace WorldCreator.Core.Planning;

public sealed record FacilityGeometryGroup(string Id,string TemplateId,string? ParentId,string Purpose,WorldPolygon Boundary);
public sealed record FacilityGeometryBuilding(string Id,string OwnerId,string SlotId,string Purpose,WorldPolygon Site,WorldPolygon Footprint,
    WorldPolyline Access,MetadataStructureDefinition Source,IReadOnlyList<BuildingComponentRule> Components,IReadOnlyList<PlannedSiteArea> Areas);
public sealed record FacilityGeometryRoad(string Id,string OwnerId,string SourceRoadId,StreetCorridor Corridor,RoadClass Class,bool IsEntrance=false);
public sealed record FacilityGeometryObject(string Id,string OwnerId,string SlotId,string Purpose,WorldPolygon Site,WorldPolygon Footprint,
    WorldPolyline? Access,ResolvedSiteObject Source,IReadOnlyList<BuildingComponentRule> Components);
public sealed record FacilityGeometryArea(string SlotId,PlannedSiteArea Area,ResolvedSiteArea Source);
public sealed record FacilityGeometryPlan(ResolvedSettlementCatalog Catalog,string DefinitionId,double WidthMeters,double DepthMeters,
    IReadOnlyList<FacilityDefinition> Templates,IReadOnlyList<FacilityGeometryGroup> Groups,IReadOnlyList<FacilityGeometryBuilding> Buildings,
    IReadOnlyList<FacilityGeometryRoad> Roads,StreetTopology Network,IReadOnlyList<FacilityGeometryObject>? Objects=null,IReadOnlyList<FacilityGeometryArea>? Areas=null);

public sealed partial class FacilityCompositionPlanner
{
    /// <summary>One north-gate rule shared by root assembly and compiled child links.</summary>
    internal static (WorldPoint Gate, FacilityRoad Target, WorldPolyline Centerline) PlanEntrance(
        double width, double depth, IReadOnlyList<FacilityRoad> roads, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (roads.Count == 0) throw new ArgumentException("FACILITY_GEOMETRY_ROADS_REQUIRED");
        var gate = new WorldPoint(width / 2, 0);
        var target = roads.Where(r => r.Centerline.Points.Count == 2 && r.Centerline.Points[0].Y == r.Centerline.Points[1].Y &&
            r.Centerline.Points.Min(p => p.X) <= gate.X && r.Centerline.Points.Max(p => p.X) >= gate.X && r.Centerline.Points[0].Y > 0)
            .OrderBy(r => r.Centerline.Points[0].Y).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
        if (target is null) throw new ArgumentException("FACILITY_GEOMETRY_ENTRY_ROUTE_REQUIRED");
        var line = new WorldPolyline([gate, new(gate.X, target.Centerline.Points[0].Y)]);
        if (StreetSurfaceBuilder.Build(new("entrance", line, target.Profile)).Any(s => s.Polygon.Any(p => !Rectangle(0, 0, width, depth).ContainsPoint(p))))
            throw new ArgumentException("FACILITY_GEOMETRY_ENTRY_OUTSIDE");
        return (gate, target, line);
    }

    /// <summary>Compiles exact registered facility geometry without mockup, artwork or fabricated simulation context.</summary>
    public FacilityGeometryPlan PlanGeometry(SettlementCatalogSelection selection,string definitionId,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        var pack=catalogs.Resolve(selection);var templates=pack.Facilities.ToDictionary(f=>f.Id,StringComparer.Ordinal);
        if(!templates.TryGetValue(definitionId,out var root))throw new ArgumentException("FACILITY_TEMPLATE_UNKNOWN:"+definitionId);
        var groups=new List<FacilityGeometryGroup>();var buildings=new List<FacilityGeometryBuilding>();var roads=new List<FacilityGeometryRoad>();
        var objects=new List<FacilityGeometryObject>();var objectDefinitions=(pack.Objects??[]).ToDictionary(o=>o.Id,StringComparer.Ordinal);
        var areas=new List<FacilityGeometryArea>();var areaDefinitions=(pack.Areas??[]).ToDictionary(a=>a.Id,StringComparer.Ordinal);
        var used=new HashSet<string>(StringComparer.Ordinal);
        Visit(root,"root",null,"Registered "+root.Name,new(0,0),0);
        if (roads.Count >= 1000) throw new ArgumentException("FACILITY_GEOMETRY_BUDGET");
        var entry = PlanEntrance(root.WidthMeters, root.DepthMeters,
            roads.Select(r => new FacilityRoad(r.Id, r.Corridor.Centerline, r.Class, r.Corridor.Profile)).ToArray(), token);
        var gate = entry.Gate;
        var entryRoad = new StreetCorridor("root/entrance", entry.Centerline, entry.Target.Profile);
        roads.Add(new(entryRoad.Id,"root",entry.Target.Id,entryRoad,entry.Target.Class,true));
        var network=StreetTopologyBuilder.Build(roads.Select(r=>r.Corridor).ToArray(),buildings.Select(b=>b.Access.Points[0]).Concat(objects.Where(o=>o.Access is not null).Select(o=>o.Access!.Points[0]))
            .Concat(areas.Where(a=>a.Area.Access is not null).Select(a=>a.Area.Access!.Points[0])).Append(gate).ToArray(),token);
        if(network.Diagnostics.Count>0||!network.IsConnected())throw new ArgumentException("FACILITY_GEOMETRY_ROADS_DISCONNECTED:"+string.Join(',',network.Diagnostics));
        var solids=buildings.Select(b=>b.Footprint).Concat(objects.Where(o=>o.Source.Definition.BlocksMovement).Select(o=>o.Footprint)).ToArray();
        var roadSurfaces=roads.Select(r=>r.Corridor)
            .Concat(buildings.Select(b=>new StreetCorridor(b.Id,b.Access,new(b.Source.Definition.AccessWidthMeters,1,0,0,0,0))))
            .Concat(objects.Where(o=>o.Access is not null).Select(o=>new StreetCorridor(o.Id,o.Access!,new(o.Source.Definition.AccessWidthMeters,1,0,0,0,0))))
            .Concat(areas.Where(a=>a.Area.Access is not null).Select(a=>new StreetCorridor(a.Area.Id,a.Area.Access!,new(a.Area.AccessWidthMeters,1,0,0,0,0))))
            .SelectMany(StreetSurfaceBuilder.Build).Select(s=>new WorldPolygon(s.Polygon)).ToArray();
        for(int i=0;i<solids.Length;i++)
        {
            token.ThrowIfCancellationRequested();
            for(int j=0;j<i;j++)if(TerrainCoverageTopology.IntersectionArea(solids[i],solids[j])>1e-7)throw new ArgumentException("FACILITY_GEOMETRY_BUILDING_CONFLICT");
            foreach(var surface in roadSurfaces)
            {
                token.ThrowIfCancellationRequested();
                if(TerrainCoverageTopology.IntersectionArea(solids[i],surface)>1e-7)throw new ArgumentException("FACILITY_GEOMETRY_ROAD_BUILDING_CONFLICT");
            }
            foreach(var building in buildings)
                for(int k=1;k<building.Access.Points.Count;k++)
                    if(solids[i].CrossesInterior(building.Access.Points[k-1],building.Access.Points[k]))throw new ArgumentException("FACILITY_GEOMETRY_ACCESS_BLOCKED");
            foreach(var item in objects.Where(o=>o.Access is not null))
                for(int k=1;k<item.Access!.Points.Count;k++)
                    if(solids[i].CrossesInterior(item.Access.Points[k-1],item.Access.Points[k]))throw new ArgumentException("FACILITY_GEOMETRY_ACCESS_BLOCKED");
        }
        foreach(var area in areas.Select(a=>a.Area).Concat(buildings.SelectMany(b=>b.Areas)))
        {
            token.ThrowIfCancellationRequested();
            foreach(var solid in solids)if(TerrainCoverageTopology.IntersectionArea(area.Boundary,solid)>1e-7)throw new ArgumentException("FACILITY_GEOMETRY_AREA_SOLID_CONFLICT");
            if(!area.AllowsThroughAccess)
                foreach(var route in roadSurfaces)if(TerrainCoverageTopology.IntersectionArea(area.Boundary,route)>1e-7)throw new ArgumentException("FACILITY_GEOMETRY_AREA_ACCESS_BLOCKED");
        }
        return new(pack.Provenance,root.Id,root.WidthMeters,root.DepthMeters,used.Order(StringComparer.Ordinal).Select(id=>templates[id]).ToArray(),groups,buildings,roads,network,objects,areas);

        void Visit(FacilityDefinition template,string id,string? parent,string purpose,WorldPoint offset,int depth)
        {
            token.ThrowIfCancellationRequested();
            if(depth>16||groups.Count+buildings.Count+objects.Count+areas.Count>=1000||roads.Count>=1000)throw new ArgumentException("FACILITY_GEOMETRY_BUDGET");
            used.Add(template.Id);groups.Add(new(id,template.Id,parent,purpose,Rectangle(offset.X,offset.Y,template.WidthMeters,template.DepthMeters)));
            WorldPoint Shift(WorldPoint p)=>new(p.X+offset.X,p.Y+offset.Y);
            foreach(var road in template.Roads.OrderBy(r=>r.Id,StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();if(roads.Count>=1000)throw new ArgumentException("FACILITY_GEOMETRY_BUDGET");
                string roadId=id+"/road/"+road.Id;
                roads.Add(new(roadId,id,road.Id,new(roadId,new(road.Centerline.Points.Select(Shift).ToArray()),road.Profile),road.Class));
            }
            foreach(var slot in template.Structures.OrderBy(s=>s.Id,StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();if(groups.Count+buildings.Count+objects.Count+areas.Count>=1000)throw new ArgumentException("FACILITY_GEOMETRY_BUDGET");
                string member=id+"/"+slot.Id+"-0";var source=pack.Structures.Single(d=>d.Id==slot.Envelope.BuildingType);
                var road=template.Roads.Single(r=>r.Id==slot.FrontagePathId);
                var site=new WorldPolygon(slot.Site.Vertices.Select(Shift).ToArray());
                double front=Shift(road.Centerline.Points[0]).Y,cx=site.Vertices.Average(p=>p.X);
                var parcel=new BuildableParcel(member,id,site,new(cx,front),"registered-facility-slot");
                var sized=new BuildingSizePlanner().PlanGeometry(parcel,slot.Envelope,source,slot.SizeVariantId);
                if(!sized.Valid)throw new ArgumentException("FACILITY_GEOMETRY_BUILDING_INVALID:"+member+":"+string.Join(',',sized.Issues));
                if(road.Profile.LaneWidthMeters*road.Profile.LaneCount+1e-7<Math.Max(source.MinimumRoadWidthMeters,sized.Variant!.MinimumRoadWidthMeters))
                    throw new ArgumentException("FACILITY_GEOMETRY_ROAD_UNDERSIZED:"+member);
                var shape=sized.Footprint!;bool north=Math.Abs(front-site.Vertices.Min(p=>p.Y))<1e-7;
                var access=new WorldPolyline([new(cx,front),new(cx,north?shape.Vertices.Min(p=>p.Y):shape.Vertices.Max(p=>p.Y))]);
                var grounds=new BuildingSitePlanner().Plan(member,id,road.Id,parcel,shape,source);
                if(grounds.Issues.Count>0)throw new ArgumentException("FACILITY_GEOMETRY_GROUNDS_INVALID:"+member+":"+string.Join(',',grounds.Issues));
                var groundAreas=grounds.Features.Where(f=>f.Kind!=SiteFeatureKind.AccessWalkway).Select(f=>
                {
                    var matches=source.SiteRules!.Where(r=>f.RuleId==source.Id+":"+r.Id||f.RuleId==source.Id+":"+r.Id+"-left"||f.RuleId==source.Id+":"+r.Id+"-right").ToArray();
                    if(matches.Length!=1)throw new ArgumentException("FACILITY_GEOMETRY_GROUND_RULE_AMBIGUOUS:"+f.RuleId);
                    var rule=matches[0];
                    return new PlannedSiteArea(member+"/site/"+Uri.EscapeDataString(f.RuleId),member,f.Kind.ToString(),f.Boundary,f.RuleId,f.Purpose,rule.Band,f.SpeciesAssetId,f.PlantSpacingMeters,f.PlantMarginMeters,AllowsThroughAccess:BuildingSitePlanner.PermitsThroughAccess(f.Kind));
                }).ToArray();
                buildings.Add(new(member,id,slot.Id,slot.Purpose,site,shape,access,new(pack.Provenance,source,sized.Variant!),slot.Components,groundAreas));
            }
            foreach(var slot in (template.Objects??[]).OrderBy(o=>o.Id,StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();if(groups.Count+buildings.Count+objects.Count+areas.Count>=1000)throw new ArgumentException("FACILITY_GEOMETRY_BUDGET");
                string member=id+"/"+slot.Id+"-0";var definition=objectDefinitions[slot.DefinitionId];var road=template.Roads.Single(r=>r.Id==slot.FrontagePathId);
                var shifted=slot with{Id=member,Site=new(slot.Site.Vertices.Select(Shift).ToArray())};
                var planned=new SiteObjectPlanner().Plan(shifted,definition,Shift(road.Centerline.Points[0]).Y,token);
                if(planned.Issues.Count>0||planned.Footprint is null||planned.Variant is null)throw new ArgumentException("FACILITY_GEOMETRY_OBJECT_INVALID:"+member+":"+string.Join(',',planned.Issues));
                if(road.Profile.LaneWidthMeters*road.Profile.LaneCount+1e-7<definition.MinimumRoadWidthMeters)throw new ArgumentException("FACILITY_GEOMETRY_OBJECT_ROAD_UNDERSIZED:"+member);
                objects.Add(new(member,id,slot.Id,slot.Purpose,shifted.Site,planned.Footprint,planned.Access,new(pack.Provenance,definition,planned.Variant),slot.Components));
            }
            foreach(var slot in (template.Areas??[]).OrderBy(a=>a.Id,StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();if(groups.Count+buildings.Count+objects.Count+areas.Count>=1000)throw new ArgumentException("FACILITY_GEOMETRY_BUDGET");
                string member=id+"/area/"+slot.Id;var definition=areaDefinitions[slot.DefinitionId];var road=template.Roads.Single(r=>r.Id==slot.FrontagePathId);
                var shifted=slot with{Id=member,Site=new(slot.Site.Vertices.Select(Shift).ToArray())};
                var planned=new SiteAreaPlanner().Plan(shifted,definition,Shift(road.Centerline.Points[0]).Y,token);
                if(planned.Issues.Count>0||planned.Boundary is null||planned.Variant is null)throw new ArgumentException("FACILITY_GEOMETRY_AREA_INVALID:"+member+":"+string.Join(',',planned.Issues));
                if(road.Profile.LaneWidthMeters*road.Profile.LaneCount+1e-7<definition.MinimumRoadWidthMeters)throw new ArgumentException("FACILITY_GEOMETRY_AREA_ROAD_UNDERSIZED:"+member);
                var area=new PlannedSiteArea(member,id,definition.Kind,planned.Boundary,slot.Id,slot.Purpose,null,AllowsThroughAccess:definition.AllowsThroughAccess,
                    ReservedSite:shifted.Site,SourceKey:pack.Provenance.Id+"/"+definition.Id+"/"+planned.Variant.Id,Access:planned.Access,AccessWidthMeters:definition.AccessWidthMeters,
                    FrontageRoadId:id+"/road/"+slot.FrontagePathId,Components:slot.Components);
                areas.Add(new(slot.Id,area,new(pack.Provenance,definition,planned.Variant)));
            }
            foreach(var child in template.Children.OrderBy(c=>c.Id,StringComparer.Ordinal))
                Visit(templates[child.DefinitionId],id+"/"+child.Id+"-0",id,child.Purpose,Shift(child.OffsetMeters),depth+1);
        }
    }
}
