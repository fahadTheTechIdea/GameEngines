using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Realizes selected room relationships as shared-boundary openings.
/// Room geometry and its placement remain owned by the catalog/placement solver.</summary>
public sealed record MetadataInteriorFloorRequirement(double MinimumClearArea=0,double MinimumClearFraction=0);
public sealed record MetadataInteriorRule(string Id,string Purpose,string SourceRuleId,string ScopeNodeId,
    IReadOnlyList<string> RoomKinds,IReadOnlyList<string> ConnectionKinds,double OpeningWidth,double WallThickness,
    double ApproachDepth,WorldTraversalProfile Profile,WorldRouteSearch Search,IReadOnlyList<MetadataInteriorEntrance>? Entrances=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataInteriorFloorRequirement? FloorRequirement=null);
public sealed record MetadataInteriorEntrance(string ConnectionId,string RoomPortId,string? OutsidePortId=null);
public sealed record MetadataInteriorDefinition(IReadOnlyList<MetadataInteriorRule> Rules,long MaximumWork=20000000);
public sealed record MetadataInteriorWall(string Id,string RoomNodeId,int EdgeIndex,WorldPoint From,WorldPoint To,WorldPolygon Footprint);
public sealed record MetadataInteriorOpening(string ConnectionId,string FromId,string ToId,WorldPoint From,WorldPoint To,
    WorldPoint FromApproach,WorldPoint ToApproach,double Width,MetadataInteriorEntrance? Entrance=null);
public sealed record MetadataInteriorResult(string RuleId,IReadOnlyList<string> RoomNodeIds,
    IReadOnlyList<MetadataInteriorWall> Walls,IReadOnlyList<MetadataInteriorOpening> Openings,
    IReadOnlyList<MetadataRoutedConnection> Routes);
public sealed record MetadataInteriorPlan(MetadataInteriorDefinition Definition,IReadOnlyList<MetadataInteriorResult> Results,long Work);

/// <summary>Bounded convex-room assembly. Shared boundaries determine doors;
/// the common surface route owner qualifies all swept passages around walls/content.</summary>
public sealed class MetadataInteriorPlanner
{
    /// <summary>Consumes a generated/replayed assembly without modifying the saved base terrain.</summary>
    public static WorldSurfaceDefinition WithWalls(WorldSurfaceDefinition surface,MetadataInteriorPlan? plan)
    {
        if(plan is null)return surface;
        var exclusions=surface.Exclusions.Concat(plan.Results.SelectMany(r=>r.Walls.Select(w=>new WorldSurfaceExclusion(
            "interior-"+MetadataExecutionCache.Hash(new{r.RuleId,w.RoomNodeId,w.Id}),"Interior wall: "+w.RoomNodeId,
            plan.Definition.Rules.Single(d=>d.Id==r.RuleId).SourceRuleId,w.Footprint)))).ToArray();
        if(exclusions.Length>WorldSurfaceQuery.MaximumExclusions)throw new ArgumentException("METADATA_INTERIOR_EXCLUSION_BUDGET");
        return surface with{Exclusions=exclusions};
    }
    internal static void ValidateRouteOwnership(MetadataInteriorDefinition? definition,IReadOnlyList<MetadataRouteRule>? routes,IReadOnlyList<WorldMetadataConnection> connections)
    {
        if(definition?.Rules is null||routes is null)return;
        var entranceIds=definition.Rules.SelectMany(r=>r?.Entrances??[]).Where(e=>e is not null).Select(e=>e.ConnectionId).ToHashSet(StringComparer.Ordinal);
        var kinds=definition.Rules.Where(r=>r?.ConnectionKinds is not null).SelectMany(r=>r.ConnectionKinds).Concat(connections.Where(c=>entranceIds.Contains(c.Id)).Select(c=>c.Kind)).ToHashSet(StringComparer.Ordinal);
        if(routes.Any(r=>r is not null&&kinds.Contains(r.ConnectionKind)))throw new ArgumentException("METADATA_INTERIOR_ROUTE_OWNERSHIP_CONFLICT");
    }
    private static Contact? SharedContact(MetadataInteriorRule rule,Edge[] first,Edge[] second,WorldSpatialWorkBudget budget)
    {
        var contacts=new List<Contact>();
        foreach(var a in first)foreach(var b in second)
        {
            budget.Charge();
            if(a.NX*b.NX+a.NY*b.NY> -1+Eps||PlanarIntersections.Intersect(a.A,a.B,b.A,b.B).Kind!=IntersectionKind.CollinearOverlap)continue;
            double Projection(WorldPoint p)=>(p.X-a.A.X)*a.X+(p.Y-a.A.Y)*a.Y;
            double low=Math.Max(0,Math.Min(Projection(b.A),Projection(b.B))),high=Math.Min(a.Length,Math.Max(Projection(b.A),Projection(b.B)));
            // A full corner pier is retained at both ends of the shared span.
            if(high-low+Eps>=rule.OpeningWidth+2*rule.WallThickness)contacts.Add(new(a,b,low,high));
        }
        return contacts.OrderByDescending(c=>c.High-c.Low).ThenBy(c=>c.A.Index).ThenBy(c=>c.B.Index).FirstOrDefault()
            ;
    }
    // Only necessary geometry checks on already placed room pairs. Descendants
    // cannot repair a missing shared span. Full assembly/routing still runs later.
    internal sealed record ContactRejection(string Reason,string FromId,string ToId);
    internal static ContactRejection? PlacementContacts(MetadataInteriorDefinition? definition,
        IReadOnlyDictionary<string,WorldMetadataNode> nodes,IReadOnlyDictionary<string,WorldSitePlacement> placed,
        IReadOnlyList<WorldMetadataConnection> connections,WorldSpatialWorkBudget budget)
    {
        if(definition?.Rules is null)return null;
        foreach(var rule in definition.Rules)
        {
            if(rule?.RoomKinds is null||rule.ConnectionKinds is null||!double.IsFinite(rule.OpeningWidth)||rule.OpeningWidth<=0||
                !double.IsFinite(rule.WallThickness)||rule.WallThickness<=0)continue; // Full validation owns malformed declarations.
            foreach(var link in connections)
            {
                budget.Charge();
                if(!rule.ConnectionKinds.Contains(link.Kind,StringComparer.Ordinal)||
                    !nodes.TryGetValue(link.FromId,out var from)||!nodes.TryGetValue(link.ToId,out var to)||
                    from.ParentId!=rule.ScopeNodeId||to.ParentId!=rule.ScopeNodeId||
                    !rule.RoomKinds.Contains(from.Kind,StringComparer.Ordinal)||!rule.RoomKinds.Contains(to.Kind,StringComparer.Ordinal)||
                    !placed.TryGetValue(from.Id,out var a)||!placed.TryGetValue(to.Id,out var b))continue;
                if(SharedContact(rule,Edges(a.Footprint.Footprint),Edges(b.Footprint.Footprint),budget) is null)
                    return new("METADATA_INTERIOR_SHARED_BOUNDARY_REQUIRED:"+link.Id,link.FromId,link.ToId);
            }
        }
        return null;
    }

    private const double Eps=1e-8;
    private sealed record Edge(int Index,WorldPoint A,WorldPoint B,double X,double Y,double NX,double NY,double Length);
    private sealed record Contact(Edge A,Edge B,double Low,double High);
    public MetadataInteriorPlan Plan(MetadataInteriorDefinition definition,WorldSurfaceDefinition surface,
        IReadOnlyList<WorldMetadataNode> nodes,IReadOnlyList<WorldMetadataConnection> connections,
        MetadataPhysicalAccessPlan? access,CancellationToken token=default)
        =>Plan(definition,surface,nodes,connections,access,null,token);

    internal MetadataInteriorPlan Plan(MetadataInteriorDefinition definition,WorldSurfaceDefinition surface,
        IReadOnlyList<WorldMetadataNode> nodes,IReadOnlyList<WorldMetadataConnection> connections,
        MetadataPhysicalAccessPlan? access,WorldSpatialWorkBudget? parent,CancellationToken token,MetadataInteriorPlan? preserved=null)
    {
        token.ThrowIfCancellationRequested();
        if(definition?.Rules is not {Count:>=1 and <=16}||definition.MaximumWork is <1 or >200000000||access is null)
            throw new ArgumentException("METADATA_INTERIOR_DEPENDENCIES_INVALID");
        foreach(var rule in definition.Rules)
            if(rule?.FloorRequirement is {} required)MetadataPhysicalAccountingPlanner.ValidateFloorRequirement(required);
        definition=JsonSerializer.Deserialize<MetadataInteriorDefinition>(JsonSerializer.Serialize(definition,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        var budget=new WorldSpatialWorkBudget(definition.MaximumWork,token,parent);
        if(preserved is not null&&!MetadataPopulationSources.Equal(preserved.Definition,definition))
            throw new ArgumentException("METADATA_POPULATION_SOURCE_INTERIOR_RULE_CHANGED");
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);var sites=access.Sites.ToDictionary(s=>s.NodeId,StringComparer.Ordinal);
        var allRooms=nodes.Where(n=>definition.Rules.Any(r=>r?.RoomKinds is not null&&n.ParentId==r.ScopeNodeId&&r.RoomKinds.Contains(n.Kind,StringComparer.Ordinal)))
            .Where(n=>sites.ContainsKey(n.Id)).Select(n=>sites[n.Id]).OrderBy(s=>s.NodeId,StringComparer.Ordinal).ToArray();
        var ids=new HashSet<string>(StringComparer.Ordinal);var owned=new HashSet<string>(StringComparer.Ordinal);var results=new List<MetadataInteriorResult>();
        var ownedEntrances=new HashSet<string>(StringComparer.Ordinal);
        foreach(var rule in definition.Rules.OrderBy(r=>r?.Id,StringComparer.Ordinal))
        {
            if(rule is null||!WorldEnvironmentQuery.Id(rule.Id)||!ids.Add(rule.Id)||!WorldEnvironmentQuery.Text(rule.Purpose)||!WorldEnvironmentQuery.Text(rule.SourceRuleId)||
                rule.ScopeNodeId is null||!lookup.ContainsKey(rule.ScopeNodeId)||!Kinds(rule.RoomKinds)||!Kinds(rule.ConnectionKinds)||
                !double.IsFinite(rule.OpeningWidth)||rule.OpeningWidth<=0||rule.OpeningWidth>1e6||!double.IsFinite(rule.WallThickness)||rule.WallThickness<=0||rule.WallThickness>1e5||
                !double.IsFinite(rule.ApproachDepth)||rule.ApproachDepth<=rule.WallThickness||rule.ApproachDepth>1e6||rule.Profile is null||rule.Search is null)
                throw new ArgumentException("METADATA_INTERIOR_RULE_INVALID");
            _=new WorldSurfaceTraversal(new WorldSurfaceQuery(surface,token),rule.Profile,token);
            _=WorldSurfaceRoutePlanner.ValidateRequest(new(rule.Id,rule.Purpose,rule.SourceRuleId,new(0,0),new(1,0),rule.Profile,rule.Search));
            if(rule.OpeningWidth<=2*rule.Profile.Radius||rule.ApproachDepth<=rule.WallThickness+rule.Profile.Radius)
                throw new ArgumentException("METADATA_INTERIOR_PASSAGE_DIMENSIONS_INVALID");
            budget.Charge(nodes.Count);
            // Scope is an actual floor/container. Nested contents are not other rooms.
            var rooms=nodes.Where(n=>n.ParentId==rule.ScopeNodeId&&rule.RoomKinds.Contains(n.Kind,StringComparer.Ordinal)).OrderBy(n=>n.Id,StringComparer.Ordinal).ToArray();
            if(rooms.Length is <2 or >32)throw new ArgumentException("METADATA_INTERIOR_ROOM_COUNT");
            var roomIds=rooms.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
            var shapes=new Dictionary<string,WorldPolygon>(StringComparer.Ordinal);var edges=new Dictionary<string,Edge[]>(StringComparer.Ordinal);
            foreach(var room in rooms)
            {
                if(!owned.Add(room.Id))throw new ArgumentException("METADATA_INTERIOR_ROOM_OWNERSHIP");
                if(room.Position is not {Count:2}||!sites.TryGetValue(room.Id,out var site)||site.Occupancy!=MetadataOccupancyKind.Reservation||
                    site.Footprint.Vertices.Count>16||!ConvexParcelGeometry.IsConvex(site.Footprint))throw new ArgumentException("METADATA_INTERIOR_CONVEX_FLOOR_REQUIRED:"+room.Id);
                shapes.Add(room.Id,site.Footprint);edges.Add(room.Id,Edges(site.Footprint));
            }
            for(int i=0;i<rooms.Length;i++)for(int j=i+1;j<rooms.Length;j++)
            {
                budget.Charge((long)shapes[rooms[i].Id].Vertices.Count*shapes[rooms[j].Id].Vertices.Count);
                if(TerrainCoverageTopology.IntersectionArea(shapes[rooms[i].Id],shapes[rooms[j].Id])>Eps)
                    throw new MetadataSpatialConstraintException("METADATA_INTERIOR_ROOM_OVERLAP");
            }
            if(rule.Entrances is {Count:0 or >16})throw new ArgumentException("METADATA_INTERIOR_ENTRANCES_INVALID");
            var entranceIds=new HashSet<string>(StringComparer.Ordinal);var entranceLinks=new List<WorldMetadataConnection>();
            foreach(var entrance in rule.Entrances??[])
            {
                if(entrance is null||!WorldEnvironmentQuery.Text(entrance.ConnectionId)||!ownedEntrances.Add(entrance.ConnectionId)||
                    !entranceIds.Add(entrance.ConnectionId)||!WorldEnvironmentQuery.Id(entrance.RoomPortId)||entrance.OutsidePortId is {} outsidePort&&!WorldEnvironmentQuery.Id(outsidePort))
                    throw new ArgumentException("METADATA_INTERIOR_ENTRANCE_INVALID");
                var link=connections.SingleOrDefault(c=>c.Id==entrance.ConnectionId)??throw new ArgumentException("METADATA_INTERIOR_ENTRANCE_CONNECTION_REQUIRED");
                if(roomIds.Contains(link.FromId)==roomIds.Contains(link.ToId)||!lookup.ContainsKey(link.FromId)||!lookup.ContainsKey(link.ToId))
                    throw new ArgumentException("METADATA_INTERIOR_ENTRANCE_ENDPOINT_INVALID");
                string outside=roomIds.Contains(link.FromId)?link.ToId:link.FromId;
                if(allRooms.Any(r=>r.NodeId==outside))throw new ArgumentException("METADATA_INTERIOR_ENTRANCE_OUTSIDE_REQUIRED");
                entranceLinks.Add(link);
            }
            var links=connections.Where(c=>!entranceIds.Contains(c.Id)&&rule.ConnectionKinds.Contains(c.Kind,StringComparer.Ordinal)&&(roomIds.Contains(c.FromId)||roomIds.Contains(c.ToId)))
                .OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray();
            if(links.Length is <1 or >64||links.Any(c=>!roomIds.Contains(c.FromId)||!roomIds.Contains(c.ToId)||c.FromId==c.ToId))
                throw new ArgumentException("METADATA_INTERIOR_CONNECTIONS_INVALID");
            var reached=new HashSet<string>(StringComparer.Ordinal){rooms[0].Id};bool changed;
            do{changed=false;foreach(var link in links){budget.Charge();if(reached.Contains(link.FromId))changed|=reached.Add(link.ToId);if(reached.Contains(link.ToId))changed|=reached.Add(link.FromId);}}while(changed);
            if(reached.Count!=rooms.Length)throw new ArgumentException("METADATA_INTERIOR_DISCONNECTED");
            var cuts=new Dictionary<(string Room,int Edge),List<(double Low,double High)>>();var openings=new List<MetadataInteriorOpening>();
            foreach(var link in links)
            {
                var contact=SharedContact(rule,edges[link.FromId],edges[link.ToId],budget)
                    ??throw new MetadataSpatialConstraintException("METADATA_INTERIOR_SHARED_BOUNDARY_REQUIRED:"+link.Id);
                double middle=(contact.Low+contact.High)/2,lowCut=middle-rule.OpeningWidth/2,highCut=middle+rule.OpeningWidth/2;
                var p=At(contact.A,lowCut);var q=At(contact.A,highCut);var center=At(contact.A,middle);
                AddCut(link.FromId,contact.A,lowCut,highCut);
                double Position(Edge edge,WorldPoint point)=>(point.X-edge.A.X)*edge.X+(point.Y-edge.A.Y)*edge.Y;
                double bp=Position(contact.B,p),bq=Position(contact.B,q);AddCut(link.ToId,contact.B,Math.Min(bp,bq),Math.Max(bp,bq));
                openings.Add(new(link.Id,link.FromId,link.ToId,p,q,center.Offset(contact.A.NX*rule.ApproachDepth,contact.A.NY*rule.ApproachDepth),
                    center.Offset(contact.B.NX*rule.ApproachDepth,contact.B.NY*rule.ApproachDepth),rule.OpeningWidth));
            }
            foreach(var entrance in rule.Entrances??[])
            {
                var link=entranceLinks.Single(c=>c.Id==entrance.ConnectionId);bool fromRoom=roomIds.Contains(link.FromId);string room=fromRoom?link.FromId:link.ToId;
                var port=sites[room].Ports.SingleOrDefault(p=>p.Id==entrance.RoomPortId)??throw new ArgumentException("METADATA_INTERIOR_ENTRANCE_PORT_REQUIRED");
                if(port.Width+Eps<rule.OpeningWidth)throw new ArgumentException("METADATA_INTERIOR_ENTRANCE_PORT_TOO_NARROW");
                var center=port.BoundaryPosition;var outside=port.ApproachPosition;double length=center.DistanceTo(outside);
                var edge=edges[room].SingleOrDefault(e=>Math.Abs(e.A.DistanceTo(center)+center.DistanceTo(e.B)-e.Length)<Eps&&
                    (outside.X-center.X)*e.NX+(outside.Y-center.Y)*e.NY< -Eps)
                    ??throw new ArgumentException("METADATA_INTERIOR_ENTRANCE_PORT_GEOMETRY_INVALID");
                if(length<=rule.Profile.Radius||Math.Abs((outside.X-center.X)*edge.X+(outside.Y-center.Y)*edge.Y)>Eps)
                    throw new ArgumentException("METADATA_INTERIOR_ENTRANCE_NORMAL_APPROACH_REQUIRED");
                double position=(center.X-edge.A.X)*edge.X+(center.Y-edge.A.Y)*edge.Y,low=position-rule.OpeningWidth/2,high=position+rule.OpeningWidth/2;
                if(low<rule.WallThickness-Eps||high>edge.Length-rule.WallThickness+Eps)
                    throw new MetadataSpatialConstraintException("METADATA_INTERIOR_ENTRANCE_CORNER_PIER_REQUIRED");
                AddCut(room,edge,low,high);var inside=center.Offset(edge.NX*rule.ApproachDepth,edge.NY*rule.ApproachDepth);
                openings.Add(new(link.Id,link.FromId,link.ToId,At(edge,low),At(edge,high),fromRoom?inside:outside,fromRoom?outside:inside,rule.OpeningWidth,entrance));
            }
            void AddCut(string room,Edge edge,double low,double high)
            {
                if(!cuts.TryGetValue((room,edge.Index),out var existing))cuts.Add((room,edge.Index),existing=[]);
                if(existing.Any(c=>low<c.High+rule.WallThickness&&high>c.Low-rule.WallThickness))
                    throw new MetadataSpatialConstraintException("METADATA_INTERIOR_OPENING_CONFLICT");
                existing.Add((low,high));
            }
            var walls=new List<MetadataInteriorWall>();
            foreach(var room in rooms)foreach(var edge in edges[room.Id])
            {
                double start=0;
                foreach(var cut in cuts.GetValueOrDefault((room.Id,edge.Index),[]).OrderBy(c=>c.Low).Append((edge.Length,edge.Length)))
                {if(cut.Item1-start>Eps)Wall(room.Id,edge,start,cut.Item1);start=cut.Item2;}
            }
            void Wall(string room,Edge edge,double from,double to)
            {
                var a=At(edge,from);var b=At(edge,to);
                var polygon=WallFootprint(edges[room],edge,from,to,rule.WallThickness,budget);
                walls.Add(new("wall-"+walls.Count.ToString("D3",System.Globalization.CultureInfo.InvariantCulture),room,edge.Index,a,b,polygon));
                // The assembly must fit the same bounded exclusion set consumed
                // by the shared surface query. Source perimeter segments remain
                // separate solids; never drop or merge a closed wall to fit.
                if(surface.Exclusions.Count+walls.Count>WorldSurfaceQuery.MaximumExclusions)throw new ArgumentException("METADATA_INTERIOR_WALL_BUDGET");
            }
            foreach(var wall in walls)foreach(var solid in access.Sites.Where(s=>s.Occupancy==MetadataOccupancyKind.Solid))
            {
                budget.Charge((long)solid.Footprint.Vertices.Count*wall.Footprint.Vertices.Count);
                foreach(var triangle in LandscapePolygonTriangulator.Triangulate(solid.Footprint))
                    if(TerrainCoverageTopology.IntersectionArea(triangle,wall.Footprint)>Eps)
                        throw new MetadataSpatialConstraintException("METADATA_INTERIOR_CONTENT_WALL_COLLISION:"+solid.NodeId);
            }
            var wallSurface=surface with{Exclusions=surface.Exclusions.Concat(walls.Select(w=>new WorldSurfaceExclusion("interior-"+w.Id,rule.Purpose,rule.SourceRuleId,w.Footprint))).ToArray()};
            var query=new MetadataPhysicalAccessPlanner().RouteSurface(wallSurface,access,token);
            var solidSurface=query.Definition;
            var planner=new WorldSurfaceRoutePlanner();var routes=new List<MetadataRoutedConnection>();
            var retained=preserved?.Results.SingleOrDefault(r=>r.RuleId==rule.Id);
            if(preserved is not null&&(retained is null||!MetadataPopulationSources.Equal(retained.RoomNodeIds,rooms.Select(r=>r.Id).ToArray())||
                !MetadataPopulationSources.Equal(retained.Walls,walls)||!MetadataPopulationSources.Equal(retained.Openings,openings)||retained.Routes.Count!=openings.Count))
                throw new ArgumentException("METADATA_POPULATION_SOURCE_INTERIOR_GEOMETRY_CHANGED:"+rule.Id);
            var coverageProfile=new WorldTraversalProfile("interior-coverage","Stay on selected room floors","engine:interior-coverage",rule.Profile.Radius,0,new Dictionary<string,double>{{"floor",1}});
            WorldSurfaceTraversal Coverage(IEnumerable<string> selected)
            {
                var patches=selected.Select((id,i)=>new WorldTerrainPatch("room-"+i,"floor",rule.Purpose,rule.SourceRuleId,shapes[id],i,new(0,0),0,0,0)).ToArray();
                return new(new WorldSurfaceQuery(surface with{Exclusions=[],Patches=patches,Terrain=null,HeightField=null,TerrainCapture=null,TerrainCapturePlan=null},token),coverageProfile);
            }
            foreach(var opening in openings)
            {
                var link=links.Concat(entranceLinks).Single(l=>l.Id==opening.ConnectionId);
                if(retained is not null)
                {
                    var saved=retained.Routes.SingleOrDefault(r=>r.ConnectionId==link.Id);
                    if(saved is null||saved.RuleId!=rule.Id||(saved.Reverse is not null)!=link.Bidirectional||saved.Forward.Path is null)
                        throw new ArgumentException("METADATA_POPULATION_SOURCE_INTERIOR_ROUTE_CHANGED:"+link.Id);
                    // The authenticated source keeps identical rooms/openings and endpoint
                    // poses. Recheck every full-width saved segment against current terrain,
                    // walls and solids; do not search a replacement around new content.
                    WorldPoint Endpoint(string id)=>roomIds.Contains(id)?new(lookup[id].Position![0],lookup[id].Position![1]):
                        new MetadataPhysicalAccessPlanner().Endpoint(lookup[id],opening.Entrance!.OutsidePortId,access,wallSurface,rule.Profile,budget,token);
                    var start=Endpoint(opening.FromId);var end=Endpoint(opening.ToId);
                    MetadataPopulationSources.ValidatePassage(saved.Forward,start,end,query,rule.Profile,link.Id,budget,token,access);
                    if(saved.Reverse is {} reverse)
                        MetadataPopulationSources.ValidatePassage(reverse,end,start,query,rule.Profile,link.Id,budget,token,access);
                    routes.Add(saved);continue;
                }
                IReadOnlyList<WorldPoint> Leg(string room,WorldPoint approach,bool reverse)
                {
                    if(!roomIds.Contains(room))
                    {
                        var endpoint=new MetadataPhysicalAccessPlanner().Endpoint(lookup[room],opening.Entrance!.OutsidePortId,access,wallSurface,rule.Profile,budget,token);
                        if(endpoint==approach)return new[]{endpoint};
                        var blockedRooms=solidSurface.Exclusions.Concat(allRooms.Select(r=>new WorldSurfaceExclusion("room-floor-"+MetadataExecutionCache.Hash(r.NodeId),rule.Purpose,rule.SourceRuleId,r.Footprint))).ToArray();
                        var outsideQuery=new WorldSurfaceQuery(solidSurface with{Exclusions=blockedRooms},token,query.PhysicalObstacles);
                        var outsideResult=planner.Plan(outsideQuery,new(link.Id+(reverse?"/return-outside":"/arrival-outside"),rule.Purpose,rule.SourceRuleId,
                            reverse?approach:endpoint,reverse?endpoint:approach,rule.Profile,rule.Search),token,budget);
                        if(outsideResult.Status!=WorldRouteStatus.Found)throw new MetadataSpatialConstraintException("METADATA_INTERIOR_EXTERIOR_PASSAGE_REJECTED:"+room);
                        return outsideResult.Path!.Points;
                    }
                    var origin=lookup[room].Position!;var center=new WorldPoint(origin[0],origin[1]);var coverage=Coverage([room]);
                    // Candidates and complete swept segments must remain in this room.
                    var constraint=new WorldRouteConstraint(shapes[room].Vertices,(p,b)=>{b.Charge(shapes[room].Vertices.Count);return shapes[room].ContainsPoint(p);},
                        (a,b,work,t)=>coverage.Evaluate(a,b,work,t) is not null);
                    if(center==approach)return new[]{center};
                    if(center.DistanceTo(approach)<Eps)throw new MetadataSpatialConstraintException("METADATA_INTERIOR_APPROACH_COINCIDES");
                    var result=planner.Plan(query,new(link.Id+(reverse?"/return-":"/arrival-")+room,rule.Purpose,rule.SourceRuleId,
                        reverse?approach:center,reverse?center:approach,rule.Profile,rule.Search),token,budget,constraint);
                    if(result.Status!=WorldRouteStatus.Found)throw new MetadataSpatialConstraintException("METADATA_INTERIOR_ROOM_PASSAGE_REJECTED:"+room);
                    return result.Path!.Points;
                }
                WorldRouteResult Passage(bool reverse)
                {
                    string from=reverse?opening.ToId:opening.FromId,to=reverse?opening.FromId:opening.ToId;
                    var first=Leg(from,reverse?opening.ToApproach:opening.FromApproach,false);
                    var last=Leg(to,reverse?opening.FromApproach:opening.ToApproach,true);
                    var points=first.Concat(last).ToArray();
                    if(opening.Entrance is null)
                    {
                        var coverage=Coverage([from,to]);
                        for(int i=1;i<points.Length;i++)if(coverage.Evaluate(points[i-1],points[i],budget,token) is null)
                            throw new MetadataSpatialConstraintException("METADATA_INTERIOR_FLOOR_PASSAGE_REJECTED");
                    }
                    else
                    {
                        string room=roomIds.Contains(from)?from:to;
                        var crossingSurface=solidSurface with{Exclusions=solidSurface.Exclusions.Concat(allRooms.Where(r=>r.NodeId!=room).Select(r=>new WorldSurfaceExclusion("other-floor-"+MetadataExecutionCache.Hash(r.NodeId),rule.Purpose,rule.SourceRuleId,r.Footprint))).ToArray()};
                        if(new WorldSurfaceTraversal(new WorldSurfaceQuery(crossingSurface,token,query.PhysicalObstacles),rule.Profile,token).Evaluate(first[^1],last[0],budget,token) is null)
                            throw new MetadataSpatialConstraintException("METADATA_INTERIOR_ENTRANCE_CROSSING_REJECTED");
                    }
                    var result=planner.Compile(query,new(link.Id+(reverse?"/reverse":"/forward"),rule.Purpose,rule.SourceRuleId,points[0],points[^1],rule.Profile,rule.Search),new(points),token,budget);
                    if(result.Status!=WorldRouteStatus.Found)throw new MetadataSpatialConstraintException("METADATA_INTERIOR_OPENING_PASSAGE_REJECTED:"+link.Id);
                    return result;
                }
                routes.Add(new(link.Id,rule.Id,Passage(false),link.Bidirectional?Passage(true):null));
            }
            results.Add(new(rule.Id,rooms.Select(r=>r.Id).ToArray(),walls.AsReadOnly(),openings.AsReadOnly(),routes.AsReadOnly()));
        }
        if(preserved is not null&&results.Count!=preserved.Results.Count)throw new ArgumentException("METADATA_POPULATION_SOURCE_INTERIOR_GEOMETRY_CHANGED");
        new MetadataPhysicalAccountingPlanner().ValidateRequiredInteriorFloors(new(definition,results.AsReadOnly(),budget.Used),access.Sites,nodes,surface.Unit,budget,token);
        return new(definition,results.AsReadOnly(),budget.Used);
    }
    private static bool Kinds(IReadOnlyList<string>? kinds)=>kinds is {Count:>=1 and <=16}&&kinds.All(WorldEnvironmentQuery.Id)&&kinds.Distinct(StringComparer.Ordinal).Count()==kinds.Count;
    private static WorldPolygon WallFootprint(Edge[] sides,Edge edge,double from,double to,double thickness,WorldSpatialWorkBudget budget)
    {
        budget.Charge((long)sides.Length*16);var a=At(edge,from);var b=At(edge,to);
        WorldPolygon? polygon=new([a,b,b.Offset(edge.NX*thickness,edge.NY*thickness),a.Offset(edge.NX*thickness,edge.NY*thickness)]);
        foreach(var side in sides)
        {if(polygon is null)break;polygon=ConvexParcelGeometry.ClipHalfPlane(polygon,side.NX,side.NY,side.NX*side.A.X+side.NY*side.A.Y);}
        return polygon??throw new MetadataSpatialConstraintException("METADATA_INTERIOR_WALL_GEOMETRY_REJECTED");
    }
    internal static IReadOnlyList<WorldPolygon> ClosedWallFootprints(WorldPolygon room,double thickness,WorldSpatialWorkBudget budget)
    {
        var edges=Edges(room);
        return edges.Select(e=>WallFootprint(edges,e,0,e.Length,thickness,budget)).ToArray();
    }
    private static WorldPoint At(Edge edge,double distance)=>edge.A.Offset(edge.X*distance,edge.Y*distance);
    private static Edge[] Edges(WorldPolygon shape)
    {
        double sign=Math.Sign(StreetSurfaceBuilder.SignedArea(shape.Vertices));
        return shape.Vertices.Select((a,i)=>{var b=shape.Vertices[(i+1)%shape.Vertices.Count];double length=a.DistanceTo(b),x=(b.X-a.X)/length,y=(b.Y-a.Y)/length;
            return new Edge(i,a,b,x,y,-y*sign,x*sign,length);}).ToArray();
    }
    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        if(document.InteriorPlan is not {} saved)return;
        if(document.PlacementPlan?.Definition.SourceWorld is {} source)
        {
            if(source.PlacementPlan?.Definition.SourceWorld is not null||source.PlacementPlan?.TemplateDefinition?.SourceWorld is not null)
                throw new ArgumentException("METADATA_POPULATION_SOURCE_LEVEL_INVALID");
            // Standalone owner validation must authenticate the original floor/path
            // proof too; it cannot trust a supplied source receipt as a route cache.
            ValidateDocument(source,token);
        }
        ValidateRouteOwnership(saved.Definition,document.RoutePlan?.Rules,document.Connections);
        var actual=Plan(saved.Definition,document.Surface??throw new ArgumentException("METADATA_INTERIOR_SURFACE_REQUIRED"),document.Nodes,document.Connections,document.AccessPlan,null,token,
            document.PlacementPlan?.Definition.SourceWorld?.InteriorPlan);
        if(!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(actual,WorldJson.CreateOptions()),JsonSerializer.SerializeToElement(saved,WorldJson.CreateOptions())))
            throw new ArgumentException("METADATA_INTERIOR_PLAN_MISMATCH");
    }
}
